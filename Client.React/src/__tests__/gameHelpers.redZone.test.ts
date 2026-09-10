import { describe, it, expect } from 'vitest';
import { isConsistentRedZone } from '../utils/gameHelpers';

// frizat: live bug report — ESPN's own situation.isRedZone was observed true with yardLine=35
// (not within 20 yards of either goal), and ScoresPage's card border trusted the raw flag
// directly while FieldPosition's stripe (correctly) cross-checked yardLine first, so the two
// visually contradicted each other for the same game. Both now go through this one helper.
describe('isConsistentRedZone', () => {
  it('returns false when isRedZone is false, regardless of yardLine', () => {
    expect(isConsistentRedZone({ isRedZone: false, yardLine: 5 })).toBe(false);
  });

  it('returns true when isRedZone is true and yardLine is within 20 of the home goal', () => {
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 15 })).toBe(true);
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 20 })).toBe(true);
  });

  it('returns true when isRedZone is true and yardLine is within 20 of the away goal', () => {
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 85 })).toBe(true);
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 80 })).toBe(true);
  });

  it('returns false when isRedZone is true but yardLine is at midfield (inconsistent upstream data)', () => {
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 35 })).toBe(false);
    expect(isConsistentRedZone({ isRedZone: true, yardLine: 50 })).toBe(false);
  });
});
