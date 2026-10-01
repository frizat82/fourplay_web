# IV League — Ideas Backlog

Brainstorm of additions to grow users, keep them engaged, and make the game more fun. Nothing here
is committed work; promote an idea to a GitHub issue (bd syncs it to a bead) when it's picked up.
Everything below applies to **both NFL and CFB** through the shared adapter/service path — no
sport-specific copies.

## The core problem these ideas target

Every pick must hit, so most players are eliminated early in the week (often Thursday night or
the Sunday early window) and have nothing to watch or do until next week. Most of the ideas
below are about keeping eliminated players engaged, and about spreading the game through group
chats.

---

## Game changers

### 1. Push notifications — final result only, not live score chasing (iOS + Android)
Deliberately **not** a live in-game feed — the Scores page already shows red/green cover state in
real time for anyone watching, so a push on every lead change would just duplicate the UI and spam
people. Two triggers only, both fired once per event, never continuously:
- **Per-game final result**, when a game you have a pick in goes final: "Your BUF pick covered ✅"
  / "Your BUF pick missed ❌." One push per pick, at that game's final whistle — not during it.
- **Week result**, once every pick you made for the week has a final game and all of them covered:
  "You went 4/4 this week 🎉." No separate "you lost" push — the per-game misses already told you.
- **One Web Push implementation covers both platforms:**
  - Standard Web Push: service worker, VAPID keys, and a `PushSubscriptions` table keyed by user and device.
  - **Android:** works in Chrome, Edge and Firefox, installed or not.
  - **iOS:** needs iOS 16.4+ with the app added to the Home Screen, so onboarding should prompt iPhone users to "Add to Home Screen" first.
  - **Desktop:** works too.
- **What this reuses vs. needs new:** the cover computation itself already exists
  (`computeHomeCovers`/`computeAwayCovers`); the scores jobs already detect when a game transitions
  to final. What's new: persisting "has this game's final-result push already fired" (so a job
  retry doesn't double-send), a per-pick fan-out from "game X went final" to "everyone who picked a
  team in game X," and a per-user check after each fan-out for "is this now their last outstanding
  pick, and did all of them cover." No MissingPicksJob involvement — that job was removed
  (reminders are a different, already-settled decision) and isn't part of this.
- Per-user settings: pick results, weekly results. (Lock reminders are a separate, already-rejected
  idea — see the removed `MissingPicksJob` note above.)
- Effort: small-medium — simpler than it first looked, since it's two one-time state transitions
  per pick/week, not a continuous live feed to maintain server-side.

### 2. "Still Alive" live board
- A survivor-style banner during games: "7 of 12 still alive, 4 riding the Chiefs."
- It updates in real time through the existing live score connection, with animated eliminations.
- It makes being knocked out part of the show instead of the end of your week.
- Effort: small to medium, since it's mostly UI on data we already have.

