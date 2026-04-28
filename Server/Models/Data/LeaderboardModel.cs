using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Models.Data;

public class LeaderboardModel {
    public ApplicationUser User { get; set; }
    public long Total { get; set; }
    public string Rank { get; set; }
    public LeaderboardWeekResults[] WeekResults { get; set; }
}
