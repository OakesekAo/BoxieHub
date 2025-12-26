using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BoxieHub.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }

        public Guid? ProfilePictureId { get; set; }

        public virtual ImageUpload? ProfilePicture { get; set; }
    }

}
