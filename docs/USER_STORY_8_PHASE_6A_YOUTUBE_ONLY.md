# User Story 8 - Phase 6A: YouTube Import Only
## Sprint 7A: YouTube Audio Import (MVP)

**Status:** ?? Ready to Start  
**Branch:** `feature/user-story-8-phase-6a-youtube-import`  
**Estimated Effort:** 4-6 hours  
**Priority:** High  

---

## ?? Goals

Allow users to import audio from YouTube videos:
1. Paste YouTube URL
2. Preview video metadata (title, thumbnail, duration)
3. Download audio and convert to MP3
4. Save to user's library with chosen storage provider
5. Track import progress
6. Handle errors gracefully

**Scope Reduction:** Podcasts and Direct URLs moved to Phase 6B (future)

---

## ? Acceptance Criteria

### YouTube Import
- [ ] User can paste YouTube video URL
- [ ] System validates URL format before processing
- [ ] Shows preview with thumbnail, title, author, duration
- [ ] User can edit title/description before import
- [ ] System extracts audio and converts to MP3/M4A
- [ ] Shows download progress (spinner with status text)
- [ ] Auto-detects audio duration
- [ ] Saves to user's default storage provider
- [ ] Creates MediaLibraryItem record
- [ ] Shows success toast with link to library
- [ ] Handles errors (invalid URL, private video, age-restricted, etc.)
- [ ] Max duration validation (90 minutes)

---

## ?? Technical Plan

### 1. NuGet Packages to Add

```xml
<!-- BoxieHub.csproj -->
<PackageReference Include="YoutubeExplode" Version="6.3.16" />
```

**Why YoutubeExplode:**
- ? Free and open source (MIT license)
- ? No API key needed
- ? Actively maintained (last update: recent)
- ? Extracts audio-only streams efficiently
- ? Works with .NET 8
- ?? YouTube can change internals (plan for graceful failure)

**No Hangfire:** We'll build a lightweight background job system using:
- `IHostedService` for background processing
- `Channel<T>` for job queue
- `ConcurrentDictionary` for progress tracking
- Entity Framework for job persistence

---

### 2. Domain Models

```csharp
// BoxieHub/Models/ImportSource.cs
namespace BoxieHub.Models;

public enum ImportSource
{
    Upload = 0,      // Manual file upload
    YouTube = 1,     // YouTube video
    Podcast = 2,     // Podcast RSS feed (future)
    DirectUrl = 3    // Direct audio URL (future)
}

// BoxieHub/Models/ImportJobStatus.cs
public enum ImportJobStatus
{
    Pending = 0,     // Created but not started
    Validating = 1,  // Checking URL, fetching metadata
    Downloading = 2, // Downloading audio
    Processing = 3,  // Converting/processing audio
    Saving = 4,      // Saving to storage and database
    Completed = 5,   // Successfully completed
    Failed = 6,      // Failed with error
    Cancelled = 7    // User cancelled
}

// BoxieHub/Models/ImportJob.cs
public class ImportJob
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    
    public ImportSource Source { get; set; }
    
    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = default!;
    
    [MaxLength(500)]
    public string? SourceTitle { get; set; }
    
    [MaxLength(2000)]
    public string? SourceDescription { get; set; }
    
    [MaxLength(1024)]
    public string? SourceThumbnailUrl { get; set; }
    
    public float? SourceDurationSeconds { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Store as string for flexibility
    
    public ImportJobStatus StatusEnum 
    {
        get => Enum.Parse<ImportJobStatus>(Status);
        set => Status = value.ToString();
    }
    
    public int? MediaLibraryItemId { get; set; }
    public MediaLibraryItem? MediaLibraryItem { get; set; }
    
    /// <summary>
    /// Progress from 0-100
    /// </summary>
    public int ProgressPercentage { get; set; }
    
    /// <summary>
    /// User-friendly status message (e.g., "Downloading audio...", "Processing file...")
    /// </summary>
    [MaxLength(200)]
    public string? StatusMessage { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? StartedAt { get; set; }
    
    public DateTimeOffset? CompletedAt { get; set; }
}
```

---

### 3. Services to Implement

#### A. YouTube Import Service

