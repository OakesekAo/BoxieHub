using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// User's connected external storage accounts (Dropbox, Google Drive)
/// Used for OAuth token management and quota tracking
/// </summary>
public class UserStorageAccount
{
    public int Id { get; set; }
    
    /// <summary>
    /// Owner of this storage account
    /// </summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// Storage provider type
    /// </summary>
    [Required]
    public StorageProvider Provider { get; set; }
    
    /// <summary>
    /// Display name for this account (e.g., "My Dropbox", "oakes@email.com")
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Account identifier (email, account ID, etc.)
    /// </summary>
    [MaxLength(256)]
    public string? AccountIdentifier { get; set; }
    
    /// <summary>
    /// Encrypted OAuth access token
    /// </summary>
    public string? EncryptedAccessToken { get; set; }
    
    /// <summary>
    /// Encrypted OAuth refresh token
    /// </summary>
    public string? EncryptedRefreshToken { get; set; }
    
    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }
    
    /// <summary>
    /// Storage quota in bytes (if available from provider)
    /// </summary>
    public long? QuotaTotalBytes { get; set; }
    
    /// <summary>
    /// Storage used in bytes (if available from provider)
    /// </summary>
    public long? QuotaUsedBytes { get; set; }
    
    /// <summary>
    /// When quota was last checked
    /// </summary>
    public DateTimeOffset? QuotaLastCheckedAt { get; set; }
    
    /// <summary>
    /// Whether this account is currently active/connected
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When this account was connected
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// When this account was last modified
    /// </summary>
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Helper property for quota percentage
    /// </summary>
    public int? QuotaUsedPercentage
    {
        get
        {
            if (QuotaTotalBytes == null || QuotaUsedBytes == null || QuotaTotalBytes == 0)
                return null;
            
            return (int)((QuotaUsedBytes.Value * 100) / QuotaTotalBytes.Value);
        }
    }
}
