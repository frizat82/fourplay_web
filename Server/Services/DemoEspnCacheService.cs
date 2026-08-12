using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Demo-only implementation of IEspnCacheService that serves frozen ESPN data
/// from sample_espn_nfl.json. Only registered when DEMO_MODE=true (the Railway "development"
/// environment included — DEMO_MODE is an app-config flag, not tied to any particular
/// environment name). Real (non-demo) prod never registers this class at all.
/// </summary>
public class DemoEspnCacheService : IEspnCacheService
{
    private readonly EspnScores? _scores;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public DemoEspnCacheService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _scores = DemoFixtureLoader.Load("sample_espn_nfl.json", json =>
        {
            foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping)
                json = json.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
            return json;
        });
    }

    public event Action? ScoresChanged; // never fired — demo data is static

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_scores);

    // Backs the "browse a non-current week" path on the Scores page (GetWeekScores in
    // EspnController) — unlike GetScoresAsync above (a single frozen "in-progress" fixture that
    // can't come from the DB), historical weeks are read straight from the seeded NflScores table,
    // since every seeded game is already FINAL by the time it's persisted (see NflScoresJob).
    // Registered as a Singleton, so a scoped ApplicationDbContext can't be injected directly —
    // use the DbContext factory instead (same pattern as InvitationLeagueTests' BuildFactory).
    public async Task<EspnScores?> GetWeekScoresAsync(int week, int year, bool postSeason = false)
    {
        var nflWeek = GameHelpers.GetWeekFromEspnWeek(week, postSeason);

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var rows = await db.NflScores
            .Where(s => s.Season == year && s.NflWeek == nflWeek)
            .ToListAsync();

        if (rows.Count == 0) return null;

        var events = rows.Select(row => BuildEvent(row, week, year, postSeason)).ToArray();

        return new EspnScores {
            Leagues = [],
            Season = new Season { Year = year, Type = postSeason ? 3 : 2 },
            Week = new Week { Number = week },
            Events = events,
        };
    }

    private static Event BuildEvent(Shared.Models.Data.NflScores row, int week, int year, bool postSeason)
    {
        var id = row.Id.ToString();
        var competition = new Competition {
            Id = id,
            Date = row.GameTime,
            Competitors = [
                new Competitor {
                    Id = "1",
                    HomeAway = HomeAway.Home,
                    Team = new EspnTeam { Abbreviation = row.HomeTeam },
                    Score = row.HomeTeamScore,
                    Records = [],
                },
                new Competitor {
                    Id = "2",
                    HomeAway = HomeAway.Away,
                    Team = new EspnTeam { Abbreviation = row.AwayTeam },
                    Score = row.AwayTeamScore,
                    Records = [],
                },
            ],
            Status = new EspnStatus {
                Clock = 0,
                DisplayClock = "0:00",
                Period = 4,
                Type = new StatusType {
                    Id = 3,
                    Name = TypeName.StatusFinal,
                    State = State.Post,
                    Completed = true,
                    Description = Description.Final,
                    Detail = "Final",
                    ShortDetail = "Final",
                },
            },
            Odds = [],
            Situation = null,
        };

        return new Event {
            Id = id,
            Season = new Season { Year = year, Type = postSeason ? 3 : 2 },
            Week = new Week { Number = week },
            Date = row.GameTime,
            Competitions = [competition],
            Weather = null,
        };
    }
}
