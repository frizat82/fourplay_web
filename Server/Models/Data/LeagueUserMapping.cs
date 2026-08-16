using FourPlayWebApp.Server.Models.Identity;

namespace FourPlayWebApp.Server.Models.Data;
public class LeagueUserMapping {
    public int Id { get; set; }
    public LeagueInfo League { get; set; }
    public int LeagueId { get; set; }
    // Foreign key to the AspNetUsers table
    public string UserId { get; set; }
    public ApplicationUser User { get; set; } // Navigation property
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    // Soft-delete: a removed member keeps their row (and their pick history stays intact for
    // audit purposes) — they're just excluded from active-membership reads. See
    // LeagueRepository.RemoveLeagueUserMappingAsync / AddLeagueUserMappingAsync.
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? RemovedAt { get; set; }
}
