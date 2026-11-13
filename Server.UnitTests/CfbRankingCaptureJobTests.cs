using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 5): CfbRankingCaptureJob captures
// CfbRanking rows as soon as a week's schedule is known, independent of that week's spread lock —
// CFB-only, no NFL equivalent (rank/eligibility is a CFB-specific concept).
public class CfbRankingCaptureJobTests
{
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;
    private readonly TimeProvider _timeProvider;

    // Fixed, controlled "now" — not tied to the real wall clock, so the EndDate-based skip filter
    // is deterministic regardless of when the suite actually runs. Set before BuildSlate's default
    // EndDate (2026-09-07) so existing "current" slates are still in scope by default.
    private static readonly DateTimeOffset FakeNow = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public CfbRankingCaptureJobTests()
    {
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
        _timeProvider = new FakeTimeProvider(FakeNow);
    }

    private CfbRankingCaptureJob BuildJob() => new(_fetcher, _repo, _timeProvider);

    private static CfbSeasonWeekConfig BuildSlate(int slateId = 1) => new()
    {
        Id = slateId, Season = 2026, IvLeagueWeekNumber = 1, InScopeIvLeague = true,
        WeekLabel = "Week 1", WeekStartDate = new DateOnly(2026, 9, 1),
        WeekEndDate   = new DateOnly(2026, 9, 7),

        ScoringFormat = ScoringFormat.Standard,
    };

    private static EspnScores BuildScoreboard(
        string eventId = "401677183",
        string homeAbbr = "ORE", string awayAbbr = "OSU",
        TypeName status = TypeName.StatusScheduled,
        int? homeRank = null, int? awayRank = null)
    {
        var competition = new Competition {
            Date = new DateTimeOffset(2026, 9, 5, 18, 0, 0, TimeSpan.Zero),
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = 0, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = [],
                    CuratedRank = homeRank is { } hr ? new CuratedRankInfo { Current = hr } : null },
                new Competitor { HomeAway = HomeAway.Away, Score = 0, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = [],
                    CuratedRank = awayRank is { } ar ? new CuratedRankInfo { Current = ar } : null },
            ],
            Status = new EspnStatus { Type = new StatusType { Name = status, Description = status switch {
                TypeName.StatusFinal => Description.Final,
                TypeName.StatusHalftime => Description.Halftime,
                TypeName.StatusInProgress => Description.InProgress,
                TypeName.StatusScheduled => Description.Scheduled,
                _ => Description.EndOfPeriod,
            } } },
            Odds = [],
        };
        return new EspnScores {
            Season = new Season { Year = 2026, Type = 3 },
            Week = new Week { Number = 1 },
            Events = [new Event { Id = eventId, Season = new Season { Year = 2026, Type = 3 }, Week = new Week { Number = 1 }, Competitions = [competition] }],
        };
    }

    [Fact]
    public async Task Execute_NoSlatesForSeason_LogsWarningAndSkips()
    {
        _repo.GetSlatesForSeasonAsync(2026).Returns([]);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_PersistsRankingOnlyForTheRankedCompetitor_NotTheUnrankedOne()
    {
        // "Rankings" now means one row per (season, week, ranked team) — CuratedRank=99 (ESPN's
        // unranked sentinel) is not a rank, so it's never persisted at all, not even as a row.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(homeRank: 3, awayRank: 99));

        IEnumerable<CfbRanking>? saved = null;
        await _repo.AddRankingsAsync(Arg.Do<IEnumerable<CfbRanking>>(r => saved = r));

        await BuildJob().Execute(_context);

        var rankings = saved!.ToList();
        Assert.Single(rankings);
        Assert.Equal("ORE", rankings[0].TeamAbbreviation);
        Assert.Equal(3, rankings[0].CuratedRank);
    }

    [Fact]
    public async Task Execute_ScheduledUnrankedTeam_ClearsItsEarlierRankForThatWeek()
    {
        // Prod 2026 wk 11: Northwestern's #20 from a Nov 1 capture stayed after it fell out of the
        // poll, so it and Ohio State (the real #20) both showed #20. A still-scheduled game with
        // an unranked team must clear that team's row for the week, not just leave it.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(homeRank: 20, awayRank: 99));

        IEnumerable<CfbUnrankedSighting>? cleared = null;
        await _repo.RemoveRankingsAsync(Arg.Do<IEnumerable<CfbUnrankedSighting>>(u => cleared = u));

        await BuildJob().Execute(_context);

        Assert.Equal([new CfbUnrankedSighting(2026, slate.SlateNumber, "OSU", 401677183)], cleared!.ToList());
    }

    [Theory]
    [InlineData(null)] // no curatedRank at all
    [InlineData(0)]    // malformed
    public async Task Execute_RankMissingOrMalformed_DoesNotClearIt(int? awayRank)
    {
        // Only ESPN's explicit 99 means "unranked": a gap or junk value in the response must never
        // wipe a real rank, least of all on the last capture before picks lock.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(homeRank: 20, awayRank: awayRank));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().RemoveRankingsAsync(Arg.Any<IEnumerable<CfbUnrankedSighting>>());
    }

    [Fact]
    public async Task Execute_WeekWithNoRankedTeamsAtAll_ClearsNothing()
    {
        // A response where every team is 99 is a bad payload, not the whole poll dropping out.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(homeRank: 99, awayRank: 99));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().RemoveRankingsAsync(Arg.Any<IEnumerable<CfbUnrankedSighting>>());
    }

    [Fact]
    public async Task Execute_GameAlreadyUnderway_LeavesItsCapturedRanksAlone()
    {
        // Rank at kickoff is what counted for that game: once it's no longer scheduled, a newer
        // poll must neither overwrite nor clear it.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(status: TypeName.StatusInProgress, awayRank: 99));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().RemoveRankingsAsync(Arg.Any<IEnumerable<CfbUnrankedSighting>>());
    }

    [Fact]
    public async Task Execute_NoRankedCompetitors_DoesNotPersist()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard());

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_FetcherReturnsNullEvents_SkipsSlateGracefully()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_SkipsSlateWhoseEndDateHasPassed_NoWastedFetch()
    {
        // CfbRankingExtractor only emits rows for StatusScheduled competitions, so a slate whose
        // games are all already final is a guaranteed zero-yield ESPN call — skip it entirely
        // rather than fetching and discarding.
        var pastSlate = BuildSlate();
        pastSlate.WeekEndDate = DateOnly.FromDateTime(FakeNow.UtcDateTime.AddDays(-1));
        _repo.GetSlatesForSeasonAsync(2026).Returns([pastSlate]);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForSlateAsync(Arg.Any<CfbSeasonWeekConfig>(), Arg.Any<bool>());
        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_DoesNotFetchOddsOrSaveSpreads()
    {
        // This job captures rankings only — no odds/spread concern at all, unlike CfbSpreadJob.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate, Arg.Any<bool>()).Returns(BuildScoreboard(homeRank: 3));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<FourPlayWebApp.Shared.Models.Data.CfbSpreads>>());
    }
}
