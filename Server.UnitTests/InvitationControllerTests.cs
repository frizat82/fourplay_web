using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class InvitationControllerTests
{
    private static (InvitationController ctrl, IInvitationService invitationService, ILeagueRepository leagueRepo, UserManager<ApplicationUser> userManager, ILeagueMembershipInviteService membershipInviteService)
        BuildControllerWithDeps(IInvitationService? invitationService = null)
    {
        invitationService ??= Substitute.For<IInvitationService>();
        var leagueRepo = Substitute.For<ILeagueRepository>();
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        var membershipInviteService = Substitute.For<ILeagueMembershipInviteService>();
        var ctrl = new InvitationController(
            invitationService, Substitute.For<IEmailSender<ApplicationUser>>(),
            userManager, leagueRepo, membershipInviteService);
        return (ctrl, invitationService, leagueRepo, userManager, membershipInviteService);
    }

    [Fact]
    public async Task Create_PassesBaseUrlThrough_SoTheInvitationServiceCanSendTheEmail()
    {
        var invitationService = Substitute.For<IInvitationService>();
        invitationService
            .CreateInvitationAsync("target@example.com", "admin-1", null, "https://ivleague.com")
            .Returns(new Invitation { Id = 1, Email = "target@example.com", InvitationCode = "code-abc" });
        var (ctrl, _, _, _, _) = BuildControllerWithDeps(invitationService);

        await ctrl.Create("target@example.com", "admin-1", baseUrl: "https://ivleague.com");

        await invitationService.Received(1).CreateInvitationAsync("target@example.com", "admin-1", null, "https://ivleague.com");
    }

    // ── Existing-user detection when a league is specified ──────────────────
    // Mirrors LeagueController.InviteToLeague: an admin using the global "Manage Invitations"
    // page to invite someone to a specific league must get the same existing-user treatment a
    // commissioner's "Invite Player" gets — otherwise an already-registered user (NFL or CFB)
    // silently gets a registration-style email Invitation instead of the in-app accept/decline
    // banner, and never sees anything (the bug this test locks in).

    [Fact]
    public async Task Create_ExistingUser_WithLeagueId_CreatesPendingMembershipInvite_NotAnEmailInvitation()
    {
        var (ctrl, invitationService, leagueRepo, userManager, membershipInviteService) = BuildControllerWithDeps();
        var existingUser = new ApplicationUser { Id = "existing-user-1", Email = "already-registered@example.com" };
        userManager.FindByEmailAsync("already-registered@example.com").Returns(existingUser);
        leagueRepo.UserExistsInLeagueAsync("existing-user-1", 5).Returns(false);

        var result = await ctrl.Create("already-registered@example.com", "admin-1", leagueId: 5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteResultDto>(ok.Value);
        Assert.Equal(LeagueInviteOutcome.ExistingUserInvitePending, dto.Outcome);
        await membershipInviteService.Received(1).CreateOrReopenAsync(5, "existing-user-1", "admin-1");
        await invitationService.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Create_ExistingUser_AlreadyMember_ReturnsConflict_WithoutCreatingAnything()
    {
        var (ctrl, invitationService, leagueRepo, userManager, membershipInviteService) = BuildControllerWithDeps();
        var existingUser = new ApplicationUser { Id = "existing-user-1", Email = "already-member@example.com" };
        userManager.FindByEmailAsync("already-member@example.com").Returns(existingUser);
        leagueRepo.UserExistsInLeagueAsync("existing-user-1", 5).Returns(true);

        var result = await ctrl.Create("already-member@example.com", "admin-1", leagueId: 5);

        Assert.IsType<ConflictObjectResult>(result.Result);
        await membershipInviteService.DidNotReceiveWithAnyArgs().CreateOrReopenAsync(default, default!, default!);
        await invitationService.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Create_ExistingUser_LeagueDoesNotExist_ReturnsNotFound_WithoutCreatingAnything()
    {
        // /code-review caught this: LoadOwnedLeagueAsync (LeagueController's sibling flow)
        // 404s on a missing league before doing anything else; this branch didn't, so a stale or
        // crafted leagueId reached CreateOrReopenAsync, which inserts a LeagueMembershipInvite
        // with a required FK to LeagueInfo, tripped a DbUpdateException that CreateOrReopenAsync's
        // catch block silently swallows (it exists only for a concurrent-duplicate-insert race),
        // and this endpoint returned 200 OK with ExistingUserInvitePending even though nothing
        // was ever persisted — a false success shown to the admin.
        var (ctrl, invitationService, leagueRepo, userManager, membershipInviteService) = BuildControllerWithDeps();
        var existingUser = new ApplicationUser { Id = "existing-user-1", Email = "already-registered@example.com" };
        userManager.FindByEmailAsync("already-registered@example.com").Returns(existingUser);
        leagueRepo.GetLeagueInfoAsync(999).Returns(Task.FromException<LeagueInfo>(new InvalidOperationException("Sequence contains no elements")));

        var result = await ctrl.Create("already-registered@example.com", "admin-1", leagueId: 999);

        Assert.IsType<NotFoundResult>(result.Result);
        await membershipInviteService.DidNotReceiveWithAnyArgs().CreateOrReopenAsync(default, default!, default!);
        await invitationService.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default!, default, default);
        await leagueRepo.DidNotReceiveWithAnyArgs().UserExistsInLeagueAsync(default!, default);
    }

    [Fact]
    public async Task Create_NewUser_LeagueDoesNotExist_ReturnsNotFound_WithoutCreatingAnything()
    {
        // /code-review's second pass caught that the first fix only guarded the existing-user
        // branch — a brand-new email with a bad leagueId still fell through to
        // CreateInvitationAsync, which has the identical FK-violation-mishandled-as-duplicate-
        // race bug (InvitationService.CreateInvitationAsync's catch re-reads a row that was never
        // inserted, throwing an unhandled InvalidOperationException — a 500, not even the wrong
        // 200 the existing-user branch had). The league-existence check now runs before either
        // branch, so this path never reaches CreateInvitationAsync at all.
        var (ctrl, invitationService, leagueRepo, userManager, membershipInviteService) = BuildControllerWithDeps();
        userManager.FindByEmailAsync("newplayer@example.com").Returns((ApplicationUser?)null);
        leagueRepo.GetLeagueInfoAsync(999).Returns(Task.FromException<LeagueInfo>(new InvalidOperationException("Sequence contains no elements")));

        var result = await ctrl.Create("newplayer@example.com", "admin-1", leagueId: 999);

        Assert.IsType<NotFoundResult>(result.Result);
        await invitationService.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default!, default, default);
        await membershipInviteService.DidNotReceiveWithAnyArgs().CreateOrReopenAsync(default, default!, default!);
    }

    [Fact]
    public async Task Create_NoExistingUser_WithLeagueId_StillCreatesEmailInvitation()
    {
        var (ctrl, invitationService, _, userManager, membershipInviteService) = BuildControllerWithDeps();
        userManager.FindByEmailAsync("newplayer@example.com").Returns((ApplicationUser?)null);
        invitationService.CreateInvitationAsync("newplayer@example.com", "admin-1", 5, null)
            .Returns(new Invitation { Id = 1, Email = "newplayer@example.com", InvitationCode = "code-abc" });

        var result = await ctrl.Create("newplayer@example.com", "admin-1", leagueId: 5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteResultDto>(ok.Value);
        Assert.Equal(LeagueInviteOutcome.NewUserInvitationSent, dto.Outcome);
        await invitationService.Received(1).CreateInvitationAsync("newplayer@example.com", "admin-1", 5, null);
        await membershipInviteService.DidNotReceiveWithAnyArgs().CreateOrReopenAsync(default, default!, default!);
    }

    [Fact]
    public async Task Create_ExistingUser_NoLeagueId_SkipsMembershipCheck_CreatesEmailInvitationAsBefore()
    {
        // No league context means there's nothing to build a LeagueMembershipInvite against —
        // this leagueless "just register" invite keeps its pre-existing behavior untouched.
        var (ctrl, invitationService, _, userManager, membershipInviteService) = BuildControllerWithDeps();
        invitationService.CreateInvitationAsync("someone@example.com", "admin-1", null, null)
            .Returns(new Invitation { Id = 1, Email = "someone@example.com", InvitationCode = "code-abc" });

        var result = await ctrl.Create("someone@example.com", "admin-1");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeagueInviteResultDto>(ok.Value);
        Assert.Equal(LeagueInviteOutcome.NewUserInvitationSent, dto.Outcome);
        await userManager.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
        await membershipInviteService.DidNotReceiveWithAnyArgs().CreateOrReopenAsync(default, default!, default!);
    }

    [Fact]
    public async Task Resend_CallsResendInvitationEmailAsync()
    {
        var invitationService = Substitute.For<IInvitationService>();
        var (ctrl, _, _, _, _) = BuildControllerWithDeps(invitationService);

        await ctrl.Resend(7, "https://ivleague.com");

        await invitationService.Received(1).ResendInvitationEmailAsync(7, "https://ivleague.com");
    }

    [Fact]
    public async Task Delete_CallsDeleteInvitationAsync_ReturnsNoContent()
    {
        var invitationService = Substitute.For<IInvitationService>();
        var (ctrl, _, _, _, _) = BuildControllerWithDeps(invitationService);

        var result = await ctrl.Delete(7);

        await invitationService.Received(1).DeleteInvitationAsync(7);
        Assert.IsType<NoContentResult>(result);
    }
}
