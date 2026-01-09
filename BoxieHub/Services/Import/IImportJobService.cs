using BoxieHub.Models;

namespace BoxieHub.Services.Import;

/// <summary>
/// Service for managing import jobs (CRUD operations)
/// </summary>
public interface IImportJobService
{
    /// <summary>
    /// Create a new YouTube import job (validates and creates record)
    /// </summary>
    Task<ImportJob> CreateYouTubeImportJobAsync(
        string userId, 
        string youtubeUrl, 
        string? customTitle = null,
        string? customDescription = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Create multiple YouTube import jobs for batch processing (e.g., playlists)
    /// </summary>
    Task<List<ImportJob>> CreateYouTubeBatchImportAsync(
        string userId,
        List<string> youtubeUrls,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get job status by ID
    /// </summary>
    Task<ImportJob?> GetJobAsync(int jobId, string userId, CancellationToken ct = default);
    
    /// <summary>
    /// Get user's recent import jobs
    /// </summary>
    Task<List<ImportJob>> GetUserJobsAsync(
        string userId, 
        int take = 20, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Cancel a pending or running import job
    /// </summary>
    Task<bool> CancelJobAsync(int jobId, string userId, CancellationToken ct = default);
}
