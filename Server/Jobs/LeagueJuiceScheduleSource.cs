using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Jobs;

// frizat-ugs: produces one reminder candidate + one lock candidate per (league, season) — driven
// entirely by whatever season rows currently exist in NflSeasonWeekConfigs/CfbSeasonWeekConfigs,
// never a hardcoded year (see LeagueJuiceScheduleSourceTests's
// NeverHardcodesASeason_ArbitraryFutureSeasonIsPickedUpAutomatically — this is exactly the failure
// mode that caused CfbCurrentSlateService's old `ConfiguredSeason = 2026` bug). Only one class (not
// a per-sport ISpreadScheduleSource-style split) because the per-sport difference here is small —
// which config table gives the season's first-game date — unlike the spread schedulers, which also
// need a different "already has data" table per sport.
public class LeagueJuiceScheduleSource(ILeagueRepository leagueRepo, ICfbRepository cfbRepo, TimeProvider timeProvider) {
    // /code-review: a season row that predates a league's creation looks "unconfigured" forever
    // (the league simply never had a mapping for it) — without this cutoff, a brand-new league
    // would get retroactive reminder emails and bogus auto-filled rows for every already-settled
    // past season on record. A season is only ever a real candidate within this window of its lock
    // time — long enough to cover any realistic app-downtime catch-up, nowhere near long enough to
    // mistake a genuinely historical season for a current one.
    private static readonly TimeSpan StaleSeasonCutoff = TimeSpan.FromDays(30);

    public async Task<(IEnumerable<TimedTriggerCandidate> Reminders, IEnumerable<TimedTriggerCandidate> Locks)> GetCandidatesAsync() {
        var leaguesTask = leagueRepo.GetAllLeaguesAsync();
        var nflConfigsTask = leagueRepo.GetNflSeasonWeekConfigsAsync();
        var cfbConfigsTask = cfbRepo.GetAllWeekConfigsAsync();
        var remindersSentTask = leagueRepo.GetJuiceRemindersSentAsync();
        await Task.WhenAll(leaguesTask, nflConfigsTask, cfbConfigsTask, remindersSentTask);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var remindersSent = remindersSentTask.Result;

        // WeekId/IvLeagueWeekNumber == 1 is each sport's canonical "first week of the season" —
        // both are unique per (Season, *) via DB constraint, so no duplicate-key risk. Only the
        // DATE is used below (FirstGameOfWeekStartDatetime's time-of-day component comes from a
        // hand-curated control-table spreadsheet, not a live/precise UTC source).
        var nflSeasonStarts = nflConfigsTask.Result
            .Where(c => c.WeekId == 1 && c.FirstGameOfWeekStartDatetime.HasValue)
            .Select(c => (c.Season, Date: DateOnly.FromDateTime(c.FirstGameOfWeekStartDatetime!.Value)))
            .Where(x => LockTimeUtc(x.Date) > now - StaleSeasonCutoff)
            .ToDictionary(x => x.Season, x => x.Date);
        var cfbSeasonStarts = cfbConfigsTask.Result
            .Where(c => c.IvLeagueWeekNumber == 1 && c.InScopeIvLeague)
            .Select(c => (c.Season, c.WeekStartDate))
            .Where(x => LockTimeUtc(x.WeekStartDate) > now - StaleSeasonCutoff)
            .ToDictionary(x => x.Season, x => x.WeekStartDate);

        var reminders = new List<TimedTriggerCandidate>();
        var locks = new List<TimedTriggerCandidate>();

        foreach (var league in leaguesTask.Result) {
            var seasonStarts = league.LeagueType == LeagueType.Cfb ? cfbSeasonStarts : nflSeasonStarts;
            foreach (var (season, startDate) in seasonStarts) {
                var lockTimeUtc = LockTimeUtc(startDate);
                var reminderTimeUtc = lockTimeUtc.AddDays(-2);
                var hasJuice = league.LeagueJuiceMappings.Any(m => m.Season == season);
                var identitySuffix = $"{league.Id}-{season}";
                var jobData = new Dictionary<string, string> {
                    [LeagueJuiceJobData.LeagueIdKey] = league.Id.ToString(),
                    [LeagueJuiceJobData.SeasonKey] = season.ToString(),
                };

                // /code-review: "Juice configured" can't be the reminder's own "already handled"
                // signal — sending the reminder doesn't configure anything, so that would make the
                // scheduler re-send the same email on every catch-up pass (daily cron, every
                // restart) for as long as the owner hasn't acted. A persisted "already sent"
                // marker (LeagueJuiceReminderSent, written by LeagueJuiceReminderJob itself once it
                // succeeds) is the real "done" signal for the reminder specifically.
                var reminderHasData = hasJuice || remindersSent.Contains((league.Id, season));

                reminders.Add(new TimedTriggerCandidate(
                    reminderTimeUtc, $"Juice Reminder {identitySuffix}",
                    $"Remind league {league.Id} owner to configure Juice for season {season}",
                    reminderHasData, jobData));

                locks.Add(new TimedTriggerCandidate(
                    lockTimeUtc, $"Juice Lock {identitySuffix}",
                    $"Auto-fill Juice for league {league.Id} season {season} if still unconfigured",
                    hasJuice, jobData));
            }
        }

        return (reminders, locks);
    }

    private static DateTime LockTimeUtc(DateOnly seasonStartDate) {
        var wallClockCentral = seasonStartDate.ToDateTime(new TimeOnly(14, 0));
        return TimeZoneInfo.ConvertTimeToUtc(wallClockCentral, AppTimeZones.Central);
    }
}
