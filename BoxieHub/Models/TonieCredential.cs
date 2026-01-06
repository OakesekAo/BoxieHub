using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models;

/// <summary>
/// Stores encrypted Tonie Cloud credentials for a BoxieHub user
/// Each BoxieHub user can link multiple Tonie Cloud accounts
/// </summary>
public class TonieCredential
{
    public int Id { get; set; }
    
    /// <summary>
    /// BoxieHub user ID (ASP.NET Core Identity user)
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;
    
    /// <summary>
    /// Tonie Cloud username/email
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string TonieUsername { get; set; } = default!;
    
    /// <summary>
    /// Encrypted Tonie Cloud password
    /// Never store passwords in plain text
    /// </summary>
    [Required]
    public string EncryptedPassword { get; set; } = default!;
    
    /// <summary>
    /// User-friendly display name for this Tonie account
    /// e.g., "Family Account", "Kids' Tonies"
    /// </summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// Is this the default/active account for the user?
    /// Only one credential per user should have IsDefault = true
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// Last time we successfully authenticated with this credential
    /// Used for monitoring and troubleshooting
    /// </summary>
    public DateTimeOffset? LastAuthenticated { get; set; }
    
    private DateTimeOffset _created;
    public DateTimeOffset Created
    {
        get => _created;
        set => _created = value.ToUniversalTime();
    }
    
    private DateTimeOffset _modified;
    public DateTimeOffset Modified
    {
        get => _modified;
        set => _modified = value.ToUniversalTime();
    }
    
    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
}
