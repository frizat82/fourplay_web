import { computeLeagueCost } from '../utils/leagueHelpers';

describe('computeLeagueCost', () => {
  it('returns $100 for leagues with 1-10 members', () => {
    expect(computeLeagueCost(1)).toBe(100);
    expect(computeLeagueCost(5)).toBe(100);
    expect(computeLeagueCost(10)).toBe(100);
  });

  it('charges $10 per member over 10', () => {
    expect(computeLeagueCost(11)).toBe(110);
    expect(computeLeagueCost(12)).toBe(120);
    expect(computeLeagueCost(20)).toBe(200);
  });

  it('returns $100 for 0 members (empty league)', () => {
    expect(computeLeagueCost(0)).toBe(100);
  });
});
