using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Helpers;

public static class GameHelpers {
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
    public static int GetWeekFromEspnWeek(long week, bool isPostSeason = false) => (int)(isPostSeason ? week + 18 : week);
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

    // Uses ESPN Style for Post Season which is resetting weeks to 1-4
    public static int GetEspnRequiredPicks(long week, bool isPostSeason = false) {
        if (!isPostSeason) {
            return 4;
        }
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
