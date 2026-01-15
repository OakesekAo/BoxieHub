using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoxieHub.Models;

/// <summary>
/// Represents a background job for uploading audio from library to a Tonie
/// Similar to ImportJob but for the reverse direction (library → Tonie)
/// </summary>
public class TonieUploadJob
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// The user who initiated this upload
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = default!;
    
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// The library item being uploaded
    /// </summary>
    public int MediaLibraryItemId { get; set; }
    
    public MediaLibraryItem? MediaLibraryItem { get; set; }
    
    /// <summary>
    /// Target Tonie household ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string HouseholdId { get; set; } = default!;
    
    /// <summary>
    /// Target Tonie ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TonieId { get; set; } = default!;
    
    /// <summary>
    /// Optional: Custom chapter title (defaults to library item title)
    /// </summary>
    [MaxLength(200)]
    public string? ChapterTitle { get; set; }
    
    /// <summary>
    /// Current status of the upload job
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = nameof(TonieUploadJobStatus.Pending);
    
    /// <summary>
    /// Parsed status enum for easy querying
    /// </summary>
    [NotMapped]
    public TonieUploadJobStatus StatusEnum
    {
        get => Enum.TryParse<TonieUploadJobStatus>(Status, out var result) ? result : TonieUploadJobStatus.Pending;
        set => Status = value.ToString();
    }
    
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercentage { get; set; }
    
    /// <summary>
    /// Human-readable status message (shown in UI)
    /// </summary>
    [MaxLength(500)]
    public string? StatusMessage { get; set; }
    
    /// <summary>
    /// Error message if upload failed
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Tonie sync result details (JSON)
    /// </summary>
    public string? SyncResultJson { get; set; }
    
    /// <summary>
    /// Number of retry attempts (max 3)
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// When processing started
    /// </summary>
    public DateTimeOffset? Started { get; set; }
    
    /// <summary>
    /// When job completed (success or failure)
    /// </summary>
    public DateTimeOffset? Completed { get; set; }
    
    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Status values for TonieUploadJob
/// </summary>
public enum TonieUploadJobStatus
{
    /// <summary>
    /// Job is queued, waiting to be processed
    /// </summary>
    Pending,
    
    /// <summary>
    /// Preparing audio file for upload
    /// </summary>
    Preparing,
    
    /// <summary>
    /// Compressing audio (if needed)
    /// </summary>
    Compressing,
    
    /// <summary>
    /// Uploading to Tonie Cloud API
    /// </summary>
    Uploading,
    
    /// <summary>
    /// Refreshing Tonie data from API
    /// </summary>
    Refreshing,
    
    /// <summary>
    /// Upload completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Upload failed (see ErrorMessage)
    /// </summary>
    Failed,
    
    /// <summary>
    /// Job was cancelled by user
    /// </summary>
    Cancelled
}
