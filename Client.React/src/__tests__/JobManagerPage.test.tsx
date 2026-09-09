import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import AdminJobManagerPage from '../pages/admin/JobManagerPage';
import type { JobStatusResponse } from '../types/admin';

vi.mock('../api/jobManager', () => ({
  getAllJobsStatus: vi.fn(),
  runSpreads: vi.fn(),
  runCfbSpreads: vi.fn(),
  runUserManager: vi.fn(),
  runScores: vi.fn(),
  runCfbScores: vi.fn(),
}));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));

import { getAllJobsStatus, runScores, runCfbScores, runSpreads, runCfbSpreads } from '../api/jobManager';

const mockedGetAllJobsStatus = vi.mocked(getAllJobsStatus);

function makeJob(overrides: Partial<JobStatusResponse> = {}): JobStatusResponse {
  return {
    jobName: 'User Manager',
    description: 'Manages initial user admin',
    status: 'Idle',
    nextRun: null,
    lastSucceededUtc: null,
    lastFailedUtc: null,
    lastMessage: null,
    category: 'System',
    isDynamic: false,
    ...overrides,
  };
}

describe('AdminJobManagerPage', () => {
  beforeEach(() => {
    mockedGetAllJobsStatus.mockReset();
  });

  // Regression: the previous "Show background jobs" toggle hid every isDynamic job (Juice
  // Reminder/Lock, per-week NFL/CFB Spreads) behind a default-off switch — reported as "doesn't
  // do anything" and as hiding jobs an admin needed to see (there's no other way to check a
  // league's juice-lock reminder is actually scheduled). Every job is now always visible.
  it('shows every job — dynamic (per-league/per-week) and fixed — with no hide/show toggle', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([
      makeJob({ jobName: 'User Manager', category: 'System', isDynamic: false }),
      makeJob({ jobName: 'Juice Reminder 6-2026', category: 'Juice', isDynamic: true }),
    ]);

    render(<AdminJobManagerPage />);

    expect(await screen.findByText('User Manager')).toBeInTheDocument();
    expect(screen.getByText('Juice Reminder 6-2026')).toBeInTheDocument();
    expect(screen.queryByLabelText(/show background jobs/i)).not.toBeInTheDocument();
  });

  it('groups jobs under their category header', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([
      makeJob({ jobName: 'User Manager', category: 'System', isDynamic: false }),
      makeJob({ jobName: 'Juice Reminder 6-2026', category: 'Juice', isDynamic: true }),
    ]);

    render(<AdminJobManagerPage />);

    expect(await screen.findByText('System', { exact: true })).toBeInTheDocument();
    expect(screen.getByText('Juice', { exact: true })).toBeInTheDocument();
  });

  // Regression: a single generically-labeled "Run Scores Job" button previously ran NFL's job
  // only, with no way to trigger CFB's scores/spreads jobs from this page at all — a real prod
  // admin mix-up (clicked expecting it to cover CFB, it silently only ran NFL). Each sport now
  // gets its own explicitly-labeled button wired to its own API call — pin both the labels and
  // that each one calls the correct (and only the correct) underlying function.
  it('renders a dedicated button per job type, never combining NFL and CFB', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([]);
    render(<AdminJobManagerPage />);

    expect(await screen.findByRole('button', { name: /run nfl scores job/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /run cfb scores job/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /run nfl spreads job/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /run cfb spreads job/i })).toBeInTheDocument();
  });

  it('clicking "Run NFL Scores Job" calls runScores only, never runCfbScores', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([]);
    render(<AdminJobManagerPage />);

    await userEvent.click(await screen.findByRole('button', { name: /run nfl scores job/i }));

    expect(runScores).toHaveBeenCalledTimes(1);
    expect(runCfbScores).not.toHaveBeenCalled();
  });

  it('clicking "Run CFB Scores Job" calls runCfbScores only, never runScores', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([]);
    render(<AdminJobManagerPage />);

    await userEvent.click(await screen.findByRole('button', { name: /run cfb scores job/i }));

    expect(runCfbScores).toHaveBeenCalledTimes(1);
    expect(runScores).not.toHaveBeenCalled();
  });

  it('clicking "Run CFB Spreads Job" calls runCfbSpreads only, never runSpreads', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([]);
    render(<AdminJobManagerPage />);

    await userEvent.click(await screen.findByRole('button', { name: /run cfb spreads job/i }));

    expect(runCfbSpreads).toHaveBeenCalledTimes(1);
    expect(runSpreads).not.toHaveBeenCalled();
  });
});
