using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

public class NflLiveScoreFetcher(IEspnApiService espnApi) : INflLiveScoreFetcher {
    public async Task<EspnScores?> FetchForWeekAsync(NflSeasonWeekConfig week) {
        var startDate = DateOnly.FromDateTime(week.WeekStartDatetime);
        var endDate = DateOnly.FromDateTime(week.WeekEndDatetime);

        var scoreboard = await espnApi.GetScoresByDateRangeAsync(startDate, endDate, week.IsPostSeason);
        if (scoreboard?.Events is null) return null;

        var events = GameHelpers.FilterEventsToDateWindow(scoreboard.Events, week.WeekStartDatetime, week.WeekEndDatetime);
        return events.Length == 0 ? null : GameHelpers.WithEvents(scoreboard, events);
    }
}
