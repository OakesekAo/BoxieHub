namespace BoxieHub.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive credentials
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts plain text using AES encryption
    /// </summary>
    /// <param name="plaintext">The text to encrypt</param>
    /// <returns>Base64 encoded encrypted string</returns>
    string Encrypt(string plaintext);
    
    /// <summary>
    /// Decrypts encrypted text back to plain text
    /// </summary>
    /// <param name="ciphertext">Base64 encoded encrypted string</param>
    /// <returns>Decrypted plain text</returns>
    string Decrypt(string ciphertext);
}
