using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ILeagueInviteLinkService
{
    Task<LeagueInviteLink> GenerateAsync(int leagueId, string createdByUserId, LeagueInfo league);
    Task<LeagueInviteLink?> ValidateAsync(string token);
}
