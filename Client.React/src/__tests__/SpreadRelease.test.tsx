import { act, render, screen } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('../services/spreadRelease', () => ({ getNextSpreadJob: vi.fn() }));

import { getNextSpreadJob } from '../services/spreadRelease';
import SpreadRelease from '../components/SpreadRelease';

const mockedGetNextSpreadJob = vi.mocked(getNextSpreadJob);

// fake timers control Date.now() deterministically; advanceTimersByTimeAsync(0) flushes the
// pending getNextSpreadJob() promise + its state updates without needing waitFor (whose own
// polling loop deadlocks against fake timers since nothing else advances them).
async function renderAndFlush(sport: 'nfl' | 'cfb') {
  render(<SpreadRelease sport={sport} />);
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
}

describe('SpreadRelease', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-27T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows "Odds Not Posted" when there is no scheduled spread job at all', async () => {
    mockedGetNextSpreadJob.mockResolvedValue(null);

    await renderAndFlush('nfl');

    expect(screen.getByText('Odds Not Posted')).toBeInTheDocument();
  });

  // frizat: previously this component hid the scheduled date/countdown entirely once the next
  // release was more than 7 days out, falling back to the same generic "Odds Not Posted" message
  // as "no job scheduled at all" — indistinguishable from actually having no information. A
  // scheduled date that's merely far away is still useful information and must always render.
  it('shows the scheduled date and countdown even when the release is more than 7 days away', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-09-20T12:00:00Z'); // 24 days out

    await renderAndFlush('cfb');

    expect(screen.getByText(/Next Spread Reload/i)).toBeInTheDocument();
    expect(screen.getByText(/Scheduled for/i)).toBeInTheDocument();
    expect(screen.queryByText('Odds Not Posted')).not.toBeInTheDocument();
  });

  it('shows the scheduled date and countdown when the release is within 7 days', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-09-01T12:00:00Z'); // 5 days out

    await renderAndFlush('nfl');

    expect(screen.getByText(/Next Spread Reload/i)).toBeInTheDocument();
    expect(screen.getByText(/Scheduled for/i)).toBeInTheDocument();
  });

  // frizat: a countdown weeks out doesn't need a per-second re-render just to keep its seconds
  // digit live — that's up to ~604,800 avoidable re-renders/week for a release that's a season
  // away. Ticks every minute until under an hour remains, then switches to every second.
  it('ticks once a minute (not once a second) when the release is far away', async () => {
    const setTimeoutSpy = vi.spyOn(window, 'setTimeout');
    mockedGetNextSpreadJob.mockResolvedValue('2026-09-20T12:00:00Z'); // 24 days out

    await renderAndFlush('cfb');

    expect(setTimeoutSpy).toHaveBeenCalledWith(expect.any(Function), 60_000);
    expect(setTimeoutSpy).not.toHaveBeenCalledWith(expect.any(Function), 1_000);
  });

  it('ticks once a second when under an hour remains', async () => {
    const setTimeoutSpy = vi.spyOn(window, 'setTimeout');
    mockedGetNextSpreadJob.mockResolvedValue('2026-08-27T12:30:00Z'); // 30 minutes out

    await renderAndFlush('nfl');

    expect(setTimeoutSpy).toHaveBeenCalledWith(expect.any(Function), 1_000);
    expect(setTimeoutSpy).not.toHaveBeenCalledWith(expect.any(Function), 60_000);
  });
});
