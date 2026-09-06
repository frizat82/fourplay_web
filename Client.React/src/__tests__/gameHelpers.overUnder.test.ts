import { describe, it, expect } from 'vitest';
import { computeOverWins, computeUnderWins } from '../utils/gameHelpers';

// frizat: found while auditing for other gaps after the home/away covers negation bug — same
// shape, different pair. The backend juices Over and Under independently (SpreadCalculator.
// GetOverUnder: Over threshold = rawTotal - juice, Under threshold = rawTotal + juice), so they
// are NOT the same number once juice is nonzero. The Scores page used to derive "did Under win"
// by negating "did Over win" (`!ov`), which is only correct when the two thresholds are equal.
describe('computeOverWins / computeUnderWins', () => {
  it('Over wins when the total exceeds its own (juiced) threshold', () => {
    // Raw total 44.5, juice 13 -> Over threshold 31.5. Final total 40: 40 > 31.5.
    expect(computeOverWins('final', 31.5, 24, 16)).toBe(true);
  });

  it('both Over and Under can win the same game once juice widens the gap between thresholds', () => {
    // Raw total 44.5, juice 13 -> Over threshold 31.5, Under threshold 57.5. Final total 40 sits
    // strictly between them: Over wins (40 > 31.5) AND Under wins (40 < 57.5) simultaneously.
    // Deriving Under as !overWins would wrongly report exactly one of these as a loss.
    expect(computeOverWins('final', 31.5, 24, 16)).toBe(true);
    expect(computeUnderWins('final', 57.5, 24, 16)).toBe(true);
  });

  it('negating overWins would give the wrong answer for this exact matchup (regression guard)', () => {
    const overWins = computeOverWins('final', 31.5, 24, 16);
    const underWins = computeUnderWins('final', 57.5, 24, 16);
    expect(overWins).toBe(true);
    expect(underWins).toBe(true);
    expect(underWins).not.toBe(!overWins);
  });

  it('Under fails when the total exceeds its own threshold', () => {
    // Under threshold 57.5, final total 60: 60 is not < 57.5.
    expect(computeUnderWins('final', 57.5, 40, 20)).toBe(false);
  });

  it('returns null when the game is not decided', () => {
    expect(computeUnderWins('scheduled', 57.5, null, null)).toBeNull();
  });

  it('returns null when the under threshold is unavailable', () => {
    expect(computeUnderWins('final', null, 24, 16)).toBeNull();
  });
});
