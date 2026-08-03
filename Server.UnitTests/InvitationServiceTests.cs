using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests
{
    public class InvitationServiceTests
    {
        private static (InvitationService service, IEmailSender emailSender) BuildService(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            var dbContextFactory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
            // Each CreateInvitationAsync/ResendInvitationEmailAsync call disposes its own
            // context, so the factory must hand out a fresh instance every time (same
            // in-memory database name keeps the data shared across instances).
            dbContextFactory.CreateDbContextAsync().Returns(_ => new ApplicationDbContext(options));
            var emailSender = Substitute.For<IEmailSender>();
            return (new InvitationService(dbContextFactory, emailSender), emailSender);
        }

        [Fact]
        public async Task CreateInvitationAsync_ValidData_ReturnsInvitation()
        {
            var (service, _) = BuildService(nameof(CreateInvitationAsync_ValidData_ReturnsInvitation));

            var result = await service.CreateInvitationAsync("test@example.com", "user123");

            Assert.Equal("test@example.com", result.Email);
            Assert.Equal("user123", result.InvitedByUserId);
        }

        [Fact]
        public async Task CreateInvitationAsync_WithBaseUrl_SendsInvitationEmailWithRegistrationLink()
        {
            var (service, emailSender) = BuildService(nameof(CreateInvitationAsync_WithBaseUrl_SendsInvitationEmailWithRegistrationLink));

            var result = await service.CreateInvitationAsync("test@example.com", "user123", baseUrl: "https://ivleague.com");

            await emailSender.Received(1).SendEmailAsync(
                "test@example.com",
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains($"inviteCode={result.InvitationCode}")));
        }

        [Fact]
        public async Task CreateInvitationAsync_WithoutBaseUrl_DoesNotSendEmail()
        {
            var (service, emailSender) = BuildService(nameof(CreateInvitationAsync_WithoutBaseUrl_DoesNotSendEmail));

            await service.CreateInvitationAsync("test@example.com", "user123");

            await emailSender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
        }

        [Fact]
        public async Task CreateInvitationAsync_WhenEmailSendThrows_InvitationIsStillCreated()
        {
            var (service, emailSender) = BuildService(nameof(CreateInvitationAsync_WhenEmailSendThrows_InvitationIsStillCreated));
            emailSender.SendEmailAsync(default!, default!, default!)
                .ReturnsForAnyArgs<Task>(_ => throw new InvalidOperationException("SMTP down"));

            var result = await service.CreateInvitationAsync("test@example.com", "user123", baseUrl: "https://ivleague.com");

            Assert.Equal("test@example.com", result.Email);
        }

        [Fact]
        public async Task ResendInvitationEmailAsync_SendsEmailForExistingInvitation()
        {
            var (service, emailSender) = BuildService(nameof(ResendInvitationEmailAsync_SendsEmailForExistingInvitation));
            var created = await service.CreateInvitationAsync("resend@example.com", "user123");

            await service.ResendInvitationEmailAsync(created.Id, "https://ivleague.com");

            await emailSender.Received(1).SendEmailAsync(
                "resend@example.com",
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains($"inviteCode={created.InvitationCode}")));
        }
    }
}
