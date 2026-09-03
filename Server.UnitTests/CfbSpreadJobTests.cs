using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// CFP week=999/date-filter and ranked-team-filter behavior is tested directly in
/// CfbLiveScoreFetcherTests (frizat-703.6) — this file only tests what CfbSpreadJob does with
/// whatever the fetcher returns: filter to scheduled-only, fetch odds, save.
/// </summary>
public class CfbSpreadJobTests
{
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly IEspnCoreOddsService _oddsService;
    private readonly ICfbRepository _repo;
    private readonly ICfbCurrentSlateService _currentSlateService;
    private readonly IJobExecutionContext _context;
    private readonly TimeProvider _timeProvider;

    // Fixed, controlled "now" — not tied to the real wall clock, so lock-time boundary tests are
    // deterministic regardless of when the suite actually runs.
    private static readonly DateTimeOffset FakeNow = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime PastLockTime = FakeNow.UtcDateTime.AddDays(-1);
    private static readonly DateTime FutureLockTime = FakeNow.UtcDateTime.AddDays(1);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public CfbSpreadJobTests()
    {
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _oddsService = Substitute.For<IEspnCoreOddsService>();
        _repo = Substitute.For<ICfbRepository>();
        _currentSlateService = Substitute.For<ICfbCurrentSlateService>();
        _context = Substitute.For<IJobExecutionContext>();
        _timeProvider = new FakeTimeProvider(FakeNow);

        _context.MergedJobDataMap.Returns(new JobDataMap());
    }

    private CfbSpreadJob BuildJob() => new(_fetcher, _oddsService, _repo, _currentSlateService, _timeProvider);

    private static CfbSlates BuildSlate(int slateId = 1) => new()
    {
        Id = slateId, Season = 2025, SlateNumber = 1,
        Label = "Week 1", SlateType = "RegularSeason",
        StartDate = new DateOnly(2025, 12, 19),
        EndDate   = new DateOnly(2025, 12, 20),
        EspnWeekNumber = 1,
        ScoringFormat = "Spread",
    };

    // Wires a CfbSlates entity as "the current slate" — both the CfbSlateInfo the current-slate
    // service resolves to and the full entity GetSlateByIdAsync returns, matching how the job
    // now actually looks up its target slate (mirrors NflSpreadJob's single-week scope).
    private void SetCurrentSlate(CfbSlates slate, DateTime? lockDatetime = null)
    {
        _currentSlateService.GetCurrentSlateAsync().Returns(new CfbSlateInfo(
            slate.Id, slate.Season, slate.SlateNumber, slate.Label, slate.SlateType,
            slate.StartDate, slate.EndDate, slate.FirstGameUtc, lockDatetime ?? PastLockTime));
        _repo.GetSlateByIdAsync(slate.Id).Returns(slate);
    }

