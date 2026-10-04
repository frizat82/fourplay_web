import { getTeamColors } from '../components/sports/teamColors';

// ESPN's scoreboard abbreviations (what our games store): COLO = Colorado Buffaloes, BUFF = Buffalo
// Bulls, AFA = Air Force, JXST (formerly JVST) = Jacksonville State. ESPN's /teams list uses
// different codes for some (AF, BUF), which is how these drifted before.
describe('getTeamColors', () => {
  it('resolves Colorado under COLO', () => {
    expect(getTeamColors('COLO').primary).toBe('#cfb87c');
  });

  it('does not give Buffalo (BUFF) Colorado\'s colors', () => {
    expect(getTeamColors('BUFF').primary).not.toBe('#cfb87c');
  });
});

describe('CFB logo files exist under the scoreboard abbreviation', () => {
  const files = Object.keys(import.meta.glob('../../public/Icons/Logos/cfb/*.png')).map((f) => f.split('/').pop());
  for (const abbr of ['colo', 'buff', 'afa', 'jxst', 'jvst']) {
    it(abbr, () => {
      expect(files).toContain(`${abbr}.png`);
    });
  }
});
