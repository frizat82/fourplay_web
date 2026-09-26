using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Builds a LeagueController with a substitute for every dependency a test doesn't pass — so a new
/// constructor dependency is added here once, not in every test file that builds the controller.
/// </summary>
internal static class LeagueControllerFactory {
    public static LeagueController Build(
        ILeagueRepository? repo = null,
        UserManager<ApplicationUser>? userManager = null,
        ISpreadCalculatorProvider? spreadCalculatorProvider = null,
        IEspnCacheService? espnCacheService = null,
        IInvitationService? invitationService = null,
        ILeagueInviteLinkService? leagueInviteLinkService = null,
        ILeagueMembershipInviteService? membershipInviteService = null,
        IMemoryCache? memoryCache = null) =>
        new(
            memoryCache ?? new MemoryCache(new MemoryCacheOptions()),
            repo ?? Substitute.For<ILeagueRepository>(),
            NullLogger<LeagueController>.Instance,
            userManager ?? UserManagerStub.Create(),
            spreadCalculatorProvider ?? Substitute.For<ISpreadCalculatorProvider>(),
            espnCacheService ?? Substitute.For<IEspnCacheService>(),
            invitationService ?? Substitute.For<IInvitationService>(),
            leagueInviteLinkService ?? Substitute.For<ILeagueInviteLinkService>(),
            membershipInviteService ?? Substitute.For<ILeagueMembershipInviteService>());
}
