using Amazon.S3;
using Amazon.S3.Model;
using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BoxieHub.Services.Storage;

/// <summary>
/// One-time migration tool to move files from Database storage to S3
/// </summary>
public interface IDatabaseToS3Migrator
{
    Task<MigrationResult> MigrateAllFilesAsync(CancellationToken ct = default);
    Task<MigrationSummary> GetMigrationSummaryAsync(CancellationToken ct = default);
}

public class DatabaseToS3Migrator : IDatabaseToS3Migrator
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseToS3Migrator> _logger;
    
    private readonly string _bucketName;

    public DatabaseToS3Migrator(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<DatabaseToS3Migrator> logger)
    {
        _dbContextFactory = dbContextFactory;
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
        
        _bucketName = _configuration["S3Storage:BucketName"] ?? "boxiehub-media";
    }

    public async Task<MigrationSummary> GetMigrationSummaryAsync(CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var summary = new MigrationSummary();
        
        var filesByProvider = await dbContext.FileUploads
            .GroupBy(f => f.Provider)
            .Select(g => new { Provider = g.Key, Count = g.Count(), TotalBytes = g.Sum(f => f.FileSizeBytes) })
            .ToListAsync(ct);
        
        foreach (var group in filesByProvider)
        {
            if (group.Provider == StorageProvider.Database)
            {
                summary.FilesToMigrate = group.Count;
                summary.TotalBytesToMigrate = group.TotalBytes;
            }
            else
            {
                summary.FilesAlreadyInS3 = group.Count;
            }
        }
        
        summary.InconsistentFiles = await dbContext.FileUploads
            .Where(f => f.Provider != StorageProvider.Database && f.Data != null)
            .CountAsync(ct);
        
        return summary;
    }

    public async Task<MigrationResult> MigrateAllFilesAsync(CancellationToken ct = default)
    {
        var result = new MigrationResult { StartedAt = DateTimeOffset.UtcNow };
        
        try
        {
            _logger.LogInformation("Checking S3 connectivity");
            await CheckS3ConnectivityAsync(ct);
            result.S3Available = true;
            
            _logger.LogInformation("Ensuring bucket exists");
            await EnsureBucketExistsAsync(ct);
            result.BucketExists = true;
            
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            
            var filesToMigrate = await dbContext.FileUploads
                .Where(f => f.Provider == StorageProvider.Database && f.Data != null)
                .OrderBy(f => f.Created)
                .ToListAsync(ct);
            
            result.TotalFiles = filesToMigrate.Count;
            _logger.LogInformation("Found {Count} files to migrate", result.TotalFiles);
            
            if (result.TotalFiles == 0)
            {
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
            
            for (int i = 0; i < filesToMigrate.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                
                var file = filesToMigrate[i];
                
                try
                {
                    await MigrateFileAsync(file, dbContext, ct);
                    result.SuccessfulFiles++;
                    _logger.LogInformation("Migrated {Current}/{Total}: {FileName}", 
                        i + 1, result.TotalFiles, file.FileName);
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    result.Errors.Add(new MigrationError
                    {
                        FileId = file.Id,
                        FileName = file.FileName ?? "unknown",
                        Error = ex.Message
                    });
                    _logger.LogError(ex, "Failed to migrate {FileName}", file.FileName);
                }
                
                if ((i + 1) % 10 == 0)
                {
                    await dbContext.SaveChangesAsync(ct);
                }
            }
            
            await dbContext.SaveChangesAsync(ct);
            result.CompletedAt = DateTimeOffset.UtcNow;
            
            return result;
        }
        catch (Exception ex)
        {
            result.CompletedAt = DateTimeOffset.UtcNow;
            _logger.LogError(ex, "Migration failed");
            throw;
        }
    }

    private async Task MigrateFileAsync(FileUpload file, ApplicationDbContext dbContext, CancellationToken ct)
    {
        if (file.Data == null || file.Data.Length == 0)
            throw new InvalidOperationException($"File {file.Id} has no data");
        
        var userId = await GetUserIdForFileAsync(file.Id, dbContext, ct);
        var s3Key = $"users/{userId}/{file.Id}/{file.FileName ?? "file"}";
        
        using var stream = new MemoryStream(file.Data);
        
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            InputStream = stream,
            ContentType = file.ContentType ?? "application/octet-stream"
        };
        
        var response = await _s3Client.PutObjectAsync(putRequest, ct);
        
        if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException($"S3 upload failed with status {response.HttpStatusCode}");
        
        file.Provider = StorageProvider.S3Railway;
        file.StoragePath = s3Key;
        file.Data = null;
        file.UserStorageAccountId = null;
    }

    private async Task<string> GetUserIdForFileAsync(Guid fileId, ApplicationDbContext dbContext, CancellationToken ct)
    {
        var userId = await dbContext.MediaLibraryItems
            .Where(m => m.FileUploadId == fileId)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);
        
        if (!string.IsNullOrEmpty(userId)) return userId;
        
        userId = await dbContext.Characters
            .Where(c => c.CustomImageId == fileId)
            .SelectMany(c => c.Household!.Members)
            .Select(m => m.UserId)
            .FirstOrDefaultAsync(ct);
        
        if (!string.IsNullOrEmpty(userId)) return userId;
        
        return "orphaned";
    }

    private async Task CheckS3ConnectivityAsync(CancellationToken ct)
    {
        try
        {
            await _s3Client.ListBucketsAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Cannot connect to S3. Ensure MinIO is running.", ex);
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        try
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            
            if (!bucketExists)
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, ct);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to ensure bucket '{_bucketName}' exists", ex);
        }
    }
}

public class MigrationSummary
{
    public int FilesToMigrate { get; set; }
    public long TotalBytesToMigrate { get; set; }
    public int FilesAlreadyInS3 { get; set; }
    public int InconsistentFiles { get; set; }
    
    public string FormattedSize
    {
        get
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = TotalBytesToMigrate;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

public class MigrationResult
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    
    public bool S3Available { get; set; }
    public bool BucketExists { get; set; }
    
    public int TotalFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    
    public List<MigrationError> Errors { get; set; } = new List<MigrationError>();
    
    public TimeSpan Duration => (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt;
    
    public bool IsComplete => CompletedAt.HasValue;
    public bool IsSuccess => IsComplete && FailedFiles == 0;
    
    public string Summary => $"Migrated {SuccessfulFiles}/{TotalFiles} files successfully ({FailedFiles} failed) in {Duration.TotalSeconds:F1}s";
}

public class MigrationError
{
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
