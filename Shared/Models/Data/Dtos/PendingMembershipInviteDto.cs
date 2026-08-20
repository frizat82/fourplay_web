namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record PendingMembershipInviteDto(
    int Id,
    int LeagueId,
    string LeagueName,
    string? InvitedByUserName,
    DateTimeOffset CreatedAt
);
