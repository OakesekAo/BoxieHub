using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class ImageUpload
    {
        public Guid Id { get; set; }
        [Required]
        public byte[]? Data { get; set; }
        [Required]
        public string? Type { get; set; }
        public string Url => $"/uploads/{Id}";

        // For MVP: store path/key to file (local disk, S3, MinIO, etc.)
        //[Required]
        //TODO: Implement external storage support in the future
        //public string StoragePath { get; set; } = default!;
        // TODO: sha256, provider, etc. if needed later
    }
}
