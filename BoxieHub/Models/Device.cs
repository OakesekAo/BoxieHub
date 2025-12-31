using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class Device
    {
        public int Id { get; set; }

        [Required]
        public int HouseholdId { get; set; }
        public Household Household { get; set; } = default!;

        [Required]
        public string Name { get; set; } = default!;

        // Identifier used by your Python API (serial/id/ip/etc.)
        [Required]
        public string DeviceIdentifier { get; set; } = default!;

        public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

        private DateTimeOffset? _lastSeen;
        public DateTimeOffset? LastSeen
        {
            get => _lastSeen;
            set => _lastSeen = value?.ToUniversalTime();
        }

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        public ICollection<ContentAssignment> Assignments { get; set; } = [];
        public ICollection<SyncJob> SyncJobs { get; set; } = [];
    }
}