```csharp
// BoxieHub/Services/Import/YouTubeVideoInfo.cs
namespace BoxieHub.Services.Import;

public class YouTubeVideoInfo
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Author { get; set; } = default!;
    public TimeSpan Duration { get; set; }
    public string ThumbnailUrl { get; set; } = default!;
    public DateTimeOffset UploadDate { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

// BoxieHub/Services/Import/IYouTubeImportService.cs
public interface IYouTubeImportService
{
    /// <summary>
    /// Validate YouTube URL and extract video metadata
    /// </summary>
    Task<YouTubeVideoInfo> GetVideoInfoAsync(string url, CancellationToken ct = default);
    
    /// <summary>
    /// Download audio from YouTube video
    /// Returns stream of audio data (M4A format from YouTube)
    /// </summary>
    Task<Stream> DownloadAudioAsync(
        string url, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default);
}

// BoxieHub/Services/Import/YouTubeImportService.cs
public class YouTubeImportService : IYouTubeImportService
{
    private readonly YoutubeClient _youtube;
    private readonly ILogger<YouTubeImportService> _logger;

    public YouTubeImportService(ILogger<YouTubeImportService> logger)
    {
        _youtube = new YoutubeClient();
        _logger = logger;
    }

    public async Task<YouTubeVideoInfo> GetVideoInfoAsync(string url, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching YouTube video info for URL: {Url}", url);
            
            // YoutubeExplode will throw if URL is invalid or video is unavailable
            var video = await _youtube.Videos.GetAsync(url, ct);
            
            // Validate duration (max 90 minutes = 5400 seconds)
            var duration = video.Duration ?? TimeSpan.Zero;
            if (duration.TotalSeconds > 5400)
            {
                return new YouTubeVideoInfo
                {
                    IsValid = false,
                    ErrorMessage = $"Video is too long ({duration.TotalMinutes:F0} minutes). Maximum allowed is 90 minutes."
                };
            }
            
            _logger.LogInformation("Successfully fetched video info: {Title} ({Duration})", 
                video.Title, duration);
            
            return new YouTubeVideoInfo
            {
                Id = video.Id,
                Title = video.Title,
                Description = video.Description ?? string.Empty,
                Author = video.Author.ChannelTitle,
                Duration = duration,
                ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url,
                UploadDate = video.UploadDate,
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch YouTube video info for URL: {Url}", url);
            
            return new YouTubeVideoInfo
            {
                IsValid = false,
                ErrorMessage = GetUserFriendlyErrorMessage(ex)
            };
        }
    }

    public async Task<Stream> DownloadAudioAsync(
        string url, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Downloading audio from YouTube: {Url}", url);
            
            // Get stream manifest
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url, ct);
            
            // Get best audio-only stream (prefer M4A)
            var audioStreamInfo = streamManifest
                .GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp4) // M4A container
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            if (audioStreamInfo == null)
            {
                // Fallback: try any audio stream
                audioStreamInfo = streamManifest
                    .GetAudioOnlyStreams()
                    .OrderByDescending(s => s.Bitrate)
                    .FirstOrDefault();
            }

            if (audioStreamInfo == null)
                throw new InvalidOperationException("No audio stream available for this video");

            _logger.LogInformation("Selected audio stream: {Bitrate} kbps, {Container}", 
                audioStreamInfo.Bitrate.KiloBitsPerSecond, audioStreamInfo.Container);

            // Download to memory stream with progress reporting
            var memoryStream = new MemoryStream();
            
            await _youtube.Videos.Streams.CopyToAsync(
                audioStreamInfo, 
                memoryStream, 
                progress, 
                ct);
            
            memoryStream.Position = 0;
            
            _logger.LogInformation("Successfully downloaded audio ({Size} bytes)", memoryStream.Length);
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download audio from YouTube: {Url}", url);
            throw;
        }
    }

    private string GetUserFriendlyErrorMessage(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        if (message.Contains("video is unavailable") || message.Contains("not available"))
            return "This video is unavailable or has been removed.";
        
        if (message.Contains("private"))
            return "This video is private and cannot be imported.";
        
        if (message.Contains("age") || message.Contains("restricted"))
            return "This video is age-restricted and cannot be imported.";
        
        if (message.Contains("country") || message.Contains("region"))
            return "This video is not available in your region.";
        
        if (message.Contains("invalid") || message.Contains("not found"))
            return "Invalid YouTube URL. Please check the link and try again.";
        
        return "Failed to access video. Please check the URL and try again.";
    }
}
```

#### B. Import Job Service (Orchestrator)

