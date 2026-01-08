using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// User's storage provider preferences
/// Tracks default storage provider and last used settings
/// </summary>
public class UserStoragePreference
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// User's default storage provider for new uploads
    /// </summary>
    public StorageProvider DefaultProvider { get; set; } = StorageProvider.S3Railway;
    
    /// <summary>
    /// Default connected storage account (null for Database/S3Railway)
    /// Used for Dropbox and GoogleDrive
    /// </summary>
    public int? DefaultStorageAccountId { get; set; }
    public UserStorageAccount? DefaultStorageAccount { get; set; }
    
    /// <summary>
    /// Last used storage provider (for quick recall)
    /// </summary>
    public StorageProvider? LastUsedProvider { get; set; }
    
    /// <summary>
    /// Last used storage account ID
    /// </summary>
    public int? LastUsedStorageAccountId { get; set; }
    
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Modified { get; set; }
}
