namespace FourPlayWebApp.Shared.Helpers;

public static class GameSituationHelper
{
    /// <summary>
    /// Analyzes the current game state for Home/Away spreads and Over/Under lines.
    /// Only returns text when a team is “behind” on a bet; if already covered, leaves empty.
    /// </summary>
    /// <param name="homeTeam"></param>
    /// <param name="awayTeam"></param>
    /// <param name="homeScore"></param>
    /// <param name="awayScore"></param>
    /// <param name="homeSpread"></param>
    /// <param name="awaySpread"></param>
    /// <param name="overLine"></param>
    /// <param name="underLine"></param>
    /// <returns></returns>
    public static string? GetSituations(
        string homeTeam,
        string awayTeam,
        long homeScore,
        long awayScore,
        double? homeSpread = null,
        double? awaySpread = null,
        double? overLine = null,
        double? underLine = null) {
        string output = "";
        var total = homeScore + awayScore;

        // --- Home spread ---
        if (homeSpread.HasValue)
        {
            double coverMargin = (homeScore + homeSpread.Value) - awayScore;
            if (coverMargin < 0)
            {
                int needed = (int)Math.Ceiling(Math.Abs(coverMargin));
                string scoreText = PointsToText(needed);
                output = $"{homeTeam} needs {scoreText} to cover the spread ({homeSpread}).";
            }
        }

        // --- Away spread ---
        if (awaySpread.HasValue)
        {
            double coverMargin = (awayScore + awaySpread.Value) - homeScore;
            if (coverMargin < 0)
            {
                int needed = (int)Math.Ceiling(Math.Abs(coverMargin));
                string scoreText = PointsToText(needed);
                output = $"{awayTeam} needs {scoreText} to cover the spread ({awaySpread}).";
            }
        }

        // --- Over ---
        if (total < overLine)
        {
            var needed = overLine.Value - total;
            output = $"The game needs {needed} more points to hit the Over ({overLine}).";
        }
        // --- Under ---
        if (!(total > underLine)) return output;
        var extra = total - underLine.Value;
        output = $"The game has exceeded the Under by {extra} points ({underLine}).";

        return output;
    }

    // Simple football scoring text helper
    private static string PointsToText(int points)
    {
        return points switch {
            <= 3 => "one FG",
            <= 7 => "one score",
            <= 14 => "two scores",
            <= 21 => "three scores",
            _ => $"{(points / 7) + 1} scores"
        };
    }
}