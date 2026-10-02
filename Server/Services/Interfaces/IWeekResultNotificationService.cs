namespace FourPlayWebApp.Server.Services.Interfaces;

// One shared implementation for both sports (CLAUDE.md: NFL/CFB are siblings) — called directly
// by NflScoresJob/CfbScoresJob right after they persist new final scores, so this only ever runs
// in reaction to real new data rather than on a separately-maintained guessed schedule. Never
// fires on WeekResult.Lost — that was explicitly rejected once already (see docs/ideas.md's
// original push-notification scoping and the plan this service implements).
public interface IWeekResultNotificationService
{
    /// <returns>How many new week-result notifications were sent, for the caller's own logging.</returns>
    Task<int> CheckNflWeekResultsAsync(int season);

    Task<int> CheckCfbWeekResultsAsync(int season);
}
