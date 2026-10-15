using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-b4g: the migration that drops CfbSlates must carry every real CFB row across — prod has
/// hundreds of picks, spreads and scores pointing at slate ids. Runs the real migration on real
/// Postgres, from the migration before it, with slate ids deliberately shuffled against the
/// control-table ids (so a wrong join, or a re-point that collides with the unique indexes,
/// shows up).
/// </summary>
public sealed class OneCfbControlTableMigrationFixture : IAsyncLifetime {
    private const string Before = "20261014231648_CfbWeekLabelOnControlTable";
    public const int Season = 2026;
    public const string UserId = "b4g-user";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithCleanUp(true)
        .Build();

    /// <summary>Control-table row id per in-scope week number, read before the migration.</summary>
    public Dictionary<int, int> ConfigIdByWeek { get; } = [];
    /// <summary>The slate id each week had before the migration (never its control-table id).</summary>
    public Dictionary<int, int> SlateIdByWeek { get; } = [];
    public int LeagueId { get; private set; }

    public async Task InitializeAsync() {
        await _container.StartAsync();
        await using (var db = OpenDb()) {
            await db.GetService<IMigrator>().MigrateAsync(Before);

            for (var week = 1; week <= 18; week++) {
                ConfigIdByWeek[week] = await db.Database.SqlQuery<int>($"""
                    SELECT "Id" AS "Value" FROM "CfbSeasonWeekConfigs"
                    WHERE "Season" = {Season} AND "IvLeagueWeekNumber" = {week} AND "InScopeIvLeague"
                    """).SingleAsync();
            }
            // Each week's slate takes the NEXT week's control-table id: every reference has to move,
            // and moving them one at a time would collide on the (CfbSlateId, HomeTeam) indexes.
            foreach (var (week, _) in ConfigIdByWeek) {
                SlateIdByWeek[week] = ConfigIdByWeek[week % 18 + 1];
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO "CfbSlates" ("Id", "Season", "SlateNumber", "StartDate", "EndDate", "ScoringFormat")
                    VALUES ({SlateIdByWeek[week]}, {Season}, {week}, DATE '2026-09-01', DATE '2026-09-07', 'Standard')
                    """);
            }

            db.Users.Add(new ApplicationUser { Id = UserId, UserName = "b4g", Email = "b4g@test.local" });
            var league = new LeagueInfo { LeagueName = "b4g", OwnerUserId = UserId, LeagueType = LeagueType.Cfb };
            db.LeagueInfo.Add(league);
            await db.SaveChangesAsync();
            LeagueId = league.Id;

            foreach (var (week, slateId) in SlateIdByWeek) {
                db.CfbSpreads.Add(new CfbSpreads { CfbSlateId = slateId, HomeTeam = "HOME", AwayTeam = $"AWAY{week}", GameTime = DateTimeOffset.UtcNow });
                db.CfbScores.Add(new CfbScores { CfbSlateId = slateId, HomeTeam = "HOME", AwayTeam = $"AWAY{week}", GameStatus = TypeName.StatusFinal, GameTime = DateTimeOffset.UtcNow });
                db.CfbPicks.Add(new CfbPicks { CfbSlateId = slateId, Season = Season, LeagueId = LeagueId, UserId = UserId, Team = "HOME" });
                db.TeamLiveNotificationStates.Add(new TeamLiveNotificationState {
                    Sport = LeagueType.Cfb, LeagueId = LeagueId, Team = "HOME", PickType = PickType.Spread, Period = slateId });
            }
            // An NFL row with the same Period value is NFL's week id, not a slate: it must not move.
            db.TeamLiveNotificationStates.Add(new TeamLiveNotificationState {
                Sport = LeagueType.Nfl, LeagueId = LeagueId, Team = "KC", PickType = PickType.Spread, Period = SlateIdByWeek[1] });
            await db.SaveChangesAsync();

            // Rankings were keyed by ESPN's week number: the semifinal's ESPN week, plus one ESPN
            // week that has no in-scope row (captured for no slate).
            var semisEspnWeek = await db.Database.SqlQuery<int>($"""
                SELECT "EspnWeekNumber" AS "Value" FROM "CfbSeasonWeekConfigs" WHERE "Id" = {ConfigIdByWeek[17]}
                """).SingleAsync();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "CfbRankings" ("Season", "EspnWeekNumber", "TeamAbbreviation", "CuratedRank", "EspnEventId", "CapturedAtUtc")
                VALUES ({Season}, {semisEspnWeek}, 'IU', 1, 1, now()), ({Season}, 777, 'OSU', 2, 2, now())
                """);
        }

        await using (var db = OpenDb()) await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext OpenDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_container.GetConnectionString()).Options);
}

