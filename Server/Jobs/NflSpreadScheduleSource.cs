using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Jobs;

public class NflSpreadScheduleSource(ILeagueRepository repo) : ISpreadScheduleSource {
    public async Task<IEnumerable<SpreadTriggerCandidate>> GetCandidatesAsync() {
        var configs = await repo.GetNflSeasonWeekConfigsAsync();
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        return configs.Select(cfg => new SpreadTriggerCandidate(
            cfg.SpreadLockDatetime,
            $"NFL Spreads {cfg.Season} Wk{cfg.WeekId}",
            $"NFL spreads for {cfg.WeekLabel} — scheduled lock time",
            weeksWithData.Contains((cfg.Season, cfg.WeekId))));
    }
}
