using BoxieHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<ImageUpload> Images { get; set; }
        public DbSet<Household> Households { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<Character> Characters { get; set; }
        public DbSet<ContentItem> ContentItems { get; set; }
        public DbSet<ContentAssignment> ContentAssignments { get; set; }
        public DbSet<SyncJob> SyncJobs { get; set; }
        public DbSet<HouseholdMember> HouseholdMembers { get; set; }
        public DbSet<TonieCredential> TonieCredentials { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Index for efficient lookup of user's default credential
            builder.Entity<TonieCredential>()
                .HasIndex(t => new { t.UserId, t.IsDefault })
                .HasDatabaseName("IX_TonieCredentials_UserId_IsDefault");
            
            // Ensure only one default credential per user via unique filtered index
            // PostgreSQL syntax for filtered index
            builder.Entity<TonieCredential>()
                .HasIndex(t => t.UserId)
                .HasFilter("\"IsDefault\" = true")
                .IsUnique()
                .HasDatabaseName("IX_TonieCredentials_UserId_UniqueDefault");
        }
    }
}
