using System.Security.Claims;
using BoxieHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BoxieHub.Components.Account
{
    public class CustomUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> options)
                                                                : UserClaimsPrincipalFactory<ApplicationUser>(userManager, options)
    {
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

            List<Claim> customClaims =
                [
                new Claim("Name", user.Name!),
                //new Claim("LastName", user.LastName!)
                ];

            identity.AddClaims(customClaims);

            return identity;
        }
    }
}
