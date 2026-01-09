using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services;

/// <summary>
/// Service for managing user storage provider preferences
/// </summary>
public interface IStoragePreferenceService
{
    Task<UserStoragePreference> GetUserPreferenceAsync(string userId, CancellationToken ct = default);
    Task<StorageProvider> GetDefaultProviderAsync(string userId, CancellationToken ct = default);
    Task SetDefaultProviderAsync(string userId, StorageProvider provider, int? storageAccountId = null, CancellationToken ct = default);
    Task UpdateLastUsedAsync(string userId, StorageProvider provider, int? storageAccountId = null, CancellationToken ct = default);
    Task<List<StorageProvider>> GetAvailableProvidersAsync(string userId, CancellationToken ct = default);
}

public class StoragePreferenceService : IStoragePreferenceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StoragePreferenceService> _logger;

    public StoragePreferenceService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<StoragePreferenceService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserStoragePreference> GetUserPreferenceAsync(string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var preference = await dbContext.UserStoragePreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (preference == null)
        {
            // Create default preference
            preference = new UserStoragePreference
            {
                UserId = userId,
                DefaultProvider = StorageProvider.S3Railway,
                Created = DateTimeOffset.UtcNow
            };
            
            dbContext.UserStoragePreferences.Add(preference);
            await dbContext.SaveChangesAsync(ct);
            
            _logger.LogInformation("Created default storage preference for user {UserId}", userId);
        }

        return preference;
    }

    public async Task<StorageProvider> GetDefaultProviderAsync(string userId, CancellationToken ct = default)
    {
        var preference = await GetUserPreferenceAsync(userId, ct);
        return preference.DefaultProvider;
    }

    public async Task SetDefaultProviderAsync(
        string userId, 
        StorageProvider provider, 
        int? storageAccountId = null, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var preference = await GetUserPreferenceAsync(userId, ct);
        
        preference.DefaultProvider = provider;
        preference.DefaultStorageAccountId = storageAccountId;
        preference.Modified = DateTimeOffset.UtcNow;
        
        dbContext.UserStoragePreferences.Update(preference);
        await dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Updated default storage provider for user {UserId} to {Provider}", 
            userId, provider);
    }

    public async Task UpdateLastUsedAsync(
        string userId, 
        StorageProvider provider, 
        int? storageAccountId = null, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var preference = await GetUserPreferenceAsync(userId, ct);
        
        preference.LastUsedProvider = provider;
        preference.LastUsedStorageAccountId = storageAccountId;
        preference.Modified = DateTimeOffset.UtcNow;
        
        dbContext.UserStoragePreferences.Update(preference);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<StorageProvider>> GetAvailableProvidersAsync(string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var providers = new List<StorageProvider>();
        
        // Database is always available
        providers.Add(StorageProvider.Database);
        
        // Check if S3 is configured
        var s3Config = _configuration.GetSection("S3Storage");
        if (!string.IsNullOrEmpty(s3Config["AccessKey"]) && !string.IsNullOrEmpty(s3Config["SecretKey"]))
        {
            providers.Add(StorageProvider.S3Railway);
        }
        
        // Check for connected storage accounts
        var connectedAccounts = await dbContext.UserStorageAccounts
            .Where(a => a.UserId == userId && a.IsActive)
            .Select(a => a.Provider)
            .ToListAsync(ct);
        
        providers.AddRange(connectedAccounts);
        
        return providers.Distinct().ToList();
    }
}
