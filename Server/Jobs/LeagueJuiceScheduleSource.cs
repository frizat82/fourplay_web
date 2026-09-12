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
        var nflConfigs = nflConfigsTask.Result;
        var cfbConfigs = cfbConfigsTask.Result;

        // WeekId/IvLeagueWeekNumber == 1 is each sport's canonical "first week of the season" —
        // both are unique per (Season, *) via DB constraint, so no duplicate-key risk. Lock time
        // is computed once per DISTINCT season here (an O(1) dictionary lookup per league below),
        // not once per (league, season) pair — the result only depends on (sport, season), and
        // many leagues share a sport.
        var nflLockTimes = nflConfigs.Where(c => c.WeekId == 1 && c.FirstGameOfWeekStartDatetime.HasValue)
            .Select(c => c.Season).Distinct()
            .ToDictionary(s => s, s => GetSeasonStartLockTimeUtc(LeagueType.Nfl, s, nflConfigs, cfbConfigs)!.Value);
        var cfbLockTimes = cfbConfigs.Where(c => c.IvLeagueWeekNumber == 1 && c.InScopeIvLeague)
            .Select(c => c.Season).Distinct()
            .ToDictionary(s => s, s => GetSeasonStartLockTimeUtc(LeagueType.Cfb, s, nflConfigs, cfbConfigs)!.Value);

        var reminders = new List<TimedTriggerCandidate>();
        var locks = new List<TimedTriggerCandidate>();

        foreach (var league in leaguesTask.Result) {
            var lockTimes = league.LeagueType == LeagueType.Cfb ? cfbLockTimes : nflLockTimes;
            foreach (var (season, lockTimeUtc) in lockTimes) {
                // /code-review: a season row that predates a league's creation looks
                // "unconfigured" forever (the league simply never had a mapping for it) — without
                // this cutoff, a brand-new league would get retroactive reminder emails and bogus
                // auto-filled rows for every already-settled past season on record. A season is
                // only ever a real candidate within this window of its lock time — long enough to
                // cover any realistic app-downtime catch-up, nowhere near long enough to mistake a
                // genuinely historical season for a current one. See StaleSeasonCutoff's own doc
                // comment above for the full rationale.
                if (lockTimeUtc <= now - StaleSeasonCutoff) continue;

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

    // frizat: LeagueController.UpdateLeagueJuice/GetLeagueJuiceForSeason's single-season lock
    // check — season-scoped repo calls (not GetCandidatesAsync's own full-table fetch above,
    // which genuinely needs every season at once for the batch scheduler) so a single request
    // only pulls the 1-2 config rows it actually needs. `startWeek` must be the mapping's own
    // current StartWeek (not always 1) — see GetSeasonStartLockTimeUtc's comment.
    public async Task<(bool TeaseLocked, bool WeeklyCostLocked)> GetJuiceLockStateAsync(LeagueType leagueType, int season, int startWeek) {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        IEnumerable<NflSeasonWeekConfig> nflConfigs = leagueType == LeagueType.Cfb ? [] : await leagueRepo.GetNflSeasonWeekConfigsAsync(season);
        IEnumerable<CfbSeasonWeekConfig> cfbConfigs = leagueType == LeagueType.Cfb ? await cfbRepo.GetWeekConfigsForSeasonAsync(season) : [];

        var teaseLockTime = GetSeasonStartLockTimeUtc(leagueType, season, nflConfigs, cfbConfigs, startWeek);
        var weeklyCostLockTime = GetSeasonEndLockTimeUtc(leagueType, season, nflConfigs, cfbConfigs);
        return (teaseLockTime is not null && now >= teaseLockTime, weeklyCostLockTime is not null && now >= weeklyCostLockTime);
    }

    // frizat: single source of truth for "when does a given week/slate start" (2pm
    // America/Chicago on its own start date) — shared by GetCandidatesAsync above (drives the
    // Juice Reminder/Lock Quartz triggers off week/slate 1) and
    // LeagueController.UpdateLeagueJuice (blocks editing tease points once week/slate 1 has
    // started, and WeeklyCost once the season's final week/slate has started). Pure, no I/O —
    // callers pass in whatever config rows they already have. Null means no matching config row
    // exists yet for that (season, weekOrSlateNumber) — never a reason to block anything.
    public static DateTime? GetWeekLockTimeUtc(LeagueType leagueType, int season, int weekOrSlateNumber,
        IEnumerable<NflSeasonWeekConfig> nflConfigs, IEnumerable<CfbSeasonWeekConfig> cfbConfigs) {
        if (leagueType == LeagueType.Cfb) {
            var slate = cfbConfigs.FirstOrDefault(c =>
                c.Season == season && c.IvLeagueWeekNumber == weekOrSlateNumber && c.InScopeIvLeague);
            return slate is null ? null : LockTimeUtc(slate.WeekStartDate);
        }
        var week = nflConfigs.FirstOrDefault(c =>
            c.Season == season && c.WeekId == weekOrSlateNumber && c.FirstGameOfWeekStartDatetime.HasValue);
        return week is null ? null : LockTimeUtc(DateOnly.FromDateTime(week.FirstGameOfWeekStartDatetime!.Value));
    }

    // Tease points (Juice/JuiceDivisional/JuiceConference) AND Start Week lock together, here —
    // at the league's OWN currently-configured Start Week's own kickoff, not always week/slate 1.
    // `startWeek` defaults to 1 for GetCandidatesAsync above (the Juice Reminder/Lock scheduler
    // genuinely always means the season's literal week 1 — it exists to auto-fill a league that
    // never configured Juice at all before the season proper begins). LeagueController's editable-
    // or-not checks pass the mapping's actual StartWeek instead: a league created after the
    // season's week 1 already locked previously had Tease Pts/Start Week frozen at whatever the
    // create-league form happened to submit, forever, with no way to ever configure them — this
    // parameterization is what lets CreateLeague's own Start Week default (see LeagueController)
    // — and any further edits before that week's own kickoff — actually take effect.
    public static DateTime? GetSeasonStartLockTimeUtc(LeagueType leagueType, int season,
        IEnumerable<NflSeasonWeekConfig> nflConfigs, IEnumerable<CfbSeasonWeekConfig> cfbConfigs, int startWeek = 1) =>
        GetWeekLockTimeUtc(leagueType, season, startWeek, nflConfigs, cfbConfigs);

    // WeeklyCost locks here — the season's final week (NFL WeekId 22 Super Bowl) or slate (CFB
    // slate 18 Championship), per CLAUDE.md's pick-count tables (/pick-rules).
    public static DateTime? GetSeasonEndLockTimeUtc(LeagueType leagueType, int season,
        IEnumerable<NflSeasonWeekConfig> nflConfigs, IEnumerable<CfbSeasonWeekConfig> cfbConfigs) =>
        GetWeekLockTimeUtc(leagueType, season, leagueType == LeagueType.Cfb ? 18 : 22, nflConfigs, cfbConfigs);

    public static DateTime LockTimeUtc(DateOnly seasonStartDate) {
        var wallClockCentral = seasonStartDate.ToDateTime(new TimeOnly(14, 0));
        return TimeZoneInfo.ConvertTimeToUtc(wallClockCentral, AppTimeZones.Central);
    }
}
