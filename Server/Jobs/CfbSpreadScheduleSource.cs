using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Jobs;

public class CfbSpreadScheduleSource(ICfbRepository repo) : ISpreadScheduleSource {
    public async Task<IEnumerable<SpreadTriggerCandidate>> GetCandidatesAsync() {
        var allConfigsTask = repo.GetAllWeekConfigsAsync();
        var weeksWithDataTask = repo.GetWeeksWithSpreadDataAsync();
        await Task.WhenAll(allConfigsTask, weeksWithDataTask);
        var configs = allConfigsTask.Result.Where(c => c.InScopeIvLeague && c.IvLeagueWeekNumber != 99);
        var weeksWithData = weeksWithDataTask.Result;

        return configs.Select(cfg => new SpreadTriggerCandidate(
            cfg.SpreadLockDatetime,
            $"CFB Spreads {cfg.Season} Wk{cfg.IvLeagueWeekNumber}",
            $"CFB spreads for {CfbWeekLabelHelper.LabelFromConfig(cfg)} — scheduled lock time",
            weeksWithData.Contains((cfg.Season, cfg.IvLeagueWeekNumber))));
    }
}
