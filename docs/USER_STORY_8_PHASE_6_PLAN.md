# User Story 8 - Phase 6: Import from URLs
## Sprint 7: External Media Import

**Status:** ?? Ready to Start  
**Branch:** `feature/user-story-8-phase-6-import`  
**Estimated Effort:** 8-12 hours  
**Priority:** High  

---

## ?? Goals

Allow users to import audio from external sources:
1. **YouTube Videos** - Extract and convert audio
2. **Podcast RSS Feeds** - Import episodes
3. **Direct URLs** - Download audio files

---

## ? Acceptance Criteria

### YouTube Import
- [ ] User can paste YouTube video URL
- [ ] System extracts audio and converts to MP3
- [ ] Auto-fills title, description from video metadata
- [ ] Shows video thumbnail as preview
- [ ] Validates URL before processing
- [ ] Shows download progress
- [ ] Handles YouTube age restrictions
- [ ] Handles private/unavailable videos gracefully

### Podcast Import
- [ ] User can paste podcast RSS feed URL
- [ ] System parses feed and shows episodes list
- [ ] User can select which episodes to import
- [ ] Auto-fills title, description, cover art from feed
- [ ] Shows episode duration before import
- [ ] Supports common podcast formats (MP3, M4A)
- [ ] Handles pagination for large feeds

### Direct URL Import
- [ ] User can paste direct audio file URL
- [ ] System detects audio format
- [ ] Downloads and stores in library
- [ ] Validates URL is accessible
- [ ] Shows download progress
- [ ] Handles HTTP errors (404, 403, etc.)

### General Requirements
- [ ] All imports save to user's library
- [ ] User chooses storage provider per import
- [ ] Auto-detects audio duration
- [ ] Comprehensive error handling
- [ ] Toast notifications for success/failure
- [ ] Cancel in-progress imports

---

## ?? Technical Plan

### 1. NuGet Packages to Add

```xml
<!-- BoxieHub.csproj -->
<PackageReference Include="YoutubeExplode" Version="6.3.16" />
<PackageReference Include="TagLibSharp" Version="2.3.0" />
<PackageReference Include="System.ServiceModel.Syndication" Version="8.0.0" />
```

**Package Descriptions:**
- **YoutubeExplode:** Download YouTube videos and extract metadata
- **TagLibSharp:** Read/write audio metadata (ID3 tags)
- **System.ServiceModel.Syndication:** Parse RSS/Atom podcast feeds

---

### 2. Domain Models

```csharp
// BoxieHub/Models/ImportSource.cs
public enum ImportSource
{
    Upload,
    YouTube,
    Podcast,
    DirectUrl
}

// BoxieHub/Models/ImportJob.cs
public class ImportJob
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ImportSource Source { get; set; }
    public string SourceUrl { get; set; } = default!;
    public string? SourceTitle { get; set; }
    public string? SourceDescription { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed
    public int? MediaLibraryItemId { get; set; }
    public MediaLibraryItem? MediaLibraryItem { get; set; }
    public int ProgressPercentage { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

---

### 3. Services to Implement

#### A. YouTube Import Service

```csharp
// BoxieHub/Services/Import/IYouTubeImportService.cs
public interface IYouTubeImportService
{
    Task<YouTubeVideoInfo> GetVideoInfoAsync(string url, CancellationToken ct = default);
    Task<Stream> DownloadAudioAsync(string url, IProgress<double> progress, CancellationToken ct = default);
}

