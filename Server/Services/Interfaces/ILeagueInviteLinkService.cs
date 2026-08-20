using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ILeagueInviteLinkService
{
    Task<LeagueInviteLink> GenerateAsync(int leagueId, string createdByUserId);
    Task<LeagueInviteLink?> ValidateAsync(string token);
    Task<LeagueInviteLink?> GetCurrentAsync(int leagueId);
    Task RevokeAsync(int leagueId);
}
