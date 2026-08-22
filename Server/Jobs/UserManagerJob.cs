using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.AspNetCore.Identity;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;
[DisallowConcurrentExecution]
public class UserManagerJob(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IServiceProvider services)
    : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Executing UserManagerJob");
        await CreateRolesAndAdminUser();

        if (configuration["DEMO_MODE"] == "true")
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
    }

    private async Task CreateRolesAndAdminUser() {
        Log.Information("Adding roles and assigning Admins");
        const string adminRoleName = "Administrator";
        string[] roleNames = [adminRoleName, "User", "LeagueManager"];

        foreach (var roleName in roleNames) {
            await CreateRole(roleName);
        }

        var adminUserEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL configuration is required");
        var wasNewlyCreated = await CreateUser(adminUserEmail);
        await SyncAdminPassword(adminUserEmail, wasNewlyCreated);
        await AddUserToRole(adminUserEmail, adminRoleName);
        Log.Information("UserManagerJob completed successfully");
    }

    /// <summary>
    /// Sync admin password from configuration. Only overwrites the password hash when the
    /// account was just bootstrapped this run (<paramref name="isNewAccount"/>), or when the
    /// operator has explicitly opted into a forced resync via ADMIN_FORCE_PASSWORD_SYNC=true.
    /// </summary>
    // frizat: RemovePasswordAsync then AddPasswordAsync used to do this as two separate writes to
    // the same row — a real incident hit an EF optimistic-concurrency conflict on the second write
    // (something else touched the row between the two SaveChanges) and left the admin account
    // passwordless until the next redeploy. Hashing directly and writing PasswordHash via a single
    // UpdateAsync closes that window entirely — one write, nothing in between to race against.
    //
    // frizat-wyo: this used to run unconditionally on EVERY startup, which silently overwrote an
    // already-existing admin's deliberately-changed password back to ADMIN_PASSWORD on every
    // redeploy — a real incident (2026-08-22) that locked the repo owner out of their own account
    // via Identity's failed-login lockout. Now it only fires for a brand-new account (unchanged
    // bootstrap/recovery guarantee) or when explicitly forced via ADMIN_FORCE_PASSWORD_SYNC=true,
    // which an operator sets for one redeploy to recover a genuinely-forgotten custom password.
    internal async Task SyncAdminPassword(string emailAddress, bool isNewAccount) {
        var forceResyncRequested = configuration["ADMIN_FORCE_PASSWORD_SYNC"] == "true";
        if (!isNewAccount && !forceResyncRequested) {
            Log.Information(
                "Admin account {Email} already exists — skipping password sync to preserve any password the owner has since set. Set ADMIN_FORCE_PASSWORD_SYNC=true for one redeploy to force a resync to ADMIN_PASSWORD.",
                emailAddress);
            return;
        }

        // frizat-wyo: log the forced-override case loudly and distinctly from a routine bootstrap
        // sync — this is the one path that still overwrites an existing admin's password, and it
        // depends on an operator remembering to unset ADMIN_FORCE_PASSWORD_SYNC afterward. A clear
        // "FORCED" warning here (versus a generic Information line) makes it obvious in the logs
        // that a real password overwrite just happened, in case the flag was left on by mistake.
        if (!isNewAccount && forceResyncRequested) {
            Log.Warning(
                "ADMIN_FORCE_PASSWORD_SYNC=true — forcing a password resync for existing admin {Email}. Unset this variable after this deploy or it will resync (overwrite) the password again on every subsequent restart.",
                emailAddress);
        }

        var adminPassword = configuration["ADMIN_PASSWORD"] ?? throw new InvalidOperationException("ADMIN_PASSWORD not set");
        var adminUser = await userManager.FindByEmailAsync(emailAddress);
        if (adminUser == null) return;
        adminUser.PasswordHash = userManager.PasswordHasher.HashPassword(adminUser, adminPassword);
        var result = await userManager.UpdateAsync(adminUser);
        if (result.Succeeded)
            Log.Information("Admin password synced for {Email} (newAccount={IsNewAccount}, forced={Forced})", emailAddress, isNewAccount, forceResyncRequested);
        else
            Log.Error("Failed to sync admin password: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Create a role if not exists.
    /// </summary>
    /// <param name="roleName">Role Name</param>
    internal async Task CreateRole(string roleName) {

        var roleExists = await roleManager.RoleExistsAsync(roleName);

        if (!roleExists) {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            Log.Information("New Role {RoleName} {@Identity}", roleName, result);
        }
    }
    /// <summary>
    /// Create a user if not exists.
    /// </summary>
    /// <param name="emailAddress">email</param>
    /// <returns>true if a new account was created this call; false if it already existed.</returns>
    internal async Task<bool> CreateUser(string emailAddress) {
        var adminPassword = configuration["ADMIN_PASSWORD"] ?? throw new InvalidOperationException("ADMIN_PASSWORD not set");
        var adminUser = await userManager.FindByEmailAsync(emailAddress);
        if (adminUser != null) {
            Log.Information("Admin user {Email} already exists — skipping create", emailAddress);
            return false;
        }

        Log.Warning("Admin user {Email} not found — creating new user. If this is unexpected, check why the user was deleted or the DB was reset.", emailAddress);
        adminUser = new ApplicationUser()
        {
            UserName = configuration["ADMIN_USERNAME"] ?? throw new InvalidOperationException("ADMIN_USERNAME configuration is required"),
            Email = emailAddress,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (!result.Succeeded)
        {
            Log.Error("Failed to create admin user {Email}. Errors: {Errors}",
                emailAddress,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            throw new InvalidOperationException("Failed to create admin user.");
        }

        return true;
    }


    /// <summary>
    /// Add user to a role if the user exists, otherwise, create the user and adds him to the role.
    /// </summary>
    internal async Task AddUserToRole(string userEmail,
        string roleName) {
        var adminUser = await userManager.FindByEmailAsync(userEmail);
        if (adminUser is null) {
            Log.Error("AddUserToRole: user {Email} not found — cannot assign role {Role}", userEmail, roleName);
            return;
        }

        // Ensure user is in role
        if (!await userManager.IsInRoleAsync(adminUser!, roleName)) {
            await userManager.AddToRoleAsync(adminUser!, roleName);
            Log.Information("User Added To Role {@Identity}", roleName);
        }
    }

}
