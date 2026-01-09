using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Models.BoxieCloud;
using BoxieHub.Services.BoxieCloud;
using BoxieHub.Services.Storage;
using BoxieHub.Client.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BoxieHub.Services;

/// <summary>
/// Business logic service for managing Creative Tonies
/// Database-first architecture with smart API sync
/// </summary>
public interface ITonieService
{
    /// <summary>
    /// Get all Creative Tonies for the authenticated user from DATABASE
    /// Only fetches from API if data is missing or forceRefresh is true
    /// </summary>
    Task<(List<CreativeTonieDto> Tonies, bool IsStale)> GetUserCreativeTonieAsync(string userId, bool forceRefresh = false, CancellationToken ct = default);
    
    /// <summary>
    /// Get aggregated statistics for all user's Tonies
    /// </summary>
    Task<TonieStatsViewModel> GetUserStatsAsync(string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Get detailed information for a specific Tonie from database or API
    /// </summary>
    Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(string userId, string householdExternalId, string tonieId, bool forceRefresh = false, CancellationToken ct = default);
    
    /// <summary>
    /// Upload audio file to a Creative Tonie and refresh cache
    /// </summary>
    Task<SyncResultDto> UploadAudioToTonieAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        Stream audioStream, 
        string chapterTitle, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Delete a chapter from a Creative Tonie and refresh cache
    /// </summary>
    Task<bool> DeleteChapterAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        string chapterId, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Reorder chapters on a Creative Tonie
    /// </summary>
    Task ReorderChaptersAsync(
        string userId,
        string householdId,
        string tonieId,
        string tonieName,
        List<ChapterDto> newChapterOrder,
        CancellationToken ct = default);
    
    /// <summary>
    /// Update a chapter's title
    /// </summary>
    Task<bool> UpdateChapterTitleAsync(
        string userId,
        string householdId,
        string tonieId,
        string chapterId,
        string newTitle,
        CancellationToken ct = default);
    
    /// <summary>
    /// Upload custom image for a Tonie (local only, not synced to Tonie Cloud)
    /// </summary>
    Task<bool> UploadCustomTonieImageAsync(
        string userId,
        string householdId,
        string tonieId,
        Stream imageStream,
        string contentType,
        string fileName,
        CancellationToken ct = default);
    
    /// <summary>
    /// Delete custom image for a Tonie (reverts to Tonie Cloud image)
    /// </summary>
    Task<bool> DeleteCustomTonieImageAsync(
        string userId,
        string householdId,
        string tonieId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Delete all synced data associated with a Tonie account
    /// Removes households and creative tonies (characters) synced from that account
    /// Does NOT delete local content items, assignments, or devices
    /// </summary>
    Task DeleteAccountDataAsync(int credentialId, CancellationToken ct = default);
}

public class TonieService : ITonieService
{
    private readonly IBoxieCloudClient _boxieCloudClient;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStoragePreferenceService _storagePreferenceService;
    private readonly ILogger<TonieService> _logger;

    public TonieService(
        IBoxieCloudClient boxieCloudClient,
        ICredentialEncryptionService encryption,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IFileStorageService fileStorageService,
        IStoragePreferenceService storagePreferenceService,
        ILogger<TonieService> logger)
    {
        _boxieCloudClient = boxieCloudClient;
        _encryption = encryption;
        _dbContextFactory = dbContextFactory;
        _fileStorageService = fileStorageService;
        _storagePreferenceService = storagePreferenceService;
        _logger = logger;
    }

    public async Task<(List<CreativeTonieDto> Tonies, bool IsStale)> GetUserCreativeTonieAsync(
        string userId, 
        bool forceRefresh = false, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        // Get user's households from database
        var households = await dbContext.Households
            .Include(h => h.Characters.Where(c => c.Type == CharacterType.Creative))
                .ThenInclude(c => c.CustomImage)
            .ToListAsync(ct);

        bool hasData = households.Any() && households.Any(h => h.Characters.Any());
        bool isStale = households.Any(h => h.IsStale);

        // If no data or force refresh, fetch from API
        if (!hasData || forceRefresh)
        {
            _logger.LogInformation("Fetching Tonies from API for user {UserId} (forceRefresh: {ForceRefresh}, hasData: {HasData})", 
                userId, forceRefresh, hasData);
            
            await SyncTonieDataFromApiAsync(userId, dbContext, ct);
            
            // Re-query after sync
            households = await dbContext.Households
                .Include(h => h.Characters.Where(c => c.Type == CharacterType.Creative))
                .ToListAsync(ct);
            
            isStale = false; // Fresh data
        }
        else
        {
            _logger.LogDebug("Returning cached Tonies for user {UserId} (stale: {IsStale})", userId, isStale);
        }

        // Convert to DTOs
        var tonies = households
            .SelectMany(h => h.Characters.Where(c => c.Type == CharacterType.Creative))
            .Select(CharacterToDto)
            .ToList();

        return (tonies, isStale);
    }

    public async Task<TonieStatsViewModel> GetUserStatsAsync(string userId, CancellationToken ct = default)
    {
        var (tonies, _) = await GetUserCreativeTonieAsync(userId, forceRefresh: false, ct);

        return new TonieStatsViewModel
        {
            TotalTonies = tonies.Count,
            TotalChapters = tonies.Sum(t => t.ChaptersPresent),
            TotalSecondsPresent = tonies.Sum(t => t.SecondsPresent),
            TotalSecondsRemaining = tonies.Sum(t => t.SecondsRemaining),
            TotalDuration = TimeSpan.FromSeconds(tonies.Sum(t => t.SecondsPresent))
        };
    }

    public async Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(
        string userId,
        string householdExternalId,
        string tonieId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Try to get from database first (unless force refresh)
        Character? character = null;
        if (!forceRefresh)
        {
            character = await dbContext.Characters
                .Include(c => c.CustomImage)
                .FirstOrDefaultAsync(c => 
                    c.ExternalCharacterId == tonieId && 
                    c.Type == CharacterType.Creative &&
                    c.Household!.ExternalId == householdExternalId, ct);

            if (character != null && !character.IsStale)
            {
                _logger.LogDebug("Returning cached Tonie details for {TonieId}", tonieId);
                return CharacterToDto(character);
            }
        }

        // Fetch from API (either forced or cache was stale/missing)
        _logger.LogInformation("Fetching Tonie details from API for {TonieId} (forceRefresh: {ForceRefresh})", 
            tonieId, forceRefresh);
        
        var credentials = await GetUserCredentialsAsync(userId);
        var tonie = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
            credentials.Username,
            credentials.Password,
            householdExternalId,
            tonieId,
            ct);

        // Update database if character exists, otherwise find it again
        if (character == null)
        {
            character = await dbContext.Characters
                .Include(c => c.CustomImage)
                .FirstOrDefaultAsync(c => c.ExternalCharacterId == tonieId, ct);
        }

        if (character != null)
        {
            UpdateCharacterFromDto(character, tonie);
            character.LastSyncedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            
            // Override with custom image if available
            if (!string.IsNullOrEmpty(character.DisplayImageUrl))
            {
                tonie.ImageUrl = character.DisplayImageUrl;
            }
        }

        return tonie;
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
                _logger.LogInformation("Successfully uploaded audio '{Title}' to Tonie {TonieId}", chapterTitle, tonieId);
                
                // Refresh this specific Tonie from API
                await RefreshTonieFromApiAsync(userId, householdId, tonieId, ct);
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
            var tonie = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                ct);

            var updatedChapters = tonie.Chapters?
                .Where(c => c.Id != chapterId)
                .ToList() ?? new List<ChapterDto>();

            if (updatedChapters.Count == tonie.Chapters?.Count)
            {
                _logger.LogWarning("Chapter {ChapterId} not found in Tonie {TonieId}", chapterId, tonieId);
                return false;
            }

            await _boxieCloudClient.PatchCreativeTonieAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                tonie.Name,
                updatedChapters,
                ct);

