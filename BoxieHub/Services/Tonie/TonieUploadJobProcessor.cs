using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Models.BoxieCloud;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Tonie;

/// <summary>
/// Background service that processes Tonie upload jobs from the queue
/// Similar to ImportJobProcessor but for library → Tonie uploads
/// Uses Channel<T> for job queue and processes jobs sequentially
/// </summary>
public class TonieUploadJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TonieUploadJobProcessor> _logger;
    private readonly Channel<int> _jobQueue;

    // Track active jobs for cancellation support
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new();

    public TonieUploadJobProcessor(
        IServiceProvider serviceProvider,
        ILogger<TonieUploadJobProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Bounded channel with max 50 queued uploads
        _jobQueue = Channel.CreateBounded<int>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Enqueue an upload job for processing
    /// Called by the UI after creating the TonieUploadJob record
    /// </summary>
    public async Task EnqueueJobAsync(int jobId)
    {
        await _jobQueue.Writer.WriteAsync(jobId);
        _logger.LogInformation("Enqueued Tonie upload job {JobId}", jobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tonie upload job processor started");

        // Poll for pending jobs on startup (in case app restarted)
        await EnqueuePendingJobsAsync(stoppingToken);

        // Process jobs from queue
        await foreach (var jobId in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _activeJobs[jobId] = cts;

            try
            {
                await ProcessJobAsync(jobId, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing Tonie upload job {JobId}", jobId);
            }
            finally
            {
                _activeJobs.TryRemove(jobId, out _);
                cts.Dispose();
            }
        }

        _logger.LogInformation("Tonie upload job processor stopped");
    }

    private async Task EnqueuePendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

            // Find any jobs that were pending when app last stopped
            var pendingJobs = await dbContext.TonieUploadJobs
                .Where(j => j.Status == nameof(TonieUploadJobStatus.Pending) ||
                           j.Status == nameof(TonieUploadJobStatus.Preparing) ||
                           j.Status == nameof(TonieUploadJobStatus.Compressing) ||
                           j.Status == nameof(TonieUploadJobStatus.Uploading))
                .OrderBy(j => j.Created)
                .Select(j => j.Id)
                .ToListAsync(ct);

            if (pendingJobs.Any())
            {
                _logger.LogInformation("Re-queuing {Count} pending Tonie upload jobs", pendingJobs.Count);
                foreach (var jobId in pendingJobs)
                {
                    await EnqueueJobAsync(jobId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-queuing pending Tonie upload jobs");
        }
    }

    private async Task ProcessJobAsync(int jobId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var tonieService = scope.ServiceProvider.GetRequiredService<ITonieService>();
        var jobService = scope.ServiceProvider.GetRequiredService<ITonieUploadJobService>();
        var fileStorageService = scope.ServiceProvider.GetRequiredService<Storage.IFileStorageService>();

        _logger.LogInformation("Processing Tonie upload job {JobId}", jobId);

        // Get job details
        var job = await jobService.GetJobByIdAsync(jobId, ct);
        if (job == null)
        {
            _logger.LogWarning("Tonie upload job {JobId} not found", jobId);
            return;
        }

        // Check if cancelled
        if (job.StatusEnum == TonieUploadJobStatus.Cancelled)
        {
            _logger.LogInformation("Tonie upload job {JobId} was cancelled", jobId);
            return;
        }

        try
        {
            // Step 1: Prepare (download from storage)
            await jobService.UpdateJobStatusAsync(jobId, TonieUploadJobStatus.Preparing, 10,
                "Preparing audio file...", ct);

            var libraryItem = job.MediaLibraryItem!;
            var fileUpload = libraryItem.FileUpload;
            
            if (fileUpload == null)
            {
                throw new InvalidOperationException("Library item has no associated file upload");
            }
            
            Stream audioStream;

            if (fileUpload.Provider == StorageProvider.Database && fileUpload.Data != null)
            {
                audioStream = new MemoryStream(fileUpload.Data);
            }
            else if (!string.IsNullOrEmpty(fileUpload.StoragePath))
            {
                audioStream = await fileStorageService.DownloadFileAsync(
                    fileUpload.StoragePath,
                    fileUpload.UserStorageAccountId,
                    ct);
            }
            else
            {
                throw new InvalidOperationException("Library item has no audio data");
            }

            _logger.LogInformation("Downloaded {Size} bytes for upload", audioStream.Length);

            // Step 2: TODO - Compress audio (Phase 5B)
            // For now, skip compression

            await jobService.UpdateJobStatusAsync(jobId, TonieUploadJobStatus.Uploading, 50,
                "Uploading to Tonie Cloud...", ct);

            // Step 3: Upload to Tonie Cloud API (with timeout)
            _logger.LogInformation("Starting upload to Tonie Cloud for job {JobId} (Size: {Size} bytes)", 
                jobId, audioStream.Length);
            
            // Create timeout token (10 minutes max)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            SyncResultDto result;
            try
            {
                result = await tonieService.UploadAudioToTonieAsync(
                    job.UserId,
                    job.HouseholdId,
                    job.TonieId,
                    audioStream,
                    job.ChapterTitle!,
                    linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogError("Upload to Tonie Cloud timed out after 10 minutes for job {JobId}", jobId);
                await jobService.FailJobAsync(jobId,
                    "Upload timed out after 10 minutes. Please try again or use a smaller file.", ct);
                return;
            }

            audioStream.Dispose();

            if (!result.Success)
            {
                await jobService.FailJobAsync(jobId,
                    result.ErrorDetails ?? result.Message ?? "Upload failed", ct);
                return;
            }

            // Step 4: Refresh Tonie data
            await jobService.UpdateJobStatusAsync(jobId, TonieUploadJobStatus.Refreshing, 90,
                "Refreshing Tonie data...", ct);

            // (TonieService already refreshes in UploadAudioToTonieAsync)

            // Step 5: Complete
            var resultJson = JsonSerializer.Serialize(result);
            await jobService.CompleteJobAsync(jobId, resultJson, ct);

            _logger.LogInformation("Tonie upload job {JobId} completed successfully", jobId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tonie upload job {JobId} was cancelled", jobId);
            await jobService.CancelJobAsync(jobId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Tonie upload job {JobId}", jobId);
            await jobService.FailJobAsync(jobId, ex.Message, ct);
        }
    }

    /// <summary>
    /// Cancel an active job
    /// </summary>
    public bool CancelJob(int jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled Tonie upload job {JobId}", jobId);
            return true;
        }

        return false;
    }
}
