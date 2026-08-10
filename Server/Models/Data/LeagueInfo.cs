using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;
public class LeagueInfo {
    public int Id { get; set; }
    public string LeagueName { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    // Foreign key to the AspNetUsers table
    public string OwnerUserId { get; set; }
    public ApplicationUser Owner { get; set; } // Navigation property
    public LeagueType LeagueType { get; set; } = LeagueType.Nfl;
    public ICollection<LeagueJuiceMapping> LeagueJuiceMappings { get; set; } = new List<LeagueJuiceMapping>();

    // Renamed from "LeagueUsers" (frizat-896 schema audit) — that name collided with the unrelated
    // LeagueUsers entity/table (an orphaned write-only email allowlist, see audit notes on
    // frizat-896), even though this collection is actually LeagueUserMapping rows.
    public ICollection<LeagueUserMapping> LeagueUserMappings { get; set; }
    public ICollection<NflPicks> NflPicks { get; set; }
}
