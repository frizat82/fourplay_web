import { describe, expect, it } from 'vitest';
import { createCompetition } from '../test/fixtures';
import { displayDetails, isGameOver, isGameStarted, isHalfTime } from '../utils/gameHelpers';

// frizat-703.5: before this file, STATUS_IN_PROGRESS/STATUS_HALFTIME wire-value parsing had zero
// unit coverage anywhere — only a hand-crafted, internally-inconsistent e2e fixture exercised
// STATUS_IN_PROGRESS at the display level (detail:"Final" while type.name:"STATUS_IN_PROGRESS",
// which a real ESPN response would never produce), and STATUS_HALFTIME had no fixture at all.
//
// These values are real: captured from a completed 2025 NFL game's actual play-by-play
// (ATL @ IND, end of Q2 and mid-Q3 — see sample_espn_nfl_halftime.json / sample_espn_nfl_in_progress.json
// at the repo root, spliced from the live ESPN API on 2026-07-29).

describe('gameHelpers — live status parsing (real captured values)', () => {
  const halftime = createCompetition({
    homeTeam: 'IND',
    awayTeam: 'ATL',
    homeScore: 13,
    awayScore: 14,
    liveStatus: { name: 'status_halftime', period: 2, displayClock: '0:00' },
  });

  const inProgress = createCompetition({
    homeTeam: 'IND',
    awayTeam: 'ATL',
    homeScore: 13,
    awayScore: 17,
    liveStatus: { name: 'status_in_progress', period: 3, displayClock: '9:47' },
  });

  it('displayDetails shows "Half Time" for a real halftime snapshot', () => {
    expect(displayDetails(halftime)).toBe('Half Time');
  });

  it('displayDetails shows quarter + clock for a real in-progress snapshot', () => {
    expect(displayDetails(inProgress)).toBe('Q3 9:47');
  });

  it('isGameStarted is true for both halftime and in-progress', () => {
    expect(isGameStarted(halftime)).toBe(true);
    expect(isGameStarted(inProgress)).toBe(true);
  });

  it('isGameOver is false for both halftime and in-progress — neither is final', () => {
    expect(isGameOver(halftime)).toBe(false);
    expect(isGameOver(inProgress)).toBe(false);
  });

  it('isHalfTime distinguishes halftime from in-progress', () => {
    expect(isHalfTime(halftime)).toBe(true);
    expect(isHalfTime(inProgress)).toBe(false);
  });
});
