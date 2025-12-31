using System.Net.Http.Json;
using BoxieHub.TonieCloud.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BoxieHub.TonieCloud.Services;

/// <summary>
/// Handles OAuth authentication with Tonie Cloud
/// Manages token lifecycle and in-memory caching
/// </summary>
public class TonieAuthService : ITonieAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TonieAuthService> _logger;
    
    private const string TOKEN_ENDPOINT = "https://login.tonies.com/auth/realms/tonies/protocol/openid-connect/token";
    private const string CLIENT_ID = "tonies-webapp";
    private const int TOKEN_CACHE_MINUTES = 55; // Refresh before 60min expiry
    
    public TonieAuthService(
        HttpClient httpClient, 
        IMemoryCache cache,
        ILogger<TonieAuthService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Get access token for user (cached or fresh)
    /// </summary>
    public async Task<string> GetAccessTokenAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("Username is required", nameof(username));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required", nameof(password));
        
        var cacheKey = $"tonie_token_{username}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            _logger.LogDebug("Using cached token for {Username}", username);
            return cachedToken;
        }
        
        // Request new token
        _logger.LogInformation("Requesting new token for {Username}", username);
        var tokenResponse = await RequestTokenAsync(username, password, ct);
        
        // Cache with expiration
        _cache.Set(cacheKey, tokenResponse.AccessToken, TimeSpan.FromMinutes(TOKEN_CACHE_MINUTES));
        
        _logger.LogInformation("Token cached for {Username} (expires in {Minutes}min)", username, TOKEN_CACHE_MINUTES);
        return tokenResponse.AccessToken;
    }
    
    /// <summary>
    /// Request OAuth token from Tonie Cloud
    /// </summary>
    private async Task<TonieTokenResponse> RequestTokenAsync(string username, string password, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["client_id"] = CLIENT_ID
        });
        
        try
        {
            var response = await _httpClient.PostAsync(TOKEN_ENDPOINT, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OAuth token request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode(); // Will throw with details
            }
            
            var tokenResponse = await response.Content.ReadFromJsonAsync<TonieTokenResponse>(cancellationToken: ct);
            return tokenResponse ?? throw new InvalidOperationException("Failed to deserialize token response");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to request OAuth token for {Username}", username);
            throw new InvalidOperationException("Failed to authenticate with Tonie Cloud. Check username and password.", ex);
        }
    }
    
    /// <summary>
    /// Invalidate cached token (e.g., on logout or error)
    /// </summary>
    public Task InvalidateTokenAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("Username is required", nameof(username));
        
        var cacheKey = $"tonie_token_{username}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Token invalidated for {Username}", username);
        
        return Task.CompletedTask;
    }
}
