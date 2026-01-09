namespace BoxieHub.Models;

/// <summary>
/// Status of an import job
/// </summary>
public enum ImportJobStatus
{
    Pending = 0,     // Created but not started
    Validating = 1,  // Checking URL, fetching metadata
    Downloading = 2, // Downloading audio
    Processing = 3,  // Converting/processing audio
    Saving = 4,      // Saving to storage and database
    Completed = 5,   // Successfully completed
    Failed = 6,      // Failed with error
    Cancelled = 7    // User cancelled
}
