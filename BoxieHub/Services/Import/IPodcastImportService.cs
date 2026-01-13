namespace BoxieHub.Services.Import;

/// <summary>
/// Service for importing podcast episodes from RSS feeds
/// </summary>
public interface IPodcastImportService
{
    /// <summary>
    /// Parse RSS feed and extract podcast metadata + episodes
    /// </summary>
    /// <param name="rssUrl">RSS feed URL</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Podcast feed info with episodes</returns>
    Task<BoxieHub.Models.PodcastFeedInfo> GetFeedInfoAsync(string rssUrl, CancellationToken ct = default);
    
    /// <summary>
    /// Download audio from podcast episode URL
    /// </summary>
    /// <param name="audioUrl">Direct audio file URL</param>
    /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Audio stream</returns>
    Task<Stream> DownloadEpisodeAsync(
        string audioUrl, 
        IProgress<double>? progress = null, 
        CancellationToken ct = default);
}
