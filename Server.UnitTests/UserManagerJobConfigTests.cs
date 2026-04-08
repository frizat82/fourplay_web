using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        var dbOptions = new DbContextOptionsBuilder<Data.ApplicationDbContext>()
            .UseInMemoryDatabase("UMJobTest_" + Guid.NewGuid())
            .Options;
        var db = new Data.ApplicationDbContext(dbOptions);

        var config = BuildConfig(configEmail, configUser);
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, db, services);

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

        var dbOptions = new DbContextOptionsBuilder<Data.ApplicationDbContext>()
            .UseInMemoryDatabase("UMJobTest_" + Guid.NewGuid())
            .Options;
        var db = new Data.ApplicationDbContext(dbOptions);

        var config = BuildConfig(configEmail, configUser);
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, db, services);

        await job.CreateUser(configEmail);

        Assert.NotNull(capturedUser);
        Assert.Equal(configUser, capturedUser.UserName);  // fails before fix ("frizat" hardcoded)
        Assert.NotEqual("frizat", capturedUser.UserName); // belt-and-suspenders
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

        var dbOptions = new DbContextOptionsBuilder<Data.ApplicationDbContext>()
            .UseInMemoryDatabase("UMJobTest_" + Guid.NewGuid())
            .Options;
        var db = new Data.ApplicationDbContext(dbOptions);
        var config = BuildConfig("ghost@example.com", "ghost");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, db, services);

        // Should not throw, should not call AddToRoleAsync
        await job.AddUserToRole("ghost@example.com", "Administrator");

        await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }
}
