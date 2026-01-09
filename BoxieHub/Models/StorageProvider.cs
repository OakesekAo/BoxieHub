namespace BoxieHub.Models;

/// <summary>
/// Supported storage providers for files
/// </summary>
public enum StorageProvider
{
    /// <summary>
    /// Files stored in PostgreSQL database (legacy, for small images only)
    /// </summary>
    Database = 0,
    
    /// <summary>
    /// Files stored in S3-compatible storage (MinIO dev, Railway prod)
    /// BoxieHub-managed storage
    /// </summary>
    S3Railway = 1,
    
    /// <summary>
    /// Files stored in user's connected Dropbox account
    /// </summary>
    Dropbox = 2,
    
    /// <summary>
    /// Files stored in user's connected Google Drive account
    /// </summary>
    GoogleDrive = 3
}
