using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-d47: the control tables carry IsPostSeason and RequiredPicks, so pick caps, teases and
/// the postseason switch no longer live in code. The migration that adds them must backfill every
/// existing row (the 2025/2026 seasons earlier migrations seed) to the rule that was hardcoded
/// until now — the /pick-rules tables, as TestWeekConfigs models them.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PeriodRulesBackfillPostgresTests(PostgresFixture postgres) {
    [Fact]
    public async Task Migration_BackfillsEveryNflControlTableRow_ToThePickRules() {
        await using var db = postgres.OpenDb();
        var rows = await db.NflSeasonWeekConfigs.AsNoTracking().ToListAsync();
        var expected = TestWeekConfigs.Nfl(season: 0).ToDictionary(w => w.WeekId);

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(
            (expected[r.WeekId].IsPostSeason, expected[r.WeekId].RequiredPicks), (r.IsPostSeason, r.RequiredPicks)));
    }

    [Fact]
    public async Task Migration_BackfillsEveryCfbControlTableRow_ToThePickRules() {
        await using var db = postgres.OpenDb();
        var rows = await db.CfbSeasonWeekConfigs.AsNoTracking().ToListAsync();

        Assert.Contains(rows, r => r.IvLeagueWeekNumber == 99);
        Assert.All(rows, r => Assert.Equal(
            r.IvLeagueWeekNumber == 99
                ? (false, 0) // not an IV League week
                // Postseason (O/U offered, the Postseason selector) starts at Conference Championships (14).
                : (r.IvLeagueWeekNumber >= 14, TestWeekConfigs.CfbRequiredPicks(r.IvLeagueWeekNumber)),
            (r.IsPostSeason, r.RequiredPicks)));
    }

    // A real round asks for 1-4 picks: 0 (what a forgotten value would be) is rejected, not
    // silently turned into a 0-pick cap that rejects every pick and scores every week a win.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task Nfl_RequiredPicks_OutsideOneToFour_IsRejectedByTheDatabase(int requiredPicks) {
        await using var db = postgres.OpenDb();
        var row = await db.NflSeasonWeekConfigs.FirstAsync();
        row.RequiredPicks = requiredPicks;

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Cfb_RequiredPicks_ZeroIsOnlyAllowedOnExcludedWeeks() {
        await using var db = postgres.OpenDb();
        var excluded = await db.CfbSeasonWeekConfigs.FirstAsync(c => c.IvLeagueWeekNumber == 99);
        Assert.Equal(0, excluded.RequiredPicks); // excluded weeks legitimately carry 0

        var round = await db.CfbSeasonWeekConfigs.FirstAsync(c => c.IvLeagueWeekNumber == 1);
        round.RequiredPicks = 0;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // The columns have no database default after the backfill: a hand-written season row that
    // forgets them fails the insert (NOT NULL) instead of quietly getting 0 / false.
    [Fact]
    public async Task NewRow_WithoutTheRuleColumns_FailsTheInsert() {
        await using var db = postgres.OpenDb();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "NflSeasonWeekConfigs" ("Season", "WeekId", "WeekLabel", "WeekType", "ScoringFormat",
                "WeekStartDatetime", "WeekEndDatetime", "SpreadLockDatetime")
            VALUES (2099, 1, 'Week 1', 'Regular Season', 'Standard', now(), now(), now())
            """));
    }
}
