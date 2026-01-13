using BoxieHub.Models;

namespace BoxieHub.Services.Import;

/// <summary>
/// Service for managing saved podcast feeds (with deduplication)
/// </summary>
public interface ISavedPodcastService
{
    /// <summary>
    /// Subscribe user to a podcast feed (or create if new)
    /// </summary>
    Task<PodcastSubscription> SubscribeToPodcastAsync(
        string userId,
        PodcastFeedInfo feedInfo,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get all podcast subscriptions for a user
    /// </summary>
    Task<List<PodcastSubscription>> GetUserSubscriptionsAsync(
        string userId,
        bool favoritesOnly = false,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get a specific podcast subscription
    /// </summary>
    Task<PodcastSubscription?> GetSubscriptionAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get cached episodes from a saved podcast (with pagination)
    /// </summary>
    Task<List<PodcastEpisodeCache>> GetCachedEpisodesAsync(
        int podcastId,
        string userId,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default);
    
    /// <summary>
    /// Refresh podcast feed and update cache
    /// </summary>
    Task<SavedPodcast> RefreshPodcastAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Toggle favorite status for a subscription
    /// </summary>
    Task<bool> ToggleFavoriteAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Unsubscribe from a podcast
    /// </summary>
    Task<bool> UnsubscribeAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Find or create cached episode (for import deduplication)
    /// </summary>
    Task<PodcastEpisodeCache> FindOrCreateEpisodeCacheAsync(
        int podcastId,
        PodcastEpisode episode,
        CancellationToken ct = default);
    
    /// <summary>
    /// Link a FileUpload to a cached episode (after import)
    /// </summary>
    Task LinkFileToEpisodeAsync(
        int episodeCacheId,
        Guid fileUploadId,
        CancellationToken ct = default);
}
