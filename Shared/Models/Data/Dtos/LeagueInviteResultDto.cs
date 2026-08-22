using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public enum LeagueInviteOutcome { NewUserInvitationSent, ExistingUserInvitePending }

public record LeagueInviteResultDto(
    string Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] LeagueInviteOutcome Outcome
);
