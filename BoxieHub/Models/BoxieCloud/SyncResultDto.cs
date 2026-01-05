namespace BoxieHub.Models.BoxieCloud;

/// <summary>
/// Result of a sync/upload operation
/// </summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public int TracksProcessed { get; set; }
}
