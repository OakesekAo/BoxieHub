using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services;

/// <summary>
/// Service for managing user's media library
/// Allows storing and reusing audio files across multiple Tonies
/// </summary>
public interface IMediaLibraryService
{
    // CRUD Operations
    Task<List<MediaLibraryItem>> GetUserLibraryAsync(string userId, CancellationToken ct = default);
    Task<MediaLibraryItem?> GetLibraryItemAsync(int id, string userId, CancellationToken ct = default);
    Task<MediaLibraryItem> AddToLibraryAsync(string userId, Stream audioStream, MediaLibraryItemDto dto, StorageProvider? provider = null, int? storageAccountId = null, CancellationToken ct = default);
    Task<bool> UpdateLibraryItemAsync(int id, string userId, MediaLibraryItemDto dto, CancellationToken ct = default);
    Task<bool> DeleteLibraryItemAsync(int id, string userId, CancellationToken ct = default);
    
    // Search & Filter
    Task<List<MediaLibraryItem>> SearchLibraryAsync(string userId, LibrarySearchDto search, CancellationToken ct = default);
    
    // Usage Tracking
    Task TrackUsageAsync(int mediaLibraryItemId, string householdId, string tonieId, string chapterId, string? tonieName = null, string? chapterTitle = null, CancellationToken ct = default);
    Task<List<MediaLibraryUsage>> GetItemUsageAsync(int mediaLibraryItemId, CancellationToken ct = default);
    
    // Statistics
    Task<LibraryStatsDto> GetLibraryStatsAsync(string userId, CancellationToken ct = default);
}

public class MediaLibraryService : IMediaLibraryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStoragePreferenceService _storagePreferenceService;
    private readonly ILogger<MediaLibraryService> _logger;

    public MediaLibraryService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IFileStorageService fileStorageService,
        IStoragePreferenceService storagePreferenceService,
        ILogger<MediaLibraryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorageService = fileStorageService;
        _storagePreferenceService = storagePreferenceService;
        _logger = logger;
    }

    public async Task<List<MediaLibraryItem>> GetUserLibraryAsync(string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.MediaLibraryItems
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Created)
            .ToListAsync(ct);
    }

    public async Task<MediaLibraryItem?> GetLibraryItemAsync(int id, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.MediaLibraryItems
            .Include(m => m.FileUpload)
            .Include(m => m.Usages)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
    }

    public async Task<MediaLibraryItem> AddToLibraryAsync(
        string userId, 
        Stream audioStream, 
        MediaLibraryItemDto dto, 
        StorageProvider? provider = null,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            // Validate file size (max 200MB)
            const long maxSizeBytes = 200 * 1024 * 1024;
            if (audioStream.Length > maxSizeBytes)
            {
                throw new InvalidOperationException($"Audio file too large. Maximum size is 200MB.");
            }

            // Determine storage provider
            if (!provider.HasValue)
            {
                provider = await _storagePreferenceService.GetDefaultProviderAsync(userId, ct);
            }

            string? storagePath = null;
            byte[]? fileData = null;

            // Handle storage based on provider
            if (provider == StorageProvider.Database)
            {
                // Legacy: Store in database
                _logger.LogInformation("Storing audio file '{FileName}' in database for user {UserId}", 
                    dto.OriginalFileName, userId);
                
                using var ms = new MemoryStream();
                await audioStream.CopyToAsync(ms, ct);
                fileData = ms.ToArray();
            }
            else
            {
                // Store in external storage (S3, Dropbox, GDrive)
                _logger.LogInformation("Uploading audio file '{FileName}' to {Provider} for user {UserId}", 
                    dto.OriginalFileName, provider.Value, userId);

                storagePath = await _fileStorageService.UploadFileAsync(
                    audioStream,
                    dto.OriginalFileName ?? "audio.mp3",
                    dto.ContentType,
                    userId,
                    storageAccountId,
                    ct);

                _logger.LogInformation("Successfully uploaded to {Provider}: {StoragePath}", 
                    provider.Value, storagePath);
            }

            // Create FileUpload record
            var fileUpload = new FileUpload
            {
                Id = Guid.NewGuid(),
                Data = fileData, // NULL for external storage
                ContentType = dto.ContentType,
                FileName = dto.OriginalFileName,
                FileCategory = "Audio",
                FileSizeBytes = dto.FileSizeBytes,
                Provider = provider.Value,
                StoragePath = storagePath,
                UserStorageAccountId = storageAccountId,
                Created = DateTimeOffset.UtcNow
            };

            dbContext.FileUploads.Add(fileUpload);

            // Create MediaLibraryItem
            var item = new MediaLibraryItem
            {
                UserId = userId,
                Title = dto.Title,
                Description = dto.Description,
                FileUploadId = fileUpload.Id,
                DurationSeconds = dto.DurationSeconds,
                FileSizeBytes = dto.FileSizeBytes,
                ContentType = dto.ContentType,
                OriginalFileName = dto.OriginalFileName,
                Tags = dto.Tags,
                Category = dto.Category,
                UseCount = 0,
                Created = DateTimeOffset.UtcNow
            };

            dbContext.MediaLibraryItems.Add(item);
            await dbContext.SaveChangesAsync(ct);

            // Update user's last used provider
            await _storagePreferenceService.UpdateLastUsedAsync(userId, provider.Value, storageAccountId, ct);

            _logger.LogInformation("Added media item '{Title}' to library for user {UserId} (Provider: {Provider})", 
                dto.Title, userId, provider.Value);

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding media item to library for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateLibraryItemAsync(
        int id, 
        string userId, 
        MediaLibraryItemDto dto, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var item = await dbContext.MediaLibraryItems
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

            if (item == null)
            {
                _logger.LogWarning("Media library item {Id} not found for user {UserId}", id, userId);
                return false;
            }

            // Update editable fields
            item.Title = dto.Title;
            item.Description = dto.Description;
            item.Category = dto.Category;
            item.Tags = dto.Tags;

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Updated media library item {Id} for user {UserId}", id, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media library item {Id} for user {UserId}", id, userId);
            throw;
        }
    }

    public async Task<bool> DeleteLibraryItemAsync(int id, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var item = await dbContext.MediaLibraryItems
                .Include(m => m.FileUpload)
                .Include(m => m.Usages)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

            if (item == null)
            {
                _logger.LogWarning("Media library item {Id} not found for user {UserId}", id, userId);
                return false;
            }

            // Remove usages
            dbContext.MediaLibraryUsages.RemoveRange(item.Usages);
            
            // Delete file from S3 storage
            if (item.FileUpload != null && !string.IsNullOrEmpty(item.FileUpload.StoragePath))
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(
                        item.FileUpload.StoragePath,
                        item.FileUpload.UserStorageAccountId,
                        ct);
                    _logger.LogInformation("Deleted file from S3: {StoragePath}", item.FileUpload.StoragePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file from S3: {StoragePath}", item.FileUpload.StoragePath);
                    // Continue with database deletion even if S3 delete fails
                }
                
                dbContext.FileUploads.Remove(item.FileUpload);
            }
            
            // Remove the library item
            dbContext.MediaLibraryItems.Remove(item);

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted media library item {Id} ({Title}) for user {UserId}", 
                id, item.Title, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media library item {Id} for user {UserId}", id, userId);
            throw;
        }
    }

    public async Task<List<MediaLibraryItem>> SearchLibraryAsync(
        string userId, 
        LibrarySearchDto search, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var query = dbContext.MediaLibraryItems
            .Where(m => m.UserId == userId)
            .AsQueryable();

        // Search by query (title, description, or filename)
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var lowerQuery = search.Query.ToLower();
            query = query.Where(m => 
                m.Title.ToLower().Contains(lowerQuery) ||
                (m.Description != null && m.Description.ToLower().Contains(lowerQuery)) ||
                (m.OriginalFileName != null && m.OriginalFileName.ToLower().Contains(lowerQuery)));
        }

        // Filter by category
        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            query = query.Where(m => m.Category == search.Category);
        }

        // Filter by tag
        if (!string.IsNullOrWhiteSpace(search.Tag))
        {
            query = query.Where(m => m.TagsJson != null && m.TagsJson.Contains(search.Tag));
        }

        // Sort
        query = search.SortBy.ToLower() switch
        {
            "name" => search.SortDescending 
                ? query.OrderByDescending(m => m.Title)
                : query.OrderBy(m => m.Title),
            "duration" => search.SortDescending 
                ? query.OrderByDescending(m => m.DurationSeconds)
                : query.OrderBy(m => m.DurationSeconds),
            "mostused" => search.SortDescending 
                ? query.OrderByDescending(m => m.UseCount)
                : query.OrderBy(m => m.UseCount),
            _ => search.SortDescending 
                ? query.OrderByDescending(m => m.Created)
                : query.OrderBy(m => m.Created)
        };

        // Pagination
        var results = await query
            .Skip(search.Skip)
            .Take(search.Take)
            .ToListAsync(ct);

        return results;
    }

    public async Task TrackUsageAsync(
        int mediaLibraryItemId, 
        string householdId, 
        string tonieId, 
        string chapterId, 
        string? tonieName = null, 
        string? chapterTitle = null, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            // Create usage record
            var usage = new MediaLibraryUsage
            {
                MediaLibraryItemId = mediaLibraryItemId,
                HouseholdId = householdId,
                TonieId = tonieId,
                TonieName = tonieName,
                ChapterId = chapterId,
                ChapterTitle = chapterTitle,
                UsedAt = DateTimeOffset.UtcNow
            };

            dbContext.MediaLibraryUsages.Add(usage);

            // Update item's use count and last used timestamp
            var item = await dbContext.MediaLibraryItems
                .FirstOrDefaultAsync(m => m.Id == mediaLibraryItemId, ct);

            if (item != null)
            {
                item.UseCount++;
                item.LastUsed = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Tracked usage of media library item {ItemId} on Tonie {TonieId}", 
                mediaLibraryItemId, tonieId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking usage for media library item {ItemId}", mediaLibraryItemId);
            throw;
        }
    }

    public async Task<List<MediaLibraryUsage>> GetItemUsageAsync(int mediaLibraryItemId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.MediaLibraryUsages
            .Where(u => u.MediaLibraryItemId == mediaLibraryItemId)
            .OrderByDescending(u => u.UsedAt)
            .ToListAsync(ct);
    }

    public async Task<LibraryStatsDto> GetLibraryStatsAsync(string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var items = await dbContext.MediaLibraryItems
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        var stats = new LibraryStatsDto
        {
            TotalItems = items.Count,
            TotalSizeBytes = items.Sum(m => m.FileSizeBytes),
            TotalDurationSeconds = items.Sum(m => m.DurationSeconds),
            TotalUses = items.Sum(m => m.UseCount),
            ItemsByCategory = items
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .GroupBy(m => m.Category!)
                .ToDictionary(g => g.Key, g => g.Count()),
            MostUsedItems = items
                .OrderByDescending(m => m.UseCount)
                .Take(5)
                .ToList(),
            RecentlyAdded = items
                .OrderByDescending(m => m.Created)
                .Take(5)
                .ToList()
        };

        return stats;
    }
}
