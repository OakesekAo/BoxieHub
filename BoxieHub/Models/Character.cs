using BoxieHub.Client.Models.Enums;

namespace BoxieHub.Models
{
    public class Character
    {
        public int Id { get; set; }
        public CharacterType Type { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? ExternalCharacterId { get; set; }

        public int? HouseholdId { get; set; }
        public Household? Household { get; set; }

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        public ICollection<ContentAssignment> Assignments { get; set; } = [];

    }
}
