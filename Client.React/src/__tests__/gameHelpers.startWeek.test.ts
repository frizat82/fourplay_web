import { describe, it, expect } from 'vitest';
import { isWeekExcludedFromSeason } from '../utils/gameHelpers';

// frizat-u66: mirrors Shared/Helpers/GameHelpers.cs's IsWeekExcludedFromSeason exactly — the
// frontend and backend must agree on which weeks a league's configured StartWeek (frizat-o3x)
// excludes from scoring.
describe('isWeekExcludedFromSeason', () => {
  it('a week before StartWeek is excluded', () => {
    expect(isWeekExcludedFromSeason(1, 3)).toBe(true);
    expect(isWeekExcludedFromSeason(2, 3)).toBe(true);
  });

  it('the StartWeek itself is not excluded', () => {
    expect(isWeekExcludedFromSeason(3, 3)).toBe(false);
  });

  it('a week after StartWeek is not excluded', () => {
    expect(isWeekExcludedFromSeason(4, 3)).toBe(false);
  });

  it('default StartWeek of 1 excludes nothing', () => {
    expect(isWeekExcludedFromSeason(1, 1)).toBe(false);
  });
});
