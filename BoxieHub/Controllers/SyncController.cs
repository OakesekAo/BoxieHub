using BoxieHub.Services.Sync;
using Microsoft.AspNetCore.Mvc;

namespace BoxieHub.Controllers;

/// <summary>
/// API endpoints for managing sync jobs and applying assignments to devices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncJobService _syncJobService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncJobService syncJobService, ILogger<SyncController> logger)
    {
        _syncJobService = syncJobService ?? throw new ArgumentNullException(nameof(syncJobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Applies a content assignment to a device immediately.
    /// Creates a sync job that pushes the assigned content to the device via the Python adapter.
    /// </summary>
    /// <param name="deviceId">The device ID to sync to.</param>
    /// <param name="assignmentId">The content assignment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created sync job with status.</returns>
    [HttpPost("{deviceId}/apply/{assignmentId}")]
    public async Task<IActionResult> ApplyAsync(int deviceId, int assignmentId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value ?? "system";
            var job = await _syncJobService.ExecuteSyncAsync(deviceId, assignmentId, userId, cancellationToken);
            return Ok(job);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Apply request failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply request failed with unexpected error");
            return StatusCode(500, new { error = "Sync operation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the status of a specific sync job.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync job details, or 404 if not found.</returns>
    [HttpGet("jobs/{jobId}")]
    public async Task<IActionResult> GetJobAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await _syncJobService.GetJobAsync(jobId, cancellationToken);
        if (job == null)
            return NotFound(new { error = "Sync job not found" });

        return Ok(job);
    }

    /// <summary>
    /// Gets all sync jobs for a household.
    /// </summary>
    /// <param name="householdId">The household ID.</param>
    /// <param name="limit">Maximum number of results (optional, default 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sync jobs ordered by most recent first.</returns>
    [HttpGet("households/{householdId}/jobs")]
    public async Task<IActionResult> GetJobsAsync(int householdId, [FromQuery] int? limit = null, CancellationToken cancellationToken = default)
    {
        var jobs = await _syncJobService.GetJobsAsync(householdId, limit ?? 50, cancellationToken);
        return Ok(jobs);
    }
}
