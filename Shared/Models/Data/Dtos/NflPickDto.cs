using FourPlayWebApp.Shared.Models.Enum;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class NflPickDto : IEquatable<NflPickDto>
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PickType Pick { get; set; }  // Represents PickType enum as int for DTO
    public int NflWeek { get; set; }
    public int Season { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTime.UtcNow;
    public override int GetHashCode() {
        return HashCode.Combine(Team, Pick, Season, NflWeek, UserId, LeagueId);
    }

    public override bool Equals(object obj) {
        return Equals(obj as NflPickDto);
    }

    public bool Equals(NflPickDto other) {
        if (other == null)
            return false;

        return Team == other.Team && Pick == other.Pick && Season == other.Season
               && NflWeek == other.NflWeek && UserId == other.UserId && LeagueId == other.LeagueId;
    }

    public static bool operator ==(NflPickDto left, NflPickDto right) {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(NflPickDto left, NflPickDto right) {
        return !(left == right);
    }
}