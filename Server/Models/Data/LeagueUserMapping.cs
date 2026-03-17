using FourPlayWebApp.Server.Models.Identity;

namespace FourPlayWebApp.Server.Models.Data;
public class LeagueUserMapping {
    public int Id { get; set; }
    public LeagueInfo League { get; set; }
    public int LeagueId { get; set; }
    // Foreign key to the AspNetUsers table
    public string UserId { get; set; }
    public ApplicationUser User { get; set; } // Navigation property
    public DateTimeOffset DateCreated { get; set; } = DateTime.UtcNow;
}