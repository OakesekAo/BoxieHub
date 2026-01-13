using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// Represents a podcast feed (shared across users who subscribe to it)
/// </summary>
public class SavedPodcast
{
    public int Id { get; set; }
    
    /// <summary>
    /// RSS feed URL (unique identifier for podcast)
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string FeedUrl { get; set; } = default!;
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = default!;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? Author { get; set; }
    
    [MaxLength(1024)]
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Total number of episodes in feed (at last refresh)
    /// </summary>
    public int TotalEpisodes { get; set; }
    
    /// <summary>
    /// Last time the feed was fetched/refreshed
    /// </summary>
    public DateTimeOffset? LastFetched { get; set; }
    
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? Modified { get; set; }
    
    /// <summary>
    /// User subscriptions to this podcast
    /// </summary>
    public ICollection<PodcastSubscription> Subscriptions { get; set; } = new List<PodcastSubscription>();
    
    /// <summary>
    /// Cached episodes for this podcast
    /// </summary>
    public ICollection<PodcastEpisodeCache> Episodes { get; set; } = new List<PodcastEpisodeCache>();
}

/// <summary>
/// Many-to-many relationship: Users subscribe to Podcasts
/// </summary>
public class PodcastSubscription
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    
    public int SavedPodcastId { get; set; }
    public SavedPodcast? SavedPodcast { get; set; }
    
    /// <summary>
    /// Is this podcast marked as favorite by this user
    /// </summary>
    public bool IsFavorite { get; set; }
    
    /// <summary>
    /// User's personal notes about this podcast
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Last time this user accessed this podcast
    /// </summary>
    public DateTimeOffset? LastAccessed { get; set; }
    
    public DateTimeOffset SubscribedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Cached episode metadata from RSS feed
/// Prevents re-parsing RSS for every user
/// </summary>
public class PodcastEpisodeCache
{
    public int Id { get; set; }
    
    public int SavedPodcastId { get; set; }
    public SavedPodcast? SavedPodcast { get; set; }
    
    /// <summary>
    /// Episode GUID from RSS feed (unique per podcast)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string EpisodeGuid { get; set; } = default!;
    
    /// <summary>
    /// Direct audio URL (used for deduplication)
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string AudioUrl { get; set; } = default!;
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = default!;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(1024)]
    public string? ImageUrl { get; set; }
    
    public float DurationSeconds { get; set; }
    
    public long FileSizeBytes { get; set; }
    
    public DateTimeOffset PublishDate { get; set; }
    
    [MaxLength(50)]
    public string ContentType { get; set; } = "audio/mpeg";
    
    /// <summary>
    /// Reference to shared FileUpload (if already downloaded)
    /// </summary>
    public Guid? FileUploadId { get; set; }
    public FileUpload? FileUpload { get; set; }
    
    /// <summary>
    /// Number of users who have imported this episode
    /// </summary>
    public int ImportCount { get; set; }
    
    public DateTimeOffset CachedAt { get; set; } = DateTimeOffset.UtcNow;
}
