using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Jobs;

public class CfbSpreadScheduleSource(ICfbRepository repo) : ISpreadScheduleSource {
    public async Task<IEnumerable<SpreadTriggerCandidate>> GetCandidatesAsync() {
        var configs = (await repo.GetAllWeekConfigsAsync())
            .Where(c => c.InScopeIvLeague && c.IvLeagueWeekNumber != 99);
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        return configs.Select(cfg => new SpreadTriggerCandidate(
            cfg.SpreadLockDatetime,
            $"CFB Spreads {cfg.Season} Wk{cfg.IvLeagueWeekNumber}",
            $"CFB spreads for {CfbWeekLabelHelper.LabelFromConfig(cfg)} — scheduled lock time",
            weeksWithData.Contains((cfg.Season, cfg.IvLeagueWeekNumber))));
    }
}