```csharp
// BoxieHub/Services/Import/IImportJobService.cs
public interface IImportJobService
{
    /// <summary>
    /// Create a new YouTube import job (validates and creates record)
    /// </summary>
    Task<ImportJob> CreateYouTubeImportJobAsync(
        string userId, 
        string youtubeUrl, 
        string? customTitle = null,
        string? customDescription = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get job status by ID
    /// </summary>
    Task<ImportJob?> GetJobAsync(int jobId, string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Get user's recent import jobs
    /// </summary>
    Task<List<ImportJob>> GetUserJobsAsync(
        string userId, 
        int take = 20, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Cancel a pending or running import job
    /// </summary>
    Task<bool> CancelJobAsync(int jobId, string userId, CancellationToken ct = default);
}

// BoxieHub/Services/Import/ImportJobService.cs
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

        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = customTitle ?? videoInfo.Title,
            SourceDescription = customDescription ?? videoInfo.Description,
            SourceThumbnailUrl = videoInfo.ThumbnailUrl,
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
```

#### C. Background Job Processor (Custom Implementation)

```csharp
// BoxieHub/Services/Import/ImportJobProcessor.cs
/// <summary>
/// Background service that processes import jobs from the queue
/// Lightweight alternative to Hangfire - no additional costs!
/// </summary>
public class ImportJobProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImportJobProcessor> _logger;
    private readonly Channel<int> _jobQueue;
    
    // Track active jobs for progress updates
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
            var progress = new Progress<double>(percent =>
            {
                // Update progress in database (throttled to avoid too many writes)
                var progressInt = (int)(10 + percent * 0.7); // 10-80%
                if (progressInt > job.ProgressPercentage + 5) // Only update every 5%
                {
                    job.ProgressPercentage = progressInt;
                    dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }
            });

            using var audioStream = await youtubeService.DownloadAudioAsync(
                job.SourceUrl, 
                progress, 
                ct);

            job.StatusEnum = ImportJobStatus.Saving;
            job.StatusMessage = "Saving to library...";
            job.ProgressPercentage = 85;
            await dbContext.SaveChangesAsync(ct);

            // Get user's default storage provider
            var storageProvider = await storagePreferenceService.GetDefaultProviderAsync(job.UserId, ct);

            // Create library item
            var libraryItemDto = new MediaLibraryItemDto
            {
                Title = job.SourceTitle ?? "Imported from YouTube",
                Description = job.SourceDescription,
                Category = "Audio",
                Tags = new List<string> { "youtube-import", "imported" },
                ContentType = "audio/mp4", // M4A from YouTube
                OriginalFileName = $"{job.SourceTitle}.m4a",
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
            
            job.StatusEnum = ImportJobStatus.Failed;
            job.StatusMessage = "Import failed";
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
```

---

### 4. Database Migration

```bash
# Run this command to create migration:
dotnet ef migrations add AddImportJobs --project BoxieHub --startup-project BoxieHub
```

```csharp
// BoxieHub/Migrations/[Timestamp]_AddImportJobs.cs
// (Auto-generated by EF Core)
```

---

### 5. Blazor Components

#### A. YouTube Import Page

