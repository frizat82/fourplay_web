# Changelog

Notable changes to IV League, most recent first. For admins — see the version footer for the exact build SHA if you need to correlate with a specific deploy.

Format: `## ` release headings and single-line `- ` bullets only — no bold, links, or multi-line/nested bullets. The admin Changelog page renders this with a small hand-rolled parser (`parseChangelog.ts`), not a full Markdown engine; anything outside this subset renders as literal syntax instead of being formatted. A test guards this file against drifting outside the subset.

## 2026-09-11

- Fixed: the Scores page could sit on stale scores after switching away from the tab and back — the polling pause for a hidden tab resumed correctly, but nothing forced an immediate refresh, so it just waited out the usual 5-20 minute interval instead of updating right away

## 2026-09-10

- Added: leagues can now set a Start Week (1-5, default 1) on the Juice tab so a league's season can skip early weeks — mainly for CFB leagues that want to skip week 1's often lopsided matchups. Weeks before it require no picks, don't appear on the leaderboard, and aren't billed the weekly cost; the following week settles normally, not as a doubled pot
- Added: Picks and Scores now show a clear "Week Not Scored" notice instead of an ordinary pick grid for any week before a league's configured Start Week, so members don't submit picks that silently don't count; the server also rejects a direct pick submission for those weeks
- Fixed: the Scores page's live-update connection could silently go dead for the rest of a game — a dropped network blip, laptop sleep, or wifi/mobile handoff now triggers an automatic reconnect instead of leaving live scores stuck until you reload the page
- Fixed: live scores could stay visibly stale even with a healthy connection — the server only pushed an update when the score itself changed, so a game sitting through any stretch of play with no score change (most plays) never pushed the clock, down/distance, or ball position even though they kept changing
- Fixed: an NFL Scores card could show the ball marker on the field with a blank down-and-distance caption when ESPN's live feed gave a quarter/clock but not the fuller play detail — it now shows nothing for that moment, matching how CFB already handled the same gap

## 2026-09-09

- Fixed: a live game's Scores card could show the red "red zone" border while the field position bar below it showed the ball outside any red zone — both now agree, since ESPN's red zone flag can be momentarily inconsistent with the actual yard line
- Added: real ESPN team logos as an opt-in alternative to the synthetic shield badges on Picks, Scores, and the picks matrix — off by default, no visible change yet
- Fixed: the admin Job Manager page had one generically-labeled "Run Scores Job" / "Run Spreads Job" button that only ever ran NFL's job, with no way to trigger CFB's scores or spreads jobs at all — each sport now has its own clearly-labeled button
- Fixed: a league's Tease Pts and Weekly Cost settings could be edited at any point in the season, with no protection against changing already-decided weeks' terms — Tease Pts now lock once the season starts, and Weekly Cost locks once the season's final week (Super Bowl / Championship) starts

## 2026-09-07

- Fixed: the admin League Costs page's season dropdown was a hardcoded 4-year window unrelated to any real league's history, and could bill a league its flat base cost for seasons before it ever existed — the dropdown now only offers seasons from the earliest any league has actually been configured for, and leagues that didn't exist yet in the selected season are excluded from the cost table entirely
- Fixed: the admin Changelog page's bullet text could visually overflow its card on long entries
- Fixed: the invite-link Copy/Share/Revoke button row on the league page was spaced too tightly on mobile
- Improved: sharing a league's invite link now includes the league's name and an inviting message instead of a bare url
- Fixed: CFB scores could go unsynced for a full week if a game finished Sunday afternoon or later — the fetch schedule now covers the same Sunday/Monday window NFL already does
- Fixed: a league week's payout could be settled — showing a user as losing money — before all of that week's picked games had a final score
- Fixed: a user who hadn't finished picking yet could be shown as having lost the week even before any of that week's games had started
- Fixed: an NFL game's kickoff time in our records could go stale after a flex-schedule move or weather delay, since only the spread itself was ever locked — kickoff time now keeps refreshing so late-game picking windows are tracked correctly
- Fixed: the demo environment's "current week/slate" could silently roll over into a new real NFL/CFB season with no demo data once the real calendar crossed that season's start, breaking Picks/Scores/Leaderboard in demo mode — demo now resolves "current" against a clock pinned to its own seeded data instead of the real wall clock

