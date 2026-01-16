using BoxieHub.Models;

namespace BoxieHub.Services.Tonie;

/// <summary>
/// Service for managing TonieUploadJob database operations
/// </summary>
public interface ITonieUploadJobService
{
    /// <summary>
    /// Create a new upload job (library item → Tonie)
    /// </summary>
    Task<TonieUploadJob> CreateUploadJobAsync(
        string userId,
        int libraryItemId,
        string householdId,
        string tonieId,
        string? customChapterTitle = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get upload job by ID
    /// </summary>
    Task<TonieUploadJob?> GetJobByIdAsync(int jobId, CancellationToken ct = default);
    
    /// <summary>
    /// Get all upload jobs for a user (with pagination)
    /// </summary>
    Task<List<TonieUploadJob>> GetUserJobsAsync(
        string userId,
        int take = 50,
        int skip = 0,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get total count of user's upload jobs
    /// </summary>
    Task<int> GetUserJobCountAsync(
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get active (pending/in-progress) jobs for a user
    /// </summary>
    Task<List<TonieUploadJob>> GetActiveJobsAsync(
        string userId,
        CancellationToken ct = default);
    
    /// <summary>
    /// Update job status and progress
    /// </summary>
    Task UpdateJobStatusAsync(
        int jobId,
        TonieUploadJobStatus status,
        int? progressPercentage = null,
        string? statusMessage = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Mark job as completed
    /// </summary>
    Task CompleteJobAsync(
        int jobId,
        string? syncResultJson = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Mark job as failed
    /// </summary>
    Task FailJobAsync(
        int jobId,
        string errorMessage,
        CancellationToken ct = default);
    
    /// <summary>
    /// Cancel a pending/in-progress job
    /// </summary>
    Task CancelJobAsync(int jobId, CancellationToken ct = default);
}
