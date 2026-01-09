# User Story 8 - Phase 6B: YouTube Playlist Import

**Status:** ?? Ready to Start  
**Branch:** `feature/user-story-8-phase-6b-playlist-import`  
**Estimated Effort:** 2-3 hours  
**Priority:** High  
**Depends On:** Phase 6A (YouTube single video import) ?  

---

## ?? Goals

Allow users to import entire YouTube playlists at once:
1. Paste YouTube playlist URL
2. System fetches all videos in playlist
3. User reviews list and selects which videos to import
4. Batch import with progress tracking
5. All videos processed sequentially in background

---

## ? Acceptance Criteria

### Playlist Import
- [ ] User can paste YouTube playlist URL
- [ ] System validates playlist URL format
- [ ] System fetches playlist metadata (title, description, video count)
- [ ] Shows list of all videos with thumbnails
- [ ] User can select/deselect individual videos
- [ ] "Select All" / "Deselect All" buttons
- [ ] Shows total duration and file size estimate
- [ ] Import button starts batch import
- [ ] Progress bar shows overall progress (e.g., "3/10 videos imported")
- [ ] Each video processed sequentially
- [ ] Handles partial failures gracefully (some videos succeed, some fail)

### Validation
- [ ] Max 50 videos per playlist (to prevent abuse)
- [ ] Each video must be under 90 minutes
- [ ] Skip private/unavailable videos automatically
- [ ] Show warning for videos that exceed limits

### UX
- [ ] Playlist preview loads in <5 seconds
- [ ] Individual video thumbnails load asynchronously
- [ ] Clear indication of which videos are selected
- [ ] Estimated total time to complete import
- [ ] Toast notification when all imports complete
- [ ] Link to view all imported items in library

---

## ?? Technical Plan

### 1. Update YouTubeImportService

Add playlist support to existing service:

```csharp
// BoxieHub/Services/Import/YouTubePlaylistInfo.cs
public class YouTubePlaylistInfo
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string ThumbnailUrl { get; set; } = default!;
    public int VideoCount { get; set; }
    public List<YouTubeVideoInfo> Videos { get; set; } = new();
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

// BoxieHub/Services/Import/IYouTubeImportService.cs (add methods)
public interface IYouTubeImportService
{
    // Existing single video methods...
    Task<YouTubeVideoInfo> GetVideoInfoAsync(string url, CancellationToken ct = default);
    Task<Stream> DownloadAudioAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default);
    
    // NEW: Playlist methods
    Task<YouTubePlaylistInfo> GetPlaylistInfoAsync(string playlistUrl, CancellationToken ct = default);
    Task<bool> IsPlaylistUrlAsync(string url);
}

// BoxieHub/Services/Import/YouTubeImportService.cs (add implementation)
public async Task<bool> IsPlaylistUrlAsync(string url)
{
    // Check if URL contains "list=" parameter
    return url.Contains("list=") || url.Contains("playlist?");
}

public async Task<YouTubePlaylistInfo> GetPlaylistInfoAsync(string playlistUrl, CancellationToken ct = default)
{
    try
    {
        _logger.LogInformation("Fetching YouTube playlist info for URL: {Url}", playlistUrl);
        
        // Extract playlist ID
        var playlistId = ExtractPlaylistId(playlistUrl);
        if (string.IsNullOrEmpty(playlistId))
        {
            return new YouTubePlaylistInfo
            {
                IsValid = false,
                ErrorMessage = "Invalid playlist URL. Could not extract playlist ID."
            };
        }
        
        // Get playlist metadata
        var playlist = await _youtube.Playlists.GetAsync(playlistId, ct);
        
        // Get all videos in playlist (with pagination support)
        var videos = new List<YouTubeVideoInfo>();
        await foreach (var video in _youtube.Playlists.GetVideosAsync(playlistId, ct))
        {
            // Limit to 50 videos to prevent abuse
            if (videos.Count >= 50)
            {
                _logger.LogWarning("Playlist has more than 50 videos. Limiting to first 50.");
                break;
            }
            
            // Get basic info for each video (lightweight)
            var videoInfo = new YouTubeVideoInfo
            {
                Id = video.Id,
                Title = video.Title,
                Description = video.Description ?? string.Empty,
                Author = video.Author.ChannelTitle,
                Duration = video.Duration ?? TimeSpan.Zero,
                ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? string.Empty,
                UploadDate = video.UploadDate,
                IsValid = video.Duration?.TotalSeconds <= 5400 // 90 minutes
            };
            
            if (!videoInfo.IsValid)
            {
                videoInfo.ErrorMessage = $"Video too long ({videoInfo.Duration.TotalMinutes:F0} minutes). Skipped.";
            }
            
            videos.Add(videoInfo);
        }
        
        _logger.LogInformation("Successfully fetched playlist with {Count} videos", videos.Count);
        
        return new YouTubePlaylistInfo
        {
            Id = playlist.Id,
            Title = playlist.Title ?? "Untitled Playlist",
            Description = playlist.Description ?? string.Empty,
            Author = playlist.Author?.ChannelTitle ?? "Unknown",
            ThumbnailUrl = playlist.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? string.Empty,
            VideoCount = videos.Count,
            Videos = videos,
            IsValid = true
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to fetch playlist info for URL: {Url}", playlistUrl);
        
        return new YouTubePlaylistInfo
        {
            IsValid = false,
            ErrorMessage = GetUserFriendlyErrorMessage(ex)
        };
    }
}

private string ExtractPlaylistId(string url)
{
    // Handle various playlist URL formats:
    // - https://www.youtube.com/playlist?list=PLxxx
    // - https://www.youtube.com/watch?v=xxx&list=PLxxx
    // - https://youtube.com/playlist?list=PLxxx
    
    var uri = new Uri(url);
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
    return query["list"] ?? string.Empty;
}
```

