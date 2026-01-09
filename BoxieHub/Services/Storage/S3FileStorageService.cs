using Amazon.S3;
using Amazon.S3.Model;
using BoxieHub.Models;

namespace BoxieHub.Services.Storage;

/// <summary>
/// S3-compatible file storage service for MinIO (dev) and Railway S3 (prod)
/// Uses AWS SDK for S3 which is compatible with all S3-compatible services
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly string _bucketName;
    
    public S3FileStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = configuration["S3Storage:BucketName"] ?? "boxiehub-media";
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
        
        // Generate unique storage path: users/{userId}/{guid}/{filename}
        var fileId = Guid.NewGuid();
        var storagePath = $"users/{userId}/{fileId}/{fileName}";
        
        try
        {
            _logger.LogInformation("Uploading file to S3: {StoragePath} ({Size} bytes)", 
                storagePath, stream.Length);
            
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath,
                InputStream = stream,
                ContentType = contentType,
                Metadata =
                {
                    ["original-filename"] = fileName,
                    ["user-id"] = userId,
                    ["upload-date"] = DateTimeOffset.UtcNow.ToString("O")
                }
            };
            
            var response = await _s3Client.PutObjectAsync(request, ct);
            
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    $"S3 upload failed with status {response.HttpStatusCode}");
            }
            
            _logger.LogInformation("Successfully uploaded file to S3: {StoragePath}", storagePath);
            return storagePath;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading file: {StoragePath}", storagePath);
            throw new InvalidOperationException("Failed to upload file to S3 storage", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file: {StoragePath}", storagePath);
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
        
        try
        {
            _logger.LogInformation("Downloading file from S3: {StoragePath}", storagePath);
            
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };
            
            var response = await _s3Client.GetObjectAsync(request, ct);
            
            // Copy to memory stream so we can dispose the S3 response
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;
            
            _logger.LogInformation("Successfully downloaded file from S3: {StoragePath} ({Size} bytes)", 
                storagePath, memoryStream.Length);
            
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found in S3: {StoragePath}", storagePath);
            throw new FileNotFoundException($"File not found in S3: {storagePath}", storagePath);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error downloading file: {StoragePath}", storagePath);
            throw new InvalidOperationException("Failed to download file from S3 storage", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading file: {StoragePath}", storagePath);
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
        
        try
        {
            _logger.LogInformation("Deleting file from S3: {StoragePath}", storagePath);
            
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };
            
            await _s3Client.DeleteObjectAsync(request, ct);
            
            _logger.LogInformation("Successfully deleted file from S3: {StoragePath}", storagePath);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error deleting file: {StoragePath}", storagePath);
            throw new InvalidOperationException("Failed to delete file from S3 storage", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting file: {StoragePath}", storagePath);
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
        
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };
            
            await _s3Client.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
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
        
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };
            
            var response = await _s3Client.GetObjectMetadataAsync(request, ct);
            
            return new FileStorageMetadata
            {
                StoragePath = storagePath,
                SizeBytes = response.ContentLength,
                LastModified = response.LastModified,
                ContentType = response.Headers.ContentType,
                Metadata = response.Metadata.Keys.ToDictionary(
                    k => k,
                    k => response.Metadata[k]
                )
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found in S3: {StoragePath}", storagePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file metadata: {StoragePath}", storagePath);
            throw;
        }
    }
}
