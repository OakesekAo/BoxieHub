
using Microsoft.AspNetCore.DataProtection;
namespace BoxieHub.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive credentials
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts plain text password for storage
    /// Uses Data Protection API (same as Identity cookies/tokens)
    /// </summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts protected password for API authentication
    /// </summary>
    string Unprotect(string protectedData);
}
