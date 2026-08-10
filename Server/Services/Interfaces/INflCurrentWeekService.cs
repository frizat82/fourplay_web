namespace FourPlayWebApp.Server.Services.Interfaces;

public record NflWeekInfo(int WeekId, int EspnWeek, int Season, bool IsPostSeason, string WeekLabel, string ScoringFormat, DateTime SpreadLockDatetime);

public interface INflCurrentWeekService {
    Task<NflWeekInfo> GetCurrentWeekAsync();
}
