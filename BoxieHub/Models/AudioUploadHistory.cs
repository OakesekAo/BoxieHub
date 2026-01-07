using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// Tracks audio uploads to Tonie Cloud for backup, retry, and history
/// </summary>
public class AudioUploadHistory
{
    public int Id { get; set; }

    /// <summary>
    /// User who performed the upload
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the stored audio file
    /// </summary>
    [Required]
    public Guid FileUploadId { get; set; }
    public FileUpload? FileUpload { get; set; }

    /// <summary>
    /// Tonie Cloud household ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string HouseholdId { get; set; } = string.Empty;

    /// <summary>
    /// Tonie Cloud tonie ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string TonieId { get; set; } = string.Empty;

    /// <summary>
    /// Chapter title given by user
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ChapterTitle { get; set; } = string.Empty;

    /// <summary>
    /// Upload status
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, InProgress

    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Tonie Cloud chapter ID (if upload succeeded)
    /// </summary>
    [MaxLength(100)]
    public string? ChapterId { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// When the upload was initiated
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the upload was last attempted
    /// </summary>
    public DateTimeOffset? LastAttemptedAt { get; set; }

    /// <summary>
    /// When the upload completed (success or final failure)
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Helper property to check if upload can be retried
    /// </summary>
    public bool CanRetry => Status == "Failed" && RetryCount < 3;

    /// <summary>
    /// Helper property to check if upload is in progress
    /// </summary>
    public bool IsInProgress => Status == "InProgress" || Status == "Pending";
}

/// <summary>
/// Upload status constants
/// </summary>
public static class UploadStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Success = "Success";
    public const string Failed = "Failed";
}