public class YouTubeVideoInfo
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Author { get; set; } = default!;
    public TimeSpan Duration { get; set; }
    public string ThumbnailUrl { get; set; } = default!;
    public DateTimeOffset UploadDate { get; set; }
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
        // Use YoutubeExplode to get metadata
        var video = await _youtube.Videos.GetAsync(url, ct);
        
        return new YouTubeVideoInfo
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            Author = video.Author.ChannelTitle,
            Duration = video.Duration ?? TimeSpan.Zero,
            ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url,
            UploadDate = video.UploadDate
        };
    }

    public async Task<Stream> DownloadAudioAsync(string url, IProgress<double> progress, CancellationToken ct = default)
    {
        // Get stream manifest
        var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url, ct);
        
        // Get best audio-only stream
        var audioStreamInfo = streamManifest
            .GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp4) // M4A
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        if (audioStreamInfo == null)
            throw new InvalidOperationException("No audio stream available");

        // Download to memory stream with progress
        var memoryStream = new MemoryStream();
        await _youtube.Videos.Streams.CopyToAsync(audioStreamInfo, memoryStream, progress, ct);
        memoryStream.Position = 0;
        
        return memoryStream;
    }
}
```

#### B. Podcast Import Service

```csharp
// BoxieHub/Services/Import/IPodcastImportService.cs
public interface IPodcastImportService
{
    Task<PodcastFeed> ParseFeedAsync(string feedUrl, CancellationToken ct = default);
    Task<Stream> DownloadEpisodeAsync(string episodeUrl, IProgress<double> progress, CancellationToken ct = default);
}

public class PodcastFeed
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? ImageUrl { get; set; }
    public List<PodcastEpisode> Episodes { get; set; } = new();
}

public class PodcastEpisode
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string AudioUrl { get; set; } = default!;
    public DateTimeOffset PublishDate { get; set; }
    public TimeSpan Duration { get; set; }
    public long FileSize { get; set; }
}

// BoxieHub/Services/Import/PodcastImportService.cs
public class PodcastImportService : IPodcastImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PodcastImportService> _logger;

    public PodcastImportService(HttpClient httpClient, ILogger<PodcastImportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PodcastFeed> ParseFeedAsync(string feedUrl, CancellationToken ct = default)
    {
        // Download RSS feed
        var response = await _httpClient.GetAsync(feedUrl, ct);
        response.EnsureSuccessStatusCode();
        
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = System.Xml.XmlReader.Create(stream);
        
        var feed = SyndicationFeed.Load(reader);
        
        return new PodcastFeed
        {
            Title = feed.Title.Text,
            Description = feed.Description.Text,
            ImageUrl = feed.ImageUrl?.ToString(),
            Episodes = feed.Items.Select(item => new PodcastEpisode
            {
                Title = item.Title.Text,
                Description = item.Summary?.Text ?? string.Empty,
                AudioUrl = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
                PublishDate = item.PublishDate,
                Duration = TimeSpan.Zero, // Parse from iTunes tags if available
                FileSize = 0 // Parse from enclosure if available
            }).ToList()
        };
    }

    public async Task<Stream> DownloadEpisodeAsync(string episodeUrl, IProgress<double> progress, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(episodeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var memoryStream = new MemoryStream();
        
        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;
        
        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytesRead += bytesRead;
            
            if (totalBytes > 0)
            {
                progress?.Report((double)totalBytesRead / totalBytes * 100);
            }
        }
        
        memoryStream.Position = 0;
        return memoryStream;
    }
}
```

#### C. Import Job Service (Orchestrator)

```csharp
// BoxieHub/Services/Import/IImportJobService.cs
public interface IImportJobService
{
    Task<ImportJob> StartYouTubeImportAsync(string userId, string youtubeUrl, CancellationToken ct = default);
    Task<ImportJob> StartPodcastImportAsync(string userId, string feedUrl, string episodeUrl, CancellationToken ct = default);
    Task<ImportJob> StartDirectUrlImportAsync(string userId, string audioUrl, CancellationToken ct = default);
    Task<ImportJob?> GetJobStatusAsync(int jobId, CancellationToken ct = default);
    Task CancelImportAsync(int jobId, CancellationToken ct = default);
}

