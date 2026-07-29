# SPREAD_GRAB_PLAN — Data-Driven Spread Job Scheduling

## Problem

`NflSpreadJob` fires on a hardcoded Thursday 2pm CST cron (`0 0 14 ? * THU`). This covers most
regular-season weeks but fails silently for:

| Gap | Why it breaks |
|---|---|
| **Thanksgiving** | First game is 11:30am CST Thursday — 2pm job fires **after kickoff** |
| **Wild Card Weekend** | Saturday games — no Saturday spread job exists |
| **Divisional Round** | Saturday/Sunday games — same gap |
| **Conference Championships** | Sunday games — same gap |
| **Super Bowl** | Sunday game — same gap |

The Christmas Eve cron (`0 0 10 24 12 ?`) is the right pattern — a one-time trigger for a known
special date. The fix is to generate that same pattern dynamically from
`NflSeasonWeekConfig.FirstGameOfWeekStartDatetime` for all special weeks.

`SpreadRelease.tsx` already queries `GET /api/jobmanager/get-next-spread-job` which returns the
nearest Quartz "Spread" trigger. Once the right triggers exist the countdown is automatically
correct — **no frontend changes needed**.

---

## Logic

A week needs a special game-day trigger when:
- `FirstGameOfWeekStartDatetime` is on a **Saturday** or **Sunday** (all postseason weeks), OR
- `FirstGameOfWeekStartDatetime` is a **Thursday before 2pm CST** (Thanksgiving, early holiday games)

Regular TNF weeks (Thursday 8:15pm CST) are covered by the existing Thursday 2pm cron — no
special trigger needed.

| Week type | First game (CST) | Needs trigger? | Fires at |
|---|---|---|---|
| Regular season TNF | Thu 8:15pm | No (`hour >= 14`) | Thu 2pm cron |
| Thanksgiving | Thu 11:30am | **Yes** (`hour < 14`) | That Thu 10am CST |
| Wild Card | Sat 1pm | **Yes** (Saturday) | That Sat 10am CST |
| Divisional | Sat/Sun | **Yes** | Game day 10am CST |
| Conf. Championships | Sun 1pm/4:30pm | **Yes** | That Sun 10am CST |
| Super Bowl | Sun 6:30pm | **Yes** | That Sun 10am CST |
| Christmas/New Year's early | Thu <2pm | **Yes** | That Thu 10am CST |

---

## Implementation

### New Job: `Server/Jobs/NflSpreadSchedulerJob.cs`

Runs once at startup. Reads `NflSeasonWeekConfig`, computes which upcoming weeks need a
game-day trigger, and registers one-time Quartz triggers. Idempotent — skips weeks already
registered or already past.

```csharp
[DisallowConcurrentExecution]
public class NflSpreadSchedulerJob(ILeagueRepository repo, ISchedulerFactory schedulerFactory) : IJob
{
    private static readonly TimeZoneInfo Cst =
        TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public async Task Execute(IJobExecutionContext context)
    {
        var configs = await repo.GetNflSeasonWeekConfigsAsync();
        var now = DateTime.UtcNow;
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);

        foreach (var cfg in configs.Where(c =>
            c.FirstGameOfWeekStartDatetime.HasValue &&
            c.FirstGameOfWeekStartDatetime.Value > now))
        {
            var firstGameCst = TimeZoneInfo.ConvertTimeFromUtc(
                cfg.FirstGameOfWeekStartDatetime!.Value, Cst);

            var needsSpecialTrigger = firstGameCst.DayOfWeek switch {
                DayOfWeek.Saturday => true,
                DayOfWeek.Sunday   => true,
                DayOfWeek.Thursday => firstGameCst.Hour < 14, // before Thu 2pm cron
                _                  => false
            };

            if (!needsSpecialTrigger) continue;

            // Fire at 10am CST on game day
            var fireCst = new DateTime(firstGameCst.Year, firstGameCst.Month,
                firstGameCst.Day, 10, 0, 0, DateTimeKind.Unspecified);
            var fireUtc = TimeZoneInfo.ConvertTimeToUtc(fireCst, Cst);

            if (fireUtc <= now) continue; // already past

            var triggerId = $"NFL Spreads {cfg.Season} Wk{cfg.WeekId} GameDay";
            if (await scheduler.GetTrigger(new TriggerKey(triggerId)) is not null)
                continue; // idempotent

            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerId)
                .WithDescription($"NFL spreads for {cfg.WeekLabel} — game day 10am CST")
                .StartAt(new DateTimeOffset(fireUtc, TimeSpan.Zero))
                .ForJob(new JobKey(nameof(NflSpreadJob)))
                .Build();

            await scheduler.ScheduleJob(trigger);
        }
    }
}
```

### `Server/Program.cs` changes

```csharp
// ADD — runs at startup, registers game-day triggers from NflSeasonWeekConfig
q.ScheduleJob<NflSpreadSchedulerJob>(trigger => trigger
    .WithIdentity("NFL Spread Scheduler")
    .WithDescription("Registers game-day spread triggers from NflSeasonWeekConfig")
    .StartNow()
    .Build());

// REMOVE — Christmas Eve now covered by NflSpreadSchedulerJob:
// q.ScheduleJob<NflSpreadJob>(trigger => trigger
//     .WithIdentity("NFL Spreads Christmas Eve") ...

// KEEP — covers all regular-season TNF weeks:
// q.ScheduleJob<NflSpreadJob>("0 0 14 ? * THU", ...)
```

---

## Tests (TDD — write these red first)

**New file: `Server.UnitTests/NflSpreadSchedulerJobTests.cs`**

| Test | Scenario | Expected |
|---|---|---|
| `Execute_ThanksgivingWeek_RegistersTrigger` | First game Thu 11:30am CST | Trigger registered |
| `Execute_WildCardWeek_RegistersTrigger` | First game Sat 1pm CST | Trigger registered |
| `Execute_SuperBowlWeek_RegistersTrigger` | First game Sun 6:30pm CST | Trigger registered |
| `Execute_RegularTnfWeek_NoTrigger` | First game Thu 8:15pm CST | No trigger (cron covers it) |
| `Execute_PastWeek_NoTrigger` | `FirstGameOfWeekStartDatetime` < now | No trigger |
| `Execute_WhenTriggerAlreadyExists_IsIdempotent` | Trigger already registered | No duplicate, no crash |
| `Execute_NullFirstGameDatetime_Skipped` | `FirstGameOfWeekStartDatetime = null` | Skipped cleanly |

---

## Files Changed

| File | Change |
|---|---|
| `Server/Jobs/NflSpreadSchedulerJob.cs` | **NEW** |
| `Server/Program.cs` | Register `NflSpreadSchedulerJob`; remove Christmas Eve hardcoded cron |
| `Server.UnitTests/NflSpreadSchedulerJobTests.cs` | **NEW** — 7 tests |

---

## What Does NOT Change

- Thursday 2pm CST `NflSpreadJob` cron — stays
- All `NflScoresJob` crons — unchanged
- `SpreadRelease.tsx` — unchanged; countdown already reads nearest Quartz "Spread" trigger
- No DB migration — reads from existing `NflSeasonWeekConfig`
- CFB unaffected

---

## Verification

```bash
# Tests
dotnet test --filter NflSpreadScheduler   # 7 new tests pass

# Manual: start demo stack, open admin job monitor
# → Wild Card Jan 10 10am CST trigger visible
# → SpreadRelease countdown shows Saturday morning for Wild Card week
```
