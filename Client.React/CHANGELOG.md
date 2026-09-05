# Changelog

Notable changes to IV League, most recent first. For admins — see the version footer for the exact build SHA if you need to correlate with a specific deploy.

Format: `## ` release headings and single-line `- ` bullets only — no bold, links, or multi-line/nested bullets. The admin Changelog page renders this with a small hand-rolled parser (`parseChangelog.ts`), not a full Markdown engine; anything outside this subset renders as literal syntax instead of being formatted. A test guards this file against drifting outside the subset.

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
