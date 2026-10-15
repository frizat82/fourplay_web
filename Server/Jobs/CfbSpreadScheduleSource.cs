using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Jobs;

public class CfbSpreadScheduleSource(ICfbRepository repo) : ISpreadScheduleSource {
    public async Task<IEnumerable<TimedTriggerCandidate>> GetCandidatesAsync() {
        var slatesTask = repo.GetAllSlatesAsync();
        var weeksWithDataTask = repo.GetWeeksWithSpreadDataAsync();
        await Task.WhenAll(slatesTask, weeksWithDataTask);
        var weeksWithData = weeksWithDataTask.Result;

        return slatesTask.Result.Select(cfg => new TimedTriggerCandidate(
            cfg.SpreadLockDatetime,
            $"CFB Spreads {cfg.Season} Wk{cfg.SlateNumber}",
            $"CFB spreads for {cfg.WeekLabel} — scheduled lock time",
            weeksWithData.Contains((cfg.Season, cfg.SlateNumber))));
    }
}
