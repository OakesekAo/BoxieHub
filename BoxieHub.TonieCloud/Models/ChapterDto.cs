using System.Text.Json.Serialization;

namespace BoxieHub.TonieCloud.Models;

/// <summary>
/// Audio chapter/track on a Creative Tonie
/// Based on real API response - verified with 29 chapters on device 72EB4F1D500304E0
/// </summary>
public class ChapterDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("title")]
    public required string Title { get; set; }
    
    [JsonPropertyName("seconds")]
    public float Seconds { get; set; }
    
    /// <summary>
    /// S3 file reference ID (used for PATCH operations)
    /// Can be either a string fileId or a complex object depending on API response
    /// </summary>
    [JsonPropertyName("file")]
    public object? File { get; set; }  // Can be string or ChapterFileDto
    
    /// <summary>
    /// Whether the chapter is currently being transcoded by Tonie Cloud
    /// </summary>
    [JsonPropertyName("transcoding")]
    public bool Transcoding { get; set; }
}

/// <summary>
/// File information for a chapter (when File is an object)
/// </summary>
public class ChapterFileDto
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    
    [JsonPropertyName("size")]
    public long? Size { get; set; }
    
    [JsonPropertyName("uploaded")]
    public bool? Uploaded { get; set; }
}
