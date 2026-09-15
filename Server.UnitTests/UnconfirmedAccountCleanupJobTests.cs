using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-o23 follow-up: 24h TTL for accounts that never confirm their email. Query is a plain
/// scalar-comparison WHERE (no bulk ExecuteDeleteAsync/ExecuteUpdateAsync .Contains() filter), so
/// EF Core InMemory is a legitimate provider here per CLAUDE.md's EF Core bulk-op gotcha — that
/// gotcha is specifically about write-side Npgsql translation, not this read-only SELECT.
/// </summary>
public class UnconfirmedAccountCleanupJobTests {
    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static UserManager<ApplicationUser> BuildUserManager() {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static ApplicationUser BuildUser(string id, bool emailConfirmed, DateTimeOffset createdAt) =>
        new() { Id = id, UserName = $"user-{id}", Email = $"user-{id}@example.com", EmailConfirmed = emailConfirmed, CreatedAt = createdAt };

    private static IJobExecutionContext BuildContext() => Substitute.For<IJobExecutionContext>();

    [Fact]
    public async Task Execute_DeletesAccountUnconfirmedPast24Hours() {
        var db = BuildDb(nameof(Execute_DeletesAccountUnconfirmedPast24Hours));
        var stale = BuildUser("stale-1", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-25));
        db.Users.Add(stale);
        await db.SaveChangesAsync();

        var userManager = BuildUserManager();
        userManager.DeleteAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);

        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-1"));
    }

    [Fact]
    public async Task Execute_DoesNotDeleteUnconfirmedAccountStillWithinThe24HourWindow() {
        var db = BuildDb(nameof(Execute_DoesNotDeleteUnconfirmedAccountStillWithinThe24HourWindow));
        var recent = BuildUser("recent-1", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-1));
        db.Users.Add(recent);
        await db.SaveChangesAsync();

        var userManager = BuildUserManager();
        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task Execute_DoesNotDeleteConfirmedAccountsRegardlessOfAge() {
        var db = BuildDb(nameof(Execute_DoesNotDeleteConfirmedAccountsRegardlessOfAge));
        var old = BuildUser("old-confirmed-1", emailConfirmed: true, createdAt: DateTimeOffset.UtcNow.AddYears(-1));
        db.Users.Add(old);
        await db.SaveChangesAsync();

        var userManager = BuildUserManager();
        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    // A logged, non-fatal failure for one account must not stop the sweep from reaching the rest —
    // mirrors AuthController.DeleteUser's own DbUpdateException resilience (frizat-5rp).
    [Fact]
    public async Task Execute_ContinuesToNextAccountWhenOneDeleteFails() {
        var db = BuildDb(nameof(Execute_ContinuesToNextAccountWhenOneDeleteFails));
        var first = BuildUser("stale-1", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-48));
        var second = BuildUser("stale-2", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-48));
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();

        var userManager = BuildUserManager();
        userManager.DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-1"))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "db error" }));
        userManager.DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-2"))
            .Returns(IdentityResult.Success);

        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-1"));
        await userManager.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-2"));
    }

    // /simplify: a THROWN DbUpdateException (an un-cascaded FK, the exact scenario
    // AuthController.DeleteUser's own try/catch exists for, frizat-5rp) is a different failure
    // mode than a failed (non-throwing) IdentityResult above — must be caught per-user too, not
    // just per-user failed results, or one bad row aborts the entire hourly batch uncaught.
    [Fact]
    public async Task Execute_ContinuesToNextAccountWhenOneDeleteThrowsDbUpdateException() {
        var db = BuildDb(nameof(Execute_ContinuesToNextAccountWhenOneDeleteThrowsDbUpdateException));
        var first = BuildUser("stale-1", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-48));
        var second = BuildUser("stale-2", emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddHours(-48));
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();

        var userManager = BuildUserManager();
        userManager.DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-1"))
            .ThrowsAsync(new DbUpdateException("duplicate key value violates unique constraint"));
        userManager.DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-2"))
            .Returns(IdentityResult.Success);

        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-1"));
        await userManager.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(u => u.Id == "stale-2"));
    }

    [Fact]
    public async Task Execute_NoStaleAccounts_NeverCallsDeleteAsync() {
        var db = BuildDb(nameof(Execute_NoStaleAccounts_NeverCallsDeleteAsync));
        var userManager = BuildUserManager();
        var job = new UnconfirmedAccountCleanupJob(db, userManager);
        await job.Execute(BuildContext());

        await userManager.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public void HasDisallowConcurrentExecutionAttribute() {
        var attr = typeof(UnconfirmedAccountCleanupJob)
            .GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), inherit: false);
        Assert.NotEmpty(attr);
    }
}
