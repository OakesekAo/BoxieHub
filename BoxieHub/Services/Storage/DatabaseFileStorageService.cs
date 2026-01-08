using BoxieHub.Data;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Storage;

/// <summary>
/// Fallback storage service that stores files in PostgreSQL database
/// Only recommended for small files (images, thumbnails)
/// Large audio files should use S3/Dropbox/GDrive
/// </summary>
public class DatabaseFileStorageService : IFileStorageService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseFileStorageService> _logger;
    
    public DatabaseFileStorageService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<DatabaseFileStorageService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<string> UploadFileAsync(
        Stream stream,
        string fileName,
        string contentType,
        string userId,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        if (stream == null || stream.Length == 0)
            throw new ArgumentException("File stream is empty", nameof(stream));
        
        // Warn if file is large
        if (stream.Length > 5 * 1024 * 1024) // 5MB
        {
            _logger.LogWarning("Storing large file ({Size} bytes) in database. Consider using S3 storage.", 
                stream.Length);
        }
        
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            
            // Read stream into byte array
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct);
            var fileData = memoryStream.ToArray();
            
            var fileUpload = new BoxieHub.Models.FileUpload
            {
                Id = Guid.NewGuid(),
                Data = fileData,
                ContentType = contentType,
                FileName = fileName,
                FileSizeBytes = fileData.Length,
                FileCategory = "Database",
                Created = DateTimeOffset.UtcNow
            };
            
            dbContext.FileUploads.Add(fileUpload);
            await dbContext.SaveChangesAsync(ct);
            
            _logger.LogInformation("Stored file in database: {FileId} ({Size} bytes)", 
                fileUpload.Id, fileData.Length);
            
            // Return the GUID as storage path
            return fileUpload.Id.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing file in database");
            throw;
        }
    }
    
    public async Task<Stream> DownloadFileAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storagePath))
            throw new ArgumentException("Storage path is required", nameof(storagePath));
        
        if (!Guid.TryParse(storagePath, out var fileId))
            throw new ArgumentException("Invalid storage path format", nameof(storagePath));
        
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            
            var fileUpload = await dbContext.FileUploads.FindAsync(new object[] { fileId }, ct);
            
            if (fileUpload == null || fileUpload.Data == null)
                throw new FileNotFoundException($"File not found in database: {storagePath}", storagePath);
            
            _logger.LogInformation("Retrieved file from database: {FileId} ({Size} bytes)", 
                fileId, fileUpload.Data.Length);
            
            return new MemoryStream(fileUpload.Data);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Error retrieving file from database: {StoragePath}", storagePath);
            throw;
        }
    }
    
    public async Task DeleteFileAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storagePath))
            throw new ArgumentException("Storage path is required", nameof(storagePath));
        
        if (!Guid.TryParse(storagePath, out var fileId))
            throw new ArgumentException("Invalid storage path format", nameof(storagePath));
        
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            
            var fileUpload = await dbContext.FileUploads.FindAsync(new object[] { fileId }, ct);
            
            if (fileUpload != null)
            {
                dbContext.FileUploads.Remove(fileUpload);
                await dbContext.SaveChangesAsync(ct);
                
                _logger.LogInformation("Deleted file from database: {FileId}", fileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from database: {StoragePath}", storagePath);
            throw;
        }
    }
    
    public async Task<bool> FileExistsAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storagePath))
            throw new ArgumentException("Storage path is required", nameof(storagePath));
        
        if (!Guid.TryParse(storagePath, out var fileId))
            return false;
        
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            return await dbContext.FileUploads.AnyAsync(f => f.Id == fileId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists: {StoragePath}", storagePath);
            throw;
        }
    }
    
    public async Task<FileStorageMetadata?> GetFileMetadataAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(storagePath))
            throw new ArgumentException("Storage path is required", nameof(storagePath));
        
        if (!Guid.TryParse(storagePath, out var fileId))
            return null;
        
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            
            var fileUpload = await dbContext.FileUploads
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId, ct);
            
            if (fileUpload == null)
                return null;
            
            return new FileStorageMetadata
            {
                StoragePath = storagePath,
                SizeBytes = fileUpload.FileSizeBytes,
                LastModified = fileUpload.Created,
                ContentType = fileUpload.ContentType,
                Metadata = new Dictionary<string, string>
                {
                    ["FileName"] = fileUpload.FileName ?? "",
                    ["FileCategory"] = fileUpload.FileCategory
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file metadata: {StoragePath}", storagePath);
            throw;
        }
    }
}
