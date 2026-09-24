using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-xbq (NFL side; the CFB twin lives in CfbPicksControllerTests): the commissioner
/// "who's missing picks" chip used to count the picks GetLeaguePicks RETURNED — which hides other
/// users' picks for games that haven't kicked off, so a non-admin owner never saw a member's pick
/// on tonight's game and the chip read 3/4 for someone with all 4 in. This endpoint returns
/// SUBMITTED counts only — never which team — for the league owner or a site admin.
/// </summary>
public class PickCountsEndpointTests
{
    private const int LeagueId = 1;
    private const int Season = 2026;
    private const int Week = 2;
    private const string OwnerId = "owner-1";
    private const string MemberId = "member-1";
    private const string OtherMemberId = "member-2";

    private static ILeagueRepository Repo(params string[] pickUserIds)
    {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueInfoAsync(LeagueId).Returns(new LeagueInfo { Id = LeagueId, LeagueName = "L", OwnerUserId = OwnerId });
        repo.GetNflPickUserIdsAsync(LeagueId, Season, Week).Returns([.. pickUserIds]);
        return repo;
    }

    private static LeagueController Controller(ILeagueRepository repo, ClaimsPrincipal principal, IEspnCacheService? espn = null)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        var controller = new LeagueController(
            new MemoryCache(new MemoryCacheOptions()),
            repo,
            NullLogger<LeagueController>.Instance,
            userManager,
            Substitute.For<ISpreadCalculatorProvider>(),
            espn ?? Substitute.For<IEspnCacheService>(),
            Substitute.For<IInvitationService>(),
            Substitute.For<ILeagueInviteLinkService>(),
            Substitute.For<ILeagueMembershipInviteService>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
        return controller;
    }

    private static List<MemberPickCountDto> Counts(ActionResult<List<MemberPickCountDto>> result) =>
        (List<MemberPickCountDto>)Assert.IsType<OkObjectResult>(result.Result).Value!;

    [Fact]
    public async Task Owner_SeesSubmittedCounts_IncludingPicksOnGamesNotYetStarted()
    {
        // Dhoward's real case: 4 submitted picks, one of them on tonight's not-yet-started game.
        var repo = Repo(MemberId, MemberId, MemberId, MemberId, OtherMemberId);
        var espn = Substitute.For<IEspnCacheService>();

        var counts = Counts(await Controller(repo, TestPrincipalFactory.Build(OwnerId), espn).GetPickCounts(LeagueId, Season, Week));

        Assert.Equal(4, counts.Single(c => c.UserId == MemberId).PickCount);
        Assert.Equal(1, counts.Single(c => c.UserId == OtherMemberId).PickCount);
        // Counting never depends on live game status...
        await espn.DidNotReceive().GetScoresAsync();
        // ...and never loads a pick row (so never a team): only user ids are read.
        await repo.DidNotReceive().GetNflPicksAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task SiteAdmin_SeesCounts_EvenWhenNotTheOwner()
    {
        var counts = Counts(await Controller(Repo(MemberId), TestPrincipalFactory.Build("admin-1", isAdmin: true)).GetPickCounts(LeagueId, Season, Week));

        Assert.Equal(1, counts.Single(c => c.UserId == MemberId).PickCount);
    }

    [Fact]
    public async Task OrdinaryLeagueMember_IsForbidden()
    {
        var result = await Controller(Repo(OtherMemberId), TestPrincipalFactory.Build(MemberId)).GetPickCounts(LeagueId, Season, Week);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UnknownLeague_Is404NotA500()
    {
        var repo = Repo();
        repo.GetLeagueInfoAsync(999).Returns<LeagueInfo>(_ => throw new InvalidOperationException("Sequence contains no elements"));

        var result = await Controller(repo, TestPrincipalFactory.Build(OwnerId)).GetPickCounts(999, Season, Week);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
