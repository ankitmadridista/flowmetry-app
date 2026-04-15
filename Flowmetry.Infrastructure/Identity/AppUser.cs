using Microsoft.AspNetCore.Identity;

namespace Flowmetry.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
