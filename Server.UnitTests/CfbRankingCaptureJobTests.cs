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

    public CfbRankingCaptureJobTests()
    {
        _fetcher = Substitute.For<ICfbLiveScoreFetcher>();
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbRankingCaptureJob BuildJob() => new(_fetcher, _repo);

    private static CfbSlates BuildSlate(int slateId = 1) => new()
    {
        Id = slateId, Season = 2026, SlateNumber = 1,
        Label = "Week 1", SlateType = "RegularSeason",
        StartDate = new DateOnly(2026, 9, 1),
        EndDate   = new DateOnly(2026, 9, 7),
        EspnWeekNumber = 1,
        ScoringFormat = "Spread",
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
            Status = new EspnStatus { Type = new StatusType { Name = status } },
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
    public async Task Execute_PersistsRankingForBothRankedCompetitors()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate).Returns(BuildScoreboard(homeRank: 3, awayRank: 99));

        IEnumerable<CfbRanking>? saved = null;
        await _repo.AddRankingsAsync(Arg.Do<IEnumerable<CfbRanking>>(r => saved = r));

        await BuildJob().Execute(_context);

        var rankings = saved!.ToList();
        Assert.Contains(rankings, r => r.TeamAbbreviation == "ORE" && r.CuratedRank == 3);
        Assert.Contains(rankings, r => r.TeamAbbreviation == "OSU" && r.CuratedRank == 99);
    }

    [Fact]
    public async Task Execute_NoRankedCompetitors_DoesNotPersist()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate).Returns(BuildScoreboard());

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_FetcherReturnsNullEvents_SkipsSlateGracefully()
    {
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate).Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddRankingsAsync(Arg.Any<IEnumerable<CfbRanking>>());
    }

    [Fact]
    public async Task Execute_DoesNotFetchOddsOrSaveSpreads()
    {
        // This job captures rankings only — no odds/spread concern at all, unlike CfbSpreadJob.
        var slate = BuildSlate();
        _repo.GetSlatesForSeasonAsync(2026).Returns([slate]);
        _fetcher.FetchForSlateAsync(slate).Returns(BuildScoreboard(homeRank: 3));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<IEnumerable<FourPlayWebApp.Shared.Models.Data.CfbSpreads>>());
    }
}
