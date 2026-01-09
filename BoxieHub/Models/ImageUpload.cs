using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    /// <summary>
    /// Represents a file upload (image, audio, etc.)
    /// Supports database storage (legacy, for small images) and external storage (S3, Dropbox, GDrive)
    /// </summary>
    public class FileUpload
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// File data (nullable - only used for Database storage provider)
        /// For external storage, this will be null and StoragePath will be used
        /// </summary>
        public byte[]? Data { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string? ContentType { get; set; }
        
        /// <summary>
        /// Original filename
        /// </summary>
        [MaxLength(256)]
        public string? FileName { get; set; }
        
        /// <summary>
        /// File category (Image, Audio, Document, etc.)
        /// </summary>
        [MaxLength(50)]
        public string FileCategory { get; set; } = "Unknown";
        
        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }
        
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
        
        public string Url => $"/uploads/{Id}";

        // ===== External Storage Support =====
        
        /// <summary>
        /// Storage provider (Database, S3Railway, Dropbox, GoogleDrive)
        /// </summary>
        public StorageProvider Provider { get; set; } = StorageProvider.Database;
        
        /// <summary>
        /// Path/key in external storage (null for Database provider)
        /// For S3: "users/{userId}/{guid}/{filename}"
        /// For Dropbox: "/BoxieHub/{userId}/{filename}"
        /// For GDrive: file ID
        /// </summary>
        [MaxLength(1024)]
        public string? StoragePath { get; set; }
        
        /// <summary>
        /// Reference to user's connected storage account (null for Database/S3Railway)
        /// Required for Dropbox and GoogleDrive
        /// </summary>
        public int? UserStorageAccountId { get; set; }
        public UserStorageAccount? UserStorageAccount { get; set; }
    }
    
    /// <summary>
    /// Legacy alias for backward compatibility
    /// </summary>
    public class ImageUpload : FileUpload
    {
        public ImageUpload()
        {
            FileCategory = "Image";
        }
    }
}
