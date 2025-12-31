using BoxieHub.Services.PythonAdapter.Dtos;

namespace BoxieHub.Services.PythonAdapter;

/// <summary>
/// Interface for communicating with the Python Tonie adapter service.
/// Abstracts all HTTP communication with the external Python adapter.
/// </summary>
public interface IPythonAdapterClient
{
    /// <summary>
    /// Checks the health status of the Python adapter service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status information.</returns>
    /// <exception cref="HttpRequestException">If the adapter is unreachable.</exception>
    Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a sync request to the Python adapter to upload audio to a Creative Tonie.
    /// </summary>
    /// <param name="request">The sync request containing device ID and track list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response indicating success or failure of the sync operation.</returns>
    /// <exception cref="HttpRequestException">If the adapter is unreachable.</exception>
    /// <exception cref="InvalidOperationException">If the request is malformed.</exception>
    Task<SyncResponseDto> SyncAsync(SyncRequestDto request, CancellationToken cancellationToken = default);
}