```razor
@* BoxieHub/Components/Pages/Library/ImportYouTube.razor *@
@page "/library/import/youtube"
@rendermode InteractiveServer
@using BoxieHub.Services.Import
@using BoxieHub.Models
@using System.Security.Claims
@inject IImportJobService ImportJobService
@inject IYouTubeImportService YouTubeService
@inject ImportJobProcessor JobProcessor
@inject AuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation
@inject ILogger<ImportYouTube> Logger
@attribute [Authorize]

<PageTitle>Import from YouTube - BoxieHub</PageTitle>

<div class="container mt-4">
    <nav aria-label="breadcrumb">
        <ol class="breadcrumb">
            <li class="breadcrumb-item"><a href="/library">Library</a></li>
            <li class="breadcrumb-item active">Import from YouTube</li>
        </ol>
    </nav>

    <div class="row">
        <div class="col-lg-8">
            <div class="card shadow-sm">
                <div class="card-header bg-danger text-white">
                    <h4 class="mb-0">
                        <i class="bi bi-youtube"></i> Import from YouTube
                    </h4>
                </div>
                <div class="card-body">
                    <div class="mb-3">
                        <label class="form-label">YouTube URL *</label>
                        <input type="text" 
                               class="form-control @(validationError != null ? "is-invalid" : "")" 
                               @bind="youtubeUrl"
                               @oninput="OnUrlChanged"
                               placeholder="https://www.youtube.com/watch?v=..."
                               disabled="@isProcessing" />
                        @if (validationError != null)
                        {
                            <div class="invalid-feedback d-block">
                                @validationError
                            </div>
                        }
                        <small class="text-muted">
                            Paste any YouTube video URL (max 90 minutes)
                        </small>
                    </div>

                    @if (isLoadingPreview)
                    {
                        <div class="text-center py-4">
                            <div class="spinner-border text-primary"></div>
                            <p class="mt-2 text-muted">Loading video preview...</p>
                        </div>
                    }
                    else if (videoInfo != null && videoInfo.IsValid)
                    {
                        <!-- Video Preview -->
                        <div class="card mb-3 border-primary">
                            <div class="row g-0">
                                <div class="col-md-4">
                                    <img src="@videoInfo.ThumbnailUrl" 
                                         class="img-fluid rounded-start" 
                                         alt="Video thumbnail"
                                         style="max-height: 200px; object-fit: cover; width: 100%;">
                                </div>
                                <div class="col-md-8">
                                    <div class="card-body">
                                        <h5 class="card-title">@videoInfo.Title</h5>
                                        <p class="card-text small text-muted mb-2">
                                            <i class="bi bi-person"></i> @videoInfo.Author
                                        </p>
                                        <p class="card-text small mb-2">
                                            <i class="bi bi-clock"></i> Duration: @FormatDuration(videoInfo.Duration)
                                        </p>
                                        <p class="card-text small text-muted">
                                            <i class="bi bi-calendar"></i> Uploaded: @videoInfo.UploadDate.ToString("MMM dd, yyyy")
                                        </p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <!-- Editable Fields -->
                        <div class="mb-3">
                            <label class="form-label">Title (editable)</label>
                            <input type="text" 
                                   class="form-control" 
                                   @bind="customTitle"
                                   maxlength="200"
                                   disabled="@isProcessing" />
                        </div>

                        <div class="mb-3">
                            <label class="form-label">Description (optional)</label>
                            <textarea class="form-control" 
                                      rows="3" 
                                      @bind="customDescription"
                                      maxlength="1000"
                                      disabled="@isProcessing"></textarea>
                        </div>

                        <button class="btn btn-primary btn-lg" 
                                @onclick="StartImport" 
                                disabled="@isProcessing">
                            @if (isProcessing)
                            {
                                <span class="spinner-border spinner-border-sm me-2"></span>
                                Starting import...
                            }
                            else
                            {
                                <i class="bi bi-download"></i>
                                <span>Import to Library</span>
                            }
                        </button>
                    }
                </div>
            </div>

            @if (recentJobs.Any())
            {
                <div class="card shadow-sm mt-4">
                    <div class="card-header">
                        <h5 class="mb-0"><i class="bi bi-clock-history"></i> Recent Imports</h5>
                    </div>
                    <div class="list-group list-group-flush">
                        @foreach (var job in recentJobs.Take(5))
                        {
                            <div class="list-group-item">
                                <div class="d-flex justify-content-between align-items-center">
                                    <div class="flex-grow-1">
                                        <h6 class="mb-1">@job.SourceTitle</h6>
                                        <small class="text-muted">
                                            @job.Created.ToString("MMM dd, yyyy h:mm tt") · 
                                            @job.Status
                                        </small>
                                    </div>
                                    @if (job.StatusEnum == ImportJobStatus.Completed && job.MediaLibraryItemId.HasValue)
                                    {
                                        <a href="/library/@job.MediaLibraryItemId" class="btn btn-sm btn-outline-primary">
                                            <i class="bi bi-box-arrow-up-right"></i> View
                                        </a>
                                    }
                                </div>
                                @if (job.StatusEnum is ImportJobStatus.Downloading or ImportJobStatus.Processing or ImportJobStatus.Saving)
                                {
                                    <div class="progress mt-2" style="height: 20px;">
                                        <div class="progress-bar progress-bar-striped progress-bar-animated" 
                                             style="width: @(job.ProgressPercentage)%">
                                            @job.ProgressPercentage%
                                        </div>
                                    </div>
                                    <small class="text-muted">@job.StatusMessage</small>
                                }
                            </div>
                        }
                    </div>
                </div>
            }
        </div>

        <!-- Sidebar -->
        <div class="col-lg-4">
            <div class="card shadow-sm">
                <div class="card-body">
                    <h5><i class="bi bi-info-circle"></i> How it Works</h5>
                    <ol class="small">
                        <li>Paste a YouTube video URL</li>
                        <li>Preview video details</li>
                        <li>Edit title/description if needed</li>
                        <li>Click "Import to Library"</li>
                        <li>Audio will be extracted and saved</li>
                    </ol>
                    
                    <hr />
                    
                    <h6><i class="bi bi-check-circle"></i> Supported</h6>
                    <ul class="small mb-0">
                        <li>Any public YouTube video</li>
                        <li>Up to 90 minutes duration</li>
                        <li>High-quality audio (M4A)</li>
                        <li>Automatic metadata</li>
                    </ul>
                    
                    <hr />
                    
                    <h6><i class="bi bi-x-circle"></i> Not Supported</h6>
                    <ul class="small mb-0">
                        <li>Private videos</li>
                        <li>Age-restricted content</li>
                        <li>Region-blocked videos</li>
                        <li>Live streams</li>
                    </ul>
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    private string youtubeUrl = string.Empty;
    private string customTitle = string.Empty;
    private string customDescription = string.Empty;
    private YouTubeVideoInfo? videoInfo;
    private string? validationError;
    private bool isLoadingPreview;
    private bool isProcessing;
    private List<ImportJob> recentJobs = new();
    private System.Threading.Timer? debounceTimer;
    private System.Threading.Timer? pollTimer;

    protected override async Task OnInitializedAsync()
    {
        await LoadRecentJobs();
        StartPolling();
    }

    private void OnUrlChanged(ChangeEventArgs e)
    {
        youtubeUrl = e.Value?.ToString() ?? string.Empty;
        validationError = null;
        videoInfo = null;
        
        // Debounce preview loading (wait 500ms after user stops typing)
        debounceTimer?.Dispose();
        debounceTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await LoadPreview();
                StateHasChanged();
            });
        }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task LoadPreview()
    {
        if (string.IsNullOrWhiteSpace(youtubeUrl))
        {
            videoInfo = null;
            return;
        }
        
        isLoadingPreview = true;
        validationError = null;
        
        try
        {
            videoInfo = await YouTubeService.GetVideoInfoAsync(youtubeUrl);
            
            if (!videoInfo.IsValid)
            {
                validationError = videoInfo.ErrorMessage;
                videoInfo = null;
            }
            else
            {
                customTitle = videoInfo.Title;
                customDescription = videoInfo.Description;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load YouTube preview");
            validationError = "Failed to load video preview. Please check the URL.";
            videoInfo = null;
        }
        finally
        {
            isLoadingPreview = false;
        }
    }

    private async Task StartImport()
    {
        if (videoInfo == null || !videoInfo.IsValid)
            return;

        isProcessing = true;
        
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                validationError = "User not authenticated";
                return;
            }

            // Create job in database
            var job = await ImportJobService.CreateYouTubeImportJobAsync(
                userId,
                youtubeUrl,
                customTitle,
                customDescription);

            // Enqueue for background processing
            await JobProcessor.EnqueueJobAsync(job.Id);

            Logger.LogInformation("Started YouTube import job {JobId}", job.Id);

            // Add to recent jobs list
            recentJobs.Insert(0, job);

            // Clear form
            youtubeUrl = string.Empty;
            customTitle = string.Empty;
            customDescription = string.Empty;
            videoInfo = null;
            validationError = null;

            // Show success message (you could use a toast notification here)
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start YouTube import");
            validationError = ex.Message;
        }
        finally
        {
            isProcessing = false;
        }
    }

    private async Task LoadRecentJobs()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                recentJobs = await ImportJobService.GetUserJobsAsync(userId, take: 10);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load recent jobs");
        }
    }

    private void StartPolling()
    {
        // Poll for job updates every 2 seconds
        pollTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await LoadRecentJobs();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    public void Dispose()
    {
        debounceTimer?.Dispose();
        pollTimer?.Dispose();
    }
}
```