public class OneCfbControlTableMigrationTests(OneCfbControlTableMigrationFixture f) : IClassFixture<OneCfbControlTableMigrationFixture> {
    private const int Season = OneCfbControlTableMigrationFixture.Season;

    [Fact]
    public async Task SpreadsScoresAndPicks_PointAtTheirWeeksControlTableRow() {
        await using var db = f.OpenDb();
        var expected = f.ConfigIdByWeek.ToDictionary(kv => $"AWAY{kv.Key}", kv => kv.Value);

        var spreads = await db.CfbSpreads.ToListAsync();
        var scores = await db.CfbScores.ToListAsync();
        Assert.Equal(18, spreads.Count);
        Assert.All(spreads, s => Assert.Equal(expected[s.AwayTeam], s.CfbSlateId));
        Assert.Equal(18, scores.Count);
        Assert.All(scores, s => Assert.Equal(expected[s.AwayTeam], s.CfbSlateId));
        Assert.Equal(f.ConfigIdByWeek.Values.Order(), (await db.CfbPicks.Select(p => p.CfbSlateId).ToListAsync()).Order());
    }

    [Fact]
    public async Task CfbNotificationStates_FollowTheirSlate_NflOnesStayPut() {
        await using var db = f.OpenDb();

        var cfb = await db.TeamLiveNotificationStates.Where(t => t.Sport == LeagueType.Cfb).Select(t => t.Period).ToListAsync();
        var nfl = await db.TeamLiveNotificationStates.SingleAsync(t => t.Sport == LeagueType.Nfl);

        Assert.Equal(f.ConfigIdByWeek.Values.Order(), cfb.Order());
        Assert.Equal(f.SlateIdByWeek[1], nfl.Period);
    }

    [Fact]
    public async Task Rankings_AreKeyedByOurWeek_AndUnmatchedOnesAreDropped() {
        await using var db = f.OpenDb();

        var ranking = Assert.Single(await db.CfbRankings.ToListAsync());

        Assert.Equal(("IU", 17), (ranking.TeamAbbreviation, ranking.WeekNumber));
    }

    [Fact]
    public async Task CfbRows_GetTheSharedEnums_AndSkippedWeeksLoseTheirNumber() {
        await using var db = f.OpenDb();
        var rows = await db.CfbSeasonWeekConfigs.Where(c => c.Season == Season).ToListAsync();

        Assert.All(rows.Where(r => !r.InScopeIvLeague), r => {
            Assert.Null(r.IvLeagueWeekNumber);
            Assert.Equal((ScoringFormat.None, 0), (r.ScoringFormat, r.RequiredPicks));
        });
        var byWeek = rows.Where(r => r.InScopeIvLeague).ToDictionary(r => r.IvLeagueWeekNumber!.Value);
        Assert.Equal(Enumerable.Range(1, 18), byWeek.Keys.Order());
        Assert.All(Enumerable.Range(1, 13), w => Assert.Equal((WeekType.RegularSeason, ScoringFormat.Standard), (byWeek[w].WeekType, byWeek[w].ScoringFormat)));
        Assert.Equal((WeekType.ConferenceChampionships, ScoringFormat.Standard), (byWeek[14].WeekType, byWeek[14].ScoringFormat));
        Assert.Equal((WeekType.Playoff, ScoringFormat.Divisional), (byWeek[15].WeekType, byWeek[15].ScoringFormat));
        Assert.Equal((WeekType.Playoff, ScoringFormat.Divisional), (byWeek[16].WeekType, byWeek[16].ScoringFormat));
        Assert.Equal((WeekType.Playoff, ScoringFormat.Conference), (byWeek[17].WeekType, byWeek[17].ScoringFormat));
        Assert.Equal((WeekType.Playoff, ScoringFormat.Final), (byWeek[18].WeekType, byWeek[18].ScoringFormat));
    }

    [Fact]
    public async Task NflRows_GetTheSharedEnums_AndAreAllInScope() {
        await using var db = f.OpenDb();
        var rows = await db.NflSeasonWeekConfigs.ToListAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => {
            Assert.True(r.InScopeIvLeague);
            Assert.Equal(r.WeekId > 18 ? WeekType.Playoff : WeekType.RegularSeason, r.WeekType);
            Assert.Equal(r.WeekId switch {
                <= 18 => ScoringFormat.Standard, 19 or 20 => ScoringFormat.Divisional, 21 => ScoringFormat.Conference, _ => ScoringFormat.Final,
            }, r.ScoringFormat);
        });
    }

    [Fact]
    public async Task CfbSlatesTable_IsGone() {
        await using var db = f.OpenDb();

        var exists = await db.Database.SqlQuery<bool>($"""SELECT to_regclass('"CfbSlates"') IS NOT NULL AS "Value" """).SingleAsync();

        Assert.False(exists);
    }
}
