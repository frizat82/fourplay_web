using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Models.Mappers;

namespace FourPlayWebApp.Server.UnitTests;

public class InvitationMapperTests
{
    [Fact]
    public void ToDto_UsedInvitation_WithConfirmedRegisteredUser_SetsRegisteredUserEmailConfirmedTrue()
    {
        var invitation = new Invitation
        {
            Email = "invited@test.com",
            IsUsed = true,
            RegisteredUserId = "user-1",
            RegisteredUser = new ApplicationUser { UserName = "invited", EmailConfirmed = true },
        };

        var dto = invitation.ToDto();

        Assert.True(dto.RegisteredUserEmailConfirmed);
    }

    [Fact]
    public void ToDto_UsedInvitation_WithUnconfirmedRegisteredUser_SetsRegisteredUserEmailConfirmedFalse()
    {
        // This is the exact case that confused an admin twice: the invitation shows "used" but
        // the registered account can't log in yet because RequireConfirmedEmail is blocking it.
        var invitation = new Invitation
        {
            Email = "invited@test.com",
            IsUsed = true,
            RegisteredUserId = "user-1",
            RegisteredUser = new ApplicationUser { UserName = "invited", EmailConfirmed = false },
        };

        var dto = invitation.ToDto();

        Assert.False(dto.RegisteredUserEmailConfirmed);
    }

    [Fact]
    public void ToDto_UnusedInvitation_SetsRegisteredUserEmailConfirmedNull()
    {
        var invitation = new Invitation { Email = "invited@test.com", IsUsed = false };

        var dto = invitation.ToDto();

        Assert.Null(dto.RegisteredUserEmailConfirmed);
    }
}
