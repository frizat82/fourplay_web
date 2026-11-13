using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Real Postgres (CLAUDE.md: deletes driven by a local key list must be proven against Npgsql, not
// SQLite). Season 2091 keeps these rows apart from anything else in the shared container.
[Collection(PostgresCollection.Name)]
public class CfbRankingRemovalTests(PostgresFixture pg)
{
    private const int Season = 2091;

    private static CfbRanking Rank(int week, string team, int rank, int season = Season) =>
        new() { Season = season, WeekNumber = week, TeamAbbreviation = team, CuratedRank = rank };

    [Fact]
    public async Task RemoveRankingsAsync_DeletesOnlyTheNamedTeamWeeks()
    {
        var repo = new CfbRepository(pg.Factory());
        // The prod shape: NU's stale #20 next to OSU's current #20 in week 11, and NU's week-10
        // rank, which a week-11 capture must not touch.
        await repo.AddRankingsAsync([Rank(10, "NU", 20), Rank(11, "NU", 20), Rank(11, "OSU", 20)]);

        var removed = await repo.RemoveRankingsAsync([new CfbUnrankedSighting(Season, 11, "NU")]);

        await using var db = pg.OpenDb();
        var left = await db.CfbRankings.Where(r => r.Season == Season)
            .OrderBy(r => r.WeekNumber).ThenBy(r => r.TeamAbbreviation)
            .Select(r => r.WeekNumber + " " + r.TeamAbbreviation + " " + r.CuratedRank).ToListAsync();
        Assert.Equal(["10 NU 20", "11 OSU 20"], left);
        Assert.Equal(1, removed);
    }

    [Fact]
    public async Task RemoveRankingsAsync_NoStoredRowForTheTeam_IsANoOp()
    {
        var repo = new CfbRepository(pg.Factory());
        await repo.AddRankingsAsync([Rank(5, "UGA", 1, season: Season + 1)]);

        await repo.RemoveRankingsAsync([new CfbUnrankedSighting(Season + 1, 5, "NOPE")]);

        await using var db = pg.OpenDb();
        Assert.True(await db.CfbRankings.AnyAsync(r => r.Season == Season + 1 && r.WeekNumber == 5 && r.TeamAbbreviation == "UGA"));
    }
}