// BoxieHub/Services/Import/ImportJobService.cs
public class ImportJobService : IImportJobService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IMediaLibraryService _mediaLibraryService;
    private readonly IYouTubeImportService _youtubeService;
    private readonly IPodcastImportService _podcastService;
    private readonly ILogger<ImportJobService> _logger;

    // ... implementation
}
```

---

### 4. Database Migration

```csharp
// BoxieHub/Migrations/[Timestamp]_AddImportJobs.cs
public partial class AddImportJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ImportJobs",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<string>(maxLength: 450, nullable: false),
                Source = table.Column<int>(nullable: false),
                SourceUrl = table.Column<string>(maxLength: 2048, nullable: false),
                SourceTitle = table.Column<string>(maxLength: 500, nullable: true),
                SourceDescription = table.Column<string>(maxLength: 2000, nullable: true),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                MediaLibraryItemId = table.Column<int>(nullable: true),
                ProgressPercentage = table.Column<int>(nullable: false),
                ErrorMessage = table.Column<string>(nullable: true),
                Created = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ImportJobs_MediaLibraryItems_MediaLibraryItemId",
                    column: x => x.MediaLibraryItemId,
                    principalTable: "MediaLibraryItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ImportJobs_UserId",
            table: "ImportJobs",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ImportJobs_Status",
            table: "ImportJobs",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ImportJobs_Created",
            table: "ImportJobs",
            column: "Created");
    }
}
```

---

### 5. Blazor Components

#### A. Import Page

```razor
@* BoxieHub/Components/Pages/Library/Import.razor *@
@page "/library/import"
@rendermode InteractiveServer
@attribute [Authorize]

<PageTitle>Import Audio - BoxieHub</PageTitle>

<div class="container mt-4">
    <h2><i class="bi bi-download"></i> Import Audio from URL</h2>
    
    <div class="row mt-4">
        <div class="col-lg-8">
            <div class="card shadow-sm">
                <div class="card-body">
                    <!-- Tab Navigation -->
                    <ul class="nav nav-tabs" role="tablist">
                        <li class="nav-item">
                            <button class="nav-link @(activeTab == "youtube" ? "active" : "")" 
                                    @onclick="() => activeTab = \"youtube\"">
                                <i class="bi bi-youtube"></i> YouTube
                            </button>
                        </li>
                        <li class="nav-item">
                            <button class="nav-link @(activeTab == "podcast" ? "active" : "")" 
                                    @onclick="() => activeTab = \"podcast\"">
                                <i class="bi bi-broadcast"></i> Podcast
                            </button>
                        </li>
                        <li class="nav-item">
                            <button class="nav-link @(activeTab == "url" ? "active" : "")" 
                                    @onclick="() => activeTab = \"url\"">
                                <i class="bi bi-link-45deg"></i> Direct URL
                            </button>
                        </li>
                    </ul>

                    <!-- Tab Content -->
                    <div class="tab-content mt-3">
                        @if (activeTab == "youtube")
                        {
                            <YouTubeImportForm OnImportStarted="HandleImportStarted" />
                        }
                        else if (activeTab == "podcast")
                        {
                            <PodcastImportForm OnImportStarted="HandleImportStarted" />
                        }
                        else if (activeTab == "url")
                        {
                            <DirectUrlImportForm OnImportStarted="HandleImportStarted" />
                        }
                    </div>
                </div>
            </div>
        </div>

        <div class="col-lg-4">
            <div class="card shadow-sm">
                <div class="card-body">
                    <h5><i class="bi bi-info-circle"></i> Import Tips</h5>
                    @if (activeTab == "youtube")
                    {
                        <ul class="small">
                            <li>Paste any YouTube video URL</li>
                            <li>Audio will be extracted automatically</li>
                            <li>Converted to MP3 format</li>
                            <li>Max duration: 90 minutes</li>
                        </ul>
                    }
                    else if (activeTab == "podcast")
                    {
                        <ul class="small">
                            <li>Paste podcast RSS feed URL</li>
                            <li>Select episodes to import</li>
                            <li>Supports MP3, M4A formats</li>
                            <li>Cover art included</li>
                        </ul>
                    }
                    else
                    {
                        <ul class="small">
                            <li>Direct link to audio file</li>
                            <li>Must be publicly accessible</li>
                            <li>Supports MP3, M4A, OGG, WAV</li>
                            <li>Max size: 200MB</li>
                        </ul>
                    }
                </div>
            </div>
        </div>
    </div>

    <!-- Active Imports -->
    @if (activeImports.Any())
    {
        <div class="card mt-4 shadow-sm">
            <div class="card-header">
                <h5><i class="bi bi-clock-history"></i> Active Imports</h5>
            </div>
            <div class="card-body">
                @foreach (var job in activeImports)
                {
                    <ImportJobProgressCard Job="@job" OnCancel="CancelImport" />
                }
            </div>
        </div>
    }
