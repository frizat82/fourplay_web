using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbScoresJob(ICfbApiService cfbApi, ICfbRepository repo) : IJob {
    private const int Season = 2025;

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbScoresJob: fetching CFP scores at {Time}", DateTime.UtcNow);

        var slates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (slates.Count == 0) {
            Log.Warning("CfbScoresJob: no slates found for season {Season}", Season);
            return;
        }

        var scores = new List<CfbScores>();

        foreach (var slate in slates) {
            for (var date = slate.StartDate; date <= slate.EndDate; date = date.AddDays(1)) {
                var scoreboard = await cfbApi.GetScoresByDateAsync(date);
                if (scoreboard?.Events is null) continue;

                foreach (var evt in scoreboard.Events) {
                    var comp = evt.Competitions.FirstOrDefault();
                    if (comp is null) continue;

                    var status = comp.Status.Type.Name;
                    if (status != TypeName.StatusFinal && status != TypeName.StatusInProgress) continue;

                    var home = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Home);
                    var away = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Away);
                    if (home is null || away is null) continue;

                    scores.Add(new CfbScores {
                        CfbSlateId    = slate.Id,
                        EspnEventId   = int.Parse(evt.Id),
                        HomeTeam      = home.Team.Abbreviation,
                        AwayTeam      = away.Team.Abbreviation,
                        HomeTeamScore = (int)home.Score,
                        AwayTeamScore = (int)away.Score,
                        GameStatus    = status.ToString(),
                        GameTime      = comp.Date,
                    });
                }
            }
        }

        if (scores.Count > 0) {
            await repo.UpsertCfbScoresAsync(scores);
            Log.Information("CfbScoresJob: upserted {Count} CFP scores", scores.Count);
        }
        Log.Information("CfbScoresJob: complete at {Time}", DateTime.UtcNow);
    }
}
