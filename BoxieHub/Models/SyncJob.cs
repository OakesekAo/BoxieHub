using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class SyncJob
    {
        public int Id { get; set; }

        [Required]
        public int HouseholdId { get; set; }
        public Household Household { get; set; } = default!;

        [Required]
        public int DeviceId { get; set; }
        public Device Device { get; set; } = default!;

        // Optional: tie to content for diagnostics
        public int? ContentItemId { get; set; }
        public ContentItem? ContentItem { get; set; }

        public SyncStatus Status { get; set; } = SyncStatus.Pending;

        // MVP: string is flexible and avoids over-enum'ing
        [Required]
        public string JobType { get; set; } = "Upload";

        public string? ErrorMessage { get; set; }

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        private DateTimeOffset? _started;
        public DateTimeOffset? Started
        {
            get => _started;
            set => _started = value?.ToUniversalTime();
        }

        private DateTimeOffset? _completed;
        public DateTimeOffset? Completed
        {
            get => _completed;
            set => _completed = value?.ToUniversalTime();
        }
    }
}
