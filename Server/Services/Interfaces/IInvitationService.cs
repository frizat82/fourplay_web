using FourPlayWebApp.Server.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IInvitationService
{
    /// <summary>
    /// Delete an invitation by its code
    /// <param name="id">Invitation to delete</param>
    /// </summary>
    Task DeleteInvitationAsync(int id);
    /// <summary>
    /// Create a new invitation for the specified email. When <paramref name="baseUrl"/> is
    /// provided, the invitation email is sent server-side immediately.
    /// </summary>
    /// <param name="email">Email to invite</param>
    /// <param name="invitedByUserId">User ID of the person sending the invite</param>
    /// <param name="baseUrl">Frontend origin (e.g. https://ivleague.com) used to build the registration link, if the email should be sent</param>
    /// <returns>The created invitation</returns>
    Task<Invitation> CreateInvitationAsync(string email, string invitedByUserId, int? leagueId = null, string? baseUrl = null);

    /// <summary>
    /// Re-send the invitation email for an existing, still-valid invitation.
    /// </summary>
    /// <param name="invitationId">Invitation to re-send</param>
    /// <param name="baseUrl">Frontend origin used to build the registration link</param>
    Task ResendInvitationEmailAsync(int invitationId, string baseUrl);

    /// <summary>
    /// Validate if the invitation code is valid
    /// </summary>
    /// <param name="code">Invitation code to validate</param>
    /// <returns>The invitation if valid, null otherwise</returns>
    Task<Invitation?> ValidateInvitationAsync(string code);

    /// <summary>
    /// Mark an invitation as used
    /// </summary>
    /// <param name="code">Invitation code</param>
    /// <param name="registeredUserId">ID of the user who registered with this invitation</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> MarkInvitationAsUsedAsync(string code, string registeredUserId);

    /// <summary>
    /// Get all invitations
    /// </summary>
    /// <returns>List of all invitations</returns>
    Task<List<Invitation>> GetAllInvitationsAsync();

    /// <summary>
    /// Get invitations created by a specific user
    /// </summary>
    /// <param name="userId">User ID who created the invitations</param>
    /// <returns>List of invitations created by the user</returns>
    Task<List<Invitation>> GetInvitationsByUserAsync(string userId);

}