            _logger.LogInformation("Successfully deleted chapter {ChapterId} from Tonie {TonieId}", chapterId, tonieId);
            
            // Refresh this specific Tonie from API
            await RefreshTonieFromApiAsync(userId, householdId, tonieId, ct);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chapter {ChapterId} from Tonie {TonieId}", chapterId, tonieId);
            throw;
        }
    }

    public async Task ReorderChaptersAsync(
        string userId,
        string householdId,
        string tonieId,
        string tonieName,
        List<ChapterDto> newChapterOrder,
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            _logger.LogInformation("Reordering {Count} chapters for Tonie {TonieId}", newChapterOrder.Count, tonieId);

            // Call API to update chapter order
            await _boxieCloudClient.PatchCreativeTonieAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                tonieName,
                newChapterOrder,
                ct);

            _logger.LogInformation("Successfully reordered chapters for Tonie {TonieId}", tonieId);
            
            // Refresh this specific Tonie from API
            await RefreshTonieFromApiAsync(userId, householdId, tonieId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering chapters for Tonie {TonieId}", tonieId);
            throw;
        }
    }

    public async Task<bool> UpdateChapterTitleAsync(
        string userId,
        string householdId,
        string tonieId,
        string chapterId,
        string newTitle,
        CancellationToken ct = default)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        try
        {
            // Get current Tonie details
            var tonie = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                ct);

            // Find and update the chapter
            var chapter = tonie.Chapters?.FirstOrDefault(c => c.Id == chapterId);
            if (chapter == null)
            {
                _logger.LogWarning("Chapter {ChapterId} not found in Tonie {TonieId}", chapterId, tonieId);
                return false;
            }

            var oldTitle = chapter.Title;
            chapter.Title = newTitle;

            // Update via API
            await _boxieCloudClient.PatchCreativeTonieAsync(
                credentials.Username,
                credentials.Password,
                householdId,
                tonieId,
                tonie.Name,
                tonie.Chapters!,
                ct);

            _logger.LogInformation("Successfully updated chapter {ChapterId} title from '{OldTitle}' to '{NewTitle}'", 
                chapterId, oldTitle, newTitle);
            
            // Refresh cache
            await RefreshTonieFromApiAsync(userId, householdId, tonieId, ct);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating chapter {ChapterId} title", chapterId);
            throw;
        }
    }

    public async Task<bool> UploadCustomTonieImageAsync(
        string userId,
        string householdId,
        string tonieId,
        Stream imageStream,
        string contentType,
        string fileName,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            // Find the character
            var character = await dbContext.Characters
                .Include(c => c.CustomImage)
                .FirstOrDefaultAsync(c => 
                    c.ExternalCharacterId == tonieId && 
                    c.Household!.ExternalId == householdId, ct);

            if (character == null)
            {
                _logger.LogWarning("Character {TonieId} not found in household {HouseholdId}", tonieId, householdId);
                return false;
            }

            // Validate image size (max 5MB)
            const long maxSizeBytes = 5 * 1024 * 1024;
            if (imageStream.Length > maxSizeBytes)
            {
                _logger.LogWarning("Image too large: {Size} bytes (max: {MaxSize})", imageStream.Length, maxSizeBytes);
                throw new InvalidOperationException($"Image size must be less than 5MB. Current size: {imageStream.Length / (1024 * 1024.0):F2}MB");
            }

            // Delete old custom image if exists
            if (character.CustomImageId.HasValue)
            {
                var oldImage = await dbContext.FileUploads.FindAsync(new object[] { character.CustomImageId.Value }, ct);
                if (oldImage != null)
                {
                    // Delete from external storage if applicable
                    if (!string.IsNullOrEmpty(oldImage.StoragePath))
                    {
                        try
                        {
                            await _fileStorageService.DeleteFileAsync(
                                oldImage.StoragePath,
                                oldImage.UserStorageAccountId,
                                ct);
                            _logger.LogInformation("Deleted old image from {Provider}: {StoragePath}", 
                                oldImage.Provider, oldImage.StoragePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete old image from storage: {StoragePath}", 
                                oldImage.StoragePath);
                            // Continue with upload even if deletion fails
                        }
                    }
                    
                    dbContext.FileUploads.Remove(oldImage);
                }
            }

            // Get user's default storage provider
            var provider = await _storagePreferenceService.GetDefaultProviderAsync(userId, ct);
            
            string? storagePath = null;
            byte[]? imageData = null;

            // Handle storage based on provider
            if (provider == StorageProvider.Database)
            {
                // Legacy: Store in database
                _logger.LogInformation("Storing Tonie image '{FileName}' in database for user {UserId}", 
                    fileName, userId);
                
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms, ct);
                imageData = ms.ToArray();
            }
            else
            {
                // Store in external storage (S3, Dropbox, GDrive)
                _logger.LogInformation("Uploading Tonie image '{FileName}' to {Provider} for user {UserId}", 
                    fileName, provider, userId);

                storagePath = await _fileStorageService.UploadFileAsync(
                    imageStream,
                    fileName,
                    contentType,
                    userId,
                    null, // No specific storage account for S3Railway
                    ct);

                _logger.LogInformation("Successfully uploaded Tonie image to {Provider}: {StoragePath}", 
                    provider, storagePath);
            }

            // Create new image upload record
            var fileUpload = new FileUpload
            {
                Id = Guid.NewGuid(),
                Data = imageData, // NULL for external storage
                ContentType = contentType,
                FileName = fileName,
                FileCategory = "Image",
                FileSizeBytes = imageStream.Length,
                Provider = provider,
                StoragePath = storagePath,
                UserStorageAccountId = null, // S3Railway doesn't use storage accounts
                Created = DateTimeOffset.UtcNow
            };

            dbContext.FileUploads.Add(fileUpload);
            character.CustomImageId = fileUpload.Id;

            await dbContext.SaveChangesAsync(ct);

            // Update user's last used provider
            await _storagePreferenceService.UpdateLastUsedAsync(userId, provider, null, ct);

            _logger.LogInformation("Successfully uploaded custom image for Tonie {TonieId} (Provider: {Provider})", 
                tonieId, provider);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading custom image for Tonie {TonieId}", tonieId);
            throw;
        }
    }

    public async Task<bool> DeleteCustomTonieImageAsync(
        string userId,
        string householdId,
        string tonieId,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            // Find the character
            var character = await dbContext.Characters
                .Include(c => c.CustomImage)
                .FirstOrDefaultAsync(c => 
                    c.ExternalCharacterId == tonieId && 
                    c.Household!.ExternalId == householdId, ct);

            if (character == null)
            {
                _logger.LogWarning("Character {TonieId} not found in household {HouseholdId}", tonieId, householdId);
                return false;
            }

            if (!character.CustomImageId.HasValue)
            {
                _logger.LogInformation("No custom image to delete for Tonie {TonieId}", tonieId);
                return true; // Nothing to delete
            }

            // Delete the file upload
            var imageId = character.CustomImageId.Value;
            character.CustomImageId = null;
            
            var image = await dbContext.FileUploads.FindAsync(new object[] { imageId }, ct);
            if (image != null)
            {
                // Delete from external storage if applicable
                if (!string.IsNullOrEmpty(image.StoragePath))
                {
                    try
                    {
                        await _fileStorageService.DeleteFileAsync(
                            image.StoragePath,
                            image.UserStorageAccountId,
                            ct);
                        _logger.LogInformation("Deleted Tonie image from {Provider}: {StoragePath}", 
                            image.Provider, image.StoragePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete Tonie image from storage: {StoragePath}", 
                            image.StoragePath);
                        // Continue with database deletion even if storage deletion fails
                    }
                }
                
                dbContext.FileUploads.Remove(image);
            }

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully deleted custom image for Tonie {TonieId}", tonieId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting custom image for Tonie {TonieId}", tonieId);
            throw;
        }
    }

    /// <summary>
    /// Sync all Tonie data from API to database
    /// </summary>
    private async Task SyncTonieDataFromApiAsync(string userId, ApplicationDbContext dbContext, CancellationToken ct)
    {
        var credentials = await GetUserCredentialsAsync(userId);

        // Fetch households from API
        var householdsDto = await _boxieCloudClient.GetHouseholdsAsync(
            credentials.Username,
            credentials.Password,
            ct);

        foreach (var householdDto in householdsDto)
        {
            // Find or create household
            var household = await dbContext.Households
                .Include(h => h.Characters)
                .FirstOrDefaultAsync(h => h.ExternalId == householdDto.Id, ct);

            if (household == null)
            {
                household = new Household
                {
                    Name = householdDto.Name,
                    ExternalId = householdDto.Id,
                    Created = DateTimeOffset.UtcNow
                };
                dbContext.Households.Add(household);
            }
            else
            {
                household.Name = householdDto.Name;
            }

            household.LastSyncedAt = DateTimeOffset.UtcNow;

            // Fetch Creative Tonies for this household
            var toniesDto = await _boxieCloudClient.GetCreativeToniesByHouseholdAsync(
                credentials.Username,
                credentials.Password,
                householdDto.Id,
                ct);

            foreach (var tonieDto in toniesDto)
            {
                // Find or create character
                var character = household.Characters
                    .FirstOrDefault(c => c.ExternalCharacterId == tonieDto.Id);

                if (character == null)
                {
                    character = new Character
                    {
                        Type = CharacterType.Creative,
                        Name = tonieDto.Name,
                        ExternalCharacterId = tonieDto.Id,
                        HouseholdId = household.Id,
                        Created = DateTimeOffset.UtcNow
                    };
                    household.Characters.Add(character);
                }

                UpdateCharacterFromDto(character, tonieDto);
                character.LastSyncedAt = DateTimeOffset.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synced {HouseholdCount} households for user {UserId}", householdsDto.Count, userId);
    }

    /// <summary>
    /// Refresh a specific Tonie from API
    /// </summary>
    private async Task RefreshTonieFromApiAsync(
        string userId, 
        string householdId, 
        string tonieId, 
        CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var credentials = await GetUserCredentialsAsync(userId);

        var tonieDto = await _boxieCloudClient.GetCreativeTonieDetailsAsync(
            credentials.Username,
            credentials.Password,
            householdId,
            tonieId,
            ct);

        var character = await dbContext.Characters
            .Include(c => c.CustomImage)
            .FirstOrDefaultAsync(c => c.ExternalCharacterId == tonieId, ct);

        if (character != null)
        {
            UpdateCharacterFromDto(character, tonieDto);
            character.LastSyncedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Update Character entity from CreativeTonieDto
    /// </summary>
    private void UpdateCharacterFromDto(Character character, CreativeTonieDto dto)
    {
        character.Name = dto.Name;
        character.ImageUrl = dto.ImageUrl;
        character.SecondsPresent = dto.SecondsPresent;
        character.SecondsRemaining = dto.SecondsRemaining;
        character.ChaptersPresent = dto.ChaptersPresent;
        character.ChaptersRemaining = dto.ChaptersRemaining;
        character.Transcoding = dto.Transcoding;
        character.Live = dto.Live;
        character.Private = dto.Private;
        character.ChaptersJson = dto.Chapters != null 
            ? JsonSerializer.Serialize(dto.Chapters) 
            : null;
    }

    /// <summary>
    /// Convert Character entity to CreativeTonieDto
    /// </summary>
    private CreativeTonieDto CharacterToDto(Character character)
    {
        var dto = new CreativeTonieDto
        {
            Id = character.ExternalCharacterId ?? string.Empty,
            HouseholdId = character.Household?.ExternalId ?? string.Empty,
            Name = character.Name,
            ImageUrl = character.DisplayImageUrl, // Use DisplayImageUrl to show custom image if available
            SecondsPresent = character.SecondsPresent ?? 0,
            SecondsRemaining = character.SecondsRemaining ?? 0,
            ChaptersPresent = character.ChaptersPresent ?? 0,
            ChaptersRemaining = character.ChaptersRemaining ?? 0,
            Transcoding = character.Transcoding ?? false,
            Live = character.Live ?? false,
            Private = character.Private ?? false
        };

        // Deserialize chapters if available
        if (!string.IsNullOrEmpty(character.ChaptersJson))
        {
            try
            {
                dto.Chapters = JsonSerializer.Deserialize<List<ChapterDto>>(character.ChaptersJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize chapters for Character {CharacterId}", character.Id);
                dto.Chapters = new List<ChapterDto>();
            }
        }

        return dto;
    }

    public async Task DeleteAccountDataAsync(int credentialId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        await DeleteAccountDataAsync(credentialId, dbContext, ct);
    }

    /// <summary>
    /// Internal method to delete account data with provided context (for testing)
    /// </summary>
    private async Task DeleteAccountDataAsync(int credentialId, ApplicationDbContext dbContext, CancellationToken ct = default)
    {
        // Get the credential to verify it exists and get the user
        var credential = await dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId, ct);

        if (credential == null)
        {
            _logger.LogWarning("Attempted to delete data for non-existent credential {CredentialId}", credentialId);
            return;
        }

        var userId = credential.UserId;
        _logger.LogInformation("Deleting synced Tonie data for credential {CredentialId} (user {UserId})", credentialId, userId);

        // Get all households that were synced from this account
        // We identify them by checking if they have an ExternalId (from Tonie Cloud)
        // and belong to the user (through HouseholdMembers)
        var households = await dbContext.Households
            .Include(h => h.Characters.Where(c => c.Type == CharacterType.Creative))
            .Include(h => h.Members)
            .Where(h => h.ExternalId != null && h.Members.Any(m => m.UserId == userId))
            .ToListAsync(ct);

        int deletedHouseholds = 0;
        int deletedCharacters = 0;

        foreach (var household in households)
        {
            // Count creative tonies (characters) before deletion
            var creativeTonieCount = household.Characters.Count(c => c.Type == CharacterType.Creative);
            deletedCharacters += creativeTonieCount;

            // Delete all creative tonie characters from this household
            var creativeTonies = household.Characters
                .Where(c => c.Type == CharacterType.Creative)
                .ToList();

            dbContext.Characters.RemoveRange(creativeTonies);

            // Delete the household itself (synced from Tonie Cloud)
            dbContext.Households.Remove(household);
            deletedHouseholds++;
        }

        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted {HouseholdCount} household(s) and {CharacterCount} creative tonie(s) for credential {CredentialId}",
            deletedHouseholds, deletedCharacters, credentialId);
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

        credential.LastAuthenticated = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

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
