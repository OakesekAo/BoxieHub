namespace BoxieHub.Services.BoxieCloud;

/// <summary>
/// S3-compatible storage service implementation
/// Works with any S3-compatible API (AWS S3, Digital Ocean Spaces, Railway S3, etc.)
/// Uses AWS Signature V4 which is standard across all S3-compatible services
/// </summary>
public class S3StorageService : IS3StorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<S3StorageService> _logger;
    
    public S3StorageService(HttpClient httpClient, ILogger<S3StorageService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Upload file to any S3-compatible storage using pre-signed POST
    /// </summary>
    public async Task<string> UploadFileAsync(
        string uploadUrl,
        Dictionary<string, string> fields,
        string fileId,
        Stream fileStream,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(uploadUrl))
            throw new ArgumentException("Upload URL is required", nameof(uploadUrl));
        if (fields == null || fields.Count == 0)
            throw new ArgumentException("Signed fields are required", nameof(fields));
        if (string.IsNullOrEmpty(fileId))
            throw new ArgumentException("File ID is required", nameof(fileId));
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));
        
        try
        {
            _logger.LogInformation("Uploading file {FileId} to S3-compatible storage at {Url}", 
                fileId, uploadUrl);
            
            // Create multipart form data with signed fields
            using var content = new MultipartFormDataContent();
            
            // Add all signed fields first (order matters for some S3 implementations)
            foreach (var field in fields.OrderBy(f => f.Key))
            {
                content.Add(new StringContent(field.Value), field.Key);
                _logger.LogDebug("Added field: {Key}", field.Key);
            }
            
            // Add file content last
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", fileId);
            
            // Upload to S3-compatible endpoint
            var response = await _httpClient.PostAsync(uploadUrl, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("S3 upload failed. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                
                throw new InvalidOperationException(
                    $"S3 upload failed with status {response.StatusCode}: {errorContent}");
            }
            
            _logger.LogInformation("Successfully uploaded file {FileId} to S3-compatible storage", fileId);
            return fileId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error uploading to S3-compatible storage");
            throw new InvalidOperationException("Failed to upload file to S3-compatible storage", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading to S3-compatible storage");
            throw;
        }
    }
}
