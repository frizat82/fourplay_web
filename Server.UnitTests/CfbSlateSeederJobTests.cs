using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Server.Models.Data;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbSlateSeederJobTests
{
    private readonly ICfbRepository _repo;
    private readonly IJobExecutionContext _context;

    public CfbSlateSeederJobTests()
    {
        _repo = Substitute.For<ICfbRepository>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    private CfbSlateSeederJob BuildJob() => new(_repo);

    private static IEnumerable<CfbSlates> MakeSlates(int count) =>
        Enumerable.Range(1, count).Select(i => new CfbSlates { Season = 2025, SlateNumber = i });

    [Fact]
    public async Task Execute_WhenAllSlatesAlreadyExist_SkipsSeeding()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns(MakeSlates(19));

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddSlatesAsync(Arg.Any<IEnumerable<CfbSlates>>());
    }

    [Fact]
    public async Task Execute_WhenNoSlatesExist_SeedsNineteenSlates()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns([]);

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddSlatesAsync(
            Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 19));
    }

    [Fact]
    public async Task Execute_WhenPartialSlatesExist_DeletesThenReseeds()
    {
        var stale = MakeSlates(4).ToList();
        _repo.GetSlatesForSeasonAsync(2025).Returns(stale);

        await BuildJob().Execute(_context);

        await _repo.Received(1).DeleteSlatesAsync(Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 4));
        await _repo.Received(1).AddSlatesAsync(Arg.Is<IEnumerable<CfbSlates>>(s => s.Count() == 19));
    }

    [Fact]
    public async Task Execute_SeedsRegularSeasonWeeks1Through14()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        var regular = saved!.Where(s => s.SlateType == "RegularSeason").OrderBy(s => s.SlateNumber).ToList();
        Assert.Equal(14, regular.Count);
        Assert.Equal(1, regular[0].SlateNumber);
        Assert.Equal(14, regular[13].SlateNumber);
    }

    [Fact]
    public async Task Execute_SeedsCorrectPostseasonSlateTypes()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns([]);
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
    public async Task Execute_SeedsCorrectNationalChampionshipDate()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        var championship = saved!.Single(s => s.SlateType == "Championship");
        Assert.Equal(new DateOnly(2026, 1, 19), championship.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 19), championship.EndDate);
    }

    [Fact]
    public async Task Execute_AllSlatesHaveSeason2025()
    {
        _repo.GetSlatesForSeasonAsync(2025).Returns([]);
        IEnumerable<CfbSlates>? saved = null;
        await _repo.AddSlatesAsync(Arg.Do<IEnumerable<CfbSlates>>(s => saved = s));

        await BuildJob().Execute(_context);

        Assert.All(saved!, s => Assert.Equal(2025, s.Season));
    }
}
