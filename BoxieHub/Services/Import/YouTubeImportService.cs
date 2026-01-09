using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Common;

namespace BoxieHub.Services.Import;

/// <summary>
/// Implementation of YouTube import service using YoutubeExplode
/// </summary>
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
                ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? string.Empty,
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
            
            // Get LOWEST bitrate audio stream for Toniebox (mono speaker, no need for stereo/high quality)
            // Typical YouTube audio streams:
            // - 160 kbps (stereo, Opus) - highest quality
            // - 128 kbps (stereo, M4A)
            // - 70 kbps (stereo, Opus)
            // - 50 kbps (mono, Opus) - optimal for Toniebox!
            var audioStreamInfo = streamManifest
                .GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp4) // Prefer M4A (compatible with Tonie Cloud)
                .OrderBy(s => s.Bitrate) // Select LOWEST bitrate (saves bandwidth and storage)
                .FirstOrDefault();

            if (audioStreamInfo == null)
            {
                // Fallback: try any audio stream (still prefer lowest)
                audioStreamInfo = streamManifest
                    .GetAudioOnlyStreams()
                    .OrderBy(s => s.Bitrate) // Lowest first
                    .FirstOrDefault();
            }

            if (audioStreamInfo == null)
                throw new InvalidOperationException("No audio stream available for this video");

            _logger.LogInformation("Selected audio stream: {Bitrate} kbps, {Container} (optimized for Toniebox mono speaker)", 
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
    
    /// <summary>
    /// Check if URL is a YouTube playlist URL
    /// </summary>
    public Task<bool> IsPlaylistUrlAsync(string url)
    {
        // Check if URL contains playlist parameters
        var isPlaylist = url.Contains("list=", StringComparison.OrdinalIgnoreCase) || 
                        url.Contains("/playlist", StringComparison.OrdinalIgnoreCase);
        
        return Task.FromResult(isPlaylist);
    }
    
    /// <summary>
    /// Get playlist metadata and all videos
    /// </summary>
    public async Task<YouTubePlaylistInfo> GetPlaylistInfoAsync(string playlistUrl, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching YouTube playlist info for URL: {Url}", playlistUrl);
            
            // Extract playlist ID from URL
            var playlistId = ExtractPlaylistId(playlistUrl);
            if (string.IsNullOrEmpty(playlistId))
            {
                return new YouTubePlaylistInfo
                {
                    IsValid = false,
                    ErrorMessage = "Invalid playlist URL. Could not extract playlist ID."
                };
            }
            
            _logger.LogDebug("Extracted playlist ID: {PlaylistId}", playlistId);
            
            // Get playlist metadata
            var playlist = await _youtube.Playlists.GetAsync(playlistId, ct);
            
            _logger.LogInformation("Fetching videos from playlist: {Title}", playlist.Title);
            
            // Get all videos in playlist (with limit to prevent abuse)
            var videos = new List<YouTubeVideoInfo>();
            const int maxVideos = 50;
            
            await foreach (var video in _youtube.Playlists.GetVideosAsync(playlistId, ct))
            {
                // Limit to 50 videos
                if (videos.Count >= maxVideos)
                {
                    _logger.LogWarning("Playlist has more than {MaxVideos} videos. Limiting to first {MaxVideos}.", 
                        maxVideos, maxVideos);
                    break;
                }
                
                // Get basic info for each video (PlaylistVideo has limited metadata)
                var duration = video.Duration ?? TimeSpan.Zero;
                var isValid = duration.TotalSeconds > 0 && duration.TotalSeconds <= 5400; // Max 90 minutes
                
                var videoInfo = new YouTubeVideoInfo
                {
                    Id = video.Id,
                    Title = video.Title,
                    Description = string.Empty, // PlaylistVideo doesn't include description
                    Author = video.Author.ChannelTitle,
                    Duration = duration,
                    ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? string.Empty,
                    UploadDate = DateTimeOffset.UtcNow, // PlaylistVideo doesn't include upload date
                    IsValid = isValid
                };
                
                if (!isValid)
                {
                    if (duration.TotalSeconds == 0)
                    {
                        videoInfo.ErrorMessage = "Video unavailable or deleted.";
                    }
                    else if (duration.TotalSeconds > 5400)
                    {
                        videoInfo.ErrorMessage = $"Video too long ({duration.TotalMinutes:F0} minutes). Max 90 minutes.";
                    }
                }
                
                videos.Add(videoInfo);
            }
            
            _logger.LogInformation("Successfully fetched playlist with {Count} videos ({Valid} valid)", 
                videos.Count, videos.Count(v => v.IsValid));
            
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
                ErrorMessage = GetUserFriendlyPlaylistErrorMessage(ex)
            };
        }
    }
    
    /// <summary>
    /// Extract playlist ID from various YouTube playlist URL formats
    /// </summary>
    private string ExtractPlaylistId(string url)
    {
        try
        {
            // Handle various playlist URL formats:
            // - https://www.youtube.com/playlist?list=PLxxx
            // - https://www.youtube.com/watch?v=xxx&list=PLxxx
            // - https://youtube.com/playlist?list=PLxxx
            
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["list"] ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Get user-friendly error message for playlist errors
    /// </summary>
    private string GetUserFriendlyPlaylistErrorMessage(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        if (message.Contains("playlist") && (message.Contains("not found") || message.Contains("unavailable")))
            return "This playlist is unavailable, private, or has been deleted.";
        
        if (message.Contains("invalid") || message.Contains("not found"))
            return "Invalid playlist URL. Please check the link and try again.";
        
        if (message.Contains("private"))
            return "This playlist is private and cannot be imported.";
        
        return "Failed to access playlist. Please check the URL and try again.";
    }
}
