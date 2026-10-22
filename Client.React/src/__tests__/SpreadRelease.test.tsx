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
  it('shows when spreads drop and how long until then, even more than 7 days away', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-09-20T12:00:00Z'); // 24 days out

    await renderAndFlush('cfb');

    expect(screen.getByText(/Spreads drop/i)).toBeInTheDocument();
    expect(screen.getByText('in 24 days')).toBeInTheDocument();
    expect(screen.queryByText('Odds Not Posted')).not.toBeInTheDocument();
  });

  // Leads with when (a day and time people can plan around), not a long hh:mm:ss figure — and no
  // seconds that would sit frozen between the once-a-minute updates.
  it('leads with the release day and time, and shows no seconds over an hour out', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-08-29T15:30:00Z'); // 2 days, 3.5 hours out

    await renderAndFlush('nfl');

    const when = new Date('2026-08-29T15:30:00Z').toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
    expect(screen.getByText(new RegExp(`${when}$`))).toBeInTheDocument();
    expect(screen.getByText('in 2 days, 3 hours')).toBeInTheDocument();
    expect(screen.queryByText(/\d{2}:\d{2}:\d{2}/)).not.toBeInTheDocument();
  });

  it('counts down mm:ss, live, inside the last hour', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-08-27T12:30:00Z'); // 30 minutes out

    await renderAndFlush('nfl');
    expect(screen.getByText('in 30:00')).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5_000);
    });
    expect(screen.getByText('in 29:55')).toBeInTheDocument();
  });

  // Minute ticks must never sleep past the one-hour mark: from 61:30 out, a plain 60s tick would
  // leave "in 1 hour" on screen half a minute into the last hour, then start the clock at 59:30.
  it('switches to the live clock right at the one-hour mark', async () => {
    mockedGetNextSpreadJob.mockResolvedValue('2026-08-27T13:01:30Z'); // 61:30 out

    await renderAndFlush('nfl');
    expect(screen.getByText('in 1 hour, 1 min')).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(91_000); // 59:59 out
    });
    expect(screen.getByText('in 59:59')).toBeInTheDocument();
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
