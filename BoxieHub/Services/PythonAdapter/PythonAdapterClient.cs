using System.Text.Json;
using BoxieHub.Services.PythonAdapter.Dtos;

namespace BoxieHub.Services.PythonAdapter;

/// <summary>
/// HTTP client for communicating with the Python Tonie adapter service.
/// Handles all network communication and serialization/deserialization.
/// </summary>
public class PythonAdapterClient : IPythonAdapterClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonAdapterClient> _logger;

    public PythonAdapterClient(HttpClient httpClient, ILogger<PythonAdapterClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthResponseDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking Python adapter health at {Timestamp}", DateTime.UtcNow);

            var response = await _httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var health = JsonSerializer.Deserialize<HealthResponseDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to deserialize health response");

            _logger.LogInformation("Python adapter health check passed: {Status}", health.Status);
            return health;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Python adapter health check failed - service unreachable");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Python adapter health response deserialization failed");
            throw;
        }
    }

    public async Task<SyncResponseDto> SyncAsync(SyncRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogInformation("Syncing Creative Tonie {TonieId} with {TrackCount} tracks at {Timestamp}",
                request.CreativeTonieExternalId,
                request.Tracks.Count(),
                DateTime.UtcNow);

            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            };
            
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request, jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("/sync", jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var syncResponse = JsonSerializer.Deserialize<SyncResponseDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to deserialize sync response");

            _logger.LogInformation("Sync completed for Creative Tonie {TonieId}: Success={Success}, Message={Message}",
                request.CreativeTonieExternalId,
                syncResponse.Success,
                syncResponse.Message);

            return syncResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Sync request to Python adapter failed for Creative Tonie {TonieId}",
                request.CreativeTonieExternalId);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Sync response deserialization failed for Creative Tonie {TonieId}",
                request.CreativeTonieExternalId);
            throw;
        }
    }
}
