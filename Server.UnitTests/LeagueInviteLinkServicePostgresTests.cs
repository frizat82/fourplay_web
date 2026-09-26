using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// LeagueInviteLinkService revokes links with a bulk ExecuteUpdateAsync, which CLAUDE.md requires
/// to be proven on real Postgres (a SQLite pass says nothing about Npgsql's translation). Its filter
/// is scalar (league id + not revoked); a future `.Contains(ids)`-shaped bulk update would need its
/// own test here. LeagueInviteLinkServiceTests covers the rest on SQLite.
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

    // The container is shared across the collection: leave nothing behind.
    private async Task DeleteSeededAsync(string ownerId) {
        await using var db = postgres.OpenDb();
        db.LeagueInviteLinks.RemoveRange(await db.LeagueInviteLinks.Where(l => l.CreatedByUserId == ownerId).ToListAsync());
        db.LeagueInfo.RemoveRange(await db.LeagueInfo.Where(l => l.OwnerUserId == ownerId).ToListAsync());
        db.Users.RemoveRange(await db.Users.Where(u => u.Id == ownerId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private async Task<List<LeagueInviteLink>> LinksAsync(int leagueId) {
        await using var db = postgres.OpenDb();
        return await db.LeagueInviteLinks.Where(l => l.LeagueId == leagueId).ToListAsync();
    }

    [Fact]
    public async Task GenerateAsync_RevokesTheLeaguesActiveLink_OnPostgres() {
        var (ownerId, leagueA, leagueB) = await SeedTwoLeaguesWithActiveLinksAsync();
        try {
            var fresh = await new LeagueInviteLinkService(postgres.Factory()).GenerateAsync(leagueA, ownerId);

            var linksA = await LinksAsync(leagueA);
            Assert.Equal(2, linksA.Count);
            Assert.Equal(fresh.Token, Assert.Single(linksA, l => !l.IsRevoked).Token);
            Assert.False(Assert.Single(await LinksAsync(leagueB)).IsRevoked);
        } finally {
            await DeleteSeededAsync(ownerId);
        }
    }

    [Fact]
    public async Task RevokeAsync_RevokesOnlyThatLeaguesActiveLink_OnPostgres() {
        var (ownerId, leagueA, leagueB) = await SeedTwoLeaguesWithActiveLinksAsync();
        try {
            await new LeagueInviteLinkService(postgres.Factory()).RevokeAsync(leagueA);

            // Revoked, and no replacement created.
            Assert.True(Assert.Single(await LinksAsync(leagueA)).IsRevoked);
            Assert.False(Assert.Single(await LinksAsync(leagueB)).IsRevoked);
        } finally {
            await DeleteSeededAsync(ownerId);
        }
    }
}
