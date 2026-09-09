using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Helpers;

public static class GameHelpers {
    // Shared by NflLiveScoreFetcher and CfbLiveScoreFetcher — defense in depth on top of each
    // fetcher's own dates= query param (frizat-11t): don't fully trust ESPN to honor a date range
    // perfectly either (e.g. a timezone-boundary edge case on a late West Coast kickoff). One
    // implementation instead of drifting per-sport copies of the same boundary check.
    public static Event[] FilterEventsToDateWindow(IEnumerable<Event> events, DateTime start, DateTime end) =>
        events.Where(e => {
            var comp = e.Competitions.FirstOrDefault();
            return comp is not null && comp.Date.Date >= start.Date && comp.Date.Date <= end.Date;
        }).ToArray();

    public static EspnScores WithEvents(EspnScores source, Event[] events) => new() {
        Leagues = source.Leagues,
        Season  = source.Season,
        Week    = source.Week,
        Events  = events,
    };

    public static bool IsPastNoonCst
    {
        get
        {
            var nowUtc = PageTimeProvider.UtcNow;
            var nowCst = TimeZoneHelpers.ConvertTimeToCst(nowUtc);

            // Sunday noon CST
            var sundayNoon = nowCst.Date.AddDays(-(int)nowCst.DayOfWeek).AddHours(12);

            // Monday 11:50 PM CST
            var monday = sundayNoon.AddDays(1);
            var monday1150Pm = monday.Date.AddHours(23).AddMinutes(50);

            // True if current time is between Sunday noon and Monday 11:50 PM
            return nowCst >= sundayNoon && nowCst <= monday1150Pm;
        }
    }

    public static bool IsNoonCst
    {
        get {
            var until = UntilNoonCst();
            if (until is null || until.Value.TotalMinutes <= 0)
                return true;
            return false;
        }
    }

