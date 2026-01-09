using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Import;

/// <summary>
/// Implementation of import job service
/// Handles CRUD operations for import jobs
/// </summary>
public class ImportJobService : IImportJobService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IYouTubeImportService _youtubeService;
    private readonly ILogger<ImportJobService> _logger;

    public ImportJobService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IYouTubeImportService youtubeService,
        ILogger<ImportJobService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _youtubeService = youtubeService;
        _logger = logger;
    }

    public async Task<ImportJob> CreateYouTubeImportJobAsync(
        string userId,
        string youtubeUrl,
        string? customTitle = null,
        string? customDescription = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating YouTube import job for user {UserId}: {Url}", 
            userId, youtubeUrl);

        // Validate URL and fetch metadata
        var videoInfo = await _youtubeService.GetVideoInfoAsync(youtubeUrl, ct);
        
        if (!videoInfo.IsValid)
        {
            throw new InvalidOperationException(
                videoInfo.ErrorMessage ?? "Invalid YouTube URL");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Truncate fields to fit database constraints
        var title = customTitle ?? videoInfo.Title;
        if (title?.Length > 500)
        {
            title = title.Substring(0, 497) + "...";
            _logger.LogWarning("Truncated title for video {VideoId} from {OriginalLength} to 500 characters", 
                videoInfo.Id, videoInfo.Title?.Length);
        }
        
        var description = customDescription ?? videoInfo.Description;
        if (description?.Length > 2000)
        {
            description = description.Substring(0, 1997) + "...";
            _logger.LogWarning("Truncated description for video {VideoId} from {OriginalLength} to 2000 characters", 
                videoInfo.Id, videoInfo.Description?.Length);
        }
        
        var thumbnailUrl = videoInfo.ThumbnailUrl;
        if (thumbnailUrl?.Length > 1024)
        {
            thumbnailUrl = thumbnailUrl.Substring(0, 1024);
            _logger.LogWarning("Truncated thumbnail URL for video {VideoId} from {OriginalLength} to 1024 characters", 
                videoInfo.Id, videoInfo.ThumbnailUrl?.Length);
        }

        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = title,
            SourceDescription = description,
            SourceThumbnailUrl = thumbnailUrl,
            SourceDurationSeconds = (float)videoInfo.Duration.TotalSeconds,
            StatusEnum = ImportJobStatus.Pending,
            StatusMessage = "Waiting to start...",
            Created = DateTimeOffset.UtcNow
        };

        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created import job {JobId} for video: {Title}", 
            job.Id, videoInfo.Title);

        return job;
    }
    
    public async Task<List<ImportJob>> CreateYouTubeBatchImportAsync(
        string userId,
        List<string> youtubeUrls,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating batch YouTube import for user {UserId}: {Count} videos", 
            userId, youtubeUrls.Count);
        
        var jobs = new List<ImportJob>();
        var errors = new List<string>();
        
        foreach (var url in youtubeUrls)
        {
            try
            {
                var job = await CreateYouTubeImportJobAsync(userId, url, null, null, ct);
                jobs.Add(job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create import job for URL: {Url}", url);
                errors.Add($"{url}: {ex.Message}");
                // Continue with other videos even if one fails
            }
        }
        
        _logger.LogInformation("Created {SuccessCount}/{TotalCount} import jobs for batch import", 
            jobs.Count, youtubeUrls.Count);
        
        if (errors.Any())
        {
            _logger.LogWarning("Batch import had {ErrorCount} failures: {Errors}", 
                errors.Count, string.Join("; ", errors));
        }
        
        return jobs;
    }

    public async Task<ImportJob?> GetJobAsync(int jobId, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.ImportJobs
            .Where(j => j.Id == jobId && j.UserId == userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ImportJob>> GetUserJobsAsync(
        string userId, 
        int take = 20, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.ImportJobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.Created)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> CancelJobAsync(int jobId, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var job = await dbContext.ImportJobs
            .Where(j => j.Id == jobId && j.UserId == userId)
            .FirstOrDefaultAsync(ct);

        if (job == null)
            return false;

        // Can only cancel pending or running jobs
        if (job.StatusEnum is ImportJobStatus.Completed or ImportJobStatus.Failed or ImportJobStatus.Cancelled)
            return false;

        job.StatusEnum = ImportJobStatus.Cancelled;
        job.StatusMessage = "Cancelled by user";
        job.CompletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Cancelled import job {JobId}", jobId);
        
        return true;
    }
}
