using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Jobs;

public class NflSpreadScheduleSource(ILeagueRepository repo) : ISpreadScheduleSource {
    public async Task<IEnumerable<SpreadTriggerCandidate>> GetCandidatesAsync() {
        var configsTask = repo.GetNflSeasonWeekConfigsAsync();
        var weeksWithDataTask = repo.GetWeeksWithSpreadDataAsync();
        await Task.WhenAll(configsTask, weeksWithDataTask);
        var configs = configsTask.Result;
        var weeksWithData = weeksWithDataTask.Result;

        return configs.Select(cfg => new SpreadTriggerCandidate(
            cfg.SpreadLockDatetime,
            $"NFL Spreads {cfg.Season} Wk{cfg.WeekId}",
            $"NFL spreads for {cfg.WeekLabel} — scheduled lock time",
            weeksWithData.Contains((cfg.Season, cfg.WeekId))));
    }
}
