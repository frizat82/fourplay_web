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
    // Abbreviation -> ESPN's full team name ("BUFF" -> "Buffalo Bulls") from a scoreboard.
    public static Dictionary<string, string> GetTeamDisplayNames(EspnScores? scores) =>
        (scores?.Events ?? [])
            .SelectMany(e => e.Competitions ?? [])
            .SelectMany(c => c.Competitors ?? [])
            .Where(c => c.Team?.Abbreviation is not null && !string.IsNullOrWhiteSpace(c.Team.DisplayName))
            .GroupBy(c => c.Team.Abbreviation)
            .ToDictionary(g => g.Key, g => g.First().Team.DisplayName!);


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

    // Shared by LeaderboardService.CalculatePicks (NFL week) and CfbLeaderboardService.EvaluateSlate
    // (CFB slate number) — a league's configured StartWeek (LeagueJuiceMapping.StartWeek, frizat-o3x)
    // excludes every week/slate before it from that league's season entirely. One predicate so both
    // sides can never drift on the boundary (e.g. off-by-one between "< " and "<=").
    public static bool IsWeekExcludedFromSeason(int week, int startWeek) => week < startWeek;

    // Shared by LeagueController.AddPicks and CfbPicksController.AddPicks (frizat-u66) — was two
    // byte-identical guard blocks, one per controller, differing only in which week/slate number
    // got passed in. Takes `startWeek` as a nullable int (not the LeagueJuiceMapping entity
    // itself) since that entity lives in Server.Models.Data, which Shared can't reference — a
    // missing mapping fails open via the `?? 1` default (StartWeek's own EF default), matching
    // every league that predates this feature.
    public static string? ValidateWeekNotExcluded(int week, int? startWeek) {
        var effectiveStartWeek = startWeek ?? 1;
        return IsWeekExcludedFromSeason(week, effectiveStartWeek)
            ? $"Picks aren't accepted before this league's configured Start Week ({effectiveStartWeek})."
            : null;
    }

    // Shared by LeagueController.AddPicks (NflSpreads) and CfbPicksController.AddPicks
    // (CfbSpreads) — was two byte-identical private methods, one per controller, differing only
    // in the spread row's concrete type. Pure control-table timestamp check, no ESPN dependency.
    public static HashSet<string> StartedTeams<T>(
        IEnumerable<T> spreads, DateTimeOffset now,
        Func<T, DateTimeOffset> gameTime, Func<T, string> homeTeam, Func<T, string> awayTeam) =>
        spreads.Where(s => gameTime(s) <= now).SelectMany(s => new[] { homeTeam(s), awayTeam(s) }).ToHashSet();

    // frizat: shared by LeagueController.AddPicks and CfbPicksController.AddPicks — a pick is only
    // ever accepted for a team that has an actual spread row in the current week/slate. The Picks
    // page never shows a game before its spread has been posted, so a submission for a team with
    // no matching spread at all can only mean bad/forged input, not a legitimate race — reject it,
    // don't fail open the way the ineligibility/kickoff guards deliberately do for absence-of-data.
    public static HashSet<string> TeamsWithSpread<T>(
        IEnumerable<T> spreads, Func<T, string> homeTeam, Func<T, string> awayTeam) =>
        spreads.SelectMany(s => new[] { homeTeam(s), awayTeam(s) }).ToHashSet();
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
