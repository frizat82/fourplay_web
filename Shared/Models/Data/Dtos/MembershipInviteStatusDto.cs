using System.Text.Json.Serialization;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record MembershipInviteStatusDto(
    int Id,
    int LeagueId,
    string InvitedUserEmail,
    string? InvitedUserName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MembershipInviteStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt
);
