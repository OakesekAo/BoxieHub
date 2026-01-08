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
        }
    }
}
