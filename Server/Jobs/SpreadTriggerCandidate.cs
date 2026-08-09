namespace FourPlayWebApp.Server.Jobs;

// A single week/slate's spread-lock trigger candidate, sport-agnostic. HasData drives the
// data-driven catch-up branch in SpreadTriggerScheduler — Quartz itself can't distinguish
// "already fired and completed" from "never fired" once a one-time trigger completes, so that
// decision has to come from real business data (does this week already have spread rows?).
public record SpreadTriggerCandidate(DateTime? LockTime, string Identity, string Description, bool HasData);