---

### 2. Update ImportJobService

Add batch import support:

```csharp
// BoxieHub/Services/Import/IImportJobService.cs (add method)
public interface IImportJobService
{
    // Existing methods...
    Task<ImportJob> CreateYouTubeImportJobAsync(...);
    
    // NEW: Batch import
    Task<List<ImportJob>> CreateYouTubeBatchImportAsync(
        string userId,
        List<string> youtubeUrls,
        CancellationToken ct = default);
}

// BoxieHub/Services/Import/ImportJobService.cs (add implementation)
public async Task<List<ImportJob>> CreateYouTubeBatchImportAsync(
    string userId,
    List<string> youtubeUrls,
    CancellationToken ct = default)
{
    _logger.LogInformation("Creating batch YouTube import for user {UserId}: {Count} videos", 
        userId, youtubeUrls.Count);
    
    var jobs = new List<ImportJob>();
    
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
            // Continue with other videos even if one fails
        }
    }
    
    _logger.LogInformation("Created {Count} import jobs for batch import", jobs.Count);
    
    return jobs;
}
```

---

### 3. Create Playlist Import UI

New component for playlist selection:

```razor
@* BoxieHub/Components/Pages/Library/ImportPlaylist.razor *@
@page "/library/import/youtube/playlist"
@rendermode InteractiveServer
@using BoxieHub.Services.Import
@using BoxieHub.Models
@using System.Security.Claims
@using Microsoft.AspNetCore.Authorization
@inject IImportJobService ImportJobService
@inject IYouTubeImportService YouTubeService
@inject ImportJobProcessor JobProcessor
@inject AuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation
@inject ILogger<ImportPlaylist> Logger
@attribute [Authorize]
@implements IDisposable

<PageTitle>Import YouTube Playlist - BoxieHub</PageTitle>

<div class="container mt-4">
    <nav aria-label="breadcrumb">
        <ol class="breadcrumb">
            <li class="breadcrumb-item"><a href="/library">Library</a></li>
            <li class="breadcrumb-item"><a href="/library/import/youtube">Import from YouTube</a></li>
            <li class="breadcrumb-item active">Playlist</li>
        </ol>
    </nav>

    <div class="card shadow-sm">
        <div class="card-header bg-danger text-white">
            <h4 class="mb-0">
                <i class="bi bi-youtube"></i> Import YouTube Playlist
            </h4>
        </div>
        <div class="card-body">
            <div class="mb-3">
                <label class="form-label">YouTube Playlist URL *</label>
                <input type="text" 
                       class="form-control @(validationError != null ? "is-invalid" : "")" 
                       @bind="playlistUrl"
                       @oninput="OnUrlChanged"
                       placeholder="https://www.youtube.com/playlist?list=..."
                       disabled="@isProcessing" />
                @if (validationError != null)
                {
                    <div class="invalid-feedback d-block">
                        @validationError
                    </div>
                }
                <small class="text-muted">
                    Paste a YouTube playlist URL (max 50 videos)
                </small>
            </div>

            @if (isLoadingPreview)
            {
                <div class="text-center py-4">
                    <div class="spinner-border text-primary"></div>
                    <p class="mt-2 text-muted">Loading playlist...</p>
                </div>
            }
            else if (playlistInfo != null && playlistInfo.IsValid)
            {
                <!-- Playlist Preview -->
                <div class="card mb-3 border-primary">
                    <div class="card-body">
                        <div class="d-flex justify-content-between align-items-start">
                            <div class="flex-grow-1">
                                <h5 class="card-title">@playlistInfo.Title</h5>
                                <p class="card-text small text-muted mb-2">
                                    <i class="bi bi-person"></i> @playlistInfo.Author
                                </p>
                                <p class="card-text small mb-2">
                                    <i class="bi bi-collection-play"></i> @playlistInfo.VideoCount videos
                                </p>
                                <p class="card-text small text-muted">
                                    <i class="bi bi-info-circle"></i> Total duration: @FormatTotalDuration()
                                </p>
                            </div>
                            <img src="@playlistInfo.ThumbnailUrl" 
                                 class="img-thumbnail" 
                                 alt="Playlist thumbnail"
                                 style="max-width: 120px;">
                        </div>
                    </div>
                </div>

                <!-- Video Selection -->
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <h6>Select Videos to Import:</h6>
                    <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-secondary" @onclick="SelectAll">
                            <i class="bi bi-check-square"></i> Select All
                        </button>
                        <button class="btn btn-outline-secondary" @onclick="DeselectAll">
                            <i class="bi bi-square"></i> Deselect All
                        </button>
                    </div>
                </div>

                <div class="list-group mb-3" style="max-height: 500px; overflow-y: auto;">
                    @foreach (var (video, index) in playlistInfo.Videos.Select((v, i) => (v, i)))
                    {
                        <div class="list-group-item @(video.IsValid ? "" : "list-group-item-secondary")">
                            <div class="d-flex align-items-start">
                                <input type="checkbox" 
                                       class="form-check-input me-3 mt-2" 
                                       id="video-@index"
                                       @bind="selectedVideos[index]"
                                       disabled="@(!video.IsValid || isProcessing)" />
                                <label class="flex-grow-1" for="video-@index" style="cursor: pointer;">
                                    <div class="d-flex">
                                        <img src="@video.ThumbnailUrl" 
                                             class="rounded me-3" 
                                             alt="Thumbnail"
                                             style="width: 120px; height: 90px; object-fit: cover;">
                                        <div class="flex-grow-1">
                                            <h6 class="mb-1">@video.Title</h6>
                                            <small class="text-muted d-block">
                                                <i class="bi bi-clock"></i> @FormatDuration(video.Duration)
                                            </small>
                                            @if (!video.IsValid)
                                            {
                                                <small class="text-danger d-block">
                                                    <i class="bi bi-exclamation-triangle"></i> @video.ErrorMessage
                                                </small>
                                            }
                                        </div>
                                    </div>
                                </label>
                            </div>
                        </div>
                    }
                </div>

                <!-- Import Summary -->
                <div class="alert alert-info">
                    <i class="bi bi-info-circle"></i>
                    <strong>@GetSelectedCount() videos selected</strong> 
                    (@FormatSelectedDuration() total duration)
                </div>

                <button class="btn btn-primary btn-lg" 
                        @onclick="StartBatchImport" 
                        disabled="@(isProcessing || GetSelectedCount() == 0)">
                    @if (isProcessing)
                    {
                        <span class="spinner-border spinner-border-sm me-2"></span>
                        <span>Starting imports...</span>
                    }
                    else
                    {
                        <i class="bi bi-download"></i>
                        <span>Import @GetSelectedCount() Videos</span>
                    }
                </button>
            }
        </div>
    </div>
</div>

@code {
    private string playlistUrl = string.Empty;
    private YouTubePlaylistInfo? playlistInfo;
    private Dictionary<int, bool> selectedVideos = new();
    private string? validationError;
    private bool isLoadingPreview;
    private bool isProcessing;
    private System.Threading.Timer? debounceTimer;

    private void OnUrlChanged(ChangeEventArgs e)
    {
        playlistUrl = e.Value?.ToString() ?? string.Empty;
        validationError = null;
        playlistInfo = null;
        selectedVideos.Clear();
        
        // Debounce preview loading
        debounceTimer?.Dispose();
        debounceTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await LoadPlaylistPreview();
                StateHasChanged();
            });
        }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task LoadPlaylistPreview()
    {
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            playlistInfo = null;
            return;
        }
        
        // Check if it's a playlist URL
        if (!await YouTubeService.IsPlaylistUrlAsync(playlistUrl))
        {
            validationError = "This doesn't appear to be a playlist URL. For single videos, use the regular import page.";
            return;
        }
        
        isLoadingPreview = true;
        validationError = null;
        
        try
        {
            playlistInfo = await YouTubeService.GetPlaylistInfoAsync(playlistUrl);
            
            if (!playlistInfo.IsValid)
            {
                validationError = playlistInfo.ErrorMessage;
                playlistInfo = null;
            }
            else
            {
                // Initialize selection (all valid videos selected by default)
                selectedVideos = playlistInfo.Videos
                    .Select((v, i) => new { Index = i, IsValid = v.IsValid })
                    .ToDictionary(x => x.Index, x => x.IsValid);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load playlist preview");
            validationError = "Failed to load playlist. Please check the URL.";
            playlistInfo = null;
        }
        finally
        {
            isLoadingPreview = false;
        }
    }

    private void SelectAll()
    {
        if (playlistInfo == null) return;
        
        for (int i = 0; i < playlistInfo.Videos.Count; i++)
        {
            if (playlistInfo.Videos[i].IsValid)
            {
                selectedVideos[i] = true;
            }
        }
    }

    private void DeselectAll()
    {
        foreach (var key in selectedVideos.Keys.ToList())
        {
            selectedVideos[key] = false;
        }
    }

    private int GetSelectedCount()
    {
        return selectedVideos.Count(x => x.Value);
    }

    private TimeSpan GetSelectedDuration()
    {
        if (playlistInfo == null) return TimeSpan.Zero;
        
        var selected = playlistInfo.Videos
            .Where((v, i) => selectedVideos.ContainsKey(i) && selectedVideos[i])
            .Sum(v => v.Duration.TotalSeconds);
        
        return TimeSpan.FromSeconds(selected);
    }

    private string FormatSelectedDuration()
    {
        return FormatDuration(GetSelectedDuration());
    }

    private string FormatTotalDuration()
    {
        if (playlistInfo == null) return "0m";
        
        var total = TimeSpan.FromSeconds(
            playlistInfo.Videos.Sum(v => v.Duration.TotalSeconds));
        
        return FormatDuration(total);
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    private async Task StartBatchImport()
    {
        if (playlistInfo == null || GetSelectedCount() == 0)
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

            // Get selected video URLs
            var selectedUrls = playlistInfo.Videos
                .Where((v, i) => selectedVideos.ContainsKey(i) && selectedVideos[i])
                .Select(v => $"https://www.youtube.com/watch?v={v.Id}")
                .ToList();

            // Create batch import jobs
            var jobs = await ImportJobService.CreateYouTubeBatchImportAsync(userId, selectedUrls);

            // Enqueue all jobs for background processing
            foreach (var job in jobs)
            {
                await JobProcessor.EnqueueJobAsync(job.Id);
            }

            Logger.LogInformation("Started batch import of {Count} videos", jobs.Count);

            // Redirect to import page to see progress
            Navigation.NavigateTo("/library/import/youtube");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start batch import");
            validationError = ex.Message;
        }
        finally
        {
            isProcessing = false;
        }
    }

    public void Dispose()
    {
        debounceTimer?.Dispose();
    }
}
```

