using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Tonie;

/// <summary>
/// Service for managing TonieUploadJob database operations
/// </summary>
public class TonieUploadJobService : ITonieUploadJobService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<TonieUploadJobService> _logger;

    public TonieUploadJobService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<TonieUploadJobService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<TonieUploadJob> CreateUploadJobAsync(
        string userId,
        int libraryItemId,
        string householdId,
        string tonieId,
        string? customChapterTitle = null,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Verify library item exists and belongs to user
        var libraryItem = await dbContext.MediaLibraryItems
            .FirstOrDefaultAsync(m => m.Id == libraryItemId && m.UserId == userId, ct);

        if (libraryItem == null)
        {
            throw new InvalidOperationException($"Library item {libraryItemId} not found or access denied");
        }

        var job = new TonieUploadJob
        {
            UserId = userId,
            MediaLibraryItemId = libraryItemId,
            HouseholdId = householdId,
            TonieId = tonieId,
            ChapterTitle = customChapterTitle ?? libraryItem.Title,
            StatusEnum = TonieUploadJobStatus.Pending,
            StatusMessage = "Upload queued...",
            ProgressPercentage = 0,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        dbContext.TonieUploadJobs.Add(job);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created upload job {JobId} for library item {LibraryItemId} → Tonie {TonieId}",
            job.Id, libraryItemId, tonieId);

        return job;
    }

    public async Task<TonieUploadJob?> GetJobByIdAsync(int jobId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.TonieUploadJobs
            .Include(j => j.MediaLibraryItem)
                .ThenInclude(m => m.FileUpload)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);
    }

    public async Task<List<TonieUploadJob>> GetUserJobsAsync(
        string userId,
        int take = 50,
        int skip = 0,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.TonieUploadJobs
            .Include(j => j.MediaLibraryItem)
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.Created)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetUserJobCountAsync(
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.TonieUploadJobs
            .Where(j => j.UserId == userId)
            .CountAsync(ct);
    }

    public async Task<List<TonieUploadJob>> GetActiveJobsAsync(
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var activeStatuses = new[]
        {
            TonieUploadJobStatus.Pending.ToString(),
            TonieUploadJobStatus.Preparing.ToString(),
            TonieUploadJobStatus.Compressing.ToString(),
            TonieUploadJobStatus.Uploading.ToString(),
            TonieUploadJobStatus.Refreshing.ToString()
        };

        return await dbContext.TonieUploadJobs
            .Include(j => j.MediaLibraryItem)
            .Where(j => j.UserId == userId && activeStatuses.Contains(j.Status))
            .OrderBy(j => j.Created)
            .ToListAsync(ct);
    }

    public async Task UpdateJobStatusAsync(
        int jobId,
        TonieUploadJobStatus status,
        int? progressPercentage = null,
        string? statusMessage = null,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var job = await dbContext.TonieUploadJobs.FindAsync(new object[] { jobId }, ct);
        if (job == null)
        {
            _logger.LogWarning("Upload job {JobId} not found", jobId);
            return;
        }

        job.StatusEnum = status;
        job.Modified = DateTimeOffset.UtcNow;

        if (progressPercentage.HasValue)
        {
            job.ProgressPercentage = progressPercentage.Value;
        }

        if (statusMessage != null)
        {
            job.StatusMessage = statusMessage;
        }

        // Mark as started if moving from Pending
        if (status != TonieUploadJobStatus.Pending && job.Started == null)
        {
            job.Started = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Updated upload job {JobId} to {Status} ({Progress}%)",
            jobId, status, job.ProgressPercentage);
    }

    public async Task CompleteJobAsync(
        int jobId,
        string? syncResultJson = null,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var job = await dbContext.TonieUploadJobs.FindAsync(new object[] { jobId }, ct);
        if (job == null)
        {
            _logger.LogWarning("Upload job {JobId} not found", jobId);
            return;
        }

        job.StatusEnum = TonieUploadJobStatus.Completed;
        job.StatusMessage = "Upload completed successfully";
        job.ProgressPercentage = 100;
        job.Completed = DateTimeOffset.UtcNow;
        job.Modified = DateTimeOffset.UtcNow;

        if (syncResultJson != null)
        {
            job.SyncResultJson = syncResultJson;
        }

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Upload job {JobId} completed successfully", jobId);
    }

    public async Task FailJobAsync(
        int jobId,
        string errorMessage,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var job = await dbContext.TonieUploadJobs.FindAsync(new object[] { jobId }, ct);
        if (job == null)
        {
            _logger.LogWarning("Upload job {JobId} not found", jobId);
            return;
        }

        job.StatusEnum = TonieUploadJobStatus.Failed;
        job.StatusMessage = "Upload failed";
        job.ErrorMessage = errorMessage;
        job.Completed = DateTimeOffset.UtcNow;
        job.Modified = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        _logger.LogError("Upload job {JobId} failed: {Error}", jobId, errorMessage);
    }

    public async Task CancelJobAsync(int jobId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var job = await dbContext.TonieUploadJobs.FindAsync(new object[] { jobId }, ct);
        if (job == null)
        {
            _logger.LogWarning("Upload job {JobId} not found", jobId);
            return;
        }

        // Only cancel if not already completed/failed
        if (job.StatusEnum == TonieUploadJobStatus.Completed ||
            job.StatusEnum == TonieUploadJobStatus.Failed)
        {
            _logger.LogWarning("Cannot cancel upload job {JobId} in status {Status}",
                jobId, job.StatusEnum);
            return;
        }

        job.StatusEnum = TonieUploadJobStatus.Cancelled;
        job.StatusMessage = "Upload cancelled by user";
        job.Completed = DateTimeOffset.UtcNow;
        job.Modified = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Upload job {JobId} cancelled", jobId);
    }
}
