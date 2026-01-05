using System.Text.Json.Serialization;

namespace BoxieHub.Models.BoxieCloud;

/// <summary>
/// Household (family group) from Tonie Cloud
/// Based on real API response from https://api.tonie.cloud/v2/households
/// </summary>
public class HouseholdDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("image")]
    public string? Image { get; set; }
    
    [JsonPropertyName("access")]
    public required string Access { get; set; } // "owner" or "member"
    
    [JsonPropertyName("ownerName")]
    public string? OwnerName { get; set; }
    
    [JsonPropertyName("canLeave")]
    public bool CanLeave { get; set; }
    
    /// <summary>
    /// Whether this household has foreign Creative Tonie content
    /// </summary>
    [JsonPropertyName("foreignCreativeTonieContent")]
    public bool ForeignCreativeTonieContent { get; set; }
}
