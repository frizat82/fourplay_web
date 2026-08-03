using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class InvitationControllerTests
{
    private static InvitationController BuildController(IInvitationService invitationService) =>
        new(invitationService, Substitute.For<IEmailSender<ApplicationUser>>());

    [Fact]
    public async Task Create_PassesBaseUrlThrough_SoTheInvitationServiceCanSendTheEmail()
    {
        var invitationService = Substitute.For<IInvitationService>();
        invitationService
            .CreateInvitationAsync("target@example.com", "admin-1", null, "https://ivleague.com")
            .Returns(new Invitation { Id = 1, Email = "target@example.com", InvitationCode = "code-abc" });
        var controller = BuildController(invitationService);

        await controller.Create("target@example.com", "admin-1", baseUrl: "https://ivleague.com");

        await invitationService.Received(1).CreateInvitationAsync("target@example.com", "admin-1", null, "https://ivleague.com");
    }

    [Fact]
    public async Task Resend_CallsResendInvitationEmailAsync()
    {
        var invitationService = Substitute.For<IInvitationService>();
        var controller = BuildController(invitationService);

        await controller.Resend(7, "https://ivleague.com");

        await invitationService.Received(1).ResendInvitationEmailAsync(7, "https://ivleague.com");
    }
}
