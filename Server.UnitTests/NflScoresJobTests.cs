using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for NflScoresJob — control-table-driven (frizat: mirrors CfbScoresJob's
/// GetSlatesForSeasonAsync(Season) loop exactly, CLAUDE.md's NFL/CFB sharing rule). Loops the
/// current season's NflSeasonWeekConfig rows, fetches each via INflLiveScoreFetcher (date-range,
/// never trusts ESPN's own week=N bucketing — frizat-11t's NFL mirror), and seeds NflWeeks from
/// the same control table.
/// </summary>
public class NflScoresJobTests
{
    private readonly INflLiveScoreFetcher _fetcher;
    private readonly ILeagueRepository _repo;
    private readonly INflCurrentWeekService _currentWeekService;
    private readonly IEspnCacheService _espnCacheService;
    private readonly IJobExecutionContext _context;
    private readonly int _year = DateTime.UtcNow.Year;

    public NflScoresJobTests()
    {
        _fetcher = Substitute.For<INflLiveScoreFetcher>();
        _repo = Substitute.For<ILeagueRepository>();
        _currentWeekService = Substitute.For<INflCurrentWeekService>();
        _espnCacheService = Substitute.For<IEspnCacheService>();
        _context = Substitute.For<IJobExecutionContext>();

        // Default: all week fetches return null so loops terminate cleanly
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>()).Returns((EspnScores?)null);

        // Default: a season is active, with one current-season config so the ESPN loop still
        // exercises normally — tests that specifically care about the off-season gate or the
        // unscoped weekList sync override this explicitly below.
        _currentWeekService.IsSeasonActiveAsync().Returns(true);
        _currentWeekService.GetCurrentWeekAsync().Returns(new NflWeekInfo(
            WeekId: 1, EspnWeek: 1, Season: _year, IsPostSeason: false,
            WeekLabel: "Week 1", ScoringFormat: "Standard", SpreadLockDatetime: DateTime.UtcNow.AddDays(-1)));
        // NflScoresJob filters the current season's rows out of the one unscoped fetch (no
        // second, season-scoped DB call) — see Execute_FetchesOnlyTheCurrentSeasonsConfigs...
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig> { BuildConfig(1, _year) });
    }

    private NflScoresJob BuildJob() => new(_fetcher, _repo, _currentWeekService, _espnCacheService);

    private static NflSeasonWeekConfig BuildConfig(int weekId, int season, bool isPostSeason = false) =>
        new() {
            Id = weekId,
            Season = season,
            WeekId = weekId,
            WeekLabel = $"Week {weekId}",
            WeekType = isPostSeason ? "PostSeason" : "RegularSeason",
            ScoringFormat = "Standard",
            WeekStartDatetime = new DateTime(season, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(weekId * 7),
            WeekEndDatetime = new DateTime(season, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(weekId * 7 + 7),
        };

    private static EspnScores BuildWeekScores(int year, bool isFinal,
        string homeAbbr = "KC", string awayAbbr = "BUF",
        int homeScore = 28, int awayScore = 21)
    {
        var statusName = isFinal ? TypeName.StatusFinal : TypeName.StatusScheduled;

        var competition = new Competition {
            Date = new DateTimeOffset(year, 9, 10, 18, 0, 0, TimeSpan.Zero),
            Competitors = new[] {
                new Competitor { Id = "1", HomeAway = HomeAway.Home, Score = homeScore, Team = new EspnTeam { Abbreviation = homeAbbr }, Records = Array.Empty<EspnRecord>() },
                new Competitor { Id = "2", HomeAway = HomeAway.Away, Score = awayScore, Team = new EspnTeam { Abbreviation = awayAbbr }, Records = Array.Empty<EspnRecord>() },
            },
            Status = new EspnStatus { Type = new StatusType { Name = statusName, Completed = isFinal, Description = statusName switch {
                TypeName.StatusFinal => Description.Final,
                _ => Description.Scheduled,
            } } },
            Odds = Array.Empty<Odd>(),
        };

        return new EspnScores {
            Season = new Season { Year = year, Type = (int)TypeOfSeason.RegularSeason },
            Week = new Week { Number = 1 },
            Events = new[] {
                new Event {
                    Id = "401547605",
                    Season = new Season { Year = year, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = 1 },
                    Date = new DateTimeOffset(year, 9, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[] { competition },
                },
            },
        };
    }

    // -----------------------------------------------------------------------
    // Off-season gate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenNoSeasonIsCurrentlyActive_SkipsEspnLoopEntirely()
    {
        _currentWeekService.IsSeasonActiveAsync().Returns(false);

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceiveWithAnyArgs().FetchForWeekAsync(default!);
        await _repo.DidNotReceive().UpsertNflScoresAsync(Arg.Any<List<NflScores>>());
    }

    // -----------------------------------------------------------------------
    // Control-table-driven fetch — season-scoped, one call per config row
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_FetchesOnlyTheCurrentSeasonsConfigs_NotEverySeasonOnRecord()
    {
        // A prior season's row must never be fetched — filtered in memory from the single
        // unscoped call, not a second season-scoped DB round trip.
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig> {
            BuildConfig(1, _year), BuildConfig(2, _year), BuildConfig(1, _year - 1),
        });

        await BuildJob().Execute(_context);

        await _fetcher.Received(1).FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 1 && c.Season == _year));
        await _fetcher.Received(1).FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 2 && c.Season == _year));
        await _fetcher.DidNotReceive().FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.Season == _year - 1));
    }

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_CallsUpsertNflScores()
    {
        _fetcher.FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 1))
                .Returns(BuildWeekScores(_year, isFinal: true));

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertNflScoresAsync(Arg.Is<List<NflScores>>(list => list.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_UsesTheConfigRowsOwnWeekId_NotAnyEspnEchoedValue()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 7, season: _year) });
        _fetcher.FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 7))
                .Returns(BuildWeekScores(_year, isFinal: true));

        List<NflScores>? captured = null;
        _repo.When(r => r.UpsertNflScoresAsync(Arg.Any<List<NflScores>>()))
             .Do(ci => captured = ci.Arg<List<NflScores>>());

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.All(captured, s => Assert.Equal(7, s.NflWeek));
    }

    // /code-review: a boundary game (e.g. Monday Night Football finishing in the small hours UTC)
    // can fall on the same calendar day as both the ending week's cutoff and the next week's
    // start, so both weeks' independent ESPN date-range fetches can legitimately return the same
    // real game. Since UpsertNflScoresAsync keys on (Season, NflWeek, HomeTeam) and NflWeek
    // genuinely differs between the two matches, nothing downstream would catch this — the job
    // itself must dedupe before ever calling upsert.
    [Fact]
    public async Task Execute_WhenTheSameRealGameIsReturnedByTwoAdjacentWeeksFetches_OnlyKeepsTheEarlierWeek()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig> {
            BuildConfig(weekId: 1, season: _year), BuildConfig(weekId: 2, season: _year),
        });
        // Same real game (identical teams/score/kickoff) shows up under both weeks' fetches.
        var boundaryGame = BuildWeekScores(_year, isFinal: true, homeAbbr: "KC", awayAbbr: "BUF");
        _fetcher.FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 1)).Returns(boundaryGame);
        _fetcher.FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 2)).Returns(boundaryGame);

        List<NflScores>? captured = null;
        _repo.When(r => r.UpsertNflScoresAsync(Arg.Any<List<NflScores>>()))
             .Do(ci => captured = ci.Arg<List<NflScores>>());

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(1, captured[0].NflWeek); // earlier (correct) week wins, not the boundary duplicate
    }

    [Fact]
    public async Task Execute_WhenWeekHasCompletedGames_PassesCorrectTeamAbbreviationsAndScores()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildWeekScores(_year, isFinal: true, homeAbbr: "SF", awayAbbr: "DAL", homeScore: 35, awayScore: 17));

        List<NflScores>? captured = null;
        _repo.When(r => r.UpsertNflScoresAsync(Arg.Any<List<NflScores>>()))
             .Do(ci => captured = ci.Arg<List<NflScores>>());

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Contains(captured, s => s.HomeTeam == "SF" && s.AwayTeam == "DAL" && s.HomeTeamScore == 35 && s.AwayTeamScore == 17);
    }

    [Fact]
    public async Task Execute_WhenAllFetchesReturnNull_DoesNotCallUpsertScores()
    {
        // Default setup: FetchForWeekAsync returns null

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflScoresAsync(Arg.Any<List<NflScores>>());
    }

    [Fact]
    public async Task Execute_WhenWeekHasOnlyScheduledGames_DoesNotCallUpsertScores()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildWeekScores(_year, isFinal: false));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflScoresAsync(Arg.Any<List<NflScores>>());
    }

    // -----------------------------------------------------------------------
    // Cache invalidation — a fresh upsert must be visible immediately
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenScoresAreUpserted_InvalidatesTheEspnCacheForThatWeek()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 3, season: _year) });
        _fetcher.FetchForWeekAsync(Arg.Is<NflSeasonWeekConfig>(c => c.WeekId == 3))
                .Returns(BuildWeekScores(_year, isFinal: true));

        await BuildJob().Execute(_context);

        _espnCacheService.Received(1).InvalidateWeekCache(_year, 3);
    }

    [Fact]
    public async Task Execute_WhenNoScoresAreUpserted_NeverInvalidatesTheCache()
    {
        // Default setup: FetchForWeekAsync returns null

        await BuildJob().Execute(_context);

        _espnCacheService.DidNotReceiveWithAnyArgs().InvalidateWeekCache(default, default);
    }

    // -----------------------------------------------------------------------
    // ESPN fetch exception — job propagates it
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenFetchThrows_Rethrows()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .ThrowsAsync(new HttpRequestException("ESPN down"));

        await Assert.ThrowsAsync<HttpRequestException>(() => BuildJob().Execute(_context));
    }

    // -----------------------------------------------------------------------
    // UpsertNflWeeksAsync — seeded from every season on record, unscoped
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenSeasonWeekConfigIsEmpty_DoesNotCallUpsertWeeks()
    {
        _repo.GetNflSeasonWeekConfigsAsync().Returns(new List<NflSeasonWeekConfig>());

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().UpsertNflWeeksAsync(Arg.Any<List<NflWeeks>>());
    }

    [Fact]
    public async Task Execute_WhenSeasonWeekConfigHasEntries_CallsUpsertWeeks()
    {
        _repo.GetNflSeasonWeekConfigsAsync()
             .Returns(new List<NflSeasonWeekConfig> { BuildConfig(1, 2025), BuildConfig(2, 2025) });

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertNflWeeksAsync(Arg.Is<List<NflWeeks>>(l => l.Count == 2));
    }

    [Fact]
    public async Task Execute_WhenSeasonWeekConfigHasPostseasonEntry_MapsWeekId19()
    {
        _repo.GetNflSeasonWeekConfigsAsync()
             .Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 19, season: 2025, isPostSeason: true) });

        List<NflWeeks>? captured = null;
        _repo.When(r => r.UpsertNflWeeksAsync(Arg.Any<List<NflWeeks>>())).Do(ci => captured = ci.Arg<List<NflWeeks>>());

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(19, captured[0].NflWeek);
        Assert.Equal(2025, captured[0].Season);
    }

    [Fact]
    public async Task Execute_UpsertsWeeksEvenWhenOffSeason_UnaffectedByTheEspnGate()
    {
        _currentWeekService.IsSeasonActiveAsync().Returns(false);
        _repo.GetNflSeasonWeekConfigsAsync()
             .Returns(new List<NflSeasonWeekConfig> { BuildConfig(1, 2025) });

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertNflWeeksAsync(Arg.Is<List<NflWeeks>>(l => l.Count == 1));
    }
}
