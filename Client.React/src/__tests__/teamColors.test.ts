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

  it('Buffalo and Colorado have different logos (they were once swapped)', () => {
    const logos = import.meta.glob<string>('../../public/Icons/Logos/cfb/{buff,colo}.png', { query: '?inline', import: 'default', eager: true });
    const [buff, colo] = ['buff', 'colo'].map((a) => Object.entries(logos).find(([k]) => k.endsWith(`/${a}.png`))?.[1]);
    expect(buff).toBeTruthy();
    expect(buff).not.toEqual(colo);
  });

  it("Colorado's helmet badge lives under COLO", () => {
    const helmets = Object.keys(import.meta.glob('../../public/Icons/Helmets/*.svg')).map((f) => f.split('/').pop());
    expect(helmets).toContain('colo.svg');
    expect(helmets).not.toContain('buff.svg');
  });
});
