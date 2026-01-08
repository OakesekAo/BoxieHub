namespace BoxieHub.Services.Storage;

/// <summary>
/// Abstraction for file storage - supports database, S3, Dropbox, Google Drive, etc.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a file to storage
    /// </summary>
    /// <param name="stream">File stream</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME type</param>
    /// <param name="userId">User ID (for organizing files)</param>
    /// <param name="storageAccountId">Optional: specific storage account to use</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Storage path/key for the uploaded file</returns>
    Task<string> UploadFileAsync(
        Stream stream,
        string fileName,
        string contentType,
        string userId,
        int? storageAccountId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Download a file from storage
    /// </summary>
    /// <param name="storagePath">Path/key returned from UploadFileAsync</param>
    /// <param name="storageAccountId">Optional: specific storage account to use</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>File stream</returns>
    Task<Stream> DownloadFileAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Delete a file from storage
    /// </summary>
    /// <param name="storagePath">Path/key returned from UploadFileAsync</param>
    /// <param name="storageAccountId">Optional: specific storage account to use</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteFileAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Check if a file exists in storage
    /// </summary>
    /// <param name="storagePath">Path/key returned from UploadFileAsync</param>
    /// <param name="storageAccountId">Optional: specific storage account to use</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> FileExistsAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get file metadata (size, last modified, etc.)
    /// </summary>
    /// <param name="storagePath">Path/key returned from UploadFileAsync</param>
    /// <param name="storageAccountId">Optional: specific storage account to use</param>
    /// <param name="ct">Cancellation token</param>
    Task<FileStorageMetadata?> GetFileMetadataAsync(
        string storagePath,
        int? storageAccountId = null,
        CancellationToken ct = default);
}

/// <summary>
/// File metadata returned from storage providers
/// </summary>
public class FileStorageMetadata
{
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
