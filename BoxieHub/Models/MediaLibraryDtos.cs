namespace BoxieHub.Models;

/// <summary>
/// DTO for creating or updating media library items
/// </summary>
public class MediaLibraryItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    
    // Metadata (populated during upload)
    public float DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
}

/// <summary>
/// DTO for library statistics
/// </summary>
public class LibraryStatsDto
{
    public int TotalItems { get; set; }
    public long TotalSizeBytes { get; set; }
    public float TotalDurationSeconds { get; set; }
    public int TotalUses { get; set; }
    public Dictionary<string, int> ItemsByCategory { get; set; } = new();
    public List<MediaLibraryItem> MostUsedItems { get; set; } = new();
    public List<MediaLibraryItem> RecentlyAdded { get; set; } = new();
    
    public string FormattedTotalSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = TotalSizeBytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
    
    public string FormattedTotalDuration
    {
        get
        {
            var timeSpan = TimeSpan.FromSeconds(TotalDurationSeconds);
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            return $"{timeSpan.Minutes}m";
        }
    }
}

/// <summary>
/// DTO for search/filter criteria
/// </summary>
public class LibrarySearchDto
{
    public string? Query { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string SortBy { get; set; } = "recent"; // recent, name, duration, mostUsed
    public bool SortDescending { get; set; } = true;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}