    private static EspnScores BuildScoreboard(
        string eventId = "401677183",
        string homeAbbr = "ORE", string awayAbbr = "OSU",
        TypeName status = TypeName.StatusScheduled,
        DateTimeOffset? date = null,
        int? homeRank = null, int? awayRank = null)
    {
        var competition = new Competition {
            Date = date ?? new DateTimeOffset(2025, 12, 19, 18, 0, 0, TimeSpan.Zero), // Friday
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 0, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [],
                    CuratedRank = homeRank is { } hr ? new CuratedRankInfo { Current = hr } : null },
                new Competitor { HomeAway = HomeAway.Away,  Score = 0, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [],
                    CuratedRank = awayRank is { } ar ? new CuratedRankInfo { Current = ar } : null },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = status } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2025, Type = 3 },
            Week = new Week { Number = 1 },
            Events = [new Event { Id = eventId, Season = new Season { Year = 2025, Type = 3 }, Week = new Week { Number = 1 }, Competitions = [competition] }],
        };
    }

    private static EspnCoreOddsItem BuildOdds(string homeSpread = "-7.5", string awaySpread = "+7.5", double ou = 52.5) =>
        new() {
            HomeTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = homeSpread } } },
            AwayTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = awaySpread } } },
            OverUnder = ou,
            Provider = new EspnCoreOddsProvider { Name = "ESPN BET" },
        };

    [Fact]
    public async Task Execute_WhenNoCurrentSlate_DoesNothing()
    {
        _currentSlateService.GetCurrentSlateAsync().Returns((CfbSlateInfo?)null);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
    }

    [Fact]
    public async Task Execute_WhenFetcherReturnsNull_SavesNoSpreads()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }

    [Fact]
    public async Task Execute_FetchesSpreadForScheduledGame_SavesSpread()
    {
        var slate = BuildSlate();
        SetCurrentSlate(slate);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard());
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(
            Arg.Is<IEnumerable<CfbSpreads>>(s => s.Count() == 1));
    }

    [Fact]
    public async Task Execute_ParsesSpreadsCorrectly()
    {
        var slate = BuildSlate();
        SetCurrentSlate(slate);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeAbbr: "ORE", awayAbbr: "OSU"));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds("-7.5", "+7.5", 52.5));

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        var spread = saved!.First();
        Assert.Equal("ORE", spread.HomeTeam);
        Assert.Equal("OSU", spread.AwayTeam);
        Assert.Equal(-7.5,  spread.HomeTeamSpread);
        Assert.Equal(7.5,   spread.AwayTeamSpread);
        Assert.Equal(52.5,  spread.OverUnder);
        Assert.Equal(slate.Id, spread.CfbSlateId);
    }

    // ── Team rank persisted onto CfbSpreads (Picks/Scores rank display) ────────

    [Fact]
    public async Task Execute_RankedHomeTeam_PersistsRankOnSpread()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 5, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        var spread = saved!.Single();
        Assert.Equal(5, spread.HomeTeamRank);
        Assert.Null(spread.AwayTeamRank); // 99 = unranked sentinel, not a displayable rank
    }

    [Fact]
    public async Task Execute_BothTeamsUnranked_PersistsNullRanksOnSpread()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 99, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        var spread = saved!.Single();
        Assert.Null(spread.HomeTeamRank);
        Assert.Null(spread.AwayTeamRank);
    }

    [Fact]
    public async Task Execute_WhenOddsUnavailable_SkipsGame()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard());
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>(), 100).Returns((EspnCoreOddsItem?)null);
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>()).Returns((EspnCoreOddsApiResponse?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }

    [Fact]
    public async Task Execute_SkipsGame_WhenNotScheduled()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(status: TypeName.StatusFinal));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }

    // ── IsLeagueEligible + CfbRanking persistence (frizat-9m0) ─────────────────

    private static CfbSlates BuildCfpSlate(int slateId = 2) => new()
    {
        Id = slateId, Season = 2025, SlateNumber = 15,
        Label = "CFP First Round", SlateType = "FirstRound",
        StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20),
        EspnWeekNumber = 16, ScoringFormat = "NFLDivisional",
    };

    [Fact]
    public async Task Execute_RegularSeason_RankedAndNotMidweek_SavesEligibleTrue()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 5, awayRank: 99)); // Friday
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.True(saved!.Single().IsLeagueEligible);
    }

    [Fact]
    public async Task Execute_RegularSeason_BothUnranked_SavesEligibleFalse()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 99, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.False(saved!.Single().IsLeagueEligible);
    }

    [Fact]
    public async Task Execute_RegularSeason_RankedButMidweek_SavesEligibleFalse()
    {
        var tuesdayEt = new DateTimeOffset(2025, 9, 30, 23, 0, 0, TimeSpan.Zero); // Tue 7pm ET
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 5, awayRank: 99, date: tuesdayEt));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.False(saved!.Single().IsLeagueEligible);
    }

    [Fact]
    public async Task Execute_CfpSlate_BothUnranked_SavesEligibleTrueRegardless()
    {
        SetCurrentSlate(BuildCfpSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 99, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbSpreads>? saved = null;
        await _repo.UpsertAsync(Arg.Do<IEnumerable<CfbSpreads>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.True(saved!.Single().IsLeagueEligible);
    }

    [Fact]
    public async Task Execute_PersistsRankingForBothCompetitors()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeAbbr: "ORE", awayAbbr: "OSU", homeRank: 3, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        IEnumerable<CfbRanking>? saved = null;
        await _repo.AddRankingsAsync(Arg.Do<IEnumerable<CfbRanking>>(r => saved = r));

        await BuildJob().Execute(_context);

        var rankings = saved!.ToList();
        Assert.Equal(2, rankings.Count);
        Assert.Contains(rankings, r => r.TeamAbbreviation == "ORE" && r.CuratedRank == 3);
        Assert.Contains(rankings, r => r.TeamAbbreviation == "OSU" && r.CuratedRank == 99);
        Assert.All(rankings, r => Assert.Equal(401677183, r.EspnEventId));
    }

    [Fact]
    public async Task Execute_PersistsRanking_EvenWhenOddsUnavailable()
    {
        SetCurrentSlate(BuildSlate());
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard(homeRank: 3, awayRank: 99));
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>(), 100).Returns((EspnCoreOddsItem?)null);
        _oddsService.GetCfbEventsWithOddsAsync(Arg.Any<int>()).Returns((EspnCoreOddsApiResponse?)null);

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddRankingsAsync(Arg.Is<IEnumerable<CfbRanking>>(r => r.Count() == 2));
    }

    // -----------------------------------------------------------------------
    // Lock-time write guard (frizat-pxy follow-on): no automated or manual write
    // before the current slate's SpreadLockDatetime, unless explicitly forced via JobDataMap.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_LockTimeInFuture_NoForce_SkipsWithoutFetching()
    {
        SetCurrentSlate(BuildSlate(), FutureLockTime);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSlates>());
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<CfbSpreads>>());
    }

    [Fact]
    public async Task Execute_WhenSlateHasNoMatchingConfig_PropagatesException()
    {
        // CfbCurrentSlateService now throws if a slate has no matching CfbSeasonWeekConfig row —
        // SpreadLockDatetime is a required column, so "missing config" is a data-integrity bug,
        // not an expected "not configured yet" state to silently skip.
        _currentSlateService.GetCurrentSlateAsync()
            .Returns(Task.FromException<CfbSlateInfo?>(new InvalidOperationException("no matching config")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));
    }

    [Fact]
    public async Task Execute_LockTimeInFuture_Forced_WritesAnyway()
    {
        SetCurrentSlate(BuildSlate(), FutureLockTime);
        var forceMap = new JobDataMap();
        forceMap.Put("force", true);
        _context.MergedJobDataMap.Returns(forceMap);
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard());
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<IEnumerable<CfbSpreads>>(s => s.Count() == 1));
    }

    [Fact]
    public async Task Execute_LockTimeInPast_NoForce_WritesNormally()
    {
        var slate = BuildSlate();
        SetCurrentSlate(slate); // defaults to PastLockTime
        _fetcher.FetchForSlateAsync(Arg.Any<CfbSlates>()).Returns(BuildScoreboard());
        _oddsService.GetCfbEventsWithOddsAsync(401677183, 100).Returns(BuildOdds());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<IEnumerable<CfbSpreads>>(s => s.Count() == 1));
    }
}
