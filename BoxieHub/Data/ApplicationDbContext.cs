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
    }
}
