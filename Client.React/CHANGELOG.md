# Changelog

Notable changes to IV League, most recent first. For admins — see the version footer for the exact build SHA if you need to correlate with a specific deploy.

Format: `## ` release headings and single-line `- ` bullets only — no bold, links, or multi-line/nested bullets. The admin Changelog page renders this with a small hand-rolled parser (`parseChangelog.ts`), not a full Markdown engine; anything outside this subset renders as literal syntax instead of being formatted. A test guards this file against drifting outside the subset.

## 2026-09-03

- Added: this Changelog page (admin-only)
- Fixed: an installed iOS/Android home-screen app could stay on a stale build indefinitely — it now re-checks for updates immediately when reopened, instead of only on a 5-minute timer that pauses while backgrounded
- Changed: NFL league platform cost is now $200 base / $20 per head (CFB unchanged at $100 / $10)
- Removed: the Share button on the Scores page — it only ever linked to the site itself, nothing worth sharing
- Added: a daily catch-up job for CFB rankings capture, so a missed Monday run or a Tuesday CFP release doesn't leave a week's eligibility data stale

## 2026-09-02

- Fixed: an already-registered user invited to a league (NFL or CFB) wasn't getting an accept/decline notification — both the admin invite page and the shareable invite-link flow were missing the existing-user check
- Fixed: Job Manager's "Last Succeeded"/"Last Failed" columns were blank for most jobs — only 2 of 12 job types were ever reporting their status
- Fixed: dark-mode background looked inconsistent while scrolling on My Leagues, Rules, Invitations, User Management, and Job Manager
- Fixed: the Invite Player / Generate Invite Link / Add User buttons had no visible gap between them on narrow screens
- Removed: Job Manager's "Show background jobs" toggle — it hid dynamic jobs (including Juice Reminder/Lock) by default
