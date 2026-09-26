using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// LeagueInviteLinkService revokes links with a bulk ExecuteUpdateAsync. CLAUDE.md: a bulk EF
/// operation must be proven on real Postgres — SQLite translates filters Npgsql can silently
/// no-op on, leaving rows untouched. LeagueInviteLinkServiceTests covers the rest on SQLite.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LeagueInviteLinkServicePostgresTests(PostgresFixture postgres) {
    private async Task<(string ownerId, int leagueA, int leagueB)> SeedTwoLeaguesWithActiveLinksAsync() {
        await using var db = postgres.OpenDb();
        var owner = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = $"invite-{Guid.NewGuid():N}", Email = "invite-owner@demo.local" };
        db.Users.Add(owner);
        var a = new LeagueInfo { LeagueName = $"Invite A {Guid.NewGuid()}", OwnerUserId = owner.Id };
        var b = new LeagueInfo { LeagueName = $"Invite B {Guid.NewGuid()}", OwnerUserId = owner.Id };
        db.LeagueInfo.AddRange(a, b);
        await db.SaveChangesAsync();
        db.LeagueInviteLinks.AddRange(ActiveLink(a.Id, owner.Id), ActiveLink(b.Id, owner.Id));
        await db.SaveChangesAsync();
        return (owner.Id, a.Id, b.Id);
    }

    private static LeagueInviteLink ActiveLink(int leagueId, string ownerId) => new() {
        Token = Guid.NewGuid().ToString("N"), LeagueId = leagueId, CreatedByUserId = ownerId,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12), IsRevoked = false,
    };

    private async Task<List<LeagueInviteLink>> LinksAsync(int leagueId) {
        await using var db = postgres.OpenDb();
        return await db.LeagueInviteLinks.Where(l => l.LeagueId == leagueId).ToListAsync();
    }

    [Fact]
    public async Task GenerateAsync_RevokesTheLeaguesActiveLink_OnPostgres() {
        var (ownerId, leagueA, leagueB) = await SeedTwoLeaguesWithActiveLinksAsync();

        var fresh = await new LeagueInviteLinkService(postgres.Factory()).GenerateAsync(leagueA, ownerId);

        var linksA = await LinksAsync(leagueA);
        Assert.Equal(2, linksA.Count);
        Assert.Equal(fresh.Token, Assert.Single(linksA, l => !l.IsRevoked).Token);
        Assert.False(Assert.Single(await LinksAsync(leagueB)).IsRevoked);
    }

    [Fact]
    public async Task RevokeAsync_RevokesOnlyThatLeaguesActiveLink_OnPostgres() {
        var (_, leagueA, leagueB) = await SeedTwoLeaguesWithActiveLinksAsync();

        await new LeagueInviteLinkService(postgres.Factory()).RevokeAsync(leagueA);

        Assert.True(Assert.Single(await LinksAsync(leagueA)).IsRevoked);
        Assert.False(Assert.Single(await LinksAsync(leagueB)).IsRevoked);
    }
}
