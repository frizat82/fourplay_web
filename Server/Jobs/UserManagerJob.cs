using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;
[DisallowConcurrentExecution]
public class UserManagerJob(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ApplicationDbContext db)
    : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Executing UserManagerJob");
        await CreateRolesAndAdminUser();
    }

    private async Task CreateRolesAndAdminUser() {
        Log.Information("Adding roles and assigning Admins");
        const string adminRoleName = "Administrator";
        string[] roleNames = [adminRoleName, "User", "LeagueManager"];

        foreach (var roleName in roleNames) {
            await CreateRole(roleName);
        }
        
        var adminUserEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL configuration is required");
        await CreateUser(adminUserEmail);
        await CreateBaseLeagueUser(adminUserEmail);
        await AddUserToRole(adminUserEmail, adminRoleName);
        Log.Information("UserManagerJob completed successfully");
    }
    /// <summary>
    /// Create base user
    /// </summary>
    internal async Task CreateBaseLeagueUser(string userEmail) {
        try {
            if (await db.LeagueUsers.AnyAsync(x => x.Email == userEmail)) {
                return;
            }
            var user = new LeagueUsers() { Email = userEmail };
            await db.LeagueUsers.AddAsync(user);
            await db.SaveChangesAsync();
            Log.Information("Base user created {@Identity}", user);
        }
        catch (Exception ex) {
            Log.Error(ex, "Unable to create base user {UserName}", userEmail);
        }
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
    internal async Task CreateUser(string emailAddress) {
        // Ensure user exists
        var adminUser = await userManager.FindByEmailAsync(emailAddress);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser()
            {
                UserName = configuration["ADMIN_USERNAME"] ?? throw new InvalidOperationException("ADMIN_USERNAME configuration is required"),
                Email = emailAddress,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, configuration["ADMIN_PASSWORD"] ?? throw new Exception("ADMIN_PASSWORD not set"));
            if (!result.Succeeded)
            {
                Log.Error("Failed to create admin user {Email}. Errors: {Errors}",
                    emailAddress,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                throw new Exception("Failed to create admin user.");
            }
        }
    }


    /// <summary>
    /// Add user to a role if the user exists, otherwise, create the user and adds him to the role.
    /// </summary>
    internal async Task AddUserToRole(string userEmail,
        string roleName) {
        var adminUser = await userManager.FindByEmailAsync(userEmail);
        if (adminUser is null) {
            Log.Information("Admin User Found {@Identity}", adminUser);
            return;
        }

        // Ensure user is in role
        if (!await userManager.IsInRoleAsync(adminUser!, roleName)) {
            await userManager.AddToRoleAsync(adminUser!, roleName);
            Log.Information("User Added To Role {@Identity}", roleName);
        }
    }

}