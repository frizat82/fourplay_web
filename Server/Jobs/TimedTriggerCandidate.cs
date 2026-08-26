namespace FourPlayWebApp.Server.Jobs;

// A single deferred one-time trigger candidate — domain-agnostic. Originally spread-lock-only
// (NflSpreadSchedulerJob/CfbSpreadSchedulerJob); renamed when LeagueJuiceSchedulerJob became its
// second, unrelated consumer, since nothing here is actually spread-specific. HasData drives the
// data-driven catch-up branch in TimedTriggerScheduler — Quartz itself can't distinguish "already
// fired and completed" from "never fired" once a one-time trigger completes, so that decision has
// to come from real business data (does this candidate's real-world outcome already exist?).
// JobData is optional — spread candidates don't need it (their jobs resolve "the current
// week/slate" globally on fire), but a candidate whose job needs to know which specific entity
// (e.g. which league) triggered it can carry that here, applied via JobBuilder.UsingJobData.
public record TimedTriggerCandidate(
    DateTime LockTime,
    string Identity,
    string Description,
    bool HasData,
    IReadOnlyDictionary<string, string>? JobData = null);
