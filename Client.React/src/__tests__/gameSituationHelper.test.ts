import { describe, it, expect } from 'vitest';
import { getSituations } from '../utils/gameSituationHelper';

describe('getSituations', () => {
  describe('home spread coverage', () => {
    it('returns null when home team is already covering the spread', () => {
      // BUF -7, BUF leads 21-7 → coverMargin = 21 + (-7) - 7 = 7 ≥ 0 → no message
      const result = getSituations('BUF', 'MIA', 21, 7, -7, undefined);
      expect(result).toBeNull();
    });

    it('returns home team cover message when home team needs one FG (≤3 points)', () => {
      // BUF -7, BUF leads 10-7 → coverMargin = 10 + (-7) - 7 = -4 → needs 4 points → "one score"
      // Actually: needs ceil(4) = 4 → "one score"
      // Let's pick a case where needs ≤ 3: BUF -7, BUF leads 10-6 → 10 + (-7) - 6 = -3 → needs 3 → "one FG"
      const result = getSituations('BUF', 'MIA', 10, 6, -7, undefined);
      expect(result).toBe('BUF needs one FG to cover the spread (-7).');
    });

    it('returns home team cover message when home team needs one score (4-7 points)', () => {
      // BUF -7, BUF leads 10-10 → coverMargin = 10 + (-7) - 10 = -7 → needs 7 → "one score"
      const result = getSituations('BUF', 'MIA', 10, 10, -7, undefined);
      expect(result).toBe('BUF needs one score to cover the spread (-7).');
    });

    it('returns home team cover message when home team needs two scores (8-14 points)', () => {
      // BUF -7, BUF tied 7-7 → 7 + (-7) - 7 = -7... needs 7 → one score
      // Let's try: BUF -10, BUF leads 10-14 → 10 + (-10) - 14 = -14 → needs 14 → "two scores"
      const result = getSituations('BUF', 'MIA', 10, 14, -10, undefined);
      expect(result).toBe('BUF needs two scores to cover the spread (-10).');
    });

    it('returns home team cover message when home team needs three scores (15-21 points)', () => {
      // BUF -3, BUF behind 0-21 → 0 + (-3) - 21 = -24 → needs 24 → "4 scores"
      // Let's do 15 points: BUF -3, BUF behind 0-18 → 0 + (-3) - 18 = -21 → needs 21 → "three scores"
      const result = getSituations('BUF', 'MIA', 0, 18, -3, undefined);
      expect(result).toBe('BUF needs three scores to cover the spread (-3).');
    });

    it('returns home team cover message with N scores when gap > 21 points', () => {
      // BUF -3, BUF behind 0-28 → 0 + (-3) - 28 = -31 → needs 31 → floor(31/7)+1 = 4+1 = 5 scores
      const result = getSituations('BUF', 'MIA', 0, 28, -3, undefined);
      expect(result).toBe('BUF needs 5 scores to cover the spread (-3).');
    });
  });

  describe('away spread coverage', () => {
    it('returns null when away team is already covering the spread', () => {
      // MIA +7, BUF leads 21-20 → awayMargin = 20 + 7 - 21 = 6 ≥ 0 → null
      const result = getSituations('BUF', 'MIA', 21, 20, undefined, 7);
      expect(result).toBeNull();
    });

    it('returns away team cover message when away team needs one FG', () => {
      // MIA +3, BUF leads 21-17 → awayMargin = 17 + 3 - 21 = -1 → needs 1 → "one FG"
      const result = getSituations('BUF', 'MIA', 21, 17, undefined, 3);
      expect(result).toBe('MIA needs one FG to cover the spread (3).');
    });

    it('returns away team cover message when away team needs one score', () => {
      // MIA +3, BUF leads 21-14 → 14 + 3 - 21 = -4 → needs 4 → "one score"
      const result = getSituations('BUF', 'MIA', 21, 14, undefined, 3);
      expect(result).toBe('MIA needs one score to cover the spread (3).');
    });
  });

  describe('over line', () => {
    it('returns null when total equals the over line', () => {
      const result = getSituations('BUF', 'MIA', 21, 26, undefined, undefined, 47, undefined);
      expect(result).toBeNull();
    });

    it('returns null when total exceeds the over line', () => {
      const result = getSituations('BUF', 'MIA', 28, 28, undefined, undefined, 47, undefined);
      expect(result).toBeNull();
    });

    it('returns over message when total is below over line', () => {
      // total = 30, overLine = 47 → needs 17 more points
      const result = getSituations('BUF', 'MIA', 17, 13, undefined, undefined, 47, undefined);
      expect(result).toBe('The game needs 17 more points to hit the Over (47).');
    });

    it('returns over message with 1 point needed', () => {
      const result = getSituations('BUF', 'MIA', 23, 23, undefined, undefined, 47, undefined);
      expect(result).toBe('The game needs 1 more points to hit the Over (47).');
    });
  });

  describe('under line', () => {
    it('returns null when total equals the under line', () => {
      const result = getSituations('BUF', 'MIA', 21, 26, undefined, undefined, undefined, 47);
      expect(result).toBeNull();
    });

    it('returns null when total is below the under line', () => {
      const result = getSituations('BUF', 'MIA', 10, 10, undefined, undefined, undefined, 47);
      expect(result).toBeNull();
    });

    it('returns under exceeded message when total exceeds under line', () => {
      // total = 50, underLine = 47 → exceeded by 3
      const result = getSituations('BUF', 'MIA', 28, 22, undefined, undefined, undefined, 47);
      expect(result).toBe('The game has exceeded the Under by 3 points (47).');
    });

    it('returns under exceeded message with 1 point over', () => {
      const result = getSituations('BUF', 'MIA', 24, 24, undefined, undefined, undefined, 47);
      expect(result).toBe('The game has exceeded the Under by 1 points (47).');
    });
  });

  describe('null / undefined spread inputs', () => {
    it('returns null when all optional params are undefined', () => {
      const result = getSituations('BUF', 'MIA', 21, 14);
      expect(result).toBeNull();
    });

    it('returns null when all optional params are null', () => {
      const result = getSituations('BUF', 'MIA', 21, 14, null, null, null, null);
      expect(result).toBeNull();
    });

    it('ignores null homeSpread and still processes awaySpread', () => {
      // MIA +7, BUF leads 21-10 → 10 + 7 - 21 = -4 → needs 4 → "one score"
      const result = getSituations('BUF', 'MIA', 21, 10, null, 7, null, null);
      expect(result).toBe('MIA needs one score to cover the spread (7).');
    });
  });

  describe('last-writer-wins ordering', () => {
    // The function overwrites `output` sequentially; the last truthy condition wins.
    it('under message takes precedence over home spread when both trigger', () => {
      // homeSpread: BUF -3, BUF leads 10-0 → covers → no home spread message
      // underLine: total 10 > 7 → exceeded by 3
      const result = getSituations('BUF', 'MIA', 10, 0, -3, undefined, undefined, 7);
      // home doesn't trigger (covers), under triggers → under wins
      expect(result).toBe('The game has exceeded the Under by 3 points (7).');
    });

    it('under message wins when it is evaluated last', () => {
      // Both over and under conditions can't both trigger simultaneously for the same game,
      // but if only under triggers it should be the result.
      const result = getSituations('BUF', 'MIA', 30, 25, undefined, undefined, undefined, 47);
      // total = 55 > 47 → exceeded by 8
      expect(result).toBe('The game has exceeded the Under by 8 points (47).');
    });
  });
});
