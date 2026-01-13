using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Import;

/// <summary>
/// Implementation of saved podcast service with deduplication
/// Podcasts and episodes are shared across users!
/// </summary>
public class SavedPodcastService : ISavedPodcastService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IPodcastImportService _podcastService;
    private readonly ILogger<SavedPodcastService> _logger;

    public SavedPodcastService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IPodcastImportService podcastService,
        ILogger<SavedPodcastService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _podcastService = podcastService;
        _logger = logger;
    }

    public async Task<PodcastSubscription> SubscribeToPodcastAsync(
        string userId,
        PodcastFeedInfo feedInfo,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Find or create the podcast (shared resource)
        var podcast = await dbContext.SavedPodcasts
            .FirstOrDefaultAsync(p => p.FeedUrl == feedInfo.FeedUrl, ct);

        if (podcast == null)
        {
            // Create new podcast
            podcast = new SavedPodcast
            {
                FeedUrl = feedInfo.FeedUrl,
                Title = Truncate(feedInfo.Title, 500),
                Description = Truncate(feedInfo.Description, 2000),
                Author = Truncate(feedInfo.Author, 500),
                ImageUrl = Truncate(feedInfo.ImageUrl, 1024),
                TotalEpisodes = feedInfo.Episodes.Count,
                LastFetched = DateTimeOffset.UtcNow,
                Created = DateTimeOffset.UtcNow
            };

            dbContext.SavedPodcasts.Add(podcast);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Created shared podcast {PodcastId}: {Title}", podcast.Id, podcast.Title);
        }

        // Check if user already subscribed
        var existing = await dbContext.PodcastSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SavedPodcastId == podcast.Id, ct);

        if (existing != null)
        {
            _logger.LogInformation("User {UserId} already subscribed to podcast {PodcastId}", userId, podcast.Id);
            return existing;
        }

        // Create subscription
        var subscription = new PodcastSubscription
        {
            UserId = userId,
            SavedPodcastId = podcast.Id,
            SubscribedAt = DateTimeOffset.UtcNow
        };

        dbContext.PodcastSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} subscribed to podcast {PodcastId}: {Title}",
            userId, podcast.Id, podcast.Title);

        return subscription;
    }

    public async Task<List<PodcastSubscription>> GetUserSubscriptionsAsync(
        string userId,
        bool favoritesOnly = false,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var query = dbContext.PodcastSubscriptions
            .Include(s => s.SavedPodcast)
            .Where(s => s.UserId == userId);

        if (favoritesOnly)
        {
            query = query.Where(s => s.IsFavorite);
        }

        return await query
            .OrderByDescending(s => s.IsFavorite)
            .ThenByDescending(s => s.LastAccessed ?? s.SubscribedAt)
            .ToListAsync(ct);
    }

    public async Task<PodcastSubscription?> GetSubscriptionAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var subscription = await dbContext.PodcastSubscriptions
            .Include(s => s.SavedPodcast)
            .FirstOrDefaultAsync(s => s.SavedPodcastId == podcastId && s.UserId == userId, ct);

        if (subscription != null)
        {
            // Update last accessed
            subscription.LastAccessed = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }

        return subscription;
    }

    public async Task<List<PodcastEpisodeCache>> GetCachedEpisodesAsync(
        int podcastId,
        string userId,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Verify user subscription
        var subscription = await dbContext.PodcastSubscriptions
            .FirstOrDefaultAsync(s => s.SavedPodcastId == podcastId && s.UserId == userId, ct);

        if (subscription == null)
        {
            throw new UnauthorizedAccessException("User not subscribed to this podcast");
        }

        // Get cached episodes (with optional FileUpload reference)
        return await dbContext.PodcastEpisodeCache
            .Include(e => e.FileUpload)
            .Where(e => e.SavedPodcastId == podcastId)
            .OrderByDescending(e => e.PublishDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<SavedPodcast> RefreshPodcastAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Verify user subscription
        var subscription = await dbContext.PodcastSubscriptions
            .Include(s => s.SavedPodcast)
            .FirstOrDefaultAsync(s => s.SavedPodcastId == podcastId && s.UserId == userId, ct);

        if (subscription?.SavedPodcast == null)
        {
            throw new UnauthorizedAccessException("User not subscribed to this podcast");
        }

        var podcast = subscription.SavedPodcast;

        // Fetch latest feed
        var feedInfo = await _podcastService.GetFeedInfoAsync(podcast.FeedUrl, ct);

        if (!feedInfo.IsValid)
        {
            throw new InvalidOperationException($"Failed to fetch podcast feed: {feedInfo.ErrorMessage}");
        }

        // Update podcast metadata
        podcast.Title = Truncate(feedInfo.Title, 500);
        podcast.Description = Truncate(feedInfo.Description, 2000);
        podcast.Author = Truncate(feedInfo.Author, 500);
        podcast.ImageUrl = Truncate(feedInfo.ImageUrl, 1024);
        podcast.TotalEpisodes = feedInfo.Episodes.Count;
        podcast.LastFetched = DateTimeOffset.UtcNow;
        podcast.Modified = DateTimeOffset.UtcNow;

        // Update episode cache (upsert new episodes)
        foreach (var episode in feedInfo.Episodes)
        {
            var episodeGuid = episode.Id;
            var existingCache = await dbContext.PodcastEpisodeCache
                .FirstOrDefaultAsync(e => e.SavedPodcastId == podcastId && e.EpisodeGuid == episodeGuid, ct);

            if (existingCache == null)
            {
                // New episode - cache it
                var cache = new PodcastEpisodeCache
                {
                    SavedPodcastId = podcastId,
                    EpisodeGuid = episodeGuid,
                    AudioUrl = episode.AudioUrl,
                    Title = Truncate(episode.Title, 500),
                    Description = Truncate(episode.Description, 2000),
                    ImageUrl = Truncate(episode.ImageUrl, 1024),
                    DurationSeconds = (float)episode.Duration.TotalSeconds,
                    FileSizeBytes = episode.FileSizeBytes,
                    PublishDate = episode.PublishDate,
                    ContentType = episode.ContentType,
                    CachedAt = DateTimeOffset.UtcNow
                };

                dbContext.PodcastEpisodeCache.Add(cache);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Refreshed podcast {PodcastId}: {Title} ({Count} episodes cached)",
            podcastId, podcast.Title, feedInfo.Episodes.Count);

        return podcast;
    }

    public async Task<bool> ToggleFavoriteAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var subscription = await dbContext.PodcastSubscriptions
            .FirstOrDefaultAsync(s => s.SavedPodcastId == podcastId && s.UserId == userId, ct);

        if (subscription == null)
            return false;

        subscription.IsFavorite = !subscription.IsFavorite;
        await dbContext.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> UnsubscribeAsync(
        int podcastId,
        string userId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var subscription = await dbContext.PodcastSubscriptions
            .FirstOrDefaultAsync(s => s.SavedPodcastId == podcastId && s.UserId == userId, ct);

        if (subscription == null)
            return false;

        dbContext.PodcastSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} unsubscribed from podcast {PodcastId}", userId, podcastId);

        // Note: We keep the SavedPodcast and cache for other users
        // Cleanup job can remove podcasts with 0 subscriptions later

        return true;
    }

    public async Task<PodcastEpisodeCache> FindOrCreateEpisodeCacheAsync(
        int podcastId,
        PodcastEpisode episode,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Try to find existing cache by GUID
        var cache = await dbContext.PodcastEpisodeCache
            .Include(e => e.FileUpload)
            .FirstOrDefaultAsync(e => e.SavedPodcastId == podcastId && e.EpisodeGuid == episode.Id, ct);

        if (cache == null)
        {
            // Try to find by AudioURL (deduplication across podcasts)
            cache = await dbContext.PodcastEpisodeCache
                .Include(e => e.FileUpload)
                .FirstOrDefaultAsync(e => e.AudioUrl == episode.AudioUrl, ct);

            if (cache != null)
            {
                _logger.LogInformation("Found duplicate episode by URL: {AudioUrl}", episode.AudioUrl);
                return cache;
            }

            // Create new cache entry
            cache = new PodcastEpisodeCache
            {
                SavedPodcastId = podcastId,
                EpisodeGuid = episode.Id,
                AudioUrl = episode.AudioUrl,
                Title = Truncate(episode.Title, 500),
                Description = Truncate(episode.Description, 2000),
                ImageUrl = Truncate(episode.ImageUrl, 1024),
                DurationSeconds = (float)episode.Duration.TotalSeconds,
                FileSizeBytes = episode.FileSizeBytes,
                PublishDate = episode.PublishDate,
                ContentType = episode.ContentType,
                CachedAt = DateTimeOffset.UtcNow
            };

            dbContext.PodcastEpisodeCache.Add(cache);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Created episode cache {CacheId} for episode: {Title}", cache.Id, episode.Title);
        }

        return cache;
    }

    public async Task LinkFileToEpisodeAsync(
        int episodeCacheId,
        Guid fileUploadId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var cache = await dbContext.PodcastEpisodeCache.FindAsync(new object[] { episodeCacheId }, ct);
        if (cache == null)
            return;

        cache.FileUploadId = fileUploadId;
        cache.ImportCount++;

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Linked FileUpload {FileId} to episode cache {CacheId} (ImportCount: {Count})",
            fileUploadId, episodeCacheId, cache.ImportCount);
    }

    private static string Truncate(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        if (input.Length <= maxLength)
            return input;

        return input.Substring(0, maxLength - 3) + "...";
    }
}