---

### 4. Update Navigation

Add link to playlist import:

```razor
@* BoxieHub/Components/Pages/Library/ImportYouTube.razor *@
@* Add this near the top of the page *@

<div class="alert alert-info mb-4">
    <i class="bi bi-info-circle"></i>
    <strong>Tip:</strong> Want to import multiple videos at once? 
    <a href="/library/import/youtube/playlist" class="alert-link">
        Import from Playlist <i class="bi bi-arrow-right"></i>
    </a>
</div>
```

---

## ?? Testing Plan

### Manual Testing
- [ ] Import small playlist (3-5 videos)
- [ ] Import large playlist (20+ videos)
- [ ] Import playlist with some invalid videos
- [ ] Import playlist with all valid videos
- [ ] Test "Select All" / "Deselect All"
- [ ] Test concurrent playlist imports
- [ ] Verify progress tracking works
- [ ] Check library shows all imported items

### Edge Cases
- [ ] Playlist with 1 video
- [ ] Playlist with 50+ videos (should limit to 50)
- [ ] Playlist with private videos (should skip)
- [ ] Playlist with videos > 90 minutes (should skip)
- [ ] Invalid playlist URL
- [ ] Deleted/unavailable playlist

---

## ?? Success Metrics

- [ ] Users can import playlists in < 10 seconds (setup)
- [ ] Playlist preview loads in < 5 seconds
- [ ] All selected videos import successfully
- [ ] Progress tracking updates in real-time
- [ ] Failed videos don't block other imports
- [ ] Clear error messages for failures

---

## ?? Implementation Order

### Phase 1: Service Layer (1 hour)
1. Add `YouTubePlaylistInfo` model
2. Implement `GetPlaylistInfoAsync()` in `YouTubeImportService`
3. Implement `CreateYouTubeBatchImportAsync()` in `ImportJobService`
4. Test with sample playlist URLs

### Phase 2: UI Layer (1.5 hours)
1. Create `ImportPlaylist.razor` component
2. Add video selection checkboxes
3. Add playlist preview with thumbnails
4. Implement batch import button
5. Add navigation links

### Phase 3: Testing & Polish (0.5 hours)
1. Test with various playlists
2. Add error handling
3. Update documentation

---

## ?? References

- [YoutubeExplode Playlists](https://github.com/Tyrrrz/YoutubeExplode/wiki/Playlists)
- [YouTube Playlist URL Formats](https://stackoverflow.com/questions/3452546/how-to-get-the-youtube-playlist-id)

---

## ? Ready to Start!

**Estimated Time:** 2-3 hours  
**Complexity:** Medium  
**Dependencies:** Phase 6A (YouTube import) ?  

**First Step:** Add `IsPlaylistUrlAsync()` and `GetPlaylistInfoAsync()` to `IYouTubeImportService`

Let's build this! ????
