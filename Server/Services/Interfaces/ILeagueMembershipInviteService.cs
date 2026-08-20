using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ILeagueMembershipInviteService
{
    Task CreateOrReopenAsync(int leagueId, string invitedUserId, string invitedByUserId);
    Task<LeagueMembershipInvite?> GetByIdAsync(int id);
    Task<List<LeagueMembershipInvite>> GetPendingForUserAsync(string userId);
    Task<List<LeagueMembershipInvite>> GetByLeagueAsync(int leagueId);
    Task MarkAcceptedAsync(int id);
    Task MarkDeclinedAsync(int id);
    Task DeleteAsync(int id);
}