    private static TimeSpan? UntilNoonCst() {
        var nowUtc = PageTimeProvider.UtcNow;
        var nowCst = TimeZoneHelpers.ConvertTimeToCst(nowUtc);

        // Figure out the upcoming Sunday noon in CST
        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)nowCst.DayOfWeek + 7) % 7;
        var nextSundayNoon = nowCst.Date.AddDays(daysUntilSunday).AddHours(12);

        // If it's already past Sunday noon this week, move to next week
        if (nowCst >= nextSundayNoon)
            return null;

        // Convert target back to UTC so subtraction is safe
        return nextSundayNoon - nowCst;
    }
    public static string? DaysHoursMinutesUntilNoonCst() {
        var span = UntilNoonCst();
        return span is null ? null : $"{span.Value.Days}d {span.Value.Hours}h {span.Value.Minutes}m";
    }
    // TODO(docs/ESPNProBowl.md): verify against a real ESPN response once the 2026 postseason
    // bracket actually exists (~Jan 2027) — the NFL discontinued the Pro Bowl GAME starting with
    // the 2026 season (announced 2026-08-26), so ESPN's postseason week numbering is expected to
    // close the old week-4 gap from here on. See docs/ESPNProBowl.md for the full writeup; update
    // this value if this guess turns out wrong. Public (not the private it was originally) so
    // ESPNApiService.FixEspnProbBowlWeek can gate its own identical quirk-correction on the same
    // threshold instead of carrying an independent, unconditional copy (frizat-4k9) — this and
    // GetWeekFromEspnWeek below are the only two places this threshold is allowed to live.
    public const int LastSeasonEspnSkippedProBowlWeek = 2025;

    // Through the 2025 season, ESPN skipped postseason week 4 for the Pro Bowl — Super Bowl was
    // raw ESPN week 5 (one slot past Conference Championship=3), not the 4th round it actually is.
    // Gated by SEASON, not by the raw week value: for 2026+ we deliberately do NOT apply the old
    // week==5 special case, so if that guess is wrong, a real ESPN week=5 postseason response for
    // 2026+ maps to a nonexistent WeekId (23) and fails loudly (no matching config/DB rows found)
    // instead of silently coinciding with the right answer by accident.
    public static int GetWeekFromEspnWeek(long week, int season, bool isPostSeason = false) {
        if (!isPostSeason) return (int)week;
        if (season <= LastSeasonEspnSkippedProBowlWeek && week == 5) return 22;
        return (int)(week + 18);
    }
    public static string GetWeekName(long week, bool isPostSeason = false) {
        if (!isPostSeason) {
            return $"Week {week}";
        }
        return week switch {
            1 => "Wild Card",
            2 => "Divisional Round",
            3 => "Conference Championship",
            4 => "Super Bowl",
            _ => throw new ArgumentException("Invalid week number")
        };
    }
    public static int GetWeekFromName(string weekName, bool isPostSeason = false) {
        if (!isPostSeason) {
            return int.Parse(weekName.Replace("Week ", ""));
        }

        return weekName switch {
            "Wild Card" => 1,
            "Divisional Round" => 2,
            "Conference Championship" => 3,
            "Super Bowl" => 4,
            _ => throw new ArgumentException("Invalid week name")
        };
    }

    // ESPN resets postseason weeks to 1-5: 1=Wild Card, 2=Divisional, 3=Conf., 4=Pro Bowl, 5=Super Bowl
    public static int GetEspnRequiredPicks(long week, bool isPostSeason = false) {
        if (!isPostSeason) return 4;
        return week switch {
            1 => 3,
            2 => 3,
            3 => 2,
            4 => 1,
            5 => 1, // ESPN Treats Post Season Week 4 as the Pro Bowl - this sucks
            _ => throw new ArgumentException("Invalid week number")
        };
    }

    public static int GetRequiredPicks(long week) {
        if (week < 19) {
            return 4;
        }
        return week switch {
            19 => 3,
            20 => 3,
            21 => 2,
            22 => 1,
            _ => throw new ArgumentException("Invalid week number")
        };
    }

    // 18-slate system: Standard(1-14)=4, NFLDivisional(15-16)=3, NFLConference(17)=2, NFLSuperBowl(18)=1
    public static int GetCfbRequiredPicks(int slateNumber) => slateNumber switch {
        <= 14 => 4,
        <= 16 => 3,
        17 => 2,
        _ => 1,
    };

    public static string? DisplayDetails(Competition competition) {
        return competition.Status.Type.Name switch {
            TypeName.StatusScheduled => TimeZoneHelpers.ConvertTimeToCst(competition.Date.DateTime)
                .ToString("ddd, MMMM dd hh:mm tt"),
            TypeName.StatusHalftime => "Half Time",
            TypeName.StatusInProgress or TypeName.StatusEndPeriod => "In Progress",
            TypeName.StatusFinal => "Final",
            _ => "          "
        };
    }
    public static Competition GetCompetitionFromHomeAwayAbbr(string homeTeamAbbr, string awayTeamAbbr, EspnScores scores) {
        foreach (var scoreEvent in scores.Events) {
            foreach (var competition in scoreEvent.Competitions) {
                if (GetHomeTeamAbbr(competition) == homeTeamAbbr && GetAwayTeamAbbr(competition) == awayTeamAbbr) {
                    return competition;
                }
            }
        }
        throw new ArgumentException("Competition not found");
    }

    public static string GetAwayTeamAbbr(Competition competition) => GetTeamAbbr(GetAwayTeam(competition));
    public static string GetHomeTeamAbbr(Competition competition) => GetTeamAbbr(GetHomeTeam(competition));
    public static Competitor GetAwayTeam(Competition competition) => competition.Competitors.First(x => x.HomeAway == HomeAway.Away);
    public static Competitor GetHomeTeam(Competition competition) => competition.Competitors.First(x => x.HomeAway == HomeAway.Home);
    //public static string GetAwayTeamLogo(Competition competition) => GetAwayTeam(competition).Team.Logo.ToString();
    //public static string GetHomeTeamLogo(Competition competition) => GetHomeTeam(competition).Team.Logo.ToString();
    public static string GetAwayTeamLogo(Competition competition) => GetTeamLogo(GetAwayTeamAbbr(competition));
    public static string GetHomeTeamLogo(Competition competition) => GetTeamLogo(GetHomeTeamAbbr(competition));
    public static string GetTeamLogo(string teamAbbr) => $"Icons/Teams/{teamAbbr.ToLower()}.png";
    public static long GetHomeTeamScore(Competition competition) => GetTeamScore(GetHomeTeam(competition));
    public static long GetAwayTeamScore(Competition competition) => GetTeamScore(GetAwayTeam(competition));
    public static bool IsGameStarted(Competition competition) => competition.Status.Type.Name != TypeName.StatusScheduled;

    // GameTime-based sibling of IsGameStarted(Competition) above — for CfbSpreads/NflSpreads rows,
    // which have no live ESPN status to check. Used by CfbLeaderboardService/LeaderboardService to
    // tell a terminal MissingPicks (every game for the week/slate has already kicked off) apart
    // from a still-open picking window (MissingGameResults) — same GameTime <= now boundary
    // CfbPicksController.StartedTeams already enforces for locking individual picks.
    // frizat-8y6: .All() is vacuously true on an empty sequence — a week/slate with literally no
    // games (spreads not released yet) must never read as "already started" (that flips a
    // genuinely not-yet-open picking window into a terminal MissingPicks loss). Require at least
    // one real game before concluding anything has started.
    public static bool AllGamesStarted(IEnumerable<DateTimeOffset> gameTimes, DateTimeOffset now) {
        var times = gameTimes as ICollection<DateTimeOffset> ?? gameTimes.ToList();
        return times.Count > 0 && times.All(t => t <= now);
    }

    // Shared by LeagueController.AddPicks (NflSpreads) and CfbPicksController.AddPicks
    // (CfbSpreads) — was two byte-identical private methods, one per controller, differing only
    // in the spread row's concrete type. Pure control-table timestamp check, no ESPN dependency.
    public static HashSet<string> StartedTeams<T>(
        IEnumerable<T> spreads, DateTimeOffset now,
        Func<T, DateTimeOffset> gameTime, Func<T, string> homeTeam, Func<T, string> awayTeam) =>
        spreads.Where(s => gameTime(s) <= now).SelectMany(s => new[] { homeTeam(s), awayTeam(s) }).ToHashSet();
    public static bool IsGameOver(Competition competition) => competition.Status.Type.Name == TypeName.StatusFinal;
    public static long GetTeamScore(Competitor competitor) => competitor.Score;
    public static string GetTeamAbbr(Competitor competitor) => competitor.Team.Abbreviation;
    public static string? GetTeamRecord(Competitor competitor) =>
        competitor.Records.FirstOrDefault(x => x.Type == EspnRecordType.Total)?.Summary;

    public static string? GetDownDistance(Competition competition) => competition?.Situation?.DownDistanceText;

    public static bool IsRedZone(Competition competition) {
        if (competition is null)
            return false;
        if (competition.Situation is null)
            return false;
        if (competition.Situation.PossessionText is null)
            return false;
        if (competition.Situation.IsRedZone is null)
            return false;
        return competition.Situation.IsRedZone.Value;
    }

    public static string? GetPossessionTeamAbbr(Competition competition) {
        if (competition.Situation is null)
            return null;
        var possessionId = competition.Situation.Possession;
        if (possessionId is null)
            return null;
        var team = competition.Competitors.FirstOrDefault(x => x.Id == possessionId);
        return GetTeamAbbr(team);
    }
    public static bool IsHalfTime(Competition competition) => competition.Status.Type.Name == TypeName.StatusHalftime;
    public static bool HasPossession(Competition competition, string teamAbbr) {
        var possessionTeamAbbr = GetPossessionTeamAbbr(competition);
        if (possessionTeamAbbr is null)
            return false;
        return possessionTeamAbbr == teamAbbr;
    }


}
