namespace BoxieHub.Models;

/// <summary>
/// Represents parsed podcast feed information
/// </summary>
public class PodcastFeedInfo
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string ImageUrl { get; set; } = default!;
    public string FeedUrl { get; set; } = default!;
    public List<PodcastEpisode> Episodes { get; set; } = new();
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a single podcast episode
/// </summary>
public class PodcastEpisode
{
    public string Id { get; set; } = default!; // Episode GUID or URL
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string AudioUrl { get; set; } = default!;
    public string ContentType { get; set; } = "audio/mpeg"; // Default to MP3
    public TimeSpan Duration { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTimeOffset PublishDate { get; set; }
    public string ImageUrl { get; set; } = default!;
    public bool IsSelected { get; set; } // For UI selection
}
