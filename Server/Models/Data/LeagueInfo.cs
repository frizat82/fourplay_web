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

    public ICollection<LeagueUserMapping> LeagueUsers { get; set; }
    public ICollection<NflPicks> NflPicks { get; set; }
}