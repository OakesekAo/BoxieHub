using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BoxieHub.Models;

/// <summary>
/// Represents an audio file stored in the user's media library
/// Can be reused across multiple Tonies
/// </summary>
public class MediaLibraryItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Owner of this media item
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// Display title for the media item
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    // ===== File Reference =====
    
    /// <summary>
    /// Reference to the uploaded audio file
    /// </summary>
    public Guid FileUploadId { get; set; }
    public FileUpload? FileUpload { get; set; }
    
    // ===== Metadata =====
    
    /// <summary>
    /// Duration of the audio in seconds
    /// </summary>
    public float DurationSeconds { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// MIME type (e.g., "audio/mpeg")
    /// </summary>
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Original filename
    /// </summary>
    [MaxLength(256)]
    public string? OriginalFileName { get; set; }
    
    // ===== Organization =====
    
    /// <summary>
    /// JSON array of tags for searchability
    /// </summary>
    public string? TagsJson { get; set; }
    
    /// <summary>
    /// Category (Music, Story, Educational, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }
    
    // ===== Tracking =====
    
    /// <summary>
    /// Number of times this item has been used on Tonies
    /// </summary>
    public int UseCount { get; set; } = 0;
    
    /// <summary>
    /// When this item was added to the library
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Last time this item was used on a Tonie
    /// </summary>
    public DateTimeOffset? LastUsed { get; set; }
    
    // ===== Navigation Properties =====
    
    /// <summary>
    /// Track where this media item is being used
    /// </summary>
    public ICollection<MediaLibraryUsage> Usages { get; set; } = [];
    
    // ===== Helper Properties =====
    
    /// <summary>
    /// Get tags as a list
    /// </summary>
    public List<string> Tags
    {
        get
        {
            if (string.IsNullOrEmpty(TagsJson))
                return new List<string>();
            
            try
            {
                return JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
        set
        {
            TagsJson = value.Any() ? JsonSerializer.Serialize(value) : null;
        }
    }
    
    /// <summary>
    /// Format duration as human-readable string
    /// </summary>
    public string FormattedDuration
    {
        get
        {
            var timeSpan = TimeSpan.FromSeconds(DurationSeconds);
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            return $"{timeSpan.TotalSeconds:F1}s";
        }
    }
    
    /// <summary>
    /// Format file size as human-readable string
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSizeBytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
