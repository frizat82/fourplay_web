using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Models.Mappers;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;
[Authorize(Roles = "Administrator")]
[ApiController]
[Route("api/invitations")]
public class InvitationController(
    IInvitationService invitationService,
    IEmailSender<ApplicationUser> emailSenderApplication,
    UserManager<ApplicationUser> userManager,
    ILeagueRepository leagueRepo,
    ILeagueMembershipInviteService membershipInviteService) : ControllerBase {
    [HttpGet("all")]
    public async Task<ActionResult<List<InvitationDto>>> GetAll()
    {
        var invitations = await invitationService.GetAllInvitationsAsync();
        return Ok(invitations.ToDtoList());
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<InvitationDto>>> GetByUser(string userId)
    {
        var invitations = await invitationService.GetInvitationsByUserAsync(userId);
        return Ok(invitations.ToDtoList());
    }

    [HttpPost]
    public async Task<ActionResult<LeagueInviteResultDto>> Create([FromQuery] string email, [FromQuery] string invitedByUserId, [FromQuery] int? leagueId = null, [FromQuery] string? baseUrl = null)
    {
        // Mirrors LeagueController.InviteToLeague's existing-user check: this admin-facing
        // "Manage Invitations" tool used to unconditionally create a registration-style email
        // Invitation, even for an email that already has an account — the invitee never saw an
        // email (they don't need one) and never got the in-app accept/decline banner either
        // (no LeagueMembershipInvite was ever created), so they got nothing. Only applies when a
        // specific league is targeted — a leagueless invite has no league to build a pending
        // membership invite against, so it keeps its original behavior.
        if (leagueId is int targetLeagueId) {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null) {
                if (await leagueRepo.UserExistsInLeagueAsync(existingUser.Id, targetLeagueId))
                    return Conflict($"{email} is already a member of this league.");
                await membershipInviteService.CreateOrReopenAsync(targetLeagueId, existingUser.Id, invitedByUserId);
                return Ok(new LeagueInviteResultDto(email, LeagueInviteOutcome.ExistingUserInvitePending));
            }
        }

        var invitation = await invitationService.CreateInvitationAsync(email, invitedByUserId, leagueId, baseUrl);
        return Ok(new LeagueInviteResultDto(invitation.Email, LeagueInviteOutcome.NewUserInvitationSent));
    }

    [HttpPost("{id:int}/resend")]
    public async Task<IActionResult> Resend(int id, [FromQuery] string baseUrl)
    {
        await invitationService.ResendInvitationEmailAsync(id, baseUrl);
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await invitationService.DeleteInvitationAsync(id);
        return NoContent();
    }

    [HttpGet("validate/{code}")]
    [AllowAnonymous] // Allow unauthenticated users to validate invitation codes
    public async Task<ActionResult<InvitationDto?>> Validate(string code)
    {
        var invitation = await invitationService.ValidateInvitationAsync(code);
        if (invitation == null) return NotFound();
        return Ok(invitation.ToDto());
    }

    [HttpPost("use")]
    public async Task<ActionResult<bool>> MarkAsUsed([FromQuery] string code, [FromQuery] string registeredUserId)
    {
        var result = await invitationService.MarkInvitationAsUsedAsync(code, registeredUserId);
        if (!result) return BadRequest("Invalid invitation code or already used/expired.");
        return Ok(true);
    }

    // -----------------------------
    // Email Endpoints
    // -----------------------------

    [HttpPost("send-confirmation")]
    public async Task<IActionResult> SendConfirmation([FromBody] ConfirmationRequest request)
    {
        var user = new ApplicationUser { UserName = request.UserName };
        await emailSenderApplication.SendConfirmationLinkAsync(user, request.Email, request.ConfirmationLink);
        return Ok("Confirmation email sent.");
    }

    [HttpPost("send-reset-link")]
    public async Task<IActionResult> SendPasswordResetLink([FromBody] PasswordResetLinkRequest request)
    {
        var user = new ApplicationUser { UserName = request.UserName };
        await emailSenderApplication.SendPasswordResetLinkAsync(user, request.Email, request.ResetLink);
        return Ok("Password reset link sent.");
    }

    [HttpPost("send-reset-code")]
    public async Task<IActionResult> SendPasswordResetCode([FromBody] PasswordResetCodeRequest request)
    {
        var user = new ApplicationUser { UserName = request.UserName };
        await emailSenderApplication.SendPasswordResetCodeAsync(user, request.Email, request.ResetCode);
        return Ok("Password reset code sent.");
    }
}
