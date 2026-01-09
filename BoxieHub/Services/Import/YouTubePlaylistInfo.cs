namespace BoxieHub.Services.Import;

/// <summary>
/// Metadata for a YouTube playlist
/// </summary>
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
