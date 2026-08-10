using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbCurrentSlateService(ICfbRepository repo) : ICfbCurrentSlateService {
    private const int ConfiguredSeason = 2026;

    public async Task<CfbSlateInfo?> GetCurrentSlateAsync() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var slates = (await repo.GetSlatesForSeasonAsync(ConfiguredSeason)).ToList();

        // Empty or pre-season: no slates yet, or all are future — fall back to prior year
        if (slates.All(s => s.StartDate > today))
            slates = (await repo.GetSlatesForSeasonAsync(ConfiguredSeason - 1)).ToList();

        if (slates.Count == 0) return null;

        // First slate whose end date hasn't passed = current; all past → show last (off-season)
        var active = slates.FirstOrDefault(s => s.EndDate >= today) ?? slates[^1];

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
}
