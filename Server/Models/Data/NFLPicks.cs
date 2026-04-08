using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Enum;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models.Data;
public class NflPicks {
    public int Id { get; set; }
    public LeagueInfo League { get; set; }
    // Foreign key to the LeagueInfo table
    public int LeagueId { get; set; }
    // Foreign key to the AspNetUsers table
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public string Team { get; set; }
    public PickType Pick { get; set; } = PickType.Spread;
    public int NflWeek { get; set; }
    public int Season { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    // Navigation property
    public NflWeeks NflWeekInfo { get; set; }
    public int NflWeekId { get; set; }
    // Foreign key to the NFlWeeks table
    public override int GetHashCode() {
        return HashCode.Combine(Team, Pick, Season, NflWeek, UserId, LeagueId);
    }

    public override bool Equals(object obj) {
        return Equals(obj as NflPicks);
    }
    //x.UserId, x.LeagueId, NFLWeek = x.NflWeek, x.Season, x.Team, x.Pick
    public bool Equals(NflPicks other) {
        if (other == null)
            return false;

        return Team == other.Team && Pick == other.Pick && Season == other.Season
               && NflWeek == other.NflWeek && UserId == other.UserId && LeagueId == other.LeagueId;
    }

    public static bool operator ==(NflPicks left, NflPicks right) {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(NflPicks left, NflPicks right) {
        return !(left == right);
    }
}