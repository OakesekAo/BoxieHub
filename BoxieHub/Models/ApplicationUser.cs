using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BoxieHub.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Name { get; set; }
        // Multi-household stub (not required for MVP features, but present)
        // TODO: Implement multi-household support in the future
    }

}
