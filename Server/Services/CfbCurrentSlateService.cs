using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo, [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider timeProvider) : ICfbCurrentSlateService {
    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var active = ResolveCurrentSlate(await repo.GetAllSlatesAsync(), timeProvider.GetUtcNow().UtcDateTime);
        return active is null ? null : active.ToSlateInfo();
    }

    // frizat-9xg: most recent slate whose own spread grab has passed, unless we're within
    // 1 day of the next slate's spread grab — see SeasonWindowResolver for the shared
    // NFL/CFB logic (applies identically across a season boundary, no special case here).
    // Shared with DemoCfbCacheService (frizat-d0t), so both resolve "current" the same way.
    public static CfbSeasonWeekConfig? ResolveCurrentSlate(IEnumerable<CfbSeasonWeekConfig> slates, DateTime now) {
        var slateWindows = BuildSlateWindows(slates);
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(slateWindows.Select(sw => sw.Window), now);
        return resolved is null ? null : slateWindows.First(sw => sw.Window == resolved.Value).Slate;
    }

    // frizat-d0t: shared so DemoCfbCacheService resolves "is this the current slate" the SAME way
    // this class does. frizat-b4g: a slate is its control-table row, so its window and its
    // spread-grab time come from the same row — no cross-table lookup to go missing.
    public static List<(CfbSeasonWeekConfig Slate, SeasonWindowResolver.WeekWindow Window)> BuildSlateWindows(
        IEnumerable<CfbSeasonWeekConfig> slates) =>
        slates.Select(s => (s, new SeasonWindowResolver.WeekWindow(
            s.Season, s.StartUtc, s.EndUtc, s.SpreadLockDatetime))).ToList();

    public async Task<bool> IsSeasonActiveAsync() {
        var slates = await repo.GetAllSlatesAsync();
        var windows = slates.Select(s => new SeasonWindowResolver.Window(s.Season, s.StartUtc, s.EndUtc));
        return SeasonWindowResolver.IsSeasonActive(windows, timeProvider.GetUtcNow().UtcDateTime);
    }
}
