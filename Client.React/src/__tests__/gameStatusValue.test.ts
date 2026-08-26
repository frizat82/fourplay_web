import { isGameDecided, isGameFinal, isGameLive } from '../utils/gameHelpers';

describe('isGameFinal', () => {
  it('is true only for "final"', () => {
    expect(isGameFinal('final')).toBe(true);
  });

  it('is false for in_progress, halftime, scheduled, and null', () => {
    expect(isGameFinal('in_progress')).toBe(false);
    expect(isGameFinal('halftime')).toBe(false);
    expect(isGameFinal('scheduled')).toBe(false);
    expect(isGameFinal(null)).toBe(false);
  });
});

describe('isGameLive', () => {
  it('is true for in_progress and halftime', () => {
    expect(isGameLive('in_progress')).toBe(true);
    expect(isGameLive('halftime')).toBe(true);
  });

  it('is false for final, scheduled, and null', () => {
    expect(isGameLive('final')).toBe(false);
    expect(isGameLive('scheduled')).toBe(false);
    expect(isGameLive(null)).toBe(false);
  });
});

describe('isGameDecided', () => {
  it('is true once a game has started or finished', () => {
    expect(isGameDecided('final')).toBe(true);
    expect(isGameDecided('in_progress')).toBe(true);
    expect(isGameDecided('halftime')).toBe(true);
  });

  it('is false while still scheduled', () => {
    expect(isGameDecided('scheduled')).toBe(false);
    expect(isGameDecided(null)).toBe(false);
  });
});
