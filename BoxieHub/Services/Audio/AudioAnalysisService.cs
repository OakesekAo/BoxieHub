using TagLib;

namespace BoxieHub.Services.Audio;

/// <summary>
/// Implementation using TagLibSharp for audio file analysis
/// Provides accurate duration detection for all major audio formats
/// </summary>
public class AudioAnalysisService : IAudioAnalysisService
{
    private readonly ILogger<AudioAnalysisService> _logger;
    
    // Tonie Creative limits (from official documentation)
    private const float MAX_DURATION_SECONDS = 90 * 60; // 90 minutes
    private const long MAX_FILE_SIZE_BYTES = 200 * 1024 * 1024; // 200 MB (practical limit)

    public AudioAnalysisService(ILogger<AudioAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<float?> GetActualDurationAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        try
        {
            // TagLib requires seekable stream
            if (!audioStream.CanSeek)
            {
                _logger.LogWarning("Audio stream is not seekable, copying to MemoryStream");
                var ms = new MemoryStream();
                await audioStream.CopyToAsync(ms, ct);
                ms.Position = 0;
                audioStream = ms;
            }

            audioStream.Position = 0;

            // Get proper file extension for TagLib detection
            var extension = GetFileExtensionFromContentType(contentType);
            var fileName = $"audio{extension}";

            // Try to create file with mimetype hint first (most reliable)
            using var file = TagLib.File.Create(new StreamFileAbstraction(fileName, audioStream), contentType, ReadStyle.Average);

            if (file.Properties?.Duration != null)
            {
                var durationSeconds = (float)file.Properties.Duration.TotalSeconds;

                _logger.LogInformation("Detected audio duration: {Duration}s (Format: {Format}, Bitrate: {Bitrate} kbps)",
                    durationSeconds, file.Properties.MediaTypes, file.Properties.AudioBitrate);

                return durationSeconds;
            }

            _logger.LogWarning("Could not determine audio duration from file properties");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing audio duration for content type: {ContentType}", contentType);
            return null;
        }
    }

    public async Task<AudioValidationResult> ValidateForTonieAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        var result = new AudioValidationResult
        {
            IsValid = true,
            FileSizeBytes = audioStream.Length
        };

        try
        {
            // Check file size first (quick check)
            if (audioStream.Length > MAX_FILE_SIZE_BYTES)
            {
                result.Errors.Add($"File too large ({audioStream.Length / (1024 * 1024)}MB). Maximum is {MAX_FILE_SIZE_BYTES / (1024 * 1024)}MB.");
                result.IsValid = false;
            }

            // Make stream seekable if needed
            if (!audioStream.CanSeek)
            {
                var ms = new MemoryStream();
                await audioStream.CopyToAsync(ms, ct);
                ms.Position = 0;
                audioStream = ms;
            }

            audioStream.Position = 0;

            // Get proper file extension for TagLib detection
            var extension = GetFileExtensionFromContentType(contentType);
            var fileName = $"audio{extension}";

            using var file = TagLib.File.Create(new StreamFileAbstraction(fileName, audioStream), contentType, ReadStyle.Average);
            
            // Extract properties
            if (file.Properties != null)
            {
                result.DurationSeconds = (float)file.Properties.Duration.TotalSeconds;
                result.Bitrate = file.Properties.AudioBitrate;
                result.SampleRate = file.Properties.AudioSampleRate;
                result.Format = file.Properties.MediaTypes.ToString();

                // Validate duration
                if (result.DurationSeconds > MAX_DURATION_SECONDS)
                {
                    var minutes = (int)(result.DurationSeconds.Value / 60);
                    result.Errors.Add($"Audio too long ({minutes} minutes). Tonies support maximum {(int)(MAX_DURATION_SECONDS / 60)} minutes per chapter.");
                    result.IsValid = false;
                }

                if (result.DurationSeconds <= 0)
                {
                    result.Warnings.Add("Audio duration is 0 seconds. This might indicate a problem with the file.");
                }

                // Validate format
                if (!IsSupportedFormat(file.MimeType))
                {
                    result.Errors.Add($"Unsupported format: {file.MimeType}. Tonies support MP3, M4A, OGG, and WAV.");
                    result.IsValid = false;
                }

                // Warnings for quality issues
                if (result.Bitrate < 64)
                {
                    result.Warnings.Add($"Low bitrate ({result.Bitrate} kbps). Consider using at least 128 kbps for better audio quality.");
                }

                if (result.SampleRate < 22050)
                {
                    result.Warnings.Add($"Low sample rate ({result.SampleRate} Hz). Consider using at least 44.1 kHz.");
                }

                _logger.LogInformation("Audio validation complete: {Duration}s, {Format}, {Bitrate} kbps, Valid: {IsValid}",
                    result.DurationSeconds, result.Format, result.Bitrate, result.IsValid);
            }
            else
            {
                result.Errors.Add("Could not read audio file properties. File may be corrupted.");
                result.IsValid = false;
            }
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            _logger.LogError(ex, "Unsupported audio format for content type: {ContentType}", contentType);
            result.Errors.Add($"Unsupported audio format. Please use MP3, M4A, OGG, or WAV files.");
            result.IsValid = false;
        }
        catch (TagLib.CorruptFileException ex)
        {
            _logger.LogError(ex, "Corrupted audio file detected");
            result.Errors.Add("Audio file appears to be corrupted. Please try a different file.");
            result.IsValid = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating audio file");
            result.Errors.Add($"Error analyzing audio file: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    private bool IsSupportedFormat(string mimeType)
    {
        var supported = new[]
        {
            "audio/mpeg",       // MP3
            "audio/mp3",        // MP3 (alternative)
            "audio/mp4",        // M4A
            "audio/m4a",        // M4A (alternative)
            "audio/x-m4a",      // M4A (alternative)
            "audio/ogg",        // OGG Vorbis
            "audio/vorbis",     // OGG Vorbis (alternative)
            "audio/wav",        // WAV
            "audio/x-wav",      // WAV (alternative)
            "audio/wave"        // WAV (alternative)
        };

            return supported.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
        }

        private string GetFileExtensionFromContentType(string contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "audio/mpeg" => ".mp3",
                "audio/mp3" => ".mp3",
                "audio/mp4" => ".m4a",
                "audio/m4a" => ".m4a",
                "audio/x-m4a" => ".m4a",
                "audio/ogg" => ".ogg",
                "audio/vorbis" => ".ogg",
                "audio/wav" => ".wav",
                "audio/x-wav" => ".wav",
                "audio/wave" => ".wav",
                _ => ".mp3" // Default fallback
            };
        }

    /// <summary>
    /// TagLib helper class for reading from Stream
    /// </summary>
    private class StreamFileAbstraction : TagLib.File.IFileAbstraction
    {
        private readonly Stream _stream;

        public StreamFileAbstraction(string name, Stream stream)
        {
            Name = name;
            _stream = stream;
        }

        public string Name { get; }

        public Stream ReadStream => _stream;

        public Stream WriteStream => _stream;

        public void CloseStream(Stream stream)
        {
            // Don't close - caller owns the stream
        }
    }
}
