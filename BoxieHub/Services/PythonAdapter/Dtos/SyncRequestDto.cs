namespace BoxieHub.Services.PythonAdapter.Dtos;

/// <summary>
/// Request payload sent to Python adapter to sync audio to a Creative Tonie.
/// </summary>
public class SyncRequestDto
{
    /// <summary>
    /// External ID of the Creative Tonie (used by Toniebox ecosystem).
    /// </summary>
    public required string CreativeTonieExternalId { get; set; }

    /// <summary>
    /// List of audio tracks to assign to this Creative Tonie.
    /// </summary>
    public required IEnumerable<SyncTrackDto> Tracks { get; set; }
}
