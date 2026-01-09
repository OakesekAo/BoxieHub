using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// Represents a background import job (YouTube, Podcast, etc.)
/// Tracks progress and status of media imports from external sources
/// </summary>
public class ImportJob
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }
    
    public ImportSource Source { get; set; }
    
    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = default!;
    
    [MaxLength(500)]
    public string? SourceTitle { get; set; }
    
    [MaxLength(2000)]
    public string? SourceDescription { get; set; }
    
    [MaxLength(1024)]
    public string? SourceThumbnailUrl { get; set; }
    
    public float? SourceDurationSeconds { get; set; }
    
    /// <summary>
    /// Current status as string (for database storage)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// Status as enum for code convenience
    /// </summary>
    public ImportJobStatus StatusEnum 
    {
        get => Enum.Parse<ImportJobStatus>(Status);
        set => Status = value.ToString();
    }
    
    /// <summary>
    /// Reference to the created MediaLibraryItem (null until completed)
    /// </summary>
    public int? MediaLibraryItemId { get; set; }
    public MediaLibraryItem? MediaLibraryItem { get; set; }
    
    /// <summary>
    /// Progress from 0-100
    /// </summary>
    public int ProgressPercentage { get; set; }
    
    /// <summary>
    /// User-friendly status message (e.g., "Downloading audio...", "Processing file...")
    /// </summary>
    [MaxLength(200)]
    public string? StatusMessage { get; set; }
    
    /// <summary>
    /// Detailed error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? StartedAt { get; set; }
    
    public DateTimeOffset? CompletedAt { get; set; }
}
