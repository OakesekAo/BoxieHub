using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    /// <summary>
    /// Represents a file upload (image, audio, etc.) stored in the database
    /// Can be extended to support external storage (S3, MinIO, etc.)
    /// </summary>
    public class FileUpload
    {
        public Guid Id { get; set; }
        
        [Required]
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

        // For future: external storage support
        // [MaxLength(512)]
        // public string? StoragePath { get; set; }
        // public string? StorageProvider { get; set; } // "Local", "S3", "MinIO"
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
