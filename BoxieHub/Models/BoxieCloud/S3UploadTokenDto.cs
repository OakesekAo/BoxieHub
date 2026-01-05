using System.Text.Json.Serialization;

namespace BoxieHub.Models.BoxieCloud;

/// <summary>
/// S3-compatible upload token response from Tonie Cloud
/// Works with AWS S3, Digital Ocean Spaces, Railway S3, etc.
/// </summary>
public class S3UploadTokenDto
{
    /// <summary>
    /// Unique file ID to reference this upload
    /// </summary>
    [JsonPropertyName("fileId")]
    public required string FileId { get; set; }
    
    /// <summary>
    /// S3 upload request details
    /// </summary>
    [JsonPropertyName("request")]
    public required S3UploadRequestDto Request { get; set; }
}

/// <summary>
/// S3 upload request details (vendor-agnostic)
/// </summary>
public class S3UploadRequestDto
{
    /// <summary>
    /// S3 endpoint URL (can be AWS, DO, Railway, etc.)
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
    
    /// <summary>
    /// S3 signed fields for upload
    /// </summary>
    [JsonPropertyName("fields")]
    public required S3UploadFieldsDto Fields { get; set; }
}

/// <summary>
/// S3 signed upload fields (AWS Signature V4 compatible)
/// Works with any S3-compatible service
/// </summary>
public class S3UploadFieldsDto
{
    [JsonPropertyName("key")]
    public required string Key { get; set; }
    
    [JsonPropertyName("x-amz-algorithm")]
    public required string AmazonAlgorithm { get; set; }
    
    [JsonPropertyName("x-amz-credential")]
    public required string AmazonCredential { get; set; }
    
    [JsonPropertyName("x-amz-date")]
    public required string AmazonDate { get; set; }
    
    [JsonPropertyName("policy")]
    public required string Policy { get; set; }
    
    [JsonPropertyName("x-amz-signature")]
    public required string AmazonSignature { get; set; }
    
    [JsonPropertyName("x-amz-security-token")]
    public string? AmazonSecurityToken { get; set; }
}
