using System.Text.Json.Serialization;

namespace BoxieHub.TonieCloud.Models;

/// <summary>
/// OAuth token response from Tonie Cloud
/// Received from: https://login.tonies.com/auth/realms/tonies/protocol/openid-connect/token
/// </summary>
public class TonieTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }
    
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