### 3. Redemption Round / weekly buy-back
See the full design in [Weekly buy-back: fair design options](#weekly-buy-back-fair-design-options) below.

### 4. Settle-up ledger
- Collecting juice is the commissioner's biggest pain.
- Show who owes whom for the week and the season, with a "paid ✓" toggle for the commissioner.
- **Venmo / Cash App / PayPal deep links** pre-fill the amount and note.
- IV League never holds or moves money (ledger only), which keeps it legally simple.
- The settlement math already exists in `LeaderboardSettlementHelper`.
- Effort: medium.

### 5. Shareable cards for group chats (growth)
- Auto-generated images:
  - "Week 4 winners 💰"
  - "Bad beat of the week: lost by half a point"
  - a player's locked picks
- Invite links get rich previews (OpenGraph image with league name and pot size) instead of a bare URL.
- Group chats are where leagues actually live, so that's where the app should show up.
- Effort: small to medium, with server-rendered images.

### 6. Season "Wrapped"
- A Spotify-style year-end recap:
  - cover rate
  - most-picked team ("you trusted the Cowboys 11 times. Why.")
  - longest streak
  - worst beat
  - net money
- Extremely shareable, and it gets people to come back for next season.
- All the data is already in the database. Effort: medium.

## Quick wins
- **League pick split after lock:** "70% on KC." A "Contrarian" badge when you fade the league and win.
- **Reactions:** emoji reactions or trash talk on locked picks and results.
- **Win-probability meter:** convert each spread into odds, e.g. "your 4 picks have a 9% chance to all cover." It moves live.
- **Practice league:** new sign-ups land in a demo league before real money. `DemoDataSeeder` already builds one.
- **Commissioner bot:** Discord or Slack posts picks at lock, live eliminations and weekly winners. The Discord notifier already exists.

## Zany
- **AI commissioner recap:** Claude writes a weekly roast of the league ("Dave took the Jets again. Dave is not well."), posted to the group or emailed.
- **Mulligan / double-down power-ups:** once a season, turn one loss into a push, or double your stake on a week. Commissioner toggle. It changes the money math, so it needs the same fairness treatment as the buy-back below.
- **Rivalry mode:** challenge one leaguemate to a head-to-head side bet on the same week.
- **More sports:** NFL/CFB is one engine with a small per-sport adapter, so NBA/NHL playoffs or March Madness is mostly a new data feed.

## Suggested order
1. Push notifications (#1) and the Still Alive board (#2): they turn Sunday into an event.
2. Share cards (#5): the cheapest growth channel.
3. Settle-up ledger (#4): happy commissioners keep leagues running.
4. Redemption Round (#3), using the side-pot design below.

---

## Weekly buy-back: fair design options

**Goal:** a player knocked out Thursday can get back into the week, without costing the players
who are still winning anything they'd otherwise have earned.

### How the money works today (from `LeaderboardSettlementHelper.SettleWeeks`)
- Pairwise settlement: **every loser pays every winner the weekly cost `C`.**
  - A winner collects `L × C`, where `L` is the number of losers.
  - A loser pays `W × C`, where `W` is the number of winners.
- If everyone wins or everyone loses, it's a **push**. Nobody pays, and next week's cost grows to `C + base`.

**What that means for any buy-back:** each winner's payout depends directly on how many losers
there are. If a buy-back can turn a loser into a winner, every existing winner loses `C`, and the
re-entrant starts collecting from the others. A buy-back that feeds back into the main week can't
be fair to winners unless its price exactly offsets that, and the right price depends on the
re-entrant's odds, which nobody can pin down. So **the main week's result has to stay final.**

### Option A — Redemption side pot (recommended)
Eliminated players can buy into a separate pot that runs on the rest of the week's games.
- **Main week unchanged.** A player who buys back is still a loser in the main week and still pays `W × C`, so winners' *guaranteed* payout never shrinks — see the winner cut below for the part that's new on top of it.
- **Entry:** a buy-in `B`, set by the commissioner (default `B = C`).
  - The window opens when you're eliminated.
  - It closes at kickoff of the first game still eligible for the Redemption Round.
- **Picks:** a reduced count on games that haven't started, with the same league tease. Default is 2 picks, or 1 on MNF-only weeks.
  - Picks stay hidden until kickoff, like normal picks.
  - One-game guards apply: no picking a game already underway.
- **Winner cut, so main-week winners aren't left out:** a commissioner-set slice `r` of the buy-in
  pool (default 20%) goes to the main-week winners, split evenly, *before* the Redemption pot is
  decided. The rest, `(1 − r) × N × B`, is what Redemption winners split.
  - This is a flat bonus funded by money that's already in hand (collected buy-ins) — not a bet on
    anyone's picks hitting, so it adds zero variance for winners. A winner's payout is `L × C +
    bonus`, always ≥ what they'd have gotten with no Redemption Round at all, never less.
  - At `r = 0` this collapses back to the original design (winners get nothing extra); the
    commissioner can set it there if they'd rather keep it simple.
- **Payout:** Redemption winners split `(1 − r) × N × B` evenly.
  - If nobody hits, that portion **rolls into next week's Redemption pot** — never to main-week
    winners, which would change their odds after the fact. The winner cut `r × N × B` is still paid
    out immediately regardless (it was never part of the contested pot).
  - If only one player buys in, there's no one to beat: refund, or let them play for bragging rights only (commissioner setting).
- **Worth it for buyers:** a hit returns a share of `(1 − r) × N × B`. With 4 buyers at $5 and `r = 20%`, the Redemption pot is $16, so one winner nets +$11, and the losers are out another $5 each.
- It keeps knocked-out players watching Sunday night, which is the actual retention goal — and now the players who are already winning have a reason to want the feature to exist too.

Example: a 12-player league with C = $5, `r = 20%`. Three players bust on Thursday and all buy back
at $5, so the pool is $15. The main week settles exactly as it would have. Winners split a $3 bonus
(say 3 winners → $1 each) immediately. The Redemption pot is the remaining $12; of the three
buyers, one hits both late picks and takes $12 (net +$7 on their $5 buy-in). The other two are out
$5 more each.

### Option B — Reduced-loss buy-back (only if a commissioner insists)
The buyer pays `B` to the main-week winners up front. If their Redemption picks all hit, their main-week debt drops by a fraction `f` (e.g. 50%).
- **Break-even price for winners:** they must gain at least as much as they can lose.
  - Winners collectively lose `f × W × C` with the probability `p` that the redemption hits.
  - That requires `B ≥ p × f × W × C`.
- **Worked example:** 2 teased legs at about 65–70% each gives `p` of roughly 0.45.
  - With `f = 0.5`, `W = 3`, `C = $5`: `B ≥ ~$3.40`.
- Downsides:
  - It adds variance winners didn't choose.
  - `p` is only an estimate.
  - It's harder to explain.
- Treat it as optional. Option A gets the same engagement without touching anyone's winnings.

### Option C — Full re-entry into the main week (not recommended)
The buyer becomes a normal main-week player again if their late picks all hit. This is unfair to
winners by the math above: every winner loses `C` for each loser who converts. A fair price would
depend on odds that can't be known up front, and the market is tiny (a handful of friends). **Don't build.**

### Implementation sketch for Option A
**Data:** a `RedemptionEntries` table, plus picks flagged as Redemption.
- `RedemptionEntries`: `LeagueId`, `Season`, `Week`/`Slate`, `UserId`, `BuyIn`, `CreatedAt`.
- Redemption picks: a flag on the existing pick tables, or a sibling table.
- Keep NFL and CFB on one shared implementation.

**Settlement:** a separate `SettleRedemption` next to `SettleWeeks`. It must never touch main-week Scores.
- Show it as its own leaderboard column, or its own row in the settle-up ledger (#4).

**League settings:** in `LeagueJuiceMapping` or the league settings.
- `RedemptionEnabled`
- `RedemptionBuyIn`
- `RedemptionPickCount`
- `RedemptionWinnerCutPercent` (default 20%, can be set to 0 to keep the original no-bonus design)
- single-entrant policy (refund, or bragging rights only)

**Pairs well with #2:** an "N in Redemption" count on the Still Alive board. (Not #1 — push
notifications are scoped to final pick/week results only, not buy-back offers.)

**Legal note:** IV League only records a ledger and never handles money. Buy-backs make the game
look more like wagering, so keep it a per-league commissioner toggle, off by default, and have
someone check this before launch.
