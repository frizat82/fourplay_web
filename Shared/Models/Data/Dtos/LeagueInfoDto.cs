using FourPlayWebApp.Shared.Models.Enum;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class LeagueInfoDto
{
    public int Id { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public DateTimeOffset DateCreated { get; set; } = DateTime.UtcNow;
    public string OwnerUserId { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LeagueType LeagueType { get; set; } = LeagueType.Nfl;
}
