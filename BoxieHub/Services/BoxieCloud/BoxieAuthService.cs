using System.Collections.Concurrent;
using System.Net.Http.Json;
using BoxieHub.Models.BoxieCloud;
using Microsoft.Extensions.Caching.Memory;

namespace BoxieHub.Services.BoxieCloud;

/// <summary>
/// Handles OAuth authentication with Tonie Cloud
/// Manages token lifecycle and in-memory caching with expiry tracking
/// </summary>
public class BoxieAuthService : IBoxieAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BoxieAuthService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _tokenExpiry = new();
    
    private const string TOKEN_ENDPOINT = "https://login.tonies.com/auth/realms/tonies/protocol/openid-connect/token";
    private const string CLIENT_ID = "my-tonies";
    private const int TOKEN_SAFETY_BUFFER_MINUTES = 5; // Refresh 5min before actual expiry
    
    public BoxieAuthService(
        HttpClient httpClient, 
        IMemoryCache cache,
        ILogger<BoxieAuthService> logger)
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
        var expiryKey = $"tonie_expiry_{username}";
        
        // Try to get from cache first and check if still valid
        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && 
            !string.IsNullOrEmpty(cachedToken) &&
            _tokenExpiry.TryGetValue(expiryKey, out var expiry) &&
            DateTime.UtcNow < expiry.AddMinutes(-TOKEN_SAFETY_BUFFER_MINUTES))
        {
            var timeRemaining = expiry - DateTime.UtcNow;
            _logger.LogDebug("Using cached token for {Username} (expires in {Minutes:F1}min)", 
                username, timeRemaining.TotalMinutes);
            return cachedToken;
        }
        
        // Request new token (expired or not in cache)
        if (_tokenExpiry.ContainsKey(expiryKey))
        {
            _logger.LogInformation("Token expired or expiring soon for {Username}, requesting new token", username);
        }
        else
        {
            _logger.LogInformation("No cached token for {Username}, requesting new token", username);
        }
        
        var tokenResponse = await RequestTokenAsync(username, password, ct);
        
        // Cache with expiration and track expiry time
        var expiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        var cacheExpiration = TimeSpan.FromSeconds(tokenResponse.ExpiresIn).Subtract(TimeSpan.FromMinutes(TOKEN_SAFETY_BUFFER_MINUTES));
        
        _cache.Set(cacheKey, tokenResponse.AccessToken, cacheExpiration);
        _tokenExpiry[expiryKey] = expiryTime;
        
        _logger.LogInformation("Token cached for {Username} (expires at {ExpiryTime:HH:mm:ss} UTC, ~{Minutes}min)", 
            username, expiryTime, tokenResponse.ExpiresIn / 60);
        return tokenResponse.AccessToken;
    }
    
    /// <summary>
    /// Request OAuth token from Tonie Cloud
    /// </summary>
    private async Task<TonieTokenResponse> RequestTokenAsync(string username, string password, CancellationToken ct)
    {
        _logger.LogInformation("Requesting token with client_id: {ClientId}", CLIENT_ID);
        
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
