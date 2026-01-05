using System.Security.Cryptography;
using System.Text;

namespace BoxieHub.Services;

/// <summary>
/// Implements AES encryption for credential storage
/// Uses a key from configuration for encryption/decryption
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<CredentialEncryptionService> _logger;
    
    public CredentialEncryptionService(IConfiguration configuration, ILogger<CredentialEncryptionService> logger)
    {
        _logger = logger;
        
        // Get encryption key from configuration (User Secrets in dev, Azure Key Vault in prod)
        var keyString = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException(
                "Encryption:Key not found in configuration. " +
                "Set it using: dotnet user-secrets set \"Encryption:Key\" \"your-32-character-key-here\"");
        
        if (keyString.Length != 32)
        {
            throw new InvalidOperationException("Encryption:Key must be exactly 32 characters for AES-256");
        }
        
        _key = Encoding.UTF8.GetBytes(keyString);
    }
    
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));
        
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV(); // Generate random IV for each encryption
            
            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // Write IV to the beginning of the stream (needed for decryption)
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plaintext);
            }
            
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt credential");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }
    
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));
        
        try
        {
            var buffer = Convert.FromBase64String(ciphertext);
            
            using var aes = Aes.Create();
            aes.Key = _key;
            
            // Extract IV from the beginning of the buffer
            var iv = new byte[aes.IV.Length];
            Array.Copy(buffer, 0, iv, 0, iv.Length);
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credential");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }
}
