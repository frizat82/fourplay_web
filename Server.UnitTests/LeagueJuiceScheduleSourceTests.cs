using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-ugs: LeagueJuiceScheduleSource computes when to remind a league owner (2 days before
// lock) and when to auto-fill Juice (at lock — 2pm America/Chicago on the season's first game
// date) for EVERY league × season combination found in the NFL/CFB season-config tables. Nothing
// here ever names a specific year — it's driven entirely by whatever rows exist, exactly like the
// existing NflSpreadScheduleSource/CfbSpreadScheduleSource it mirrors. The
// NeverHardcodesASeason_ArbitraryFutureSeasonIsPickedUpAutomatically test below exists specifically
// to prove that: this is precisely the failure mode that caused CfbCurrentSlateService's
// `ConfiguredSeason = 2026` bug (a hardcoded year silently going stale every season rollover).
public class LeagueJuiceScheduleSourceTests
{
    private readonly ILeagueRepository _leagueRepo;
    private readonly ICfbRepository _cfbRepo;

    public LeagueJuiceScheduleSourceTests()
    {
        _leagueRepo = Substitute.For<ILeagueRepository>();
        _cfbRepo = Substitute.For<ICfbRepository>();
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig>());
        _cfbRepo.GetAllWeekConfigsAsync().Returns((IEnumerable<CfbSeasonWeekConfig>)new List<CfbSeasonWeekConfig>());
        _leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo>());
        _leagueRepo.GetJuiceRemindersSentAsync().Returns(new HashSet<(int, int)>());
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private LeagueJuiceScheduleSource BuildSource(DateTimeOffset now) => new(_leagueRepo, _cfbRepo, new FakeTimeProvider(now));

    private static LeagueInfo MakeLeague(int id, LeagueType type, params LeagueJuiceMapping[] mappings) => new() {
        Id = id, LeagueName = $"League {id}", OwnerUserId = "owner", LeagueType = type,
        LeagueJuiceMappings = mappings,
    };

    private static NflSeasonWeekConfig MakeNflWeek1(int season, DateTime firstGameUtc) => new() {
        Season = season, WeekId = 1, WeekLabel = "Week 1", WeekType = "Regular Season", ScoringFormat = "Standard",
        FirstGameOfWeekStartDatetime = firstGameUtc,
    };

    private static CfbSeasonWeekConfig MakeCfbSlate1(int season, DateOnly startDate) => new() {
        Season = season, EspnWeekNumber = 1, IvLeagueWeekNumber = 1, WeekStartDate = startDate,
        WeekEndDate = startDate.AddDays(6), InScopeIvLeague = true,
    };

    // ── No hardcoded year ──────────────────────────────────────────────────────

    [Fact]
    public async Task NeverHardcodesASeason_ArbitraryFutureSeasonIsPickedUpAutomatically()
    {
        // 2031 appears nowhere in LeagueJuiceScheduleSource's source code — if this passes, the
        // source is proven to be driven entirely by whatever season rows exist in the DB, not by
        // any assumption about "the current season."
        const int madeUpFutureSeason = 2031;
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(madeUpFutureSeason, new DateTime(2031, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2031, 8, 1, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Contains(locks, c => c.Identity.Contains("1-2031"));
        Assert.Contains(reminders, c => c.Identity.Contains("1-2031"));
    }

    // ── Lock/reminder time computation ───────────────────────────────────────────

    [Fact]
    public async Task Nfl_LockTime_Is2PmCentralOnFirstGameDate_CDT()
    {
        // Sept 4 is inside daylight saving — Central Daylight Time (UTC-5).
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        var (_, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        var lockCandidate = Assert.Single(locks);
        Assert.Equal(new DateTime(2025, 9, 4, 19, 0, 0, DateTimeKind.Utc), lockCandidate.LockTime); // 2pm CDT = 19:00 UTC
    }

    [Fact]
    public async Task Nfl_LockTime_Is2PmCentralOnFirstGameDate_CST_AcrossDstBoundary()
    {
        // January is outside daylight saving — Central Standard Time (UTC-6). Proves the
        // conversion is genuinely DST-aware, not a fixed offset.
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2026, new DateTime(2027, 1, 8, 18, 0, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        var (_, locks) = await BuildSource(new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        var lockCandidate = Assert.Single(locks);
        Assert.Equal(new DateTime(2027, 1, 8, 20, 0, 0, DateTimeKind.Utc), lockCandidate.LockTime); // 2pm CST = 20:00 UTC
    }

    [Fact]
    public async Task ReminderTime_IsExactlyTwoDaysBeforeLockTime()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Equal(locks.Single().LockTime.AddDays(-2), reminders.Single().LockTime);
    }

    [Fact]
    public async Task Cfb_LockTime_Is2PmCentralOnSlate1StartDate()
    {
        _cfbRepo.GetAllWeekConfigsAsync().Returns((IEnumerable<CfbSeasonWeekConfig>)new List<CfbSeasonWeekConfig> {
            MakeCfbSlate1(2025, new DateOnly(2025, 8, 23)),
        });
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Cfb)]);

        var (_, locks) = await BuildSource(new DateTimeOffset(2025, 8, 10, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        var lockCandidate = Assert.Single(locks);
        Assert.Equal(new DateTime(2025, 8, 23, 19, 0, 0, DateTimeKind.Utc), lockCandidate.LockTime); // 2pm CDT = 19:00 UTC
    }

    // ── HasData / already-configured detection ───────────────────────────────────

    [Fact]
    public async Task HasData_TrueWhenLeagueAlreadyHasAJuiceMappingForThatSeason()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([
            MakeLeague(1, LeagueType.Nfl, new LeagueJuiceMapping { LeagueId = 1, Season = 2025 }),
        ]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.True(reminders.Single().HasData);
        Assert.True(locks.Single().HasData);
    }

    [Fact]
    public async Task HasData_FalseWhenNoJuiceMappingExistsForThatSeason()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([
            MakeLeague(1, LeagueType.Nfl, new LeagueJuiceMapping { LeagueId = 1, Season = 2024 }), // different season only
        ]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.False(reminders.Single().HasData);
        Assert.False(locks.Single().HasData);
    }

    // ── Reminder-sent tracking (distinct from Juice-configured) ──────────────────

    // /code-review: sending the reminder doesn't configure Juice, so "Juice configured" alone
    // can't be the reminder's "already handled" signal — without a separate persisted marker, the
    // scheduler would re-send the same email on every catch-up pass (daily cron, every restart)
    // for as long as the owner hasn't acted.
    [Fact]
    public async Task Reminder_HasDataTrue_WhenAlreadySent_EvenIfJuiceStillUnconfigured()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]); // no Juice mapping at all
        _leagueRepo.GetJuiceRemindersSentAsync().Returns(new HashSet<(int, int)> { (1, 2025) });

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.True(reminders.Single().HasData);
        Assert.False(locks.Single().HasData); // lock candidate is unaffected — Juice is still genuinely unconfigured
    }

    // ── Staleness cutoff (historical seasons never generate candidates) ──────────

    // /code-review: a season row that predates a league's creation looks "unconfigured" forever —
    // a brand-new league would otherwise get retroactive reminder emails and bogus auto-filled
    // rows for every already-settled past season on record.
    [Fact]
    public async Task Season_ExcludedEntirely_WhenItsLockTimeIsMoreThan30DaysInThePast()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2023, new DateTime(2023, 9, 7, 20, 20, 0, DateTimeKind.Utc)), // long-settled season
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]); // brand-new league, no history

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Empty(reminders);
        Assert.Empty(locks);
    }

    [Fact]
    public async Task Season_Included_WhenItsLockTimeIsWithin30DaysInThePast()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        // "now" is 10 days after lock time — within the catch-up window (e.g. app was down).
        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 9, 14, 19, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Single(reminders);
        Assert.Single(locks);
    }

    // ── Sport routing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CfbLeague_UsesCfbSlate1Date_NotNflWeek1Date()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _cfbRepo.GetAllWeekConfigsAsync().Returns((IEnumerable<CfbSeasonWeekConfig>)new List<CfbSeasonWeekConfig> {
            MakeCfbSlate1(2025, new DateOnly(2025, 8, 23)),
        });
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Cfb)]);

        var (_, locks) = await BuildSource(new DateTimeOffset(2025, 8, 10, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        var lockCandidate = Assert.Single(locks);
        Assert.Equal(new DateTime(2025, 8, 23, 19, 0, 0, DateTimeKind.Utc), lockCandidate.LockTime);
    }

    // /code-review: every other consumer of CfbSeasonWeekConfig (CfbSpreadScheduleSource,
    // CfbSlateSeederJob, CfbPicksController) filters on InScopeIvLeague, not just
    // IvLeagueWeekNumber != 99 — this must match, or a slate-1 row entered with scope not yet
    // confirmed would still schedule Juice reminders/locks against it.
    [Fact]
    public async Task CfbSeason_ExcludedWhenSlate1RowIsNotInScopeIvLeague()
    {
        _cfbRepo.GetAllWeekConfigsAsync().Returns((IEnumerable<CfbSeasonWeekConfig>)new List<CfbSeasonWeekConfig> {
            new() {
                Season = 2025, EspnWeekNumber = 1, IvLeagueWeekNumber = 1,
                WeekStartDate = new DateOnly(2025, 8, 23), WeekEndDate = new DateOnly(2025, 8, 29),
                InScopeIvLeague = false,
            },
        });
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Cfb)]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 10, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Empty(reminders);
        Assert.Empty(locks);
    }

    [Fact]
    public async Task League_SkippedWhenItsSportHasNoSeasonOneConfigRowYet()
    {
        // No NFL week-1 config seeded for any season, and this is an NFL league — nothing to
        // schedule (not an error, just genuinely nothing known about that season yet).
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(1, LeagueType.Nfl)]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Empty(reminders);
        Assert.Empty(locks);
    }

    // ── JobData ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidates_CarryLeagueIdAndSeasonAsJobData()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([MakeLeague(42, LeagueType.Nfl)]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        Assert.Equal("42", locks.Single().JobData?["LeagueId"]);
        Assert.Equal("2025", locks.Single().JobData?["Season"]);
        Assert.Equal("42", reminders.Single().JobData?["LeagueId"]);
        Assert.Equal("2025", reminders.Single().JobData?["Season"]);
    }

    [Fact]
    public async Task MultipleLeaguesAndSeasons_EachProducesItsOwnDistinctCandidatePair()
    {
        _leagueRepo.GetNflSeasonWeekConfigsAsync().Returns([
            MakeNflWeek1(2025, new DateTime(2025, 9, 4, 20, 20, 0, DateTimeKind.Utc)),
            MakeNflWeek1(2026, new DateTime(2026, 9, 3, 20, 20, 0, DateTimeKind.Utc)),
        ]);
        _leagueRepo.GetAllLeaguesAsync().Returns([
            MakeLeague(1, LeagueType.Nfl),
            MakeLeague(2, LeagueType.Nfl, new LeagueJuiceMapping { LeagueId = 2, Season = 2025 }),
        ]);

        var (reminders, locks) = await BuildSource(new DateTimeOffset(2025, 8, 20, 0, 0, 0, TimeSpan.Zero)).GetCandidatesAsync();

        // 2 leagues x 2 seasons = 4 candidate pairs
        Assert.Equal(4, locks.Count());
        Assert.Equal(4, reminders.Count());
        Assert.Equal(4, locks.Select(c => c.Identity).Distinct().Count());
    }
}
