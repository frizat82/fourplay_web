namespace FourPlayWebApp.Server.Services.Interfaces;

public record GameInfo(string HomeTeam, string AwayTeam, DateTime KickoffUtc);

public interface IGameScheduleService
{
    Task<List<GameInfo>> GetGamesThisWeekAsync(int season, int week);
    Task<List<DateOnly>> GetGameDaysThisWeekAsync(int season, int week);
    Task<DateTime?> GetFirstKickoffTodayAsync(int season, int week);
    Task<bool> HasGamesTodayAsync(int season, int week);
}
