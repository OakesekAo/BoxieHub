namespace BoxieHub.Services.PythonAdapter.Dtos;

/// <summary>
/// Represents a single audio track to be synced to a Creative Tonie.
/// </summary>
public class SyncTrackDto
{
    /// <summary>
    /// Display title of the track.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// URL where the Python adapter can fetch the audio file.
    /// Format: http://api-host/files/audio/{audioAssetId}
    /// </summary>
    public required string SourceUrl { get; set; }
}
