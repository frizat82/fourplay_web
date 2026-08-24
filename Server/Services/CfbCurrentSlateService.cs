using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo) : ICfbCurrentSlateService {
    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = today.ToDateTime(TimeOnly.MinValue);
        var slates = (await repo.GetAllSlatesAsync()).ToList();

        // Active window wins; else most-recently-completed (off-season); else soonest
        // upcoming (pre-season) — see SeasonWindowResolver for the shared NFL/CFB logic.
        // No hardcoded season: resolves purely from whatever slates exist in the DB.
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue)));
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(windows, now);
        if (resolved is null) return null;

        var active = slates.First(s => s.Season == resolved.Value.Season
            && s.StartDate.ToDateTime(TimeOnly.MinValue) == resolved.Value.Start
            && s.EndDate.ToDateTime(TimeOnly.MaxValue) == resolved.Value.End);

        var configs = await repo.GetWeekConfigsForSeasonAsync(active.Season);
        var matchingConfig = configs.FirstOrDefault(c => c.IvLeagueWeekNumber == active.SlateNumber);
        // Every CfbSlates row is seeded FROM a CfbSeasonWeekConfig row (CfbSlateSeederJob sets
        // SlateNumber = cfg.IvLeagueWeekNumber) — a slate with no matching config is a broken
        // data-integrity invariant, not an expected "not configured yet" state (SpreadLockDatetime
        // itself can no longer be null), so this fails loudly rather than silently skipping.
        if (matchingConfig is null)
            throw new InvalidOperationException(
                $"CfbSlates {active.Id} (Season {active.Season}, SlateNumber {active.SlateNumber}) has no matching CfbSeasonWeekConfig row — data integrity issue, not a normal state.");

        return new CfbSlateInfo(active.Id, active.Season, active.SlateNumber, active.Label,
            active.SlateType, active.StartDate, active.EndDate, active.FirstGameUtc, matchingConfig.SpreadLockDatetime);
    }

    public async Task<bool> IsSeasonActiveAsync() {
        var slates = await repo.GetAllSlatesAsync();
        var windows = slates.Select(s => new SeasonWindowResolver.Window(
            s.Season, s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue)));
        return SeasonWindowResolver.IsSeasonActive(windows, DateTime.UtcNow);
    }
}
