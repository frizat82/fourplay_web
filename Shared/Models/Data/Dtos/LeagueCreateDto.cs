using FourPlayWebApp.Shared.Models.Enum;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record LeagueCreateDto(
    string LeagueName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] LeagueType LeagueType,
    string OwnerUserId,
    int Season,
    int Juice,
    int JuiceDivisional,
    int JuiceConference,
    int WeeklyCost
);

public record LeagueCostDto(int MemberCount, decimal Cost);

public record AdminLeagueCostDto(
    int LeagueId,
    string LeagueName,
    string OwnerUserName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] LeagueType LeagueType,
    int MemberCount,
    decimal Cost
);

public record LeagueJuiceUpdateDto(int Juice, int JuiceDivisional, int JuiceConference, int WeeklyCost);
