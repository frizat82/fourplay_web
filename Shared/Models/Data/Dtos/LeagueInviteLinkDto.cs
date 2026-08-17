namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record LeagueInviteLinkDto(
    string Token,
    int LeagueId,
    string LeagueName,
    DateTimeOffset ExpiresAt
);
