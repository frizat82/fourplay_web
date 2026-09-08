using System.Linq;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;
public class LeagueInfo {
    public int Id { get; set; }
    public string LeagueName { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    // Foreign key to the AspNetUsers table
    public string OwnerUserId { get; set; }
    public ApplicationUser Owner { get; set; } // Navigation property
    public LeagueType LeagueType { get; set; } = LeagueType.Nfl;
    public ICollection<LeagueJuiceMapping> LeagueJuiceMappings { get; set; } = new List<LeagueJuiceMapping>();

    public ICollection<LeagueUserMapping> LeagueUserMappings { get; set; }
    public ICollection<NflPicks> NflPicks { get; set; }

    // The earliest season this league has ever been configured to play (via LeagueJuiceMapping) —
    // null if it's never been configured for any season. The single source of truth for "did this
    // league exist yet" across admin endpoints (GetAllLeagues' MinSeason field, GetAllLeaguesCost's
    // per-season exclusion filter), so both stay in sync instead of re-deriving it independently.
    public int? MinConfiguredSeason() =>
        LeagueJuiceMappings.Count > 0 ? LeagueJuiceMappings.Min(m => m.Season) : (int?)null;
}
