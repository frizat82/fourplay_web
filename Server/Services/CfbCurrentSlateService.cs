using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo, [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider timeProvider) : ICfbCurrentSlateService {
    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var slateWindows = BuildSlateWindows(await repo.GetAllSlatesAsync());

        // frizat-9xg: most recent slate whose own spread grab has passed, unless we're within
        // 2 days of the next slate's spread grab — see SeasonWindowResolver for the shared
        // NFL/CFB logic (applies identically across a season boundary, no special case here).
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(slateWindows.Select(sw => sw.Window), now);
        if (resolved is null) return null;

        var (active, _) = slateWindows.First(sw => sw.Window.Season == resolved.Value.Season
            && sw.Window.Start == resolved.Value.Start && sw.Window.End == resolved.Value.End);

        return new CfbSlateInfo(active.Id, active.Season, active.SlateNumber, active.WeekLabel,
            active.WeekStartDate, active.WeekEndDate, active.SpreadLockDatetime,
            active.IsPostSeason, active.RequiredPicks);
    }

    // frizat-d0t: shared so DemoCfbCacheService resolves "is this the current slate" the SAME way
    // this class does. frizat-b4g: a slate is its control-table row, so its window and its
    // spread-grab time come from the same row — no cross-table lookup to go missing.
    public static List<(CfbSeasonWeekConfig Slate, SeasonWindowResolver.WeekWindow Window)> BuildSlateWindows(
        IEnumerable<CfbSeasonWeekConfig> slates) =>
        slates.Select(s => (s, new SeasonWindowResolver.WeekWindow(
            s.Season, s.WeekStartDate.ToDateTime(TimeOnly.MinValue), s.WeekEndDate.ToDateTime(TimeOnly.MaxValue),
            s.SpreadLockDatetime))).ToList();

    public async Task<bool> IsSeasonActiveAsync() {
        var slates = await repo.GetAllSlatesAsync();
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.WeekStartDate.ToDateTime(TimeOnly.MinValue), s.WeekEndDate.ToDateTime(TimeOnly.MaxValue)));
        return SeasonWindowResolver.IsSeasonActive(windows, timeProvider.GetUtcNow().UtcDateTime);
    }
}
