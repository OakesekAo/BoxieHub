using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services;

/// <summary>
/// Service for managing Tonie credentials with automatic password unprotection
/// </summary>
public interface ITonieCredentialService
{
    Task<TonieCredential?> GetDefaultCredentialAsync(string userId);
    Task<(string Username, string Password)> GetUnprotectedCredentialsAsync(string userId);
}

public class TonieCredentialService : ITonieCredentialService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<TonieCredentialService> _logger;

    public TonieCredentialService(
        ApplicationDbContext dbContext,
        ICredentialEncryptionService encryption,
        ILogger<TonieCredentialService> logger)
    {
        _dbContext = dbContext;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<TonieCredential?> GetDefaultCredentialAsync(string userId)
    {
        return await _dbContext.TonieCredentials
            .Where(c => c.UserId == userId && c.IsDefault)
            .FirstOrDefaultAsync();
    }

    public async Task<(string Username, string Password)> GetUnprotectedCredentialsAsync(string userId)
    {
        var credential = await GetDefaultCredentialAsync(userId);

        if (credential == null)
            throw new InvalidOperationException("No default Tonie credential found");

        try
        {
            var password = _encryption.Unprotect(credential.EncryptedPassword);
            return (credential.TonieUsername, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect password for user {UserId}", userId);
            throw new InvalidOperationException("Failed to decrypt stored credentials. Please re-add your Tonie account.", ex);
        }
    }
}