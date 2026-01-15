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
        var podcastService = scope.ServiceProvider.GetRequiredService<IPodcastImportService>();
        var mediaLibraryService = scope.ServiceProvider.GetRequiredService<IMediaLibraryService>();
        var storagePreferenceService = scope.ServiceProvider.GetRequiredService<IStoragePreferenceService>();
        var audioAnalysisService = scope.ServiceProvider.GetRequiredService<Audio.IAudioAnalysisService>();

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
            
            // **DEDUPLICATION CHECK FOR PODCASTS**
            FileUpload? existingFile = null;
            if (job.Source == ImportSource.Podcast)
            {
                // Check if episode already downloaded (by AudioUrl)
                var existingCache = await dbContext.PodcastEpisodeCache
                    .Include(e => e.FileUpload)
                    .FirstOrDefaultAsync(e => e.AudioUrl == job.SourceUrl && e.FileUploadId != null, ct);

                if (existingCache?.FileUpload != null)
                {
                    existingFile = existingCache.FileUpload;
                    _logger.LogInformation("Found existing file for podcast episode {JobId}: {FileId} (ImportCount: {Count})",
                        jobId, existingFile.Id, existingCache.ImportCount);
                    
                    job.StatusEnum = ImportJobStatus.Saving;
                    job.StatusMessage = "Reusing existing file (already downloaded)...";
                    job.ProgressPercentage = 90;
                    await dbContext.SaveChangesAsync(ct);
                    
                    // Increment import count
                    existingCache.ImportCount++;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            Stream? audioStream = null;
            string contentType;
            string fileExtension;

            if (existingFile == null)
            {
                // Need to download
                job.StatusEnum = ImportJobStatus.Downloading;
                job.StatusMessage = job.Source == ImportSource.YouTube 
                    ? "Downloading audio from YouTube..." 
                    : "Downloading podcast episode...";
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

                if (job.Source == ImportSource.YouTube)
                {
                    audioStream = await youtubeService.DownloadAudioAsync(
                        job.SourceUrl,
                        progressReporter,
                        ct);
                    contentType = "audio/mp4"; // M4A from YouTube
                    fileExtension = "m4a";
                }
                else if (job.Source == ImportSource.Podcast)
                {
                    audioStream = await podcastService.DownloadEpisodeAsync(
                        job.SourceUrl,
                        progressReporter,
                        ct);
                    // Try to determine content type from URL extension
                    contentType = GuessContentType(job.SourceUrl);
                    fileExtension = GetFileExtension(contentType);
                }
                else
                {
                    throw new NotSupportedException($"Import source {job.Source} not supported yet");
                }
                
                // ? VERIFY AUDIO DURATION AFTER DOWNLOAD
                if (audioStream != null)
                {
                    try
                    {
                        audioStream.Position = 0; // Reset stream to beginning
                        
                        _logger.LogInformation("Verifying audio duration for job {JobId} (Source: {Source})", 
                            jobId, job.Source);
                        
                        var actualDuration = await audioAnalysisService.GetActualDurationAsync(
                            audioStream, 
                            contentType, 
                            ct);
                        
                        if (actualDuration.HasValue)
                        {
                            var claimedDuration = job.SourceDurationSeconds ?? 0f;
                            var discrepancy = Math.Abs(claimedDuration - actualDuration.Value);
                            
                            // Log if there's a significant discrepancy (>1 second)
                            if (discrepancy > 1.0f)
                            {
                                _logger.LogWarning(
                                    "Duration mismatch for {Source} job {JobId}: " +
                                    "Claimed {Claimed}s ({ClaimedFormatted}), " +
                                    "Actual {Actual}s ({ActualFormatted}), " +
                                    "Difference: {Diff}s",
                                    job.Source, jobId, 
                                    claimedDuration, FormatDuration(claimedDuration),
                                    actualDuration.Value, FormatDuration(actualDuration.Value),
                                    discrepancy);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "Duration verified for job {JobId}: {Duration}s ({Formatted})",
                                    jobId, actualDuration.Value, FormatDuration(actualDuration.Value));
                            }
                            
                            // ? UPDATE WITH VERIFIED DURATION
                            job.SourceDurationSeconds = actualDuration.Value;
                            
                            // ? VALIDATE TONIE 90-MINUTE LIMIT
                            const float MAX_TONIE_DURATION = 90 * 60; // 90 minutes in seconds
                            if (actualDuration.Value > MAX_TONIE_DURATION)
                            {
                                var minutes = (int)(actualDuration.Value / 60);
                                throw new InvalidOperationException(
                                    $"Audio too long ({minutes} minutes). " +
                                    $"Tonies support maximum 90 minutes per chapter. " +
                                    $"Please trim the audio or split it into multiple parts.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Could not verify audio duration for job {JobId}. Using claimed duration: {Duration}s",
                                jobId, job.SourceDurationSeconds);
                        }
                        
                        audioStream.Position = 0; // Reset stream for upload
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, 
                            "Failed to verify audio duration for job {JobId}. Continuing with claimed duration.",
                            jobId);
                        audioStream.Position = 0; // Ensure stream is reset even on error
                    }
                }
            }
            else
            {
                // Reuse existing file
                contentType = existingFile.ContentType ?? "audio/mpeg";
                fileExtension = GetFileExtension(contentType);
            }

            using (audioStream)
            {
                job.StatusEnum = ImportJobStatus.Saving;
                job.StatusMessage = "Saving to library...";
                job.ProgressPercentage = 85;
                await dbContext.SaveChangesAsync(ct);

                // Truncate description to fit MediaLibraryItem constraint (max 1000 chars)
                var description = job.SourceDescription;
                if (description?.Length > 1000)
                {
                    description = description.Substring(0, 997) + "...";
                    _logger.LogInformation("Truncated description for job {JobId} from {OriginalLength} to 1000 characters",
                        jobId, job.SourceDescription?.Length);
                }

                // Create library item
                var tag = job.Source == ImportSource.YouTube ? "youtube-import" : "podcast-import";
                var category = job.Source == ImportSource.Podcast ? "Podcast" : (job.Category ?? "Other");
                
                var libraryItemDto = new MediaLibraryItemDto
                {
                    Title = job.SourceTitle ?? $"Imported from {job.Source}",
                    Description = description,
                    Category = category,
                    Tags = new List<string> { tag, "imported" },
                    ContentType = contentType,
                    OriginalFileName = $"{SanitizeFileName(job.SourceTitle)}.{fileExtension}",
                    FileSizeBytes = audioStream?.Length ?? existingFile?.FileSizeBytes ?? 0,
                    DurationSeconds = job.SourceDurationSeconds ?? 0
                };

                MediaLibraryItem libraryItem;

                if (existingFile != null)
                {
                    // Check if user already has a library item for this file
                    var existingUserLibraryItem = await dbContext.MediaLibraryItems
                        .FirstOrDefaultAsync(m => m.UserId == job.UserId && 
                                                   m.FileUploadId == existingFile.Id, ct);
                    
                    if (existingUserLibraryItem != null)
                    {
                        // User already has this in their library - reuse it!
                        libraryItem = existingUserLibraryItem;
                        _logger.LogInformation("User already has library item {ItemId} for file {FileId} - reusing",
                            libraryItem.Id, existingFile.Id);
                    }
                    else
                    {
                        // Reuse existing file but create new library item for this user
                        var storageProvider = await storagePreferenceService.GetDefaultProviderAsync(job.UserId, ct);
                        libraryItem = await mediaLibraryService.AddExistingFileToLibraryAsync(
                            job.UserId,
                            existingFile.Id,
                            libraryItemDto,
                            ct);

                        _logger.LogInformation("Created new library item {ItemId} reusing existing file {FileId}",
                            libraryItem.Id, existingFile.Id);
                    }
                }
                else if (audioStream != null)
                {
                    // Upload new file
                    var storageProvider = await storagePreferenceService.GetDefaultProviderAsync(job.UserId, ct);
                    libraryItem = await mediaLibraryService.AddToLibraryAsync(
                        job.UserId,
                        audioStream,
                        libraryItemDto,
                        storageProvider,
                        null,
                        ct);

                    _logger.LogInformation("Uploaded new file for library item {ItemId}", libraryItem.Id);

                    // **LINK FILE TO PODCAST CACHE (FOR FUTURE DEDUP)**
                    if (job.Source == ImportSource.Podcast && libraryItem.FileUploadId != Guid.Empty)
                    {
                        var episodeCache = await dbContext.PodcastEpisodeCache
                            .FirstOrDefaultAsync(e => e.AudioUrl == job.SourceUrl, ct);

                        if (episodeCache != null && episodeCache.FileUploadId == null)
                        {
                            episodeCache.FileUploadId = libraryItem.FileUploadId;
                            episodeCache.ImportCount = 1;
                            await dbContext.SaveChangesAsync(ct);

                            _logger.LogInformation("Linked FileUpload {FileId} to episode cache {CacheId}",
                                libraryItem.FileUploadId, episodeCache.Id);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("No audio stream or existing file available");
                }

                job.MediaLibraryItemId = libraryItem.Id;
                job.StatusEnum = ImportJobStatus.Completed;
                job.StatusMessage = existingFile != null 
                    ? "Import completed (reused existing file)" 
                    : "Import completed successfully";
                job.ProgressPercentage = 100;
                job.CompletedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(ct);

                _logger.LogInformation("Successfully completed import job {JobId} -> Library item {ItemId}",
                    jobId, libraryItem.Id);
            }
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

    private static string GuessContentType(string url)
    {
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            _ => "audio/mpeg" // Default to MP3
        };
    }

    private static string GetFileExtension(string contentType)
    {
        return contentType switch
        {
            "audio/mpeg" => "mp3",
            "audio/mp4" => "m4a",
            "audio/ogg" => "ogg",
            "audio/wav" => "wav",
            _ => "mp3" // Default
        };
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

    /// <summary>
    /// Format duration in seconds to human-readable string (for logging)
    /// </summary>
    private static string FormatDuration(float seconds)
    {
        if (seconds >= 3600)
        {
            var hours = (int)(seconds / 3600);
            var minutes = (int)((seconds % 3600) / 60);
            return $"{hours}h {minutes}m";
        }
        if (seconds >= 60)
        {
            var minutes = (int)(seconds / 60);
            var secs = (int)(seconds % 60);
            return $"{minutes}m {secs}s";
        }
        return $"{seconds:F1}s";
    }
}
