using BoxieHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<FileUpload> FileUploads { get; set; }
        public DbSet<ImageUpload> Images { get; set; } // Legacy view
        public DbSet<Household> Households { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<Character> Characters { get; set; }
        public DbSet<ContentItem> ContentItems { get; set; }
        public DbSet<ContentAssignment> ContentAssignments { get; set; }
        public DbSet<SyncJob> SyncJobs { get; set; }
        public DbSet<HouseholdMember> HouseholdMembers { get; set; }
        public DbSet<TonieCredential> TonieCredentials { get; set; }
    public DbSet<AudioUploadHistory> AudioUploadHistories { get; set; }
    public DbSet<MediaLibraryItem> MediaLibraryItems { get; set; }
    public DbSet<MediaLibraryUsage> MediaLibraryUsages { get; set; }
    public DbSet<UserStorageAccount> UserStorageAccounts { get; set; }
    public DbSet<UserStoragePreference> UserStoragePreferences { get; set; }
    public DbSet<ImportJob> ImportJobs { get; set; }
    public DbSet<TonieUploadJob> TonieUploadJobs { get; set; }
    public DbSet<SavedPodcast> SavedPodcasts { get; set; }
    public DbSet<PodcastSubscription> PodcastSubscriptions { get; set; }
    public DbSet<PodcastEpisodeCache> PodcastEpisodeCache { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // TonieCredential indexes
            builder.Entity<TonieCredential>()
                .HasIndex(t => new { t.UserId, t.IsDefault })
                .HasDatabaseName("IX_TonieCredentials_UserId_IsDefault");
            
            builder.Entity<TonieCredential>()
                .HasIndex(t => t.UserId)
                .HasFilter("\"IsDefault\" = true")
                .IsUnique()
                .HasDatabaseName("IX_TonieCredentials_UserId_UniqueDefault");
            
            // Household indexes for sync tracking
            builder.Entity<Household>()
                .HasIndex(h => h.ExternalId)
                .HasDatabaseName("IX_Households_ExternalId");
            
            builder.Entity<Household>()
                .HasIndex(h => h.LastSyncedAt)
                .HasDatabaseName("IX_Households_LastSyncedAt");
            
            // Character indexes for Tonie lookup
            builder.Entity<Character>()
                .HasIndex(c => c.ExternalCharacterId)
                .HasDatabaseName("IX_Characters_ExternalCharacterId");
            
            builder.Entity<Character>()
                .HasIndex(c => new { c.HouseholdId, c.Type })
                .HasDatabaseName("IX_Characters_HouseholdId_Type");
            
            builder.Entity<Character>()
                .HasIndex(c => c.LastSyncedAt)
                .HasDatabaseName("IX_Characters_LastSyncedAt");
            
            // AudioUploadHistory indexes
            builder.Entity<AudioUploadHistory>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AudioUploadHistories_UserId");
            
            builder.Entity<AudioUploadHistory>()
                .HasIndex(a => a.Status)
                .HasDatabaseName("IX_AudioUploadHistories_Status");
            
            builder.Entity<AudioUploadHistory>()
                .HasIndex(a => new { a.TonieId, a.HouseholdId })
                .HasDatabaseName("IX_AudioUploadHistories_TonieId_HouseholdId");
            
            builder.Entity<AudioUploadHistory>()
                .HasIndex(a => a.Created)
                .HasDatabaseName("IX_AudioUploadHistories_Created");
            
            // FileUpload indexes
            builder.Entity<FileUpload>()
                .HasIndex(f => f.FileCategory)
                .HasDatabaseName("IX_FileUploads_FileCategory");
            
            builder.Entity<FileUpload>()
                .HasIndex(f => f.Created)
                .HasDatabaseName("IX_FileUploads_Created");
            
            builder.Entity<FileUpload>()
                .HasIndex(f => f.Provider)
                .HasDatabaseName("IX_FileUploads_Provider");
            
            // UserStorageAccount indexes
            builder.Entity<UserStorageAccount>()
                .HasIndex(u => u.UserId)
                .HasDatabaseName("IX_UserStorageAccounts_UserId");
            
            builder.Entity<UserStorageAccount>()
                .HasIndex(u => new { u.UserId, u.Provider })
                .HasDatabaseName("IX_UserStorageAccounts_UserId_Provider");
            
            builder.Entity<UserStorageAccount>()
                .HasIndex(u => u.IsActive)
                .HasDatabaseName("IX_UserStorageAccounts_IsActive");
            
            // UserStoragePreference unique constraint
            builder.Entity<UserStoragePreference>()
                .HasIndex(u => u.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserStoragePreferences_UserId");
            
            // ImportJob indexes
            builder.Entity<ImportJob>()
                .HasIndex(i => i.UserId)
                .HasDatabaseName("IX_ImportJobs_UserId");
            
            builder.Entity<ImportJob>()
                .HasIndex(i => i.Status)
                .HasDatabaseName("IX_ImportJobs_Status");
            
            builder.Entity<ImportJob>()
                .HasIndex(i => i.Created)
                .HasDatabaseName("IX_ImportJobs_Created");
            
            builder.Entity<ImportJob>()
                .HasOne(i => i.MediaLibraryItem)
                .WithMany()
                .HasForeignKey(i => i.MediaLibraryItemId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // SavedPodcast indexes
            builder.Entity<SavedPodcast>()
                .HasIndex(s => s.FeedUrl)
                .IsUnique()
                .HasDatabaseName("IX_SavedPodcasts_FeedUrl");
            
            builder.Entity<SavedPodcast>()
                .HasIndex(s => s.LastFetched)
                .HasDatabaseName("IX_SavedPodcasts_LastFetched");
            
            // PodcastSubscription indexes
            builder.Entity<PodcastSubscription>()
                .HasIndex(s => s.UserId)
                .HasDatabaseName("IX_PodcastSubscriptions_UserId");
            
            builder.Entity<PodcastSubscription>()
                .HasIndex(s => new { s.UserId, s.SavedPodcastId })
                .IsUnique()
                .HasDatabaseName("IX_PodcastSubscriptions_UserId_PodcastId");
            
            builder.Entity<PodcastSubscription>()
                .HasIndex(s => s.IsFavorite)
                .HasDatabaseName("IX_PodcastSubscriptions_IsFavorite");
            
            builder.Entity<PodcastSubscription>()
                .HasIndex(s => s.LastAccessed)
                .HasDatabaseName("IX_PodcastSubscriptions_LastAccessed");
            
            // PodcastEpisodeCache indexes
            builder.Entity<PodcastEpisodeCache>()
                .HasIndex(e => e.SavedPodcastId)
                .HasDatabaseName("IX_PodcastEpisodeCache_PodcastId");
            
            builder.Entity<PodcastEpisodeCache>()
                .HasIndex(e => new { e.SavedPodcastId, e.EpisodeGuid })
                .IsUnique()
                .HasDatabaseName("IX_PodcastEpisodeCache_PodcastId_Guid");
            
            builder.Entity<PodcastEpisodeCache>()
                .HasIndex(e => e.AudioUrl)
                .HasDatabaseName("IX_PodcastEpisodeCache_AudioUrl");
            
            builder.Entity<PodcastEpisodeCache>()
                .HasIndex(e => e.FileUploadId)
                .HasDatabaseName("IX_PodcastEpisodeCache_FileUploadId");
            
            builder.Entity<PodcastEpisodeCache>()
                .HasOne(e => e.FileUpload)
                .WithMany()
                .HasForeignKey(e => e.FileUploadId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
