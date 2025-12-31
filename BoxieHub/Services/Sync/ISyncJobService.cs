using BoxieHub.Models;

namespace BoxieHub.Services.Sync;

/// <summary>
/// Service for orchestrating sync operations between the C# backend and Creative Tonies
/// via the Python adapter.
/// 
/// Responsibilities:
/// - Create SyncJob records in the database
/// - Call the Python adapter to perform the sync
/// - Update job status based on adapter response
/// - Handle error cases gracefully
/// </summary>
public interface ISyncJobService
{
    /// <summary>
    /// Creates and executes a sync job for a device with the specified content assignment.
    /// </summary>
    /// <param name="deviceId">The device to sync to.</param>
    /// <param name="assignmentId">The content assignment (Device ? Content mapping).</param>
    /// <param name="requestedBy">User ID who requested the sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created and executed SyncJob record.</returns>
    /// <exception cref="ArgumentException">If device or assignment not found.</exception>
    Task<SyncJob> ExecuteSyncAsync(int deviceId, int assignmentId, string requestedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a sync job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync job details, or null if not found.</returns>
    Task<SyncJob?> GetJobAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sync jobs for a household.
    /// </summary>
    /// <param name="householdId">The household ID.</param>
    /// <param name="limit">Maximum number of jobs to return (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sync jobs ordered by most recent first.</returns>
    Task<IEnumerable<SyncJob>> GetJobsAsync(int householdId, int? limit = null, CancellationToken cancellationToken = default);
}
