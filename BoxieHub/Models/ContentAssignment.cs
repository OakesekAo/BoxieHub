using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class ContentAssignment
    {

        public int Id { get; set; }

        [Required]
        public int HouseholdId { get; set; }
        public Household Household { get; set; } = default!;

        [Required]
        public int ContentItemId { get; set; }
        public ContentItem ContentItem { get; set; } = default!;

        public AssignmentTarget TargetType { get; set; } = AssignmentTarget.Device;

        // Exactly one should be set based on TargetType (enforce in service layer)
        public int? DeviceId { get; set; }
        public Device? Device { get; set; }

        public int? CharacterId { get; set; }
        public Character? Character { get; set; }

        public bool IsActive { get; set; } = true;

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }
    }
}
