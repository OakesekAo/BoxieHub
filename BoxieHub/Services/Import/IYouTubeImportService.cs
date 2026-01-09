namespace BoxieHub.Services.Import;

/// <summary>
/// Service for importing audio from YouTube videos and playlists
/// </summary>
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
    
    /// <summary>
    /// Check if URL is a YouTube playlist URL
    /// </summary>
    Task<bool> IsPlaylistUrlAsync(string url);
    
    /// <summary>
    /// Get playlist metadata and all videos in the playlist
    /// </summary>
    Task<YouTubePlaylistInfo> GetPlaylistInfoAsync(string playlistUrl, CancellationToken ct = default);
}
