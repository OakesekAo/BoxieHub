namespace BoxieHub.Services.PythonAdapter.Dtos;

/// <summary>
/// Response from Python adapter health check endpoint.
/// </summary>
public class HealthResponseDto
{
    /// <summary>
    /// Current status of the adapter service.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Version information of the adapter.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Timestamp when the health check was performed.
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the adapter is authenticated with the Tonie API.
    /// </summary>
    public bool Authenticated { get; set; }
}
