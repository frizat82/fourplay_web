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

### 1. Push notifications that make the Sunday sweat fun (iOS + Android)
- Examples:
  - "Picks lock in 2 hours and you have 2 of 4"
  - "Your BUF pick just covered ✅"
  - "You're 1 of 3 still alive going into SNF 🔥"
  - "You won $15 this week"
- **One Web Push implementation covers both platforms:**
  - Standard Web Push: service worker, VAPID keys, and a `PushSubscriptions` table keyed by user and device.
  - **Android:** works in Chrome, Edge and Firefox, installed or not.
  - **iOS:** needs iOS 16.4+ with the app added to the Home Screen, so onboarding should prompt iPhone users to "Add to Home Screen" first.
  - **Desktop:** works too.
- Builds on pieces we already have: the live score feed and its change events, `MissingPicksJob` for lock reminders, and the pick-result logic.
- Per-user settings: lock reminders, pick results, "still alive", and weekly results.
- Effort: medium.

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
- **Main week unchanged.** A player who buys back is still a loser in the main week and still pays `W × C`, so winners are unaffected.
- **Entry:** a buy-in `B`, set by the commissioner (default `B = C`).
  - The window opens when you're eliminated.
  - It closes at kickoff of the first game still eligible for the Redemption Round.
- **Picks:** a reduced count on games that haven't started, with the same league tease. Default is 2 picks, or 1 on MNF-only weeks.
  - Picks stay hidden until kickoff, like normal picks.
  - One-game guards apply: no picking a game already underway.
- **Payout:** Redemption winners split the pot `N × B` evenly.
  - If nobody hits, the pot **rolls into next week's Redemption pot**. It never goes to main-week winners, which would change their odds after the fact.
  - If only one player buys in, there's no one to beat: refund, or let them play for bragging rights only (commissioner setting).
- **Fair to winners by construction:** their payout is identical whether or not anyone buys back.
- **Worth it for buyers:** a hit returns `N × B`. With 4 buyers at $5, one winner nets +$15, and the losers are out another $5 each.
- It keeps knocked-out players watching Sunday night, which is the actual retention goal.

Example: a 12-player league with C = $5. Three players bust on Thursday and all buy back at $5,
so the pot is $15. The main week settles exactly as it would have. Of the three, one hits both late
picks and takes $15 (net +$10). The other two are out $5 more each.

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
- single-entrant policy (refund, or bragging rights only)

**Pairs well with #1 and #2:** a push notification ("You're out — buy back into the Redemption Round for $5?") and an "N in Redemption" count on the Still Alive board.

**Legal note:** IV League only records a ledger and never handles money. Buy-backs make the game
look more like wagering, so keep it a per-league commissioner toggle, off by default, and have
someone check this before launch.
