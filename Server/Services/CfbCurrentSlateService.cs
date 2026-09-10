using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo, [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider timeProvider) : ICfbCurrentSlateService {
    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var slates = (await repo.GetAllSlatesAsync()).ToList();
        var configs = await repo.GetAllWeekConfigsAsync();
        var slateWindows = BuildSlateWindows(slates, configs);

        // frizat-9xg: most recent slate whose own spread grab has passed, unless we're within
        // 2 days of the next slate's spread grab — see SeasonWindowResolver for the shared
        // NFL/CFB logic (applies identically across a season boundary, no special case here).
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(slateWindows.Select(sw => sw.Window), now);
        if (resolved is null) return null;

        var (active, activeWindow) = slateWindows.First(sw => sw.Window.Season == resolved.Value.Season
            && sw.Window.Start == resolved.Value.Start && sw.Window.End == resolved.Value.End);

        return new CfbSlateInfo(active.Id, active.Season, active.SlateNumber, active.Label,
            active.SlateType, active.StartDate, active.EndDate, active.FirstGameUtc, activeWindow.SpreadLockDatetime);
    }

    // frizat-d0t: extracted so DemoCfbCacheService.GetSlateScoresAsync can resolve "is this the
    // current slate" the SAME way this class does — including the fail-loud throw below —
    // instead of a second, independently-derived copy that could silently disagree (a prior
    // version of that demo-mode copy defaulted a missing config to DateTime.MaxValue instead of
    // throwing, which would have let demo/e2e "current slate" resolution silently diverge from
    // real prod behavior for the exact data-integrity case this throw exists to catch).
    public static List<(CfbSlates Slate, SeasonWindowResolver.WeekWindow Window)> BuildSlateWindows(
        IEnumerable<CfbSlates> slates, IEnumerable<CfbSeasonWeekConfig> configs) {
        // Every CfbSlates row is seeded FROM a CfbSeasonWeekConfig row (CfbSlateSeederJob sets
        // SlateNumber = cfg.IvLeagueWeekNumber), which is where SpreadLockDatetime actually
        // lives — one query for every season's configs (same pattern as
        // CfbSpreadScheduleSource/LeagueJuiceScheduleSource), not one query per season, so
        // ResolveCurrentWeek can see each slate's own spread-grab time below.
        // Filter matches CfbSpreadScheduleSource/CfbSlateSeederJob's own convention: rows
        // outside IV League's scope (bye/dead weeks) legitimately share IvLeagueWeekNumber=99
        // within the same season (see CfbSeasonWeekConfigConfiguration's filtered unique index
        // and DemoDataSeeder's CfbDemoSeason rows) — keying a dictionary on it unfiltered throws
        // on the very first season with more than one such row.
        var configsByKey = configs
            .Where(c => c.InScopeIvLeague && c.IvLeagueWeekNumber != 99)
            .ToDictionary(c => (c.Season, c.IvLeagueWeekNumber));

        DateTime SpreadLockFor(CfbSlates s) =>
            configsByKey.TryGetValue((s.Season, s.SlateNumber), out var cfg)
                ? cfg.SpreadLockDatetime
                // A slate with no matching config is a broken data-integrity invariant, not an
                // expected "not configured yet" state (SpreadLockDatetime can no longer be
                // null), so this fails loudly rather than silently skipping.
                : throw new InvalidOperationException(
                    $"CfbSlates {s.Id} (Season {s.Season}, SlateNumber {s.SlateNumber}) has no matching CfbSeasonWeekConfig row — data integrity issue, not a normal state.");

        return slates.Select(s => (s, new SeasonWindowResolver.WeekWindow(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue),
            SpreadLockFor(s)))).ToList();
    }

    public async Task<bool> IsSeasonActiveAsync() {
        var slates = await repo.GetAllSlatesAsync();
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue)));
        return SeasonWindowResolver.IsSeasonActive(windows, timeProvider.GetUtcNow().UtcDateTime);
    }
}
