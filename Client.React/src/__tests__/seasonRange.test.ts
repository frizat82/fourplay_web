import { buildDescendingSeasonRange } from '../utils/seasonRange';

describe('buildDescendingSeasonRange', () => {
  it('returns years from maxSeason down to minSeason, most recent first', () => {
    expect(buildDescendingSeasonRange(2020, 2023)).toEqual([2023, 2022, 2021, 2020]);
  });

  it('returns a single-element array when minSeason equals maxSeason', () => {
    expect(buildDescendingSeasonRange(2023, 2023)).toEqual([2023]);
  });

  it('still includes minSeason when it is greater than maxSeason, rather than returning an empty range', () => {
    // Every caller (LeagueCostsPage, LeaderboardPage, WeekYearSelector) passes some hardcoded
    // "latest known" upper bound alongside a data-driven minSeason — if that data ever reports a
    // season newer than the hardcoded bound, the range must still be usable, not silently empty.
    expect(buildDescendingSeasonRange(2030, 2026)).toEqual([2030]);
  });
});
