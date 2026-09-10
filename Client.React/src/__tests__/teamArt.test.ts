import { getTeamArtMode } from '../utils/teamArt';

describe('getTeamArtMode', () => {
  afterEach(() => vi.unstubAllEnvs());

  it('defaults to badges when VITE_TEAM_ART_MODE is unset', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', '');
    expect(getTeamArtMode()).toBe('badges');
  });

  it('returns logos when VITE_TEAM_ART_MODE=logos', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', 'logos');
    expect(getTeamArtMode()).toBe('logos');
  });

  it('falls back to badges for any unrecognized value', () => {
    vi.stubEnv('VITE_TEAM_ART_MODE', 'sprites');
    expect(getTeamArtMode()).toBe('badges');
  });
});
