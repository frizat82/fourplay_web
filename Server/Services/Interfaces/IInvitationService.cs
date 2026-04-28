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
    /// Create a new invitation for the specified email
    /// </summary>
    /// <param name="email">Email to invite</param>
    /// <param name="invitedByUserId">User ID of the person sending the invite</param>
    /// <returns>The created invitation</returns>
    Task<Invitation> CreateInvitationAsync(string email, string invitedByUserId, int? leagueId = null);

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