</div>

@code {
    private string activeTab = "youtube";
    private List<ImportJob> activeImports = new();
    private System.Threading.Timer? pollingTimer;

    private async Task HandleImportStarted(ImportJob job)
    {
        activeImports.Add(job);
        StartPolling();
    }

    private void StartPolling()
    {
        pollingTimer ??= new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await UpdateImportStatus();
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private async Task UpdateImportStatus()
    {
        // Poll for updates
        foreach (var job in activeImports.ToList())
        {
            var updated = await ImportJobService.GetJobStatusAsync(job.Id);
            if (updated != null)
            {
                // Update status
                if (updated.Status == "Completed" || updated.Status == "Failed")
                {
                    activeImports.Remove(job);
                }
            }
        }

        if (!activeImports.Any())
        {
            pollingTimer?.Dispose();
            pollingTimer = null;
        }
    }
}
```

#### B. YouTube Import Form Component

```razor
@* BoxieHub/Components/Pages/Library/Components/YouTubeImportForm.razor *@

<div>
    <div class="mb-3">
        <label class="form-label">YouTube URL</label>
        <input type="text" 
               class="form-control" 
               @bind="youtubeUrl" 
               placeholder="https://www.youtube.com/watch?v=..."
               @oninput="OnUrlChanged" />
        <small class="text-muted">
            Paste any YouTube video URL
        </small>
    </div>

    @if (isLoadingPreview)
    {
        <div class="text-center py-4">
            <div class="spinner-border text-primary"></div>
            <p class="mt-2 text-muted">Loading preview...</p>
        </div>
    }
    else if (videoInfo != null)
    {
        <div class="card mb-3">
            <div class="row g-0">
                <div class="col-md-4">
                    <img src="@videoInfo.ThumbnailUrl" class="img-fluid rounded-start" alt="Thumbnail">
                </div>
                <div class="col-md-8">
                    <div class="card-body">
                        <h5 class="card-title">@videoInfo.Title</h5>
                        <p class="card-text small text-muted">@videoInfo.Author</p>
                        <p class="card-text small">
                            <i class="bi bi-clock"></i> @videoInfo.Duration.ToString(@"hh\:mm\:ss")
                        </p>
                    </div>
                </div>
            </div>
        </div>

        <div class="mb-3">
            <label class="form-label">Title (editable)</label>
            <input type="text" class="form-control" @bind="title" />
        </div>

        <div class="mb-3">
            <label class="form-label">Description (optional)</label>
            <textarea class="form-control" rows="3" @bind="description"></textarea>
        </div>

        <button class="btn btn-primary" @onclick="StartImport" disabled="@isImporting">
            @if (isImporting)
            {
                <span class="spinner-border spinner-border-sm me-2"></span>
            }
            <i class="bi bi-download"></i> Import Audio
        </button>
    }
</div>

@code {
    [Parameter]
    public EventCallback<ImportJob> OnImportStarted { get; set; }

    private string youtubeUrl = string.Empty;
    private YouTubeVideoInfo? videoInfo;
    private string title = string.Empty;
    private string description = string.Empty;
    private bool isLoadingPreview;
    private bool isImporting;
    private System.Threading.Timer? debounceTimer;

    private void OnUrlChanged(ChangeEventArgs e)
    {
        youtubeUrl = e.Value?.ToString() ?? string.Empty;
        
        // Debounce preview loading
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
        if (string.IsNullOrWhiteSpace(youtubeUrl)) return;
        
        isLoadingPreview = true;
        videoInfo = null;
        
        try
        {
            videoInfo = await YouTubeImportService.GetVideoInfoAsync(youtubeUrl);
            title = videoInfo.Title;
            description = videoInfo.Description;
        }
        catch (Exception ex)
        {
            // Show error toast
        }
        finally
        {
            isLoadingPreview = false;
        }
    }

    private async Task StartImport()
    {
        isImporting = true;
        
        try
        {
            var job = await ImportJobService.StartYouTubeImportAsync(userId, youtubeUrl);
            await OnImportStarted.InvokeAsync(job);
            
            // Clear form
            youtubeUrl = string.Empty;
            videoInfo = null;
        }
        catch (Exception ex)
        {
            // Show error toast
        }
        finally
        {
            isImporting = false;
        }
    }
}
```

---

### 6. Background Processing

**Option A: Use Hangfire for background jobs**
```xml
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.9" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.8" />
```

**Option B: Use built-in IHostedService**
```csharp
// BoxieHub/Services/Import/ImportJobProcessor.cs
public class ImportJobProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Poll for pending jobs
            // Process them one by one
            // Update status in database
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

---

## ?? Testing Plan

### Unit Tests
- [ ] YouTubeImportService - video info extraction
- [ ] YouTubeImportService - audio download
- [ ] PodcastImportService - RSS feed parsing
- [ ] PodcastImportService - episode download
- [ ] ImportJobService - job orchestration

### Integration Tests
- [ ] Full YouTube import workflow
- [ ] Full podcast import workflow
- [ ] Error handling (invalid URLs, download failures)
- [ ] Progress tracking
- [ ] Job cancellation

### Manual Testing
- [ ] Import various YouTube videos
- [ ] Import from different podcast feeds
- [ ] Test age-restricted content
- [ ] Test private/unavailable videos
- [ ] Test network interruptions
- [ ] Test concurrent imports

---

## ?? Implementation Order

### Phase 1: Core Infrastructure (2-3 hours)
1. Add NuGet packages
2. Create domain models (ImportJob, ImportSource)
3. Create database migration
4. Register services in DI

### Phase 2: YouTube Import (3-4 hours)
1. Implement YouTubeImportService
2. Create YouTubeImportForm component
3. Implement background job processor
4. Add progress tracking
5. Test with various videos

### Phase 3: Podcast Import (2-3 hours)
1. Implement PodcastImportService
2. Create PodcastImportForm component
3. Create episode selection UI
4. Test with popular podcasts

### Phase 4: Polish & Testing (2-3 hours)
1. Direct URL import (simple wrapper)
2. Error handling and retries
3. Progress indicators
4. Toast notifications
5. Unit and integration tests
6. Documentation

---

## ?? Success Metrics

- [ ] Users can import YouTube audio in <30 seconds
- [ ] Podcast episodes import successfully
- [ ] Error messages are clear and helpful
- [ ] Progress indicators are accurate
- [ ] All tests passing
- [ ] No crashes on invalid URLs
- [ ] Background jobs don't block UI

---

## ?? Security Considerations

### YouTube
- Respect YouTube Terms of Service
- Don't bypass age restrictions
- Rate limit requests (5 per minute)
- Handle DMCA takedowns gracefully

### Podcasts
- Validate RSS feed URLs
- Don't overwhelm podcast servers
- Respect robots.txt
- Cache feed data (1 hour TTL)

### General
- Validate all URLs before processing
- Sanitize metadata before storing
- Virus scan downloaded files (optional)
- Rate limit per user (10 imports/day)

---

## ?? Known Limitations

1. **YouTube Changes:** YouTube can block scrapers, may need fallback strategy
2. **File Sizes:** Large podcasts (>200MB) may timeout
3. **Format Support:** Some exotic audio formats may not work
4. **Metadata Quality:** Auto-filled data may need manual cleanup

---

## ?? References

- [YoutubeExplode Documentation](https://github.com/Tyrrrz/YoutubeExplode)
- [RSS Feed Specification](https://www.rssboard.org/rss-specification)
- [iTunes Podcast Namespace](https://help.apple.com/itc/podcasts_connect/#/itcb54353390)

---

## ? Ready to Start!

**Branch:** `feature/user-story-8-phase-6-import`

**First Task:** Install NuGet packages and create domain models

Let's build this! ??
