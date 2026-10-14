---
name: pick-rules
description: NFL/CFB pick count invariants, CFB slate mapping, seeder guards, test rot checklist. Invoke before changing any pick logic.
---

# Pick Rules & Invariants

## Where the rules live: the control tables (frizat-d47)

Each round's pick count and postseason flag are columns on its control-table row —
`NflSeasonWeekConfigs` (keyed by `WeekId`) and `CfbSeasonWeekConfigs` (keyed by
`IvLeagueWeekNumber` = slate number): `RequiredPicks` and `IsPostSeason`. There is no
code rule keyed on week/slate numbers any more. Every consumer reads the row:
AddPicks caps (NFL `NflWeekInfo.RequiredPicks`, CFB `slate.RequiredPicks`), the leaderboard,
the tease tier (`JuiceTiers.For(requiredPicks, …)`: 4 = regular, 3 = divisional,
2 = conference, 1 = the final / no tease), live pick notifications, the WeeklyCost lock
(the final = the row asking for 1 pick), the client adapters (`requiredPicks` on the API
DTOs) and the Rules page. CFB slates don't store a copy: `CfbRepository` fills
`CfbSlates.RequiredPicks`/`IsPostSeason` from the slate's config row on every read.
A `RequiredPicks BETWEEN 0 AND 4` check constraint guards hand-written rows.

**New season?** Seed its rows with these values (the tables below). The migration
`AddPeriodRulesToSeasonWeekConfigs` backfilled 2025/2026 from exactly these tables.

CFB `IsPostSeason` (slates 14–18, i.e. from Conf. Championships) is the app's postseason
(O/U offered, the Postseason selector) — NOT ESPN's season type (`CfbSlateHelpers.IsCfpSlate`
stays the ESPN-boundary answer: CFP only).

## NFL Pick Count — the `RequiredPicks` seeded on each `NflSeasonWeekConfigs` row

| NflWeek | ESPN Week | Label | Required Picks |
|---------|-----------|-------|---------------|
| 1–18 | 1–18 | Regular Season | 4 |
| 19 | 1 | Wild Card | 3 |
| 20 | 2 | Divisional | 3 |
| 21 | 3 | Conference Championship | 2 |
| 22 | 4 | Super Bowl | 1 |

ESPN numbered the Super Bowl postseason week 5 through 2025 (Pro Bowl in week 4); `GameHelpers.GetWeekFromEspnWeek` maps it to WeekId 22 at the ESPN boundary (season-gated — see frizat-jw7 for 2026+). There is no WeekId 23. Use `PopulateScoresTestDataAsync(21)` in tests, not 22.

## CFB Pick Count — the `RequiredPicks` seeded on each `CfbSeasonWeekConfigs` row

18-slate system: slates 1–13 = regular season, 14 = Conf. Champs, 15–18 = CFP rounds.

| SlateNumber | Label | Required Picks |
|-------------|-------|---------------|
| 1–14 | Regular Season Weeks 1–13 + Conf. Championships | 4 |
| 15–16 | CFP First Round + Quarterfinals | 3 |
| 17 | CFP Semifinals | 2 |
| 18 | CFP Championship | 1 |

## CFB Slate Numbering (ESPN Week → SlateNumber)

| SlateNumber | ESPN Week | Label |
|---|---|---|
| 1–13 | 1–13 | Week 1 – Week 13 |
| 14 | 14 | Conf. Championships |
| 15 | 16 | CFP First Round |
| 16 | 18 | CFP Quarterfinals |
| 17 | 20 | CFP Semifinals |
| 18 | 21 | CFP Championship |

`cfbSlateNumberToWeek(n)` / `cfbWeekToSlateNumber(week, isPostSeason)` in `src/utils/gameHelpers.ts`.
Slates ≤13 → `isPostSeason=false`; slates 14+ → `isPostSeason=true`, `week = slateNumber - 13`.

## NFL Week Numbering

Regular season weeks 1–18 map directly. Postseason: `getWeekFromEspnWeek(week, isPostSeason)` adds +18 offset (Wild Card=19, Divisional=20, Conference=21, Super Bowl=22).

## Demo Stack Pick Count Invariants (5 users, CFB Demo League)

| Slate(s) | Picks/user | Total picks |
|----------|-----------|-------------|
| 1–13 (regular season) | 4 each | 20/slate |
| 14 (Conf. Championships) | 4 spread + O/U for Bob/Dana | 22 |
| 15–16 (First Round, QF) | 3 spread + O/U for Bob/Dana | 17/slate |
| 17 (Semifinals) | 2 spread + O/U for Bob/Dana | 12 |
| 18 (Championship) | 1 spread + O/U for Bob/Dana | 7 |
| **Total** | — | **335** |

`DemoDataSeeder.ExpectedPickCount = 335` is the runtime guard — never fudge it.

## Seeder Is Production-Critical

`DemoDataSeeder` seeds the same DB that Playwright e2e tests run against.
- Every pick count must match the round's control-table `RequiredPicks` (the tables above)
- After ANY pick logic change: re-verify seeder counts AND run `npm run test:e2e:demo`
- `ExpectedPickCount` in `DemoDataSeeder.cs` is a guard — update it accurately, never fudge it

## Test Rot Checklist (Before Shipping Any Pick Logic Change)

1. Read the tables above — understand the correct rule FIRST
2. Grep ALL test files for old expected values: `PeriodRulesBackfillPostgresTests.cs`, `TestWeekConfigs.cs` (C# fixture rows), `src/test/fixtures.ts` (`nflRequiredPicks`), `LeaderboardTests.cs`, `picks.test.tsx`, e2e specs
3. Update stale assertions BEFORE fixing the implementation
4. Confirm new tests are red before writing any implementation
5. Run full suite: `dotnet test` + `npm run test -- --run` + `npm run test:e2e:demo`
