namespace FourPlayWebApp.Server.Jobs;

// Single source of truth for the admin Job Manager's grouping/toggle — classifies a Quartz job by
// its CLR type. "Dynamic" jobs are the numerous per-league/per-week ones TimedTriggerScheduler
// registers (Juice Reminder/Lock per league+season, NFL/CFB Spreads per week) — everything else is
// a fixed scheduler/cron job registered once in Program.cs. Kept here (not Shared) since it needs
// the concrete job Types, which live in this assembly.
public static class JobCategoryClassifier {
    private static readonly Dictionary<Type, (string Category, bool IsDynamic)> Map = new() {
        [typeof(UserManagerJob)] = ("System", false),
        [typeof(CfbSlateSeederJob)] = ("CFB Slates", false),
        [typeof(CfbRankingCaptureJob)] = ("CFB Rankings", false),
        [typeof(NflScoresJob)] = ("NFL Scores", false),
        [typeof(CfbScoresJob)] = ("CFB Scores", false),
        [typeof(NflSpreadSchedulerJob)] = ("NFL Spreads", false),
        [typeof(NflSpreadJob)] = ("NFL Spreads", true),
        [typeof(CfbSpreadSchedulerJob)] = ("CFB Spreads", false),
        [typeof(CfbSpreadJob)] = ("CFB Spreads", true),
        [typeof(LeagueJuiceSchedulerJob)] = ("Juice", false),
        [typeof(LeagueJuiceReminderJob)] = ("Juice", true),
        [typeof(LeagueJuiceLockJob)] = ("Juice", true),
    };

    public static (string Category, bool IsDynamic) Classify(Type? jobType) =>
        jobType is not null && Map.TryGetValue(jobType, out var result) ? result : ("Other", false);
}
