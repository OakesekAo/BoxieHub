using BoxieHub.Client.Models.Enums;
using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.PythonAdapter.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Sync;

/// <summary>
/// Service for orchestrating sync operations.
/// Coordinates between the database and Python adapter.
/// </summary>
public class SyncJobService : ISyncJobService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPythonAdapterClient _pythonAdapter;
    private readonly ILogger<SyncJobService> _logger;

    public SyncJobService(
        ApplicationDbContext dbContext,
        IPythonAdapterClient pythonAdapter,
        ILogger<SyncJobService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pythonAdapter = pythonAdapter ?? throw new ArgumentNullException(nameof(pythonAdapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SyncJob> ExecuteSyncAsync(int deviceId, int assignmentId, string requestedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting sync job for Device {DeviceId}, Assignment {AssignmentId}, requested by {User}",
            deviceId, assignmentId, requestedBy);

        // Load device and assignment
        var device = await _dbContext.Devices
            .Include(d => d.Household)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            ?? throw new ArgumentException($"Device {deviceId} not found");

        var assignment = await _dbContext.ContentAssignments
            .Include(a => a.ContentItem)
                .ThenInclude(c => c.Upload)
            .Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.DeviceId == deviceId, cancellationToken)
            ?? throw new ArgumentException($"Assignment {assignmentId} not found for device {deviceId}");

        // Create job record
        var job = new SyncJob
        {
            HouseholdId = device.HouseholdId,
            DeviceId = deviceId,
            ContentItemId = int.TryParse(assignment.ContentItemId.ToString(), out var contentId) ? contentId : null,
            Status = SyncStatus.Pending,
            JobType = "Upload",
            Created = DateTimeOffset.UtcNow,
            Started = null,
            Completed = null,
            ErrorMessage = null
        };

        _dbContext.SyncJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created SyncJob {JobId} with status Pending", job.Id);

        // Mark as started
        job.Started = DateTimeOffset.UtcNow;
        job.Status = SyncStatus.InProgress;

        try
        {
            // Build sync request
            if (assignment.ContentItem?.Upload == null)
            {
                throw new InvalidOperationException("Content item has no associated file upload");
            }

            var tracks = new List<SyncTrackDto>
            {
                new SyncTrackDto
                {
                    Title = assignment.ContentItem.Title ?? "Untitled",
                    // TODO: Replace with actual base URL from configuration
                    SourceUrl = $"http://localhost:5000/files/audio/{assignment.ContentItem.UploadId}"
                }
            };

            var syncRequest = new SyncRequestDto
            {
                CreativeTonieExternalId = device.DeviceIdentifier,
                Tracks = tracks
            };

            // Call Python adapter
            var syncResponse = await _pythonAdapter.SyncAsync(syncRequest, cancellationToken);

            // Update job with response
            job.Status = syncResponse.Success ? SyncStatus.Completed : SyncStatus.Failed;
            job.ErrorMessage = syncResponse.Success ? null : syncResponse.ErrorDetails ?? syncResponse.Message;
            job.Completed = DateTimeOffset.UtcNow;

            _logger.LogInformation("Sync job {JobId} completed with status {Status}", job.Id, job.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync job {JobId} failed with exception", job.Id);

            job.Status = SyncStatus.Failed;
            job.ErrorMessage = $"Exception: {ex.Message}";
            job.Completed = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<SyncJob?> GetJobAsync(int jobId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SyncJobs
            .Include(j => j.Device)
            .Include(j => j.ContentItem)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }

    public async Task<IEnumerable<SyncJob>> GetJobsAsync(int householdId, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SyncJobs
            .Where(j => j.HouseholdId == householdId)
            .Include(j => j.Device)
            .Include(j => j.ContentItem)
            .OrderByDescending(j => j.Created);

        if (limit.HasValue)
            query = (IOrderedQueryable<SyncJob>)query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }
}
