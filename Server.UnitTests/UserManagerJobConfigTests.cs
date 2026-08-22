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
    private static IConfiguration BuildConfig(string email, string username, string password = "Test!1234", string? forcePasswordSync = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAIL"]    = email,
                ["ADMIN_USERNAME"] = username,
                ["ADMIN_PASSWORD"] = password,
                ["ADMIN_FORCE_PASSWORD_SYNC"] = forcePasswordSync,
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

    /// <summary>
    /// Shared setup for the SyncAdminPassword gating tests below: wires FindByEmailAsync to
    /// return an admin with <paramref name="existingHash"/> as its current PasswordHash (null
    /// for the newly-bootstrapped-account case), stubs the hasher to produce "hashed-value" for
    /// <paramref name="newPassword"/>, and makes UpdateAsync succeed.
    /// </summary>
    private static ApplicationUser SetUpSyncPasswordMocks(
        UserManager<ApplicationUser> userManager, string? existingHash, string newPassword)
    {
        var adminUser = new ApplicationUser
        {
            Email = "admin@example.com",
            UserName = "admin",
            PasswordHash = existingHash,
        };
        userManager.FindByEmailAsync("admin@example.com").Returns(adminUser);
        var hasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
        hasher.HashPassword(adminUser, newPassword).Returns("hashed-value");
        userManager.PasswordHasher = hasher;
        userManager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        return adminUser;
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

        var wasCreated = await job.CreateUser(configEmail);

        // Must look up the config email, not the hardcoded one
        await userManager.Received().FindByEmailAsync(configEmail);

        // The hardcoded email must never be queried
        await userManager.DidNotReceive().FindByEmailAsync("markmjohnson@gmail.com");

        Assert.True(wasCreated);
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

        var wasCreated = await job.CreateUser(configEmail);

        Assert.NotNull(capturedUser);
        Assert.Equal(configUser, capturedUser.UserName);  // fails before fix ("frizat" hardcoded)
        Assert.NotEqual("frizat", capturedUser.UserName); // belt-and-suspenders
        Assert.True(wasCreated);
    }

    /// <summary>
    /// frizat-wyo: CreateUser must report whether it actually created a new account so the
    /// caller can decide whether SyncAdminPassword is allowed to touch the password (see below).
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsFalse_WhenAdminAccountAlreadyExists()
    {
        var (userManager, roleManager) = BuildMocks();
        var existingUser = new ApplicationUser { Email = "admin@example.com", UserName = "admin" };
        userManager.FindByEmailAsync("admin@example.com").Returns(existingUser);

        var config = BuildConfig("admin@example.com", "admin");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        var wasCreated = await job.CreateUser("admin@example.com");

        Assert.False(wasCreated);
        await userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    // frizat: SyncAdminPassword used to call RemovePasswordAsync then AddPasswordAsync — two
    // separate SaveChanges against the same row, racing against any other concurrent write to
    // that user (real incident: a redeploy's own concurrent request hit this exact window and
    // left the admin account passwordless when the second write lost an EF optimistic-concurrency
    // check). Setting PasswordHash directly and calling UpdateAsync once is atomic — no window.
    //
    // frizat-wyo: SyncAdminPassword also used to run this unconditionally on EVERY startup, even
    // for an admin account that already existed with a deliberately-changed password — a second
    // real incident (2026-08-22) where a dev->main redeploy silently reset the repo owner's own
    // custom password back to the ADMIN_PASSWORD env var and locked them out via Identity's
    // failed-login threshold. It must now only overwrite the hash when the account was just
    // bootstrapped this run (isNewAccount:true) or an explicit ADMIN_FORCE_PASSWORD_SYNC=true
    // opt-in is set — never on a routine restart of an already-existing account.
    [Fact]
    public async Task SyncAdminPassword_UpdatesPasswordHashInASingleAtomicWrite_ForNewlyBootstrappedAccount()
    {
        var (userManager, roleManager) = BuildMocks();
        var adminUser = SetUpSyncPasswordMocks(userManager, existingHash: null, newPassword: "NewPass!123");

        var config = BuildConfig("admin@example.com", "admin", "NewPass!123");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.SyncAdminPassword("admin@example.com", isNewAccount: true);

        Assert.Equal("hashed-value", adminUser.PasswordHash);
        await userManager.Received(1).UpdateAsync(adminUser);
        await userManager.DidNotReceive().RemovePasswordAsync(Arg.Any<ApplicationUser>());
        await userManager.DidNotReceive().AddPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SyncAdminPassword_DoesNotOverwriteExistingAccount_OnRoutineRestart()
    {
        var (userManager, roleManager) = BuildMocks();
        var adminUser = SetUpSyncPasswordMocks(userManager, existingHash: "owner-chosen-hash", newPassword: "NewPass!123");

        // No ADMIN_FORCE_PASSWORD_SYNC set — routine restart of an already-existing account.
        var config = BuildConfig("admin@example.com", "admin", "NewPass!123");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.SyncAdminPassword("admin@example.com", isNewAccount: false);

        // The owner's own password must survive an ordinary redeploy.
        Assert.Equal("owner-chosen-hash", adminUser.PasswordHash);
        await userManager.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SyncAdminPassword_OverwritesExistingAccount_WhenForceSyncFlagIsSet()
    {
        var (userManager, roleManager) = BuildMocks();
        var adminUser = SetUpSyncPasswordMocks(userManager, existingHash: "owner-chosen-hash", newPassword: "NewPass!123");

        var config = BuildConfig("admin@example.com", "admin", "NewPass!123", forcePasswordSync: "true");
        var services = Substitute.For<IServiceProvider>();
        var job = new UserManagerJob(roleManager, userManager, config, services);

        await job.SyncAdminPassword("admin@example.com", isNewAccount: false);

        Assert.Equal("hashed-value", adminUser.PasswordHash);
        await userManager.Received(1).UpdateAsync(adminUser);
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
