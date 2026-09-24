using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// Refresh-token rotation revokes a row on every refresh and nothing ever deleted one (prod: 4,625
// rows for ~114 live sessions); regenerating a league invite link revokes the old row the same
// way. Postgres has no row TTL, so a scheduled job clears rows that can never be accepted again.
// Plain scalar WHERE + RemoveRange (no local-list .Contains / ExecuteDelete), so InMemory is a
// legitimate provider here per CLAUDE.md.
public class DeadTokenCleanupJobTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options);

    private static Task Run(ApplicationDbContext db) =>
        new DeadTokenCleanupJob(db, new FakeTimeProvider(Now)).Execute(Substitute.For<IJobExecutionContext>());

    private static RefreshToken Token(string token, DateTimeOffset expires, DateTimeOffset? revoked = null) =>
        new() { UserId = "u1", Token = token, Expires = expires, Revoked = revoked };

    [Fact]
    public async Task Execute_DeletesRevokedAndExpiredRefreshTokens_KeepsLiveOnes() {
        await using var db = BuildDb(nameof(Execute_DeletesRevokedAndExpiredRefreshTokens_KeepsLiveOnes));
        db.RefreshTokens.AddRange(
            Token("live", Now.AddDays(10)),
            Token("revoked", Now.AddDays(10), revoked: Now.AddMinutes(-5)),
            Token("expired", Now.AddSeconds(-1)));
        await db.SaveChangesAsync();

        await Run(db);

        Assert.Equal(["live"], await db.RefreshTokens.Select(t => t.Token).ToListAsync());
    }

    // An expired-but-unrevoked link is still the league's "current" link (GetCurrentAsync shows the
    // commissioner "expired — regenerate"), so only revoked links are dead.
    [Fact]
    public async Task Execute_DeletesRevokedInviteLinks_KeepsExpiredCurrentOnes() {
        await using var db = BuildDb(nameof(Execute_DeletesRevokedInviteLinks_KeepsExpiredCurrentOnes));
        db.LeagueInviteLinks.AddRange(
            new LeagueInviteLink { Token = "revoked", LeagueId = 1, CreatedByUserId = "u1", IsRevoked = true, ExpiresAt = Now.AddHours(1) },
            new LeagueInviteLink { Token = "expired-current", LeagueId = 1, CreatedByUserId = "u1", ExpiresAt = Now.AddHours(-1) });
        await db.SaveChangesAsync();

        await Run(db);

        Assert.Equal(["expired-current"], await db.LeagueInviteLinks.Select(l => l.Token).ToListAsync());
    }

    [Fact]
    public async Task Execute_WithNothingDead_IsANoOp() {
        await using var db = BuildDb(nameof(Execute_WithNothingDead_IsANoOp));
        db.RefreshTokens.Add(Token("live", Now.AddDays(1)));
        await db.SaveChangesAsync();

        await Run(db);

        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }
}
