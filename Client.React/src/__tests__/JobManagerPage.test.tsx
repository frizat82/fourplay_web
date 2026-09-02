import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import AdminJobManagerPage from '../pages/admin/JobManagerPage';
import type { JobStatusResponse } from '../types/admin';

vi.mock('../api/jobManager', () => ({
  getAllJobsStatus: vi.fn(),
  runSpreads: vi.fn(),
  runUserManager: vi.fn(),
  runScores: vi.fn(),
}));
vi.mock('../services/toast', () => ({ useToast: () => ({ push: vi.fn() }) }));

import { getAllJobsStatus } from '../api/jobManager';

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
});
