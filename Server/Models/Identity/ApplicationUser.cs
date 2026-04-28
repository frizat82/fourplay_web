using Microsoft.AspNetCore.Identity;

namespace FourPlayWebApp.Server.Models.Identity;

public class ApplicationUser : IdentityUser {

    public bool EnableEmails { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

}
