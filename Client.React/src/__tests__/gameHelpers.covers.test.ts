import { describe, it, expect } from 'vitest';
import { computeHomeCovers, computeAwayCovers } from '../utils/gameHelpers';

// frizat: reported live bug — a CFB Scores page game (UTEP @ OU, final 0-51) showed UTEP's pick
// as a loss even though UTEP had backdoor-covered its own +53.5 line. Root cause: this app's
// "teased" spread adds the league's juice independently to EACH side (SpreadCalculator.GetSpread,
// called once per team), so home/away spreads are NOT mirror images of each other once juice is
// nonzero (e.g. OU -27.5 / UTEP +53.5 — these do not sum to zero). The Scores page derived the
// away team's cover as `!homeCovers`, which is only correct when the two spreads are exact
// opposites. It must be computed independently from the away team's own spread instead.
describe('computeHomeCovers / computeAwayCovers', () => {
  it('home team covers when it wins by more than its own (juiced) spread', () => {
    // OU -27.5, final OU 51 - UTEP 0: 51 + (-27.5) = 23.5 > 0
    expect(computeHomeCovers('final', -27.5, 51, 0)).toBe(true);
  });

  it('away team backdoor-covers its own teased spread even though it lost outright — independent of the home team result', () => {
    // UTEP +53.5, final OU 51 - UTEP 0: 0 + 53.5 = 53.5 > 51 → UTEP covers its own line,
    // even though the home team (OU) ALSO covers its own line — juice can make both sides "win"
    // their pick simultaneously, so away must never be derived by negating home.
    expect(computeAwayCovers('final', 53.5, 51, 0)).toBe(true);
  });

  it('negating homeCovers would give the wrong answer for this exact matchup (regression guard)', () => {
    const homeCovers = computeHomeCovers('final', -27.5, 51, 0);
    const awayCovers = computeAwayCovers('final', 53.5, 51, 0);
    expect(homeCovers).toBe(true);
    expect(awayCovers).toBe(true);
    expect(awayCovers).not.toBe(!homeCovers);
  });

  it('away team fails to cover when it loses by more than its own spread', () => {
    // MIA +37.5, final MIA 45 - STAN... wait, MIA is home here in the real game; use as a plain
    // away-underdog example: away +37.5, loses by 39 → -39 is not > -37.5
    expect(computeAwayCovers('final', 37.5, 39, 0)).toBe(false);
  });

  it('returns null when the game is not decided', () => {
    expect(computeAwayCovers('scheduled', 53.5, null, null)).toBeNull();
  });

  it('returns null when the away spread is unavailable', () => {
    expect(computeAwayCovers('final', null, 51, 0)).toBeNull();
  });
});
