namespace BoxieHub.Models;

/// <summary>
/// Source of an imported media file
/// </summary>
public enum ImportSource
{
    Upload = 0,      // Manual file upload
    YouTube = 1,     // YouTube video
    Podcast = 2,     // Podcast RSS feed (future)
    DirectUrl = 3    // Direct audio URL (future)
}