---

### 6. Service Registration

```csharp
// BoxieHub/Program.cs or ServiceRegistrationExtensions.cs

// Add to DI container
builder.Services.AddScoped<IYouTubeImportService, YouTubeImportService>();
builder.Services.AddScoped<IImportJobService, ImportJobService>();

// Register background job processor as singleton (runs once per app lifetime)
builder.Services.AddSingleton<ImportJobProcessor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ImportJobProcessor>());

// Add ImportJob DbSet to ApplicationDbContext
// (Already done if you ran the migration)
```

---

### 7. Add to ApplicationDbContext

```csharp
// BoxieHub/Data/ApplicationDbContext.cs

public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // ... existing configurations ...
    
    // ImportJob indexes
    modelBuilder.Entity<ImportJob>(entity =>
    {
        entity.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_ImportJobs_UserId");
        
        entity.HasIndex(e => e.Status)
            .HasDatabaseName("IX_ImportJobs_Status");
        
        entity.HasIndex(e => e.Created)
            .HasDatabaseName("IX_ImportJobs_Created");
        
        entity.HasOne(e => e.MediaLibraryItem)
            .WithMany()
            .HasForeignKey(e => e.MediaLibraryItemId)
            .OnDelete(DeleteBehavior.SetNull);
    });
}
```

