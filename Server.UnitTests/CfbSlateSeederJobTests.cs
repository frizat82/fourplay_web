using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Server.Models.Data;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbSlateSeederJobTests
{
    private const int Season = 2026;

    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;

    public CfbSlateSeederJobTests()
    {
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbSlateSeederJob BuildJob() => new(_repo);

    // 18 in-scope configs (22 ESPN weeks minus 4 excluded)
    private static List<CfbSeasonWeekConfig> Make2026Configs() =>
    [
        // Regular season weeks 1-13
        .. Enumerable.Range(1, 13).Select(i => new CfbSeasonWeekConfig {
            Season = Season, EspnWeekNumber = i, IvLeagueWeekNumber = i,
            WeekType = "Regular Season", ScoringFormat = "Standard", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7),
        }),
        // Conf. Championships (IV=14, ESPN=14)
        new() { Season = Season, EspnWeekNumber = 14, IvLeagueWeekNumber = 14,
            WeekType = "Conference Championships", ScoringFormat = "Standard", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2026, 12, 1), WeekEndDate = new DateOnly(2026, 12, 7) },
        // CFP First Round (IV=15, ESPN=16 — skips Army-Navy ESPN 15)
        new() { Season = Season, EspnWeekNumber = 16, IvLeagueWeekNumber = 15,
            WeekType = "FBS Playoff", ScoringFormat = "NFLDivisional", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2026, 12, 15), WeekEndDate = new DateOnly(2026, 12, 21) },
        // CFP Quarterfinals (IV=16, ESPN=18 — skips dead gap ESPN 17)
        new() { Season = Season, EspnWeekNumber = 18, IvLeagueWeekNumber = 16,
            WeekType = "FBS Playoff", ScoringFormat = "NFLDivisional", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2026, 12, 29), WeekEndDate = new DateOnly(2027, 1, 4) },
        // CFP Semifinals (IV=17, ESPN=20)
        new() { Season = Season, EspnWeekNumber = 20, IvLeagueWeekNumber = 17,
            WeekType = "FBS Playoff", ScoringFormat = "NFLConference", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2027, 1, 12), WeekEndDate = new DateOnly(2027, 1, 18) },
        // CFP Championship (IV=18, ESPN=21)
        new() { Season = Season, EspnWeekNumber = 21, IvLeagueWeekNumber = 18,
            WeekType = "FBS Playoff", ScoringFormat = "NFLSuperBowl", InScopeIvLeague = true,
            WeekStartDate = new DateOnly(2027, 1, 19), WeekEndDate = new DateOnly(2027, 1, 25) },
    ];

    private static IEnumerable<CfbSlates> MakeSlates(int count) =>
        Enumerable.Range(1, count).Select(i => new CfbSlates { Season = Season, SlateNumber = i });

    [Fact]
    public async Task Execute_WhenNoConfigExists_LogsWarningAndSkips()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns([]);
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddSlatesAsync(Arg.Any<IEnumerable<CfbSlates>>());
    }

    [Fact]
    public async Task Execute_WhenSlatesAlreadyFullySeeded_Skips()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns(MakeSlates(18));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddSlatesAsync(Arg.Any<IEnumerable<CfbSlates>>());
    }

    [Fact]
    public async Task Execute_WhenNoSlatesExist_SeedsEighteenSlates()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddSlatesAsync(
            Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 18));
    }

    [Fact]
    public async Task Execute_WhenPartialSlatesExist_DeletesThenReseeds()
    {
        var stale = MakeSlates(4).ToList();
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns(stale);

        await BuildJob().Execute(_context);

        await _repo.Received(1).DeleteSlatesAsync(Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 4));
        await _repo.Received(1).AddSlatesAsync(Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 18));
    }

    [Fact]
    public async Task Execute_SeedsRegularSeasonWeeks1Through13()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        var regular = saved!.Where(s => s.SlateType == "RegularSeason").OrderBy(s => s.SlateNumber).ToList();
        Assert.Equal(13, regular.Count);
        Assert.Equal(1, regular[0].SlateNumber);
        Assert.Equal(13, regular[12].SlateNumber);
    }

    [Fact]
    public async Task Execute_SeedsCorrectPostseasonSlateTypes()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        var post = saved!.Where(s => s.SlateType != "RegularSeason").OrderBy(s => s.SlateNumber).ToList();
        Assert.Equal(5, post.Count);
        Assert.Equal("ConferenceChampionship", post[0].SlateType);
        Assert.Equal("FirstRound",             post[1].SlateType);
        Assert.Equal("Quarterfinal",           post[2].SlateType);
        Assert.Equal("Semifinal",              post[3].SlateType);
        Assert.Equal("Championship",           post[4].SlateType);
    }

    [Fact]
    public async Task Execute_SeedsEspnWeekNumberOnSlates()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        // CFP First Round: IV slate 15 → ESPN week 16 (skips Army-Navy week 15)
        var firstRound = saved!.Single(s => s.SlateType == "FirstRound");
        Assert.Equal(15, firstRound.SlateNumber);
        Assert.Equal(16, firstRound.EspnWeekNumber);

        // CFP Championship: IV slate 18 → ESPN week 21
        var championship = saved!.Single(s => s.SlateType == "Championship");
        Assert.Equal(18, championship.SlateNumber);
        Assert.Equal(21, championship.EspnWeekNumber);
        Assert.Equal(new DateOnly(2027, 1, 19), championship.StartDate);
    }

    [Fact]
    public async Task Execute_AllSlatesHaveSeason2026()
    {
        _repo.GetWeekConfigsForSeasonAsync(Season).Returns(Make2026Configs());
        _repo.GetSlatesForSeasonAsync(Season).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.All(saved!, s => Assert.Equal(Season, s.Season));
    }
}
