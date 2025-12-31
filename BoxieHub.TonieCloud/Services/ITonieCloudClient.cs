using BoxieHub.TonieCloud.Models;

namespace BoxieHub.TonieCloud.Services;

/// <summary>
/// Client for Tonie Cloud API
/// Provides methods to interact with Tonie Cloud services
/// </summary>
public interface ITonieCloudClient
{
    /// <summary>
    /// Get all households for authenticated user
    /// </summary>
    Task<List<HouseholdDto>> GetHouseholdsAsync(
        string username, 
        string password, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get all Creative Tonies for a specific household
    /// </summary>
    Task<List<CreativeTonieDto>> GetCreativeToniesByHouseholdAsync(
        string username, 
        string password, 
        string householdId, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get detailed information for a specific Creative Tonie including all chapters
    /// </summary>
    Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get S3-compatible upload token from Tonie Cloud
    /// Works with AWS S3, Digital Ocean Spaces, Railway S3, etc.
    /// </summary>
    Task<S3UploadTokenDto> GetUploadTokenAsync(
        string username, 
        string password, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Update Creative Tonie with new chapters
    /// Uses PATCH to update only specified fields
    /// </summary>
    Task<CreativeTonieDto> PatchCreativeTonieAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        string name, 
        List<ChapterDto> chapters, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Upload audio file to Creative Tonie (simplified method)
    /// Handles the full S3 upload workflow internally
    /// </summary>
    Task<SyncResultDto> SyncAudioAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        Stream audioStream, 
        string title, 
        CancellationToken ct = default);
}
