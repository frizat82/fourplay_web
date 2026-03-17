/**
 * frizat-d6y: Lock Games By Individual Game Time
 *
 * These tests verify per-game kickoff time locking replaces the blanket noon-CST cutoff.
 * Write RED first: isAfterKickoff doesn't exist yet — tests will fail on import.
 */
import { isAfterKickoff, shouldShowGamePicks } from '../utils/gameHelpers';
import { createCompetition } from '../test/fixtures';

// Helper: competition with a specific date offset from now
function competitionWithKickoff(offsetMs: number, gameStarted = false) {
  const date = new Date(Date.now() + offsetMs).toISOString();
  return createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted, date });
}

describe('isAfterKickoff', () => {
  it('returns false when game kickoff is 1 hour in the future', () => {
    const comp = competitionWithKickoff(60 * 60 * 1000);
    expect(isAfterKickoff(comp)).toBe(false);
  });

  it('returns true when game kickoff was 1 hour ago', () => {
    const comp = competitionWithKickoff(-60 * 60 * 1000);
    expect(isAfterKickoff(comp)).toBe(true);
  });

  it('accepts injectable now — before kickoff returns false', () => {
    const kickoff = new Date('2024-09-08T18:00:00Z').toISOString();
    const comp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: false, date: kickoff });
    expect(isAfterKickoff(comp, new Date('2024-09-08T17:59:00Z'))).toBe(false);
  });

  it('accepts injectable now — after kickoff returns true', () => {
    const kickoff = new Date('2024-09-08T18:00:00Z').toISOString();
    const comp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: false, date: kickoff });
    expect(isAfterKickoff(comp, new Date('2024-09-08T18:01:00Z'))).toBe(true);
  });

  it('MNF 8:15pm ET game is still open at noon CST Sunday', () => {
    // noon CST (UTC-6) = 18:00 UTC; MNF 8:15pm ET (UTC-4) = 00:15 UTC Monday
    const now = new Date('2024-09-08T18:00:00Z'); // noon CST Sunday
    const mnfKickoff = new Date('2024-09-09T00:15:00Z').toISOString(); // 8:15pm ET Monday
    const comp = createCompetition({ homeTeam: 'KC', awayTeam: 'BAL', gameStarted: false, date: mnfKickoff });
    expect(isAfterKickoff(comp, now)).toBe(false);
  });

  it('TNF game locked independently of Sunday games', () => {
    // At noon CST on Sunday, TNF (Thursday) is locked but Sunday games are not
    const now = new Date('2024-09-08T18:00:00Z'); // noon CST Sunday
    const tnfKickoff = new Date('2024-09-05T17:20:00Z').toISOString(); // Thursday kickoff (past)
    const sundayKickoff = new Date('2024-09-08T21:25:00Z').toISOString(); // SNF (future)

    const tnfComp = createCompetition({ homeTeam: 'KC', awayTeam: 'BAL', gameStarted: false, date: tnfKickoff });
    const sundayComp = createCompetition({ homeTeam: 'DAL', awayTeam: 'NYG', gameStarted: false, date: sundayKickoff });

    expect(isAfterKickoff(tnfComp, now)).toBe(true);
    expect(isAfterKickoff(sundayComp, now)).toBe(false);
  });
});

describe('shouldShowGamePicks', () => {
  it('returns true when game has started (ESPN status updated)', () => {
    const comp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: true });
    expect(shouldShowGamePicks(comp)).toBe(true);
  });

  it('returns true for scheduled game past its kickoff time (ESPN status not yet updated)', () => {
    const pastKickoff = new Date(Date.now() - 5 * 60 * 1000).toISOString(); // 5 min ago
    const comp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: false, date: pastKickoff });
    expect(shouldShowGamePicks(comp)).toBe(true);
  });

  it('returns false for scheduled game before its kickoff time', () => {
    const futureKickoff = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(); // 2 hrs from now
    const comp = createCompetition({ homeTeam: 'BUF', awayTeam: 'MIA', gameStarted: false, date: futureKickoff });
    expect(shouldShowGamePicks(comp)).toBe(false);
  });
});
