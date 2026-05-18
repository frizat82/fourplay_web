import { getCfbWeekName, cfbSlateNumberToWeek, cfbWeekToSlateNumber } from '../utils/gameHelpers';

describe('getCfbWeekName', () => {
  it('returns Week N for regular season', () => {
    expect(getCfbWeekName(1)).toBe('Week 1');
    expect(getCfbWeekName(8)).toBe('Week 8');
    expect(getCfbWeekName(14)).toBe('Week 14');
  });

  it('returns correct labels for all 5 postseason weeks', () => {
    expect(getCfbWeekName(1, true)).toBe('Conf. Championships');
    expect(getCfbWeekName(2, true)).toBe('CFP First Round');
    expect(getCfbWeekName(3, true)).toBe('CFP Quarterfinals');
    expect(getCfbWeekName(4, true)).toBe('CFP Semifinals');
    expect(getCfbWeekName(5, true)).toBe('CFP Championship');
  });

  it('does not throw on week 5 postseason (regression guard)', () => {
    expect(() => getCfbWeekName(5, true)).not.toThrow();
  });

  it('returns fallback string for unknown postseason week instead of throwing', () => {
    expect(getCfbWeekName(6, true)).toMatch(/Postseason Week 6/);
  });
});

describe('cfbSlateNumberToWeek', () => {
  it('maps regular season slate numbers 1-14 to isPostSeason=false', () => {
    expect(cfbSlateNumberToWeek(1)).toEqual({ week: 1, isPostSeason: false });
    expect(cfbSlateNumberToWeek(8)).toEqual({ week: 8, isPostSeason: false });
    expect(cfbSlateNumberToWeek(14)).toEqual({ week: 14, isPostSeason: false });
  });

  it('maps postseason slate numbers 15-19 to isPostSeason=true with correct week', () => {
    expect(cfbSlateNumberToWeek(15)).toEqual({ week: 1, isPostSeason: true });  // Conf Champs
    expect(cfbSlateNumberToWeek(16)).toEqual({ week: 2, isPostSeason: true });  // First Round
    expect(cfbSlateNumberToWeek(17)).toEqual({ week: 3, isPostSeason: true });  // Quarterfinals
    expect(cfbSlateNumberToWeek(18)).toEqual({ week: 4, isPostSeason: true });  // Semifinals
    expect(cfbSlateNumberToWeek(19)).toEqual({ week: 5, isPostSeason: true });  // Championship
  });
});

describe('cfbWeekToSlateNumber', () => {
  it('maps regular season week to slate number directly', () => {
    expect(cfbWeekToSlateNumber(1, false)).toBe(1);
    expect(cfbWeekToSlateNumber(8, false)).toBe(8);
    expect(cfbWeekToSlateNumber(14, false)).toBe(14);
  });

  it('maps postseason week to slate number with offset 14', () => {
    expect(cfbWeekToSlateNumber(1, true)).toBe(15);
    expect(cfbWeekToSlateNumber(5, true)).toBe(19);
  });

  it('is the inverse of cfbSlateNumberToWeek', () => {
    for (let slate = 1; slate <= 19; slate++) {
      const { week, isPostSeason } = cfbSlateNumberToWeek(slate);
      expect(cfbWeekToSlateNumber(week, isPostSeason)).toBe(slate);
    }
  });
});
