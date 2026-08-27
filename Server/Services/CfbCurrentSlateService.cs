using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo) : ICfbCurrentSlateService {
    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var now = DateTime.UtcNow;
        var slates = (await repo.GetAllSlatesAsync()).ToList();

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
        var configsByKey = (await repo.GetAllWeekConfigsAsync())
            .Where(c => c.InScopeIvLeague && c.IvLeagueWeekNumber != 99)
            .ToDictionary(c => (c.Season, c.IvLeagueWeekNumber));

        CfbSeasonWeekConfig ConfigFor(CfbSlates s) =>
            configsByKey.TryGetValue((s.Season, s.SlateNumber), out var cfg)
                ? cfg
                // A slate with no matching config is a broken data-integrity invariant, not an
                // expected "not configured yet" state (SpreadLockDatetime can no longer be
                // null), so this fails loudly rather than silently skipping.
                : throw new InvalidOperationException(
                    $"CfbSlates {s.Id} (Season {s.Season}, SlateNumber {s.SlateNumber}) has no matching CfbSeasonWeekConfig row — data integrity issue, not a normal state.");

        // frizat-9xg: most recent slate whose own spread grab has passed, unless we're within
        // 2 days of the next slate's spread grab — see SeasonWindowResolver for the shared
        // NFL/CFB logic (applies identically across a season boundary, no special case here).
        var windows = slates.Select(s => new SeasonWindowResolver.WeekWindow(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue),
            ConfigFor(s).SpreadLockDatetime));
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(windows, now);
        if (resolved is null) return null;

        var active = slates.First(s => s.Season == resolved.Value.Season
            && s.StartDate.ToDateTime(TimeOnly.MinValue) == resolved.Value.Start
            && s.EndDate.ToDateTime(TimeOnly.MaxValue) == resolved.Value.End);

        return new CfbSlateInfo(active.Id, active.Season, active.SlateNumber, active.Label,
            active.SlateType, active.StartDate, active.EndDate, active.FirstGameUtc, ConfigFor(active).SpreadLockDatetime);
    }

    public async Task<bool> IsSeasonActiveAsync() {
        var slates = await repo.GetAllSlatesAsync();
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue)));
        return SeasonWindowResolver.IsSeasonActive(windows, DateTime.UtcNow);
    }
}