## 2026-09-08

- Fixed: a CFB game that kicked off Monday night had no scheduled fetch after it finished — scores now also refresh early Tuesday morning, matching the existing post-Monday-Night-Football fetch NFL already has
- Improved: the leaderboard now settles already-decided users right away instead of showing everyone as $0 while a single pick is still pending — the still-in-progress week's column is labeled "Not Final" until every pick that week has a result
- Added: the installed home-screen icon for each sport now shows a small NFL or CFB badge, so the two apps are distinguishable by icon alone instead of only by the label underneath
- Fixed: sharing a league invite link on a browser without native share support (e.g. most desktop browsers) silently copied just the bare url to the clipboard, dropping the inviting message entirely
- Added: shared links now have a real title, description, and image for link previews in apps that build their own instead of using the share sheet's message
- Fixed: the Picks and Scores pages let you navigate past the current week into weeks that hadn't opened yet, including showing clickable pick buttons for a week with no released spread — navigation is now capped at the real current week, and picks for any other week are rejected server-side
- Fixed: clicking "My Picks" or "Scores" while already on that page didn't return you to the current week if you'd navigated away — it now always does
- Fixed: on CFB, the Picks page still let you navigate past the current week all the way to the end of the season — the fix above only covered NFL; Picks now caps at the current week on both sports the same way Scores already did
- Fixed: a CFB score missed by the scheduled fetch (e.g. a Monday-night game) could never be recovered even by re-running the scores job, once that week's window had closed — the job now always checks ESPN fresh instead of only replaying whatever was already saved
- Fixed: NFL scores and spreads relied on ESPN's own week numbering to sort games into the right week, which could misfile a rescheduled or late-finishing game — both now go by our own schedule dates instead, matching how CFB already worked

## 2026-09-06

- Added: live field position now highlights only the actual 20-yard red zone (near whichever goal the ball is closest to) instead of tinting the entire field, and the game card gets a red outline while a live game is in the red zone, so it's spottable at a glance across a list of games
- Removed: the dead "Manage Account" button on the Manage Account page — it just navigated to the page you were already on and did nothing
- Fixed: the live field position ball marker was still on the wrong side whenever the away team had the ball (a follow-up correction to the same-day fix below) — the field-position yard line is a fixed coordinate measured from the home team's own goal regardless of possession, not from whichever team currently has the ball
- Fixed: live field position ball marker rendered on the wrong side of the field whenever the home team had possession, since the home team's own goal is on the right of the on-screen bar but the marker position was never mirrored for it

## 2026-09-05

- Added: users can now change their own username from the Account page, without needing to contact an admin
- Fixed: email subject lines with special characters (e.g. an em-dash) could render as garbled text in the received email
- Fixed: on the Scores page, an away team that "backdoor covered" its own teased spread (e.g. lost the game outright but by less than its spread) could show a red loss icon instead of green — the away result was being derived by inverting the home team's result, which only works when spreads are mirror images, but this league's tease makes them asymmetric
- Fixed: postseason Over/Under picks had the same issue as the spread backdoor-cover bug above — the Under result was derived by inverting the Over result, and CFB additionally showed the raw untaxed total instead of the teased one; the Picks page also showed the same single number for both the Over and Under buttons instead of each side's real line

## 2026-09-04

