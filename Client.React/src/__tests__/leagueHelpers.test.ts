import { computeLeagueCost } from '../utils/leagueHelpers';

describe('computeLeagueCost', () => {
  it('returns the flat base cost when memberCount is at or below the base member count', () => {
    expect(computeLeagueCost(10)).toBe(100);
    expect(computeLeagueCost(5)).toBe(100);
    expect(computeLeagueCost(0)).toBe(100);
  });

  it('adds a per-head charge for each member above the base member count', () => {
    expect(computeLeagueCost(12)).toBe(120);
    expect(computeLeagueCost(20)).toBe(200);
  });
});
