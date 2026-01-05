namespace BoxieHub.Services.BoxieCloud;

/// <summary>
/// Interface for S3-compatible file storage operations
/// Supports AWS S3, Digital Ocean Spaces, Railway S3, and any S3-compatible service
/// </summary>
public interface IS3StorageService
{
    /// <summary>
    /// Upload a file to S3-compatible storage using signed URL
    /// </summary>
    /// <param name="uploadUrl">S3 endpoint URL</param>
    /// <param name="fields">Signed upload fields (x-amz-* headers)</param>
    /// <param name="fileId">Unique file identifier</param>
    /// <param name="fileStream">File content to upload</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The fileId on successful upload</returns>
    Task<string> UploadFileAsync(
        string uploadUrl,
        Dictionary<string, string> fields,
        string fileId,
        Stream fileStream,
        CancellationToken ct = default);
}
