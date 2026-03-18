using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class GameScheduleService(ILeagueRepository leagueRepository) : IGameScheduleService
{
    public async Task<List<GameInfo>> GetGamesThisWeekAsync(int season, int week)
    {
        var spreads = await leagueRepository.GetNflSpreadsAsync(season, week);
        return spreads?.Select(s => new GameInfo(s.HomeTeam, s.AwayTeam, s.GameTime)).ToList()
               ?? [];
    }

    public async Task<List<DateOnly>> GetGameDaysThisWeekAsync(int season, int week)
    {
        var spreads = await leagueRepository.GetNflSpreadsAsync(season, week);
        return spreads?.Select(s => DateOnly.FromDateTime(s.GameTime))
                       .Distinct()
                       .OrderBy(d => d)
                       .ToList()
               ?? [];
    }

    public async Task<DateTime?> GetFirstKickoffTodayAsync(int season, int week)
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var spreads = await leagueRepository.GetNflSpreadsAsync(season, week);
        var todayGames = spreads?.Where(s => DateOnly.FromDateTime(s.GameTime) == todayUtc).ToList();
        if (todayGames is null || todayGames.Count == 0) return null;
        return todayGames.Min(s => s.GameTime);
    }

    public async Task<bool> HasGamesTodayAsync(int season, int week)
    {
        var kickoff = await GetFirstKickoffTodayAsync(season, week);
        return kickoff.HasValue;
    }
}