---

## ?? Implementation Order

### Phase 1: Core Infrastructure (1 hour)
1. ? Add YoutubeExplode NuGet package
2. ? Create domain models (ImportJob, ImportSource, ImportJobStatus)
3. ? Add DbSet to ApplicationDbContext
4. ? Create and run database migration

### Phase 2: Services (2 hours)
1. ? Implement YouTubeImportService (fetch metadata + download)
2. ? Implement ImportJobService (CRUD operations)
3. ? Implement ImportJobProcessor (background worker)
4. ? Register services in DI

### Phase 3: UI (2 hours)
1. ? Create ImportYouTube.razor page
2. ? Add URL validation and preview
3. ? Add progress tracking UI
4. ? Add navigation link in TopNavmenu

### Phase 4: Testing & Polish (1 hour)
1. ? Test with various YouTube videos
2. ? Test error handling (invalid URLs, private videos)
3. ? Test background job processing
4. ? Add toast notifications
5. ? Update documentation

---

## ?? Testing Checklist

### Manual Testing
- [ ] Import a regular YouTube video (< 90 min)
- [ ] Try an invalid URL
- [ ] Try a private video
- [ ] Try an age-restricted video
- [ ] Try a video > 90 minutes
- [ ] Test with slow internet (progress updates)
- [ ] Restart app while import is running (job resumes)
- [ ] Import multiple videos concurrently
- [ ] Check library item created correctly
- [ ] Verify audio plays correctly

### Edge Cases
- [ ] Very short video (< 1 minute)
- [ ] Very long video (89 minutes - max allowed)
- [ ] Video with no description
- [ ] Video with special characters in title
- [ ] Video from different regions/languages

---

## ?? Success Metrics

- [ ] Users can import YouTube audio in < 30 seconds (typical video)
- [ ] Preview loads in < 2 seconds
- [ ] Progress updates every 2 seconds
- [ ] Error messages are clear and helpful
- [ ] Background jobs survive app restarts
- [ ] No memory leaks with concurrent imports
- [ ] All tests passing

---

## ?? Security & Legal Considerations

### YouTube Terms of Service
?? **Important:** This feature extracts audio from YouTube videos. While technically possible:
- YouTube's ToS generally prohibit downloading content
- Use at your own risk for personal use only
- Do NOT use for redistribution or commercial purposes
- Recommend adding a disclaimer to the UI

### Suggested Disclaimer Text:
```
"This feature is for personal use only. By using this feature, you agree to 
comply with YouTube's Terms of Service. Do not download copyrighted content 
without permission."
```

### Rate Limiting (Optional)
Add to prevent abuse:
- Max 10 imports per hour per user
- Max 50 imports per day per user

---

## ?? Known Limitations

1. **YouTube Changes:** YouTube can change their internals, breaking YoutubeExplode
   - **Mitigation:** Monitor library updates, have graceful error handling
   
2. **Large Files:** Videos near 90 minutes may use significant memory
   - **Mitigation:** Stream to disk instead of memory if needed
   
3. **No Live Streams:** YoutubeExplode doesn't support live streams
   - **Mitigation:** Detect and show clear error message

---

## ?? References

- [YoutubeExplode GitHub](https://github.com/Tyrrrz/YoutubeExplode)
- [YoutubeExplode Documentation](https://github.com/Tyrrrz/YoutubeExplode/wiki)
- [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels)
- [IHostedService Background Tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

---

## ? Ready to Start!

**Estimated Time:** 4-6 hours  
**Complexity:** Medium  
**Dependencies:** YoutubeExplode (free, MIT license)  
**Cost:** $0 (no Hangfire subscription needed!)  

**Branch:** `feature/user-story-8-phase-6a-youtube-import`

**First Command:**
```bash
dotnet add BoxieHub/BoxieHub.csproj package YoutubeExplode
```

Let's build this! ????
