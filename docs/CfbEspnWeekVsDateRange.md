# CFB: ESPN week numbers vs. date ranges — history and the current design

This has flip-flopped twice already. Read this before changing how `CfbLiveScoreFetcher` queries
ESPN, so a third flip doesn't happen for a reason already covered here.

## Round 1 (pre-frizat-vaw): date-based queries — buggy

The original CFB jobs (`CfbScoresJob`/`CfbSpreadJob`) queried ESPN by **date**
(`GetTop25ByDateAsync`/`GetScoresByDateAsync`, using a `groups=80` param meant to filter to Top-25
games). Two real bugs, closed by bead **frizat-vaw** (GitHub #93):

- ESPN silently **ignored** `groups=80` — it returned every game on that date, not just Top 25.
- A slate's date range could accidentally overlap a bowl game or another event entirely — date
  ranges don't respect ESPN's own notion of "this game belongs to this bracket/round."

## Round 2 (frizat-vaw → frizat-9m0): week-based queries — fixed the above, introduced a new gap

frizat-vaw switched regular season / conf-champs to ESPN's `week={N}` scoreboard param (plus a
`curatedRank <= 25` filter, later removed by frizat-9m0 in favor of ingesting the full slate and
filtering at the serving layer). CFP rounds use ESPN's `week=999` bucket + a downstream date filter
(`FetchCfpAsync`), since `week=999` returns every CFP game at once regardless of round.

This fixed both Round 1 bugs. But it introduced a different one, not caught until a live incident:
**ESPN's own `week=N` bucketing does not respect our `CfbSeasonWeekConfig`/`CfbSlates` date
boundaries.** A team with an early "week 0"-ish opener can have that game land in the same
`week=N` response as their real week-N game. Confirmed live: querying `week=1&seasontype=2` for
the 2026 season returned **both** USC's Aug 29 opener (vs San José State, `Final`) and their real
Sep 4/5 week-1 game (vs Fresno State, `Scheduled`) — even though Aug 29 is before slate 19's own
`WeekStartDate` (Sep 1).

The frontend's live-score join (`cfbAdapter.ts`'s `buildGamesFromEspn`) keys its ESPN lookup map by
**home-team abbreviation only**, on the explicit assumption that a team plays at most one game per
slate. That assumption is normally true — but wasn't, for this one fetch. Whichever of the two USC
events iterated last in the response silently overwrote the other in the map, so the Picks/Scores
page briefly rendered USC vs. Fresno State (not yet started) with San José State's already-final
42-26 result attached.

## Round 3 (frizat-11t, current): date-range query, scoped to our own control table

The fix (bead **frizat-11t**) is neither of the above — it's not a revert to Round 1's
`groups=80` date approach:

- `ICfbApiService.GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate)` queries ESPN's
  `dates=yyyyMMdd-yyyyMMdd` scoreboard param, **not** `groups=80`. No `isPostSeason` parameter —
  CFP already has its own correct, dedicated mechanism (`GetCfpGamesAsync`, below), so this method
  is regular-season/conf-champs only; a postseason branch here would be dead code no caller
  exercises.
- `CfbLiveScoreFetcher.FetchRegularSeasonAsync` (formerly `FetchRankedWeekAsync`) calls it with
  `slate.StartDate`/`slate.EndDate` — the control table's own boundaries — instead of
  `slate.EspnWeekNumber`, **and** applies the same downstream date-window filter `FetchCfpAsync`
  already used, as defense in depth on top of the query param (the fix's whole premise is "don't
  fully trust ESPN's own bucketing," so it shouldn't fully trust the query param to be honored
  perfectly in every edge case either — e.g. a timezone boundary on a late-night/West-Coast kickoff).
- Verified live: `dates=20260901-20260907` (slate 19's actual window) returns **exactly one** USC
  game — Fresno State. The Aug 29 SJSU game is correctly excluded.

This makes "one game per team per slate" actually true instead of an unchecked assumption, so the
frontend's home-team-only join key (deliberately **not** changed by this fix — see the
`buildGamesFromEspn` comment) is safe again. It also extends the same principle PR #285
(`fix/current-week-control-table-authoritative`) established for *which slate is current* — the
control table wins over ESPN's own bucketing — to *which games belong to that slate's fetch* too.

**Why NFL doesn't need this fix too:** NFL's regular-season week query can't structurally hit this
bug class. CFB's early "week 0" openers are tagged `seasontype=2` (regular season) by ESPN despite
predating the control table's week-1 window — that's the actual mechanism that let USC's Aug 29
game leak into a `week=1` fetch. NFL has no equivalent: its preseason is a wholly separate
`seasontype=1` bucket that a `seasontype=2` week query never returns, and the regular season
schedule gives each team exactly one game per week by construction — there's no "early game
folded into week 1's bucket" scenario for `EspnCacheService`'s week-based NFL query to reproduce.
Checked per this repo's NFL/CFB sharing rule; this is a genuine CFB-only difference, not a gap.

`EspnWeekNumber` is **not removed** from `CfbSlates`/`CfbSeasonWeekConfig`. It's still required as
the natural-key component `CfbRanking` rows and `CfbPicksController`'s ranking lookup use
(`(Season, EspnWeekNumber, TeamAbbreviation)`) — it's just no longer used to construct the
scoreboard query itself. `CfbRankingCaptureJob` shares the same `FetchForSlateAsync` call as scores,
so rankings and scores are now both date-scoped together automatically — nothing is left on a
week-based path to drift out of sync with the other.

## If this needs to change a fourth time

Before reverting to week-based or introducing yet another approach: the actual failure mode was a
**team appearing twice in one raw ESPN response**, not the date-range mechanism itself. Any future
change should be checked against that specific scenario (a team with two games — e.g. a rescheduled
game, a make-up game, a true week-0 opener — landing in the same fetch) before shipping.
