using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #1: hardcoded admin email "markmjohnson@gmail.com"
/// and username "frizat" in UserManagerJob.
///
/// Before fix: CreateUser always looks up and creates "markmjohnson@gmail.com" / "frizat".
/// After fix:  it reads ADMIN_EMAIL and ADMIN_USERNAME from IConfiguration.
/// </summary>
public class UserManagerJobConfigTests
{
    private static IConfiguration BuildConfig(string email, string username, string password = "Test!1234") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAIL"]    = email,
                ["ADMIN_USERNAME"] = username,
                ["ADMIN_PASSWORD"] = password,
            })
            .Build();

    private static (UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager) BuildMocks()
    {
        var userStore   = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var roleStore   = Substitute.For<IRoleStore<IdentityRole>>();
        var roleManager = Substitute.For<RoleManager<IdentityRole>>(
            roleStore, null, null, null, null);

        return (userManager, roleManager);
    }

    [Fact]
    public async Task CreateUser_UsesAdminEmailFromConfiguration_NotHardcoded()
    {
        const string configEmail = "config-admin@example.com";
        const string configUser  = "configadmin";

        var (userManager, roleManager) = BuildMocks();

        // Admin user doesn't exist yet — job will try to create it
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var config = BuildConfig(configEmail, configUser);
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.CreateUser(configEmail);

        // Must look up the config email, not the hardcoded one
        await userManager.Received().FindByEmailAsync(configEmail);

        // The hardcoded email must never be queried
        await userManager.DidNotReceive().FindByEmailAsync("markmjohnson@gmail.com");
    }

    [Fact]
    public async Task CreateUser_UsesAdminUsernameFromConfiguration_NotHardcoded()
    {
        const string configEmail = "config-admin@example.com";
        const string configUser  = "configadmin";

        var (userManager, roleManager) = BuildMocks();

        // Admin user doesn't exist — job creates it
        userManager.FindByEmailAsync(configEmail).Returns((ApplicationUser?)null);

        ApplicationUser? capturedUser = null;
        userManager.CreateAsync(Arg.Do<ApplicationUser>(u => capturedUser = u), Arg.Any<string>())
                   .Returns(IdentityResult.Success);

        var config = BuildConfig(configEmail, configUser);
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.CreateUser(configEmail);

        Assert.NotNull(capturedUser);
        Assert.Equal(configUser, capturedUser.UserName);  // fails before fix ("frizat" hardcoded)
        Assert.NotEqual("frizat", capturedUser.UserName); // belt-and-suspenders
    }

    // frizat: SyncAdminPassword used to call RemovePasswordAsync then AddPasswordAsync — two
    // separate SaveChanges against the same row, racing against any other concurrent write to
    // that user (real incident: a redeploy's own concurrent request hit this exact window and
    // left the admin account passwordless when the second write lost an EF optimistic-concurrency
    // check). Setting PasswordHash directly and calling UpdateAsync once is atomic — no window.
    [Fact]
    public async Task SyncAdminPassword_UpdatesPasswordHashInASingleAtomicWrite_NotRemoveThenAdd()
    {
        var (userManager, roleManager) = BuildMocks();
        var adminUser = new ApplicationUser { Email = "admin@example.com", UserName = "admin" };
        userManager.FindByEmailAsync("admin@example.com").Returns(adminUser);
        var hasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
        hasher.HashPassword(adminUser, "NewPass!123").Returns("hashed-value");
        userManager.PasswordHasher = hasher;
        userManager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);

        var config = BuildConfig("admin@example.com", "admin", "NewPass!123");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.SyncAdminPassword("admin@example.com");

        Assert.Equal("hashed-value", adminUser.PasswordHash);
        await userManager.Received(1).UpdateAsync(adminUser);
        await userManager.DidNotReceive().RemovePasswordAsync(Arg.Any<ApplicationUser>());
        await userManager.DidNotReceive().AddPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    /// <summary>
    /// frizat-uvi: AddUserToRole must not silently succeed when the user is not found.
    /// Previously logged "Admin User Found" with a null value — now logs an error.
    /// This test verifies the method returns without calling AddToRoleAsync.
    /// </summary>
    [Fact]
    public async Task AddUserToRole_DoesNotAssignRole_WhenUserNotFound()
    {
        var (userManager, roleManager) = BuildMocks();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var config = BuildConfig("ghost@example.com", "ghost");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        // Should not throw, should not call AddToRoleAsync
        await job.AddUserToRole("ghost@example.com", "Administrator");

        await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }
}
