using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoxieHub.TonieCloud.Models;
using Microsoft.Extensions.Logging;

namespace BoxieHub.TonieCloud.Services;

/// <summary>
/// Client for Tonie Cloud API
/// Handles all API calls with proper authentication
/// Based on real API endpoints: https://api.tonie.cloud/v2
/// </summary>
public class TonieCloudClient : ITonieCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly ITonieAuthService _authService;
    private readonly ILogger<TonieCloudClient> _logger;
    
    private const string API_BASE = "https://api.tonie.cloud/v2";
    
    public TonieCloudClient(
        HttpClient httpClient,
        ITonieAuthService authService,
        ILogger<TonieCloudClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Set base address if not already set
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(API_BASE);
        }
    }
    
    /// <summary>
    /// Get all households for authenticated user
    /// Endpoint: GET /v2/households
    /// </summary>
    public async Task<List<HouseholdDto>> GetHouseholdsAsync(
        string username, 
        string password, 
        CancellationToken ct = default)
    {
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "/v2/households");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting households for {Username}", username);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get households. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var households = await response.Content.ReadFromJsonAsync<List<HouseholdDto>>(cancellationToken: ct);
            _logger.LogInformation("Retrieved {Count} household(s) for {Username}", households?.Count ?? 0, username);
            
            return households ?? new List<HouseholdDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting households for {Username}", username);
            throw new InvalidOperationException($"Failed to get households for {username}", ex);
        }
    }
    
    /// <summary>
    /// Get all Creative Tonies for authenticated user
    /// Endpoint: GET /v2/creativetonies
    /// </summary>
    public async Task<List<CreativeTonieDto>> GetCreativeToniesByUserAsync(
        string username, 
        string password, 
        CancellationToken ct = default)
    {
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "/v2/creativetonies");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting Creative Tonies for {Username}", username);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get Creative Tonies. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var tonies = await response.Content.ReadFromJsonAsync<List<CreativeTonieDto>>(cancellationToken: ct);
            _logger.LogInformation("Retrieved {Count} Creative Tonie(s) for {Username}", tonies?.Count ?? 0, username);
            
            return tonies ?? new List<CreativeTonieDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting Creative Tonies for {Username}", username);
            throw new InvalidOperationException($"Failed to get Creative Tonies for {username}", ex);
        }
    }
    
    /// <summary>
    /// Get detailed information for a specific Creative Tonie including all chapters
    /// Endpoint: GET /v2/creativetonies/{tonieId}
    /// </summary>
    public async Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(
        string username, 
        string password, 
        string tonieId, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tonieId))
            throw new ArgumentException("Tonie ID is required", nameof(tonieId));
        
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/creativetonies/{tonieId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting details for Tonie {TonieId}", tonieId);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get Tonie details. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var tonie = await response.Content.ReadFromJsonAsync<CreativeTonieDto>(cancellationToken: ct);
            
            if (tonie == null)
                throw new InvalidOperationException($"Failed to deserialize Tonie details for {tonieId}");
            
            _logger.LogInformation("Retrieved details for Tonie {TonieId} with {ChapterCount} chapter(s)", 
                tonieId, tonie.Chapters?.Count ?? 0);
            
            return tonie;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting Tonie details for {TonieId}", tonieId);
            throw new InvalidOperationException($"Failed to get Tonie details for {tonieId}", ex);
        }
    }
    
    /// <summary>
    /// Upload audio file to Creative Tonie
    /// Endpoint: POST /v2/creativetonies/{tonieId}/chapters
    /// </summary>
    public async Task<SyncResultDto> SyncAudioAsync(
        string username, 
        string password, 
        string tonieId, 
        Stream audioStream, 
        string title, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tonieId))
            throw new ArgumentException("Tonie ID is required", nameof(tonieId));
        if (audioStream == null)
            throw new ArgumentNullException(nameof(audioStream));
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("Title is required", nameof(title));
        
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(audioStream), "file", $"{title}.mp3");
            content.Add(new StringContent(title), "title");
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/creativetonies/{tonieId}/chapters");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;
            
            _logger.LogInformation("Uploading audio '{Title}' to Tonie {TonieId}", title, tonieId);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to upload audio. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                
                return new SyncResultDto
                {
                    Success = false,
                    Message = "Upload failed",
                    ErrorDetails = $"HTTP {response.StatusCode}: {errorContent}",
                    TracksProcessed = 0
                };
            }
            
            _logger.LogInformation("Successfully uploaded audio '{Title}' to Tonie {TonieId}", title, tonieId);
            
            return new SyncResultDto
            {
                Success = true,
                Message = $"Successfully uploaded '{title}' to Tonie",
                TracksProcessed = 1
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error uploading audio to Tonie {TonieId}", tonieId);
            return new SyncResultDto
            {
                Success = false,
                Message = "Upload failed due to network error",
                ErrorDetails = ex.Message,
                TracksProcessed = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading audio to Tonie {TonieId}", tonieId);
            return new SyncResultDto
            {
                Success = false,
                Message = "Upload failed due to unexpected error",
                ErrorDetails = ex.Message,
                TracksProcessed = 0
            };
        }
    }
}
