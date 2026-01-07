using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// Tracks where a media library item is being used
/// Helps users see which Tonies are using which library items
/// </summary>
public class MediaLibraryUsage
{
    public int Id { get; set; }
    
    /// <summary>
    /// Reference to the library item being used
    /// </summary>
    public int MediaLibraryItemId { get; set; }
    public MediaLibraryItem? MediaLibraryItem { get; set; }
    
    /// <summary>
    /// External household ID from Tonie Cloud
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string HouseholdId { get; set; } = string.Empty;
    
    /// <summary>
    /// External Tonie ID from Tonie Cloud
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TonieId { get; set; } = string.Empty;
    
    /// <summary>
    /// Tonie name (cached for display)
    /// </summary>
    [MaxLength(256)]
    public string? TonieName { get; set; }
    
    /// <summary>
    /// Chapter ID from Tonie Cloud
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ChapterId { get; set; } = string.Empty;
    
    /// <summary>
    /// Chapter title (cached for display)
    /// </summary>
    [MaxLength(256)]
    public string? ChapterTitle { get; set; }
    
    /// <summary>
    /// When this item was uploaded to the Tonie
    /// </summary>
    public DateTimeOffset UsedAt { get; set; } = DateTimeOffset.UtcNow;
}
