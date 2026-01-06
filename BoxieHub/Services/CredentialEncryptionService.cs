using Microsoft.AspNetCore.DataProtection;

namespace BoxieHub.Services;

/// <summary>
/// Implements password protection using ASP.NET Core Data Protection API
/// More secure and maintainable than custom encryption
/// Automatically handles key rotation and machine-specific keys
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<CredentialEncryptionService> _logger;

    public CredentialEncryptionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CredentialEncryptionService> logger)
    {
        _logger = logger;

        // Create a protector with a specific purpose string
        // This ensures keys are isolated from other parts of the app
        _protector = dataProtectionProvider.CreateProtector("BoxieHub.TonieCredentials.v1");

        _logger.LogInformation("CredentialEncryptionService initialized with Data Protection API");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        try
        {
            return _protector.Protect(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect credential");
            throw new InvalidOperationException("Protection failed", ex);
        }
    }

    public string Unprotect(string protectedData)
    {
        if (string.IsNullOrEmpty(protectedData))
            throw new ArgumentException("Protected data cannot be null or empty", nameof(protectedData));

        try
        {
            return _protector.Unprotect(protectedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect credential");
            throw new InvalidOperationException("Unprotection failed - data may be corrupted or keys rotated", ex);
        }
    }
}