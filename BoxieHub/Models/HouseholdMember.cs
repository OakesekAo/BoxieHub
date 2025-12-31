using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class HouseholdMember
    {

        public int Id { get; set; }

        [Required]
        public int HouseholdId { get; set; }
        public Household Household { get; set; } = default!;

        [Required]
        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        public Role Role { get; set; } = Role.Admin;

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }
    }
}
