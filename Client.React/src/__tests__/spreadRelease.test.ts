import { vi } from 'vitest';

vi.mock('../api/http', () => ({
  http: {
    get: vi.fn(),
  },
}));

import { http } from '../api/http';
import { getNextSpreadJob } from '../services/spreadRelease';

const mockedGet = vi.mocked(http.get);

describe('getNextSpreadJob', () => {
  beforeEach(() => vi.clearAllMocks());

  it('returns the next scheduled time from the sport-agnostic endpoint when no sport is given', async () => {
    mockedGet.mockResolvedValue({ data: '2026-09-09T14:20:00Z' } as never);

    const result = await getNextSpreadJob();

    expect(result).toBe('2026-09-09T14:20:00Z');
    expect(mockedGet).toHaveBeenCalledWith('/api/jobmanager/get-next-spread-job');
  });

  it('scopes the request to the given sport, lowercased', async () => {
    mockedGet.mockResolvedValue({ data: '2026-09-03T12:00:00Z' } as never);

    const result = await getNextSpreadJob('CFB');

    expect(result).toBe('2026-09-03T12:00:00Z');
    expect(mockedGet).toHaveBeenCalledWith('/api/jobmanager/get-next-spread-job?sport=cfb');
  });

  it('returns null when the server has no next scheduled job', async () => {
    mockedGet.mockResolvedValue({ data: null } as never);

    const result = await getNextSpreadJob('NFL');

    expect(result).toBeNull();
  });
});
