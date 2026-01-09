using System.Collections.Concurrent;
using System.Threading.Channels;
using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Import;

/// <summary>
/// Background service that processes import jobs from the queue
/// Lightweight alternative to Hangfire - no additional costs!
/// Uses Channel<T> for job queue and processes jobs sequentially
/// </summary>
public class ImportJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportJobProcessor> _logger;
    private readonly Channel<int> _jobQueue;
    
    // Track active jobs for cancellation support
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new();

    public ImportJobProcessor(
        IServiceProvider serviceProvider,
        ILogger<ImportJobProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Bounded channel with max 100 queued jobs
        _jobQueue = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Enqueue a job for processing
    /// Called by the UI after creating the ImportJob record
    /// </summary>
    public async Task EnqueueJobAsync(int jobId)
    {
        await _jobQueue.Writer.WriteAsync(jobId);
        _logger.LogInformation("Enqueued import job {JobId}", jobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Import job processor started");

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
                _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
            finally
            {
                _activeJobs.TryRemove(jobId, out _);
                cts.Dispose();
            }
        }

        _logger.LogInformation("Import job processor stopped");
    }

    private async Task EnqueuePendingJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

            // Find jobs that were pending when app shut down
            var pendingJobs = await dbContext.ImportJobs
                .Where(j => j.Status == "Pending" || j.Status == "Validating" || j.Status == "Downloading")
                .Select(j => j.Id)
                .ToListAsync(ct);

            foreach (var jobId in pendingJobs)
            {
                await _jobQueue.Writer.WriteAsync(jobId, ct);
                _logger.LogInformation("Re-enqueued pending job {JobId}", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue pending jobs on startup");
        }
    }

    private async Task ProcessJobAsync(int jobId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var youtubeService = scope.ServiceProvider.GetRequiredService<IYouTubeImportService>();
        var mediaLibraryService = scope.ServiceProvider.GetRequiredService<IMediaLibraryService>();
        var storagePreferenceService = scope.ServiceProvider.GetRequiredService<IStoragePreferenceService>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var job = await dbContext.ImportJobs.FindAsync(new object[] { jobId }, ct);
        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found", jobId);
            return;
        }

        // Check if already cancelled
        if (job.StatusEnum == ImportJobStatus.Cancelled)
        {
            _logger.LogInformation("Job {JobId} was cancelled before processing", jobId);
            return;
        }

        try
        {
            job.StartedAt = DateTimeOffset.UtcNow;
            job.StatusEnum = ImportJobStatus.Downloading;
            job.StatusMessage = "Downloading audio from YouTube...";
            job.ProgressPercentage = 10;
            await dbContext.SaveChangesAsync(ct);

            // Download audio with progress
            var progressReporter = new Progress<double>(percent =>
            {
                // Update progress in database (throttled to avoid too many writes)
                var progressInt = (int)(10 + percent * 0.7); // 10-80%
                if (progressInt > job.ProgressPercentage + 5) // Only update every 5%
                {
                    job.ProgressPercentage = progressInt;
                    // Fire and forget - don't await to avoid blocking
                    _ = dbContext.SaveChangesAsync(CancellationToken.None);
                }
            });

            using var audioStream = await youtubeService.DownloadAudioAsync(
                job.SourceUrl, 
                progressReporter, 
                ct);

            job.StatusEnum = ImportJobStatus.Saving;
            job.StatusMessage = "Saving to library...";
            job.ProgressPercentage = 85;
            await dbContext.SaveChangesAsync(ct);

            // Get user's default storage provider
            var storageProvider = await storagePreferenceService.GetDefaultProviderAsync(job.UserId, ct);

            // Determine content type (M4A from YouTube)
            var contentType = "audio/mp4"; // M4A
            var fileExtension = "m4a";

            // Truncate description to fit MediaLibraryItem constraint (max 1000 chars)
            var description = job.SourceDescription;
            if (description?.Length > 1000)
            {
                description = description.Substring(0, 997) + "...";
                _logger.LogInformation("Truncated description for job {JobId} from {OriginalLength} to 1000 characters", 
                    jobId, job.SourceDescription?.Length);
            }

            // Create library item
            var libraryItemDto = new MediaLibraryItemDto
            {
                Title = job.SourceTitle ?? "Imported from YouTube",
                Description = description,
                Category = "Audio",
                Tags = new List<string> { "youtube-import", "imported" },
                ContentType = contentType,
                OriginalFileName = $"{SanitizeFileName(job.SourceTitle)}.{fileExtension}",
                FileSizeBytes = audioStream.Length,
                DurationSeconds = job.SourceDurationSeconds ?? 0
            };

            var libraryItem = await mediaLibraryService.AddToLibraryAsync(
                job.UserId,
                audioStream,
                libraryItemDto,
                storageProvider,
                null,
                ct);

            job.MediaLibraryItemId = libraryItem.Id;
            job.StatusEnum = ImportJobStatus.Completed;
            job.StatusMessage = "Import completed successfully";
            job.ProgressPercentage = 100;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully completed import job {JobId} -> Library item {ItemId}", 
                jobId, libraryItem.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Import job {JobId} was cancelled", jobId);
            
            job.StatusEnum = ImportJobStatus.Cancelled;
            job.StatusMessage = "Import was cancelled";
            job.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process import job {JobId}", jobId);
            
            // Build detailed error message including inner exceptions
            var errorMessage = BuildDetailedErrorMessage(ex);
            
            job.StatusEnum = ImportJobStatus.Failed;
            job.StatusMessage = "Import failed";
            job.ErrorMessage = errorMessage;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "imported-audio";

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Remove non-ASCII characters (emojis, special characters) that cause HTTP header issues
        sanitized = new string(sanitized.Where(c => c < 128).ToArray());
        
        // Remove multiple consecutive underscores
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }
        
        // Trim underscores from start and end
        sanitized = sanitized.Trim('_');
        
        // If empty after sanitization, use default
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "imported-audio";
        }
        
        // Limit length
        if (sanitized.Length > 200)
            sanitized = sanitized.Substring(0, 200).TrimEnd('_');
        
        return sanitized;
    }

    /// <summary>
    /// Build detailed error message including all inner exceptions
    /// </summary>
    private static string BuildDetailedErrorMessage(Exception ex)
    {
        var messages = new List<string>();
        var current = ex;
        
        while (current != null)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }
        
        return string.Join(" ? ", messages);
    }
}
