import { describe, it, expect } from 'vitest';
import { getWeekFromEspnWeek } from '../utils/gameHelpers';

// frizat-4k9: getWeekFromEspnWeek's season gate (through 2025, ESPN's raw postseason week 5 meant
// the Super Bowl, since week 4 was reserved for the now-discontinued Pro Bowl exhibition) mirrors
// GameHelpers.LastSeasonEspnSkippedProBowlWeek on the backend. This is the one remaining caller's
// (nflAdapter.ts's isFrozenWeekMatch) only real decision point, so the boundary itself is what
// needs covering — not just a happy path.
describe('getWeekFromEspnWeek', () => {
  it('regular season: returns the raw week unchanged regardless of season', () => {
    expect(getWeekFromEspnWeek(10, 2025, false)).toBe(10);
    expect(getWeekFromEspnWeek(10, 2026, false)).toBe(10);
  });

  it('postseason, season <= 2025: raw week 5 (Super Bowl) maps to WeekId 22', () => {
    expect(getWeekFromEspnWeek(5, 2025, true)).toBe(22);
  });

  it('postseason, season <= 2025: weeks 1-3 map by plain +18 arithmetic, not the week-5 special case', () => {
    expect(getWeekFromEspnWeek(1, 2025, true)).toBe(19);
    expect(getWeekFromEspnWeek(2, 2025, true)).toBe(20);
    expect(getWeekFromEspnWeek(3, 2025, true)).toBe(21);
  });

  it('postseason, season > 2025: raw week 5 is NOT remapped — resolves to a deliberately nonexistent WeekId 23', () => {
    // Per docs/ESPNProBowl.md: fails loudly (an unmapped WeekId) rather than silently guessing
    // wrong if ESPN's Pro-Bowl-week gap doesn't close as expected starting the 2026 season.
    expect(getWeekFromEspnWeek(5, 2026, true)).toBe(23);
  });

  it('postseason, season > 2025: raw week 4 (Super Bowl, gap closed) maps to WeekId 22', () => {
    expect(getWeekFromEspnWeek(4, 2026, true)).toBe(22);
  });
});
