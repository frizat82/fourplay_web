import { renderHook, render, act } from '@testing-library/react';
import { MemoryRouter, useNavigate } from 'react-router-dom';
import { useCurrentWeekNav } from '../utils/useCurrentWeekNav';
import type { WeekState } from '../services/sportAdapter';

function renderHookHost(ui: React.ReactElement) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

// frizat-8y4: shared by PicksPage/ScoresPage — see those pages' own tests for the
// integration-level behavior (Next button capped, "Archived" not shown after Next-then-
// Prev back to today). This file covers the hook's own logic directly.

function makeData(overrides?: Partial<WeekState & { maxWeek: number; maxSeason: number }>) {
  return { season: 2024, week: 2, isPostSeason: false, maxWeek: 2, maxSeason: 2024, ...overrides };
}

describe('useCurrentWeekNav', () => {
  it('captures maxWeek/maxSeason from the live data while isCurrentWeek is true', () => {
    const setWeekState = vi.fn();
    const { result } = renderHook(
      ({ isCurrentWeek, data }) => useCurrentWeekNav(isCurrentWeek, data, setWeekState),
      { wrapper: MemoryRouter, initialProps: { isCurrentWeek: true, data: makeData() } },
    );

    expect(result.current.maxWeek).toBe(2);
    expect(result.current.maxSeason).toBe(2024);
  });

  it('does not update the snapshot from historical (non-current) data', () => {
    const setWeekState = vi.fn();
    const { result } = renderHook(
      ({ isCurrentWeek, data }) => useCurrentWeekNav(isCurrentWeek, data, setWeekState),
      { wrapper: MemoryRouter, initialProps: { isCurrentWeek: false, data: makeData({ week: 1, maxWeek: 1 }) } },
    );

    // Never having seen a live load, there's no real ceiling yet — callers fall back to the
    // adapter's static config in that case, not a historical load's own (misleading) maxWeek.
    expect(result.current.maxWeek).toBeUndefined();
  });

  it('routeToCurrentIfMatches returns null when the candidate matches the captured current week', () => {
    const setWeekState = vi.fn();
    const { result } = renderHook(
      ({ isCurrentWeek, data }) => useCurrentWeekNav(isCurrentWeek, data, setWeekState),
      { wrapper: MemoryRouter, initialProps: { isCurrentWeek: true, data: makeData() } },
    );

    expect(result.current.routeToCurrentIfMatches({ season: 2024, week: 2, isPostSeason: false })).toBeNull();
  });

  it('routeToCurrentIfMatches returns the candidate unchanged when it differs from the current week', () => {
    const setWeekState = vi.fn();
    const { result } = renderHook(
      ({ isCurrentWeek, data }) => useCurrentWeekNav(isCurrentWeek, data, setWeekState),
      { wrapper: MemoryRouter, initialProps: { isCurrentWeek: true, data: makeData() } },
    );

    const candidate = { season: 2024, week: 1, isPostSeason: false };
    expect(result.current.routeToCurrentIfMatches(candidate)).toEqual(candidate);
  });

  // frizat-8y4: clicking "My Picks"/"Scores" while already on that page must always land back
  // on the live current week — AppLayout passes state: { resetToCurrent: true } on every
  // navigate() call, even to the current route.
  it('calls setWeekState(null) when the location carries resetToCurrent state', () => {
    const setWeekState = vi.fn();
    function Harness() {
      const navigate = useNavigate();
      useCurrentWeekNav(false, makeData(), setWeekState);
      return <button onClick={() => navigate('/picks', { state: { resetToCurrent: true } })}>nav</button>;
    }

    const { getByRole } = renderHookHost(<Harness />);
    act(() => { getByRole('button').click(); });

    expect(setWeekState).toHaveBeenCalledWith(null);
  });

  it('does not call setWeekState when the location has no resetToCurrent state', () => {
    const setWeekState = vi.fn();
    function Harness() {
      const navigate = useNavigate();
      useCurrentWeekNav(false, makeData(), setWeekState);
      return <button onClick={() => navigate('/picks')}>nav</button>;
    }

    const { getByRole } = renderHookHost(<Harness />);
    act(() => { getByRole('button').click(); });

    expect(setWeekState).not.toHaveBeenCalled();
  });
});
