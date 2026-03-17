using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Models.Mappers;
using FourPlayWebApp.Server.Services.Interfaces;
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
public class InvitationController(IInvitationService invitationService, IEmailSender emailSender, IEmailSender<ApplicationUser> emailSenderApplication) : ControllerBase {
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
    public async Task<ActionResult<InvitationDto>> Create([FromQuery] string email, [FromQuery] string invitedByUserId)
    {
        var invitation = await invitationService.CreateInvitationAsync(email, invitedByUserId);
        return CreatedAtAction(nameof(GetAll), new { id = invitation.Id }, invitation.ToDto());
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

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
    {
        await emailSender.SendEmailAsync(request.ToEmail, request.Subject, request.HtmlBody);
        return Ok("Email sent.");
    }

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
