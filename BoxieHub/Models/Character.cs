using BoxieHub.Client.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BoxieHub.Models
{
    /// <summary>
    /// Represents a Tonie character (Creative or Content Tonie)
    /// Stores data synced from Tonie Cloud API
    /// </summary>
    public class Character
    {
        public int Id { get; set; }
        
        public CharacterType Type { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// External ID from Tonie Cloud API
        /// For Creative Tonies, this is the tonie ID
        /// </summary>
        [MaxLength(100)]
        public string? ExternalCharacterId { get; set; }

        public int? HouseholdId { get; set; }
        public Household? Household { get; set; }

        private DateTimeOffset _created;
        public DateTimeOffset Created
        {
            get => _created;
            set => _created = value.ToUniversalTime();
        }

        // ===== Tonie Cloud Sync Data (for Creative Tonies) =====
        
        /// <summary>
        /// Image URL from Tonie Cloud
        /// </summary>
        [MaxLength(512)]
        public string? ImageUrl { get; set; }
        
        /// <summary>
        /// Custom image uploaded by user (overrides ImageUrl if present)
        /// This is stored locally in BoxieHub and not synced to Tonie Cloud
        /// </summary>
        public Guid? CustomImageId { get; set; }
        public FileUpload? CustomImage { get; set; }
        
        /// <summary>
        /// Seconds of content currently on the Tonie
        /// </summary>
        public float? SecondsPresent { get; set; }
        
        /// <summary>
        /// Seconds of storage remaining on the Tonie
        /// </summary>
        public float? SecondsRemaining { get; set; }
        
        /// <summary>
        /// Number of chapters currently on the Tonie
        /// </summary>
        public int? ChaptersPresent { get; set; }
        
        /// <summary>
        /// Number of additional chapters that can fit
        /// </summary>
        public int? ChaptersRemaining { get; set; }
        
        /// <summary>
        /// Whether the Tonie is currently transcoding
        /// </summary>
        public bool? Transcoding { get; set; }
        
        /// <summary>
        /// Whether the Tonie is live/active
        /// </summary>
        public bool? Live { get; set; }
        
        /// <summary>
        /// Whether the Tonie is private
        /// </summary>
        public bool? Private { get; set; }
        
        /// <summary>
        /// JSON serialized chapters data from Tonie Cloud
        /// </summary>
        public string? ChaptersJson { get; set; }
        
        /// <summary>
        /// When this Tonie data was last synced from the API
        /// </summary>
        public DateTimeOffset? LastSyncedAt { get; set; }

        // Navigation properties
        public ICollection<ContentAssignment> Assignments { get; set; } = [];
        
        // Helper properties
        
        /// <summary>
        /// Check if Tonie data is stale (older than 1 day)
        /// </summary>
        public bool IsStale => 
            Type == CharacterType.Creative && 
            LastSyncedAt.HasValue && 
            DateTimeOffset.UtcNow - LastSyncedAt.Value > TimeSpan.FromDays(1);
        
        /// <summary>
        /// Get age of cached Tonie data
        /// </summary>
        public TimeSpan? DataAge => 
            LastSyncedAt.HasValue 
                ? DateTimeOffset.UtcNow - LastSyncedAt.Value 
                : null;
        
        /// <summary>
        /// Get the image URL to display (prioritizes custom image over Tonie Cloud image)
        /// </summary>
        public string DisplayImageUrl => 
            CustomImageId.HasValue 
                ? $"/uploads/{CustomImageId}" 
                : ImageUrl ?? "/images/default-tonie.png";
    }
}
