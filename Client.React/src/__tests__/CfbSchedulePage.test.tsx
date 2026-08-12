import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import CfbSchedulePage from '../pages/admin/CfbSchedulePage';
import type { CfbSeasonWeekConfigDto } from '../types/admin';

vi.mock('../api/cfb', () => ({ getCfbWeekConfigs: vi.fn() }));
import { getCfbWeekConfigs } from '../api/cfb';

const mockedGetCfbWeekConfigs = vi.mocked(getCfbWeekConfigs);

function makeConfig(overrides: Partial<CfbSeasonWeekConfigDto> = {}): CfbSeasonWeekConfigDto {
  return {
    espnWeekNumber: 1,
    ivLeagueWeekNumber: 1,
    weekType: 'Regular Season',
    scoringFormat: 'Standard',
    inScopeIvLeague: true,
    weekStartDate: '2026-09-01',
    weekEndDate: '2026-09-07',
    notes: null,
    ...overrides,
  };
}

// frizat: the out-of-scope sentinel rows (IvLeagueWeekNumber = 99 — ESPN weeks with no
// corresponding pick'em slate, e.g. Week 0 openers, bye/dead weeks) are real, deliberate data the
// backend needs, but they're internal scheduling detail, not something an admin needs to see —
// confusing noise on this page, not a bug in the underlying data.
describe('CfbSchedulePage', () => {
  it('hides out-of-scope rows', async () => {
    mockedGetCfbWeekConfigs.mockResolvedValue([
      makeConfig({ espnWeekNumber: 0, ivLeagueWeekNumber: 99, inScopeIvLeague: false, notes: 'Thu 8/27 openers + Sat 8/29 slate' }),
      makeConfig({ espnWeekNumber: 3, ivLeagueWeekNumber: 3, inScopeIvLeague: true }),
    ]);

    render(<CfbSchedulePage />);

    await waitFor(() => expect(screen.getAllByText('3')).toHaveLength(2));
    expect(screen.queryByText('99')).not.toBeInTheDocument();
    expect(screen.queryByText(/Thu 8\/27 openers/i)).not.toBeInTheDocument();
  });

  it('shows an empty state when every row for the season is out of scope', async () => {
    mockedGetCfbWeekConfigs.mockResolvedValue([
      makeConfig({ espnWeekNumber: 0, ivLeagueWeekNumber: 99, inScopeIvLeague: false }),
    ]);

    render(<CfbSchedulePage />);

    await waitFor(() => expect(screen.getByText(/no week configs found/i)).toBeInTheDocument());
  });
});
