using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    public class Household
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = default!;

        public string? Description { get; set; }
        
        /// <summary>
        /// External household ID from Tonie Cloud API
        /// Used to sync with the Tonie Cloud service
        /// </summary>
        [MaxLength(100)]
        public string? ExternalId { get; set; }
        
        /// <summary>
        /// When this household data was last synced from Tonie Cloud API
        /// </summary>
        public DateTimeOffset? LastSyncedAt { get; set; }

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        // Navigation properties (initialized to avoid null refs)
        public ICollection<Device> Devices { get; set; } = [];
        public ICollection<Character> Characters { get; set; } = [];
        public ICollection<ContentItem> ContentItems { get; set; } = [];

        // TODO (future): Invites, Notifications, etc.
        // public ICollection<HouseholdInvite> Invites { get; set; } = [];
        // public ICollection<Notification> Notifications { get; set; } = [];

        // Multi-household stub
        public ICollection<HouseholdMember> Members { get; set; } = [];
        
        // Helper properties
        
        /// <summary>
        /// Check if household data is stale (older than 1 day)
        /// </summary>
        public bool IsStale => 
            LastSyncedAt.HasValue && 
            DateTimeOffset.UtcNow - LastSyncedAt.Value > TimeSpan.FromDays(1);
        
        /// <summary>
        /// Get age of cached household data
        /// </summary>
        public TimeSpan? DataAge => 
            LastSyncedAt.HasValue 
                ? DateTimeOffset.UtcNow - LastSyncedAt.Value 
                : null;
    }
}
