namespace BoxieHub.Services.Audio;

/// <summary>
/// Service for analyzing audio files (duration, format, quality)
/// Critical for validating audio before uploading to Tonies (90-minute limit)
/// </summary>
public interface IAudioAnalysisService
{
    /// <summary>
    /// Get the actual duration of an audio file by analyzing its content
    /// More reliable than metadata tags which can be incorrect
    /// </summary>
    /// <param name="audioStream">Audio file stream (MP3, M4A, OGG, WAV, FLAC)</param>
    /// <param name="contentType">MIME type hint</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Actual duration in seconds, or null if unable to determine</returns>
    Task<float?> GetActualDurationAsync(Stream audioStream, string contentType, CancellationToken ct = default);
    
    /// <summary>
    /// Validate audio file meets Tonie requirements
    /// - Max 90 minutes duration per chapter
    /// - Supported format (MP3, M4A, OGG, WAV)
    /// </summary>
    Task<AudioValidationResult> ValidateForTonieAsync(Stream audioStream, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Result of audio file validation
/// </summary>
public class AudioValidationResult
{
    public bool IsValid { get; set; }
    public float? DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Format { get; set; }
    public int? Bitrate { get; set; }
    public int? SampleRate { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
