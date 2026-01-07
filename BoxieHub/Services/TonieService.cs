using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Models.BoxieCloud;
using BoxieHub.Services.BoxieCloud;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace BoxieHub.Services;

/// <summary>
/// Business logic service for managing Creative Tonies
/// Provides high-level operations with automatic credential management
/// </summary>
public interface ITonieService
{
    /// <summary>
    /// Get all Creative Tonies for the authenticated user (from default account)
    /// Results are cached for 5 minutes to reduce API calls
    /// </summary>
    /// <param name="forceRefresh">If true, bypasses cache and fetches fresh data</param>
    Task<List<CreativeTonieDto>> GetUserCreativeTonieAsync(string userId, bool forceRefresh = false, CancellationToken ct = default);
    
    /// <summary>
    /// Get all households for the authenticated user
    /// </summary>
    Task<List<HouseholdDto>> GetUserHouseholdsAsync(string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Get Creative Tonies for a specific household
    /// </summary>
    Task<List<CreativeTonieDto>> GetCreativeToniesByHouseholdAsync(string userId, string householdId, CancellationToken ct = default);
    
    /// <summary>
    /// Get detailed information for a specific Tonie
    /// </summary>
    Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(string userId, string householdId, string tonieId, CancellationToken ct = default);
    
    /// <summary>
    /// Get aggregated statistics for all user's Tonies
    /// </summary>
    Task<TonieStatsViewModel> GetUserStatsAsync(string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Upload audio file to a Creative Tonie
    /// </summary>
    Task<SyncResultDto> UploadAudioToTonieAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        Stream audioStream, 
        string chapterTitle, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Delete a chapter from a Creative Tonie
    /// </summary>
    Task<bool> DeleteChapterAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        string chapterId, 
        CancellationToken ct = default);
        
    /// <summary>
    /// Clear cached Tonie data for a user (call after uploads/deletes)
    /// </summary>
    void ClearUserCache(string userId);
}

public class TonieService : ITonieService
{
    private readonly IBoxieCloudClient _boxieCloudClient;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TonieService> _logger;
    
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "tonies_user_";

    public TonieService(
        IBoxieCloudClient boxieCloudClient,
        ICredentialEncryptionService encryption,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IMemoryCache cache,
        ILogger<TonieService> logger)
    {
        _boxieCloudClient = boxieCloudClient;
        _encryption = encryption;
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<CreativeTonieDto>> GetUserCreativeTonieAsync(string userId, bool forceRefresh = false, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        
        // Return cached data if available and not forcing refresh
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<CreativeTonieDto>? cachedTonies) && cachedTonies != null)
        {
            _logger.LogDebug("Returning cached Tonies for user {UserId} ({Count} tonies)", userId, cachedTonies.Count);
            return cachedTonies;
        }
        
        _logger.LogDebug("Fetching fresh Tonie data for user {UserId} (forceRefresh: {ForceRefresh})", userId, forceRefresh);
        
        var credentials = await GetUserCredentialsAsync(userId);
        _logger.LogDebug("Retrieved credentials for {Username}", credentials.Username);
        
        var allTonies = new List<CreativeTonieDto>();

