import { buildDescendingSeasonRange } from '../utils/seasonRange';

describe('buildDescendingSeasonRange', () => {
  it('returns years from maxSeason down to minSeason, most recent first', () => {
    expect(buildDescendingSeasonRange(2020, 2023)).toEqual([2023, 2022, 2021, 2020]);
  });

  it('returns a single-element array when minSeason equals maxSeason', () => {
    expect(buildDescendingSeasonRange(2023, 2023)).toEqual([2023]);
  });
});
