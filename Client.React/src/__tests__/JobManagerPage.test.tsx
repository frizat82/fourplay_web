import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

describe('AdminJobManagerPage — background-job toggle visibility', () => {
  beforeEach(() => {
    mockedGetAllJobsStatus.mockReset();
  });

  // frizat: CLAUDE.md's "New nav link or conditional UI element" checklist item — this is the
  // toggle /code-review flagged as having no unit test, only e2e coverage.
  it('hides the toggle entirely when no job in the batch is dynamic', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([makeJob({ jobName: 'User Manager', isDynamic: false })]);

    render(<AdminJobManagerPage />);

    expect(await screen.findByText('User Manager')).toBeInTheDocument();
    expect(screen.queryByLabelText(/show background jobs/i)).not.toBeInTheDocument();
  });

  it('shows the toggle with a count, hides dynamic jobs by default, and reveals them when toggled on', async () => {
    mockedGetAllJobsStatus.mockResolvedValue([
      makeJob({ jobName: 'User Manager', category: 'System', isDynamic: false }),
      makeJob({ jobName: 'Juice Reminder 6-2026', category: 'Juice', isDynamic: true }),
    ]);

    render(<AdminJobManagerPage />);

    expect(await screen.findByText('User Manager')).toBeInTheDocument();
    const toggle = screen.getByLabelText(/show background jobs \(1\)/i);
    expect(toggle).not.toBeChecked();
    expect(screen.queryByText('Juice Reminder 6-2026')).not.toBeInTheDocument();

    await userEvent.click(toggle);

    expect(toggle).toBeChecked();
    expect(screen.getByText('Juice Reminder 6-2026')).toBeInTheDocument();
  });
});
