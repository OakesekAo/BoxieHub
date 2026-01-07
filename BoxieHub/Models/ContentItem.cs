using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class ContentItem
    {
        public int Id { get; set; }

        [Required]
        public int HouseholdId { get; set; }
        public Household Household { get; set; } = default!;

        [Required]
        public string Title { get; set; } = default!;

        public string? Description { get; set; }

        public ContentType ContentType { get; set; } = ContentType.Audio;

        // File backing this content item
        [Required]
        public Guid UploadId { get; set; }
        public FileUpload Upload { get; set; } = default!;

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        public ICollection<ContentAssignment> Assignments { get; set; } = [];

        // TODO (future): Tags, Playlists, Versions, etc.
    }
}
