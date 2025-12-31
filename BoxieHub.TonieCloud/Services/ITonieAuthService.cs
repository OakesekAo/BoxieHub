namespace BoxieHub.TonieCloud.Services;

/// <summary>
/// Service interface for Tonie Cloud authentication
/// Handles OAuth token management and caching
/// </summary>
public interface ITonieAuthService
{
    /// <summary>
    /// Get access token for user (from cache or new request)
    /// </summary>
    Task<string> GetAccessTokenAsync(string username, string password, CancellationToken ct = default);
    
    /// <summary>
    /// Invalidate cached token (e.g., on logout or 401 error)
    /// </summary>
    Task InvalidateTokenAsync(string username, CancellationToken ct = default);
}