        try
        {
            // Get households
            _logger.LogDebug("Fetching households for {Username}", credentials.Username);
            var households = await _boxieCloudClient.GetHouseholdsAsync(
                credentials.Username, 
                credentials.Password, 
                ct);
            _logger.LogInformation("Retrieved {Count} household(s) for user {UserId}", households.Count, userId);

            // Get Tonies from all households
            foreach (var household in households)
            {
                _logger.LogDebug("Fetching Tonies for household {HouseholdId} ({HouseholdName})", 
                    household.Id, household.Name);
                
                var tonies = await _boxieCloudClient.GetCreativeToniesByHouseholdAsync(
                    credentials.Username,
                    credentials.Password,
                    household.Id,
                    ct);
                
                _logger.LogDebug("Retrieved {Count} Tonies from household {HouseholdId}", 
                    tonies.Count, household.Id);

                allTonies.AddRange(tonies);
            }

            // Cache the results
            _cache.Set(cacheKey, allTonies, CacheDuration);
            _logger.LogInformation("Cached {TotalCount} Creative Tonies for user {UserId} (expires in {Minutes} minutes)", 
                allTonies.Count, userId, CacheDuration.TotalMinutes);

            return allTonies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Creative Tonies for user {UserId}. Error: {ErrorMessage}", 
                userId, ex.Message);
            throw;
        }
    }

    public async Task<List<HouseholdDto>> GetUserHouseholdsAsync(string userId, CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            var households = await _boxieCloudClient.GetHouseholdsAsync(
                credentials.Username,
                credentials.Password,
                ct);

            _logger.LogInformation("Retrieved {Count} household(s) for user {UserId}", households.Count, userId);
            return households;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting households for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<CreativeTonieDto>> GetCreativeToniesByHouseholdAsync(
        string userId, 
        string householdId, 
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            var tonies = await _boxieCloudClient.GetCreativeToniesByHouseholdAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                ct);

            _logger.LogInformation("Retrieved {Count} Creative Tonies for household {HouseholdId}", 
                tonies.Count, householdId);
            return tonies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Creative Tonies for household {HouseholdId}", householdId);
            throw;
        }
    }

    public async Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            var tonie = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                ct);

            _logger.LogInformation("Retrieved details for Tonie {TonieId} with {ChapterCount} chapters", 
                tonieId, tonie.Chapters?.Count ?? 0);
            return tonie;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for Tonie {TonieId}", tonieId);
            throw;
        }
    }

    public async Task<TonieStatsViewModel> GetUserStatsAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            // Get Tonies (will use cache if available)
            var tonies = await GetUserCreativeTonieAsync(userId, forceRefresh: false, ct);

            var stats = new TonieStatsViewModel
            {
                TotalTonies = tonies.Count,
                TotalChapters = tonies.Sum(t => t.ChaptersPresent),
                TotalSecondsPresent = tonies.Sum(t => t.SecondsPresent),
                TotalSecondsRemaining = tonies.Sum(t => t.SecondsRemaining),
                TotalDuration = TimeSpan.FromSeconds(tonies.Sum(t => t.SecondsPresent))
            };

            _logger.LogInformation("Calculated stats for user {UserId}: {TotalTonies} Tonies, {TotalChapters} chapters", 
                userId, stats.TotalTonies, stats.TotalChapters);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating stats for user {UserId}", userId);
            throw;
        }
    }

    public async Task<SyncResultDto> UploadAudioToTonieAsync(
        string userId,
        string householdId,
        string tonieId,
        Stream audioStream,
        string chapterTitle,
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            var result = await _boxieCloudClient.SyncAudioAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                audioStream,
                chapterTitle,
                ct);

            if (result.Success)
            {
                _logger.LogInformation("Successfully uploaded audio '{Title}' to Tonie {TonieId} for user {UserId}",
                    chapterTitle, tonieId, userId);
                
                // Clear cache to force refresh on next load
                ClearUserCache(userId);
            }
            else
            {
                _logger.LogWarning("Failed to upload audio '{Title}' to Tonie {TonieId}: {Error}",
                    chapterTitle, tonieId, result.ErrorDetails);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading audio to Tonie {TonieId}", tonieId);
            return new SyncResultDto
            {
                Success = false,
                Message = "Upload failed",
                ErrorDetails = ex.Message,
                TracksProcessed = 0
            };
        }
    }

    public async Task<bool> DeleteChapterAsync(
        string userId,
        string householdId,
        string tonieId,
        string chapterId,
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            // Get current Tonie data
            var tonie = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                ct);

            // Remove the chapter
            var updatedChapters = tonie.Chapters?
                .Where(c => c.Id != chapterId)
                .ToList() ?? new List<ChapterDto>();

            if (updatedChapters.Count == tonie.Chapters?.Count)
            {
                _logger.LogWarning("Chapter {ChapterId} not found in Tonie {TonieId}", chapterId, tonieId);
                return false;
            }

            // Update Tonie with remaining chapters
            await _boxieCloudClient.PatchCreativeTonieAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                tonie.Name,
                updatedChapters,
                ct);

            _logger.LogInformation("Successfully deleted chapter {ChapterId} from Tonie {TonieId}", 
                chapterId, tonieId);
            
            // Clear cache to force refresh on next load
            ClearUserCache(userId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chapter {ChapterId} from Tonie {TonieId}", 
                chapterId, tonieId);
            throw;
        }
    }

    /// <summary>
    /// Clear cached Tonie data for a user
    /// </summary>
    public void ClearUserCache(string userId)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Cleared Tonie cache for user {UserId}", userId);
    }

    /// <summary>
    /// Get user's default Tonie Cloud credentials (username and decrypted password)
    /// </summary>
    private async Task<(string Username, string Password)> GetUserCredentialsAsync(string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var credential = await dbContext.TonieCredentials
            .Where(c => c.UserId == userId && c.IsDefault)
            .FirstOrDefaultAsync();

        if (credential == null)
        {
            throw new InvalidOperationException(
                "No default Tonie Cloud account found. Please add a Tonie account first.");
        }

        // Update last authenticated timestamp
        credential.LastAuthenticated = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        // Decrypt password
        var password = _encryption.Unprotect(credential.EncryptedPassword);

        return (credential.TonieUsername, password);
    }
}

/// <summary>
/// View model for aggregated Tonie statistics
/// </summary>
public class TonieStatsViewModel
{
    public int TotalTonies { get; set; }
    public int TotalChapters { get; set; }
    public float TotalSecondsPresent { get; set; }
    public float TotalSecondsRemaining { get; set; }
    public TimeSpan TotalDuration { get; set; }

    public string FormattedDuration => 
        TotalDuration.TotalHours >= 1 
            ? $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m"
            : $"{TotalDuration.Minutes}m";

    public int StorageUsedPercentage => 
        TotalSecondsPresent + TotalSecondsRemaining > 0
            ? (int)((TotalSecondsPresent / (TotalSecondsPresent + TotalSecondsRemaining)) * 100)
            : 0;
}
