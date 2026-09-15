using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// frizat-o23 follow-up: an account that never confirms its email sits in AspNetUsers forever
// with no expiry today — the real risk isn't unauthorized login (Program.cs's
// RequireConfirmedEmail/RequireConfirmedAccount already blocks that), it's that an
// abandoned/typo'd registration permanently occupies someone else's real email address —
// FindByEmailAsync already finds that row, blocking the real owner from ever registering fresh
// with their own address. 24h matches this app's invite-only, low-volume registration flow —
// there's no attacker incentive to justify a shorter, stingier window here.
[DisallowConcurrentExecution]
public class UnconfirmedAccountCleanupJob(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : IJob {
    internal static readonly TimeSpan ConfirmationWindow = TimeSpan.FromHours(24);

    public async Task Execute(IJobExecutionContext context) {
        var cutoff = DateTimeOffset.UtcNow - ConfirmationWindow;
        var stale = await db.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoff)
            .ToListAsync();

        if (stale.Count == 0) return;

        Log.Information(
            "UnconfirmedAccountCleanupJob: deleting {Count} account(s) unconfirmed past {Hours}h",
            stale.Count, ConfirmationWindow.TotalHours);

        foreach (var user in stale) {
            // UserManager.DeleteAsync, not a raw DB delete — the same path AuthController.DeleteUser
            // uses (frizat-5rp: every FK referencing AspNetUsers cascades at the DB level, so this
            // is already a safe, well-exercised deletion path, not new cascade logic to get right).
            // /simplify: mirrors DeleteUser's own DbUpdateException handling too — one un-cascaded
            // FK on one row must not abort the whole hourly batch, leaving the rest undeleted with
            // no log until the next tick retries from scratch.
            try {
                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded) {
                    Log.Error(
                        "UnconfirmedAccountCleanupJob: failed to delete {UserId}: {Errors}",
                        user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            } catch (DbUpdateException ex) {
                Log.Error(ex, "UnconfirmedAccountCleanupJob: unhandled DB error deleting {UserId}", user.Id);
            }
        }
    }
}