- Fixed: the page background retiled every screen-height down any page taller than one viewport (Rules, Changelog, and others), creating a visible seam every scroll — root cause of the repeated "background looks inconsistent" reports; fixed once at the CSS source instead of per-page
- Fixed: returning to the installed iOS home-screen app from the Safari sheet opened by "Switch to CFB/NFL" could leave the top bar rendered under the Dynamic Island/status bar until the next reload
- Fixed: a CFB or NFL game with an unusual ESPN status (postponed, delayed, canceled, rain delay, forfeit) could get stuck showing an incorrect status like Final for every other game that week too, since one unrecognized status previously broke parsing for the entire week's scoreboard
- Fixed: a CFB or NFL game could silently show as Final before it even started if ESPN's response was ever missing the status field outright — that field now fails loudly and falls back to Scheduled instead of defaulting to Final
- Fixed: a CFB team with two games landing in ESPN's same weekly scoreboard fetch (e.g. an early-season opener plus that week's real game) could briefly show the wrong game's score and status, since the live fetch queried ESPN by its own week number instead of our own scheduled date range
- Fixed: the admin Job Manager's "run now" button for CFB scores was broken and always failed; the NFL scores button had a related bug where it could trigger the wrong sport's job
- Fixed: "Switch to CFB/NFL" from an installed home-screen app always dropped into the browser with no way back into the app — this control is now hidden inside an installed app (it's still on the regular website); each sport's installed home-screen icon is now labeled distinctly instead of both reading "IV League"

## 2026-09-03

- Fixed: CFB Picks and Scores pages showed the raw Vegas spread with no league juice (tease) applied, unlike NFL — both sports now share the same spread-plus-juice calculator
- Added: CFB Picks and Scores pages now show a team's AP Top 25 rank (e.g. #3) next to its name when ranked
- Added: team badges for 17 CFB opponents that previously fell back to a plain-text badge (ACU, Ball State, ECU, Furman, Idaho, Louisville, Marshall, Missouri State, Oregon State, Tennessee, Tennessee State, Texas A&M's alternate abbreviation, Texas State, UAPB, North Texas, UTEP, Utah Tech)
- Changed: Picks and Scores pages now list games by kickoff time, then by AP rank as a tiebreaker (CFB only — NFL games have no rank, so this stays a pure time sort there)
- Added: this Changelog page (admin-only)
- Fixed: an installed iOS/Android home-screen app could stay on a stale build indefinitely — it now re-checks for updates immediately when reopened, instead of only on a 5-minute timer that pauses while backgrounded
- Changed: NFL league platform cost is now $200 base / $20 per head (CFB unchanged at $100 / $10)
- Removed: the Share button on the Scores page — it only ever linked to the site itself, nothing worth sharing
- Added: a daily catch-up job for CFB rankings capture, so a missed Monday run or a Tuesday CFP release doesn't leave a week's eligibility data stale
- Fixed: the CFB leaderboard showed every remaining week of an in-progress season as a missed pick instead of stopping at the current week, matching NFL's behavior
- Fixed: the season selector on Picks, Scores, and Leaderboard offered years before a league had even started (e.g. a brand-new league showing 2020) — it's now bounded by that league's own earliest configured season
- Fixed: CFB AP Top 25 rankings could be stored more than once per team per week — now one row per team per week, as intended
- Changed: tapping "Switch to CFB/NFL" from an installed home-screen app now shows it will open in the regular browser, since each sport is a separate installed app on iOS
- Fixed: in dark mode, cards could blend almost invisibly into the page background near the bottom of a long page (most visible on the Changelog page) because the page background's own color matched card backgrounds exactly — they're now always visibly distinct

## 2026-09-02

- Fixed: an already-registered user invited to a league (NFL or CFB) wasn't getting an accept/decline notification — both the admin invite page and the shareable invite-link flow were missing the existing-user check
- Fixed: Job Manager's "Last Succeeded"/"Last Failed" columns were blank for most jobs — only 2 of 12 job types were ever reporting their status
- Fixed: dark-mode background looked inconsistent while scrolling on My Leagues, Rules, Invitations, User Management, and Job Manager
- Fixed: the Invite Player / Generate Invite Link / Add User buttons had no visible gap between them on narrow screens
- Removed: Job Manager's "Show background jobs" toggle — it hid dynamic jobs (including Juice Reminder/Lock) by default
