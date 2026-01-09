namespace BoxieHub.Services.Import;

/// <summary>
/// Metadata extracted from a YouTube video
/// </summary>
public class YouTubeVideoInfo
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Author { get; set; } = default!;
    public TimeSpan Duration { get; set; }
    public string ThumbnailUrl { get; set; } = default!;
    public DateTimeOffset UploadDate { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
