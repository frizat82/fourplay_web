using FourPlayWebApp.Server.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

// Refresh-token rotation revokes the old row on every refresh, and regenerating a league invite
// link revokes the old link — nothing ever deleted either (prod: 4,625 refresh tokens for ~114
// live sessions). Neither can be accepted again (RefreshTokenService/LeagueInviteLinkService both
// reject revoked/expired rows), and Postgres has no row TTL, so this clears them on a schedule
// instead of on the sign-in hot path. An expired-but-unrevoked invite link is kept: it's still the
// league's "current" link (the commissioner sees "expired — regenerate").
// Load + RemoveRange rather than ExecuteDelete (CLAUDE.md's Npgsql bulk-op gotcha).
[DisallowConcurrentExecution]
public class DeadTokenCleanupJob(ApplicationDbContext db, TimeProvider timeProvider) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var now = timeProvider.GetUtcNow();
        var tokens = await db.RefreshTokens.Where(t => t.Revoked != null || t.Expires < now).ToListAsync();
        var links = await db.LeagueInviteLinks.Where(l => l.IsRevoked).ToListAsync();
        if (tokens.Count == 0 && links.Count == 0) return;

        db.RefreshTokens.RemoveRange(tokens);
        db.LeagueInviteLinks.RemoveRange(links);
        await db.SaveChangesAsync();
        Log.Information("DeadTokenCleanupJob: deleted {Tokens} refresh token(s), {Links} invite link(s)", tokens.Count, links.Count);
    }
}
