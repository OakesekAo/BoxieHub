using System.Text.Json.Serialization;

namespace BoxieHub.TonieCloud.Models;

/// <summary>
/// Creative Tonie device from Tonie Cloud
/// Based on real API response from https://api.tonie.cloud/v2/creativetonies
/// Verified with actual device: 72EB4F1D500304E0
/// </summary>
public class CreativeTonieDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("householdId")]
    public required string HouseholdId { get; set; }
    
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("imageUrl")]
    public required string ImageUrl { get; set; }
    
    [JsonPropertyName("secondsPresent")]
    public float SecondsPresent { get; set; }
    
    [JsonPropertyName("secondsRemaining")]
    public float SecondsRemaining { get; set; }
    
    [JsonPropertyName("chaptersPresent")]
    public int ChaptersPresent { get; set; }
    
    [JsonPropertyName("chaptersRemaining")]
    public int ChaptersRemaining { get; set; }
    
    [JsonPropertyName("transcoding")]
    public bool Transcoding { get; set; }
    
    [JsonPropertyName("lastUpdate")]
    public DateTime? LastUpdate { get; set; }
    
    /// <summary>
    /// Whether this is a live Creative Tonie
    /// </summary>
    [JsonPropertyName("live")]
    public bool Live { get; set; }
    
    /// <summary>
    /// Whether this Creative Tonie is private
    /// </summary>
    [JsonPropertyName("private")]
    public bool Private { get; set; }
    
    /// <summary>
    /// Transcoding errors if any occurred during audio processing
    /// </summary>
    [JsonPropertyName("transcodingErrors")]
    public List<TranscodingErrorDto>? TranscodingErrors { get; set; }
    
    /// <summary>
    /// Detailed chapters (populated when calling GetCreativeTonieDetailsAsync)
    /// </summary>
    [JsonPropertyName("chapters")]
    public List<ChapterDto>? Chapters { get; set; }
}

/// <summary>
/// Transcoding error information when audio processing fails
/// </summary>
public class TranscodingErrorDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("deletedChapters")]
    public List<DeletedChapterDto>? DeletedChapters { get; set; }
}

/// <summary>
/// Information about a chapter that was deleted due to transcoding errors
/// </summary>
public class DeletedChapterDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("seconds")]
    public float? Seconds { get; set; }
}
