---
name: pick-rules
description: NFL/CFB pick count invariants, CFB slate mapping, seeder guards, test rot checklist. Invoke before changing any pick logic.
---

# Pick Rules & Invariants

## NFL Pick Count — `GetRequiredPicks(nflWeek)` C# / `getEspnRequiredPicks(week, isPostSeason)` TS

| NflWeek | ESPN Week | Label | Required Picks |
|---------|-----------|-------|---------------|
| 1–18 | 1–18 | Regular Season | 4 |
| 19 | 1 | Wild Card | 3 |
| 20 | 2 | Divisional | 3 |
| 21 | 3 | Conference Championship | 2 |
| 22 | 4 | Super Bowl | 1 |

`NflScoresJob` stores ESPN week 5 (Super Bowl) as NflWeek 22 via `j == 5 ? 4 : j` hack. `GetRequiredPicks(23)` throws — week 23 does not exist (Pro Bowl is skipped). Use `PopulateScoresTestDataAsync(21)` in tests, not 22.

## CFB Pick Count — `GetCfbRequiredPicks(slateNumber)` C# / `getCfbRequiredPicks(slateNumber)` TS

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
- Every pick count must match `GetCfbRequiredPicks`/`GetRequiredPicks`
- After ANY pick logic change: re-verify seeder counts AND run `npm run test:e2e:demo`
- `ExpectedPickCount` in `DemoDataSeeder.cs` is a guard — update it accurately, never fudge it

## Test Rot Checklist (Before Shipping Any Pick Logic Change)

1. Read the tables above — understand the correct rule FIRST
2. Grep ALL test files for old expected values: `GameHelpersTests.cs`, `LeaderboardTests.cs`, `picks.test.tsx`, e2e specs
3. Update stale assertions BEFORE fixing the implementation
4. Confirm new tests are red before writing any implementation
5. Run full suite: `dotnet test` + `npm run test -- --run` + `npm run test:e2e:demo`
