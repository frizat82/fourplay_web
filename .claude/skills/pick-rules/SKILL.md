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
DTOs) and the Rules page.

**One control table per sport, the same shape (frizat-b4g).** There is no `CfbSlates` table: a
CFB slate IS an in-scope `CfbSeasonWeekConfigs` row, and `CfbSpreads`/`CfbScores`/`CfbPicks.CfbSlateId`
point at that row's `Id`. Both tables carry:
- `WeekType` (shared enum: `RegularSeason`, `ConferenceChampionships`, `Playoff`, `Dead`). CFB
  `Playoff` is what selects ESPN's CFP endpoint (`CfbSlateHelpers.IsCfpSlate`), filtered to the
  row's date window; everything else is fetched by the row's dates.
- `ScoringFormat` (shared enum: `Standard`/`Divisional`/`Conference`/`Final`/`None`), which a
  check constraint ties to `RequiredPicks` (4/3/2/1/0).
- `InScopeIvLeague`, the skip flag: an out-of-scope CFB week (bye/dead week) has
  `IvLeagueWeekNumber = NULL` and `RequiredPicks = 0`. No 99 sentinel. NFL rows are all in scope.
- AP rankings (`CfbRankings`) are keyed by (Season, `WeekNumber` = our slate number, team). No
  ESPN week number is stored anywhere.

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

There is no WeekId 23. Use `PopulateScoresTestDataAsync(21)` in tests, not 22. ESPN's own postseason week numbering (it skipped week 4 for the Pro Bowl through 2025) is irrelevant: nothing reads ESPN week numbers — games are fetched by our rows' date windows and stamped with our WeekId.

## CFB Pick Count — the `RequiredPicks` seeded on each `CfbSeasonWeekConfigs` row

18-slate system: slates 1–13 = regular season, 14 = Conf. Champs, 15–18 = CFP rounds.

| SlateNumber | Label | Required Picks |
|-------------|-------|---------------|
| 1–14 | Regular Season Weeks 1–13 + Conf. Championships | 4 |
| 15–16 | CFP First Round + Quarterfinals | 3 |
| 17 | CFP Semifinals | 2 |
| 18 | CFP Championship | 1 |

## CFB Slate Numbering

The CFB week shown everywhere IS the slate number (1–18) — no `slate - 13` postseason numbering.
Each slate's label and postseason flag are its `CfbSeasonWeekConfigs` row's `WeekLabel` /
`IsPostSeason` (the week selector is built from the season's rows):

| SlateNumber | WeekType | Label |
|---|---|---|
| 1–13 | RegularSeason | Week 1 – Week 13 |
| 14 | ConferenceChampionships | Conf. Championships |
| 15 | Playoff | CFP First Round |
| 16 | Playoff | CFP Quarterfinals |
| 17 | Playoff | CFP Semifinals |
| 18 | Playoff | CFP Championship |

## NFL Week Numbering

Our WeekId everywhere: 1–18 regular season, 19 Wild Card, 20 Divisional, 21 Conference, 22 Super Bowl — each a `NflSeasonWeekConfigs` row with its own `WeekLabel` / `IsPostSeason` / `RequiredPicks`. The week selector, labels and notifications all read the rows (frizat-d47).

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
2. Grep ALL test files for old expected values: `PeriodRulesBackfillPostgresTests.cs`, `OneCfbControlTableMigrationTests.cs`, `TestWeekConfigs.cs` (C# fixture rows), `src/test/fixtures.ts` (`nflRequiredPicks`), `LeaderboardTests.cs`, `picks.test.tsx`, e2e specs
3. Update stale assertions BEFORE fixing the implementation
4. Confirm new tests are red before writing any implementation
5. Run full suite: `dotnet test` + `npm run test -- --run` + `npm run test:e2e:demo`
