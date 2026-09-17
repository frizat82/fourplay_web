import { getTeamColors } from '../components/sports/teamColors';

// frizat-72k: Colorado's real ESPN wire abbreviation is BUFF (Buffaloes), not COLO — TeamLogo.tsx
// and TeamHelmet.tsx build their asset src path directly from the live abbr prop
// (/Icons/Logos/cfb/${abbr.toLowerCase()}.png), so a key mismatch here meant Colorado silently
// fell through to the default (missing) colors/icon for every real game.
describe('getTeamColors', () => {
  it('resolves Colorado under its real ESPN abbreviation BUFF, not the generic default', () => {
    const buff = getTeamColors('BUFF');
    const unknown = getTeamColors('ZZZZ');
    expect(buff).not.toEqual(unknown);
    expect(buff.primary).toBe('#cfb87c');
  });
});
