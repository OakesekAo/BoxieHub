namespace BoxieHub.Services.PythonAdapter.Dtos;

/// <summary>
/// Response from Python adapter after attempting to sync audio to a Creative Tonie.
/// </summary>
public class SyncResponseDto
{
    /// <summary>
    /// Whether the sync operation was successful.
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// Human-readable message describing the operation result.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Optional error details if the operation failed.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Number of tracks successfully synced.
    /// </summary>
    public int TracksProcessed { get; set; }
}
