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
    }
}
