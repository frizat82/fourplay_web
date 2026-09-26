using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

public class NflLiveScoreFetcher(IEspnApiService espnApi) : INflLiveScoreFetcher {
    public async Task<EspnScores?> FetchForWeekAsync(NflSeasonWeekConfig week) {
        var isPostSeason = week.WeekType == "PostSeason";
        var startDate = DateOnly.FromDateTime(week.WeekStartDatetime);
        var endDate = DateOnly.FromDateTime(week.WeekEndDatetime);

        return FilterToWeek(await espnApi.GetScoresByDateRangeAsync(startDate, endDate, isPostSeason), week);
    }

    public async Task<EspnScores?> FetchDaysAsync(NflSeasonWeekConfig week, IReadOnlyCollection<DateOnly> days) {
        var isPostSeason = week.WeekType == "PostSeason";
        return FilterToWeek(await EspnDateRangeFetcher.FetchDaysAsync(days, day => espnApi.GetScoresForDayAsync(day, isPostSeason)), week);
    }

    private static EspnScores? FilterToWeek(EspnScores? scoreboard, NflSeasonWeekConfig week) {
        if (scoreboard?.Events is null) return null;
        var events = GameHelpers.FilterEventsToDateWindow(scoreboard.Events, week.WeekStartDatetime, week.WeekEndDatetime);
        return events.Length == 0 ? null : GameHelpers.WithEvents(scoreboard, events);
    }
}
