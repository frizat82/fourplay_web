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

        // frizat-9vm: Invitations.Email was globally unique across the whole table, not scoped
        // per league — inviting the same real-world email to two different leagues (routine once
        // self-serve league creation shipped) threw an unhandled DbUpdateException. Scoped to
        // (Email, LeagueId); CreateInvitationAsync is now an upsert instead of a blind insert.
        [Fact]
        public async Task CreateInvitationAsync_SameEmailTwoDifferentLeagues_BothSucceed()
        {
            var (service, _) = BuildService(nameof(CreateInvitationAsync_SameEmailTwoDifferentLeagues_BothSucceed));

            var first = await service.CreateInvitationAsync("multi@example.com", "user123", leagueId: 1);
            var second = await service.CreateInvitationAsync("multi@example.com", "user123", leagueId: 2);

            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal(1, first.LeagueId);
            Assert.Equal(2, second.LeagueId);
        }

        // frizat-9vm follow-up (code review): the [Index(Email, LeagueId)] unique constraint only
        // fires when LeagueId is non-null (Postgres treats every NULL as distinct); a global,
        // non-league-scoped invite (leagueId: null) relies entirely on the app-level upsert below
        // to avoid duplicating, backed by a separate partial unique index (Email WHERE LeagueId IS
        // NULL) at the DB level for the concurrent-request case this in-memory test can't exercise.
        [Fact]
        public async Task CreateInvitationAsync_ReinviteSameEmailNoLeague_RefreshesExistingRowInsteadOfDuplicating()
        {
            var (service, _) = BuildService(nameof(CreateInvitationAsync_ReinviteSameEmailNoLeague_RefreshesExistingRowInsteadOfDuplicating));
            var first = await service.CreateInvitationAsync("global@example.com", "user123");

            var second = await service.CreateInvitationAsync("global@example.com", "user123");

            Assert.Equal(first.Id, second.Id);
            Assert.Null(second.LeagueId);
        }

        [Fact]
        public async Task CreateInvitationAsync_ReinviteSameEmailSameLeague_RefreshesExistingRowInsteadOfThrowing()
        {
            var (service, _) = BuildService(nameof(CreateInvitationAsync_ReinviteSameEmailSameLeague_RefreshesExistingRowInsteadOfThrowing));
            var first = await service.CreateInvitationAsync("resend2@example.com", "user123", leagueId: 5);

            var second = await service.CreateInvitationAsync("resend2@example.com", "user123", leagueId: 5);

            Assert.Equal(first.Id, second.Id);
            Assert.True(second.ExpiresAt >= first.ExpiresAt);
        }

        [Fact]
        public async Task CreateInvitationAsync_ReinviteSameEmailSameLeague_ResendsEmail()
        {
            var (service, emailSender) = BuildService(nameof(CreateInvitationAsync_ReinviteSameEmailSameLeague_ResendsEmail));
            var first = await service.CreateInvitationAsync("resend3@example.com", "user123", leagueId: 5, baseUrl: "https://ivleague.com");

            await service.CreateInvitationAsync("resend3@example.com", "user123", leagueId: 5, baseUrl: "https://ivleague.com");

            await emailSender.Received(2).SendEmailAsync(
                "resend3@example.com",
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains($"inviteCode={first.InvitationCode}")));
        }

        [Fact]
        public async Task CreateInvitationAsync_ReinviteAlreadyUsedInvitation_DoesNotOverwriteRegistration()
        {
            var (service, emailSender) = BuildService(nameof(CreateInvitationAsync_ReinviteAlreadyUsedInvitation_DoesNotOverwriteRegistration));
            var first = await service.CreateInvitationAsync("used@example.com", "user123", leagueId: 5);
            await service.MarkInvitationAsUsedAsync(first.InvitationCode, "the-registered-user");

            var second = await service.CreateInvitationAsync("used@example.com", "user123", leagueId: 5, baseUrl: "https://ivleague.com");

            Assert.Equal(first.Id, second.Id);
            Assert.True(second.IsUsed);
            Assert.Equal("the-registered-user", second.RegisteredUserId);
            await emailSender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
        }

        [Fact]
        public async Task DeleteInvitationAsync_ExistingInvitation_RemovesIt()
        {
            var (service, _) = BuildService(nameof(DeleteInvitationAsync_ExistingInvitation_RemovesIt));
            var invitation = await service.CreateInvitationAsync("delete-me@example.com", "user123");

            await service.DeleteInvitationAsync(invitation.Id);

            var remaining = await service.GetAllInvitationsAsync();
            Assert.DoesNotContain(remaining, i => i.Id == invitation.Id);
        }

        [Fact]
        public async Task DeleteInvitationAsync_AlreadyUsedInvitation_RemovesIt()
        {
            // Deleting a used invitation is allowed today (UI shows the delete button
            // regardless of used/expired state) — it only destroys the audit trail linking a
            // user to how they joined, nothing else references Invitation as a parent FK.
            var (service, _) = BuildService(nameof(DeleteInvitationAsync_AlreadyUsedInvitation_RemovesIt));
            var invitation = await service.CreateInvitationAsync("used-then-deleted@example.com", "user123");
            await service.MarkInvitationAsUsedAsync(invitation.InvitationCode, "registered-user-1");

            await service.DeleteInvitationAsync(invitation.Id);

            var remaining = await service.GetAllInvitationsAsync();
            Assert.DoesNotContain(remaining, i => i.Id == invitation.Id);
        }

        [Fact]
        public async Task DeleteInvitationAsync_NonExistentId_DoesNotThrow()
        {
            var (service, _) = BuildService(nameof(DeleteInvitationAsync_NonExistentId_DoesNotThrow));

            await service.DeleteInvitationAsync(999999);
        }
    }
}
