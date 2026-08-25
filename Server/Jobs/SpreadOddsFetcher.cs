using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

/// <summary>
/// Shared "fetch odds, prefer DraftKings, fall back to any available provider, parse the American
/// spread strings" logic — the one piece of NflSpreadJob/CfbSpreadJob that was genuinely
/// identical (frizat-bo1). Each job still owns its own current-week/slate resolution and
/// NflSpreads/CfbSpreads construction; only this odds-fetch-and-parse step is shared. Callers pass
/// their own provider-specific delegates rather than this depending on IEspnCoreOddsService
/// directly, since NFL and CFB call different methods on it (GetEventsWithOddsAsync vs
/// GetCfbEventsWithOddsAsync).
/// </summary>
public static class SpreadOddsFetcher {
    public readonly record struct ParsedSpread(double HomeSpread, double AwaySpread, double OverUnder);

    public static async Task<ParsedSpread?> FetchAsync(
        Func<int, int, Task<EspnCoreOddsItem?>> getByProvider,
        Func<int, Task<EspnCoreOddsApiResponse?>> getAny,
        int eventId,
        string gameLabel) {
        var result = await getByProvider(eventId, (int)EspnOddsProviders.DraftKings);
        if (result is null) {
            // Warning, not Error — a game with no DraftKings line (or no market at all) is routine,
            // especially for CFB where most non-marquee matchups aren't covered by every book. This
            // is the common case, not an incident; treating it as Error here previously made
            // CfbSpreadJob (which calls this far more often than NflSpreadJob) needlessly noisy.
            Log.Warning("Spread not available using DraftKings for {Game} (event {EventId}), trying default Spreads", gameLabel, eventId);
            var allResults = await getAny(eventId);
            if (allResults is null || allResults.Count == 0) {
                Log.Warning("No spreads available for {Game} (event {EventId}), moving on", gameLabel, eventId);
                return null;
            }
            Log.Warning("Not using ESPNBet, found spread from {Provider} {Game} (event {EventId})", allResults.Items.First().Provider.Name, gameLabel, eventId);
            result = allResults.Items.First();
        }

        var cleanHomeSpread = result.HomeTeamOdds.Current.PointSpread.American.Replace("+", "");
        var cleanAwaySpread = result.AwayTeamOdds.Current.PointSpread.American.Replace("+", "");

        if (!double.TryParse(cleanHomeSpread, out var homeSpread)) return null;
        if (!double.TryParse(cleanAwaySpread, out var awaySpread)) return null;

        return new ParsedSpread(homeSpread, awaySpread, result.OverUnder);
    }
}
