using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>An NSubstitute UserManager — its constructor needs a store plus eight optional services.</summary>
internal static class UserManagerStub {
    public static UserManager<ApplicationUser> Create(IUserStore<ApplicationUser>? store = null) =>
        Substitute.For<UserManager<ApplicationUser>>(
            store ?? Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
}
