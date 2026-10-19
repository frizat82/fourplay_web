using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Jobs;

// Single source of truth for the admin Job Manager's grouping/toggle — classifies a Quartz job by
// its CLR type. "Dynamic" jobs are the numerous per-league/per-week ones TimedTriggerScheduler
// registers (Juice Reminder/Lock per league+season, NFL/CFB Spreads per week) — everything else is
// a fixed scheduler/cron job registered once in Program.cs. Kept here (not Shared) since it needs
// the concrete job Types, which live in this assembly.
// Sport is set for sport-wide jobs (the Job Manager's League column shows it as NFL/CFB);
// per-league jobs take their league's sport instead, and system jobs have none.
public static class JobCategoryClassifier {
    public record Classification(string Category, bool IsDynamic, LeagueType? Sport = null);

    private static readonly Dictionary<Type, Classification> Map = new() {
        [typeof(UserManagerJob)] = new("System", false),
        [typeof(CfbRankingCaptureJob)] = new("CFB Rankings", false, LeagueType.Cfb),
        [typeof(NflScoresJob)] = new("NFL Scores", false, LeagueType.Nfl),
        [typeof(CfbScoresJob)] = new("CFB Scores", false, LeagueType.Cfb),
        [typeof(NflSpreadSchedulerJob)] = new("NFL Spreads", false, LeagueType.Nfl),
        [typeof(NflSpreadJob)] = new("NFL Spreads", true, LeagueType.Nfl),
        [typeof(CfbSpreadSchedulerJob)] = new("CFB Spreads", false, LeagueType.Cfb),
        [typeof(CfbSpreadJob)] = new("CFB Spreads", true, LeagueType.Cfb),
        [typeof(LeagueJuiceSchedulerJob)] = new("Juice", false),
        [typeof(LeagueJuiceReminderJob)] = new("Juice", true),
        [typeof(LeagueJuiceLockJob)] = new("Juice", true),
    };

    public static Classification Classify(Type? jobType) =>
        jobType is not null && Map.TryGetValue(jobType, out var result) ? result : new("Other", false);
}
