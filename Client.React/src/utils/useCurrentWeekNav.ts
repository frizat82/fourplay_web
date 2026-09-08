import { useCallback, useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import type { WeekState } from '../services/sportAdapter';

export interface CurrentWeekSnapshot extends WeekState {
  maxWeek: number;
  maxSeason: number;
}

interface CurrentWeekNavResult {
  currentWeekSnapshot: CurrentWeekSnapshot | null;
  maxWeek: number | undefined;
  maxSeason: number | undefined;
  routeToCurrentIfMatches: (candidate: WeekState) => WeekState | null;
}

/**
 * Shared by PicksPage and ScoresPage — both track "am I viewing the live current week, or a
 * historical one" via `weekState: WeekState | null` (null = live). This hook owns the two
 * pieces of that state machine both pages need identically:
 *  - capturing the real current week's identity + navigable ceiling, only from a live load.
 *    loadHistoricalGames/Scores report the VIEWED week's own maxWeek/maxSeason, which must
 *    never be mistaken for "today's" ceiling — re-deriving the cap from whatever week is
 *    currently being viewed collapses the selector's range down to wherever the user last
 *    looked, trapping them there.
 *  - routeToCurrentIfMatches: a manual navigation that lands back on today's actual week must
 *    collapse back to weekState=null (the live query), not sit in a weekState that happens to
 *    numerically equal today — otherwise the "Archived" chip and stale (non-polling) data
 *    return the moment you Next-then-Prev back to the current week.
 *
 * Does NOT own weekState itself — each page keeps that (their handleWeekChange/
 * handleSeasonChange differ enough to stay page-owned) — callers wrap every candidate through
 * routeToCurrentIfMatches before calling their own setWeekState.
 *
 * Also owns the "always land on the current week from the nav link" reset: AppLayout's
 * handleNavClick passes `state: { resetToCurrent: true }` on every navigate() call, including
 * to the page you're already on — React Router still bumps `location.key` for that, so this
 * effect fires even when the URL itself doesn't change, and reuses the exact same "go to
 * current" path the page's own "Current Week" button already calls (no forced remount, so
 * pending/unsubmitted picks are never silently discarded by clicking a nav link).
 */
export function useCurrentWeekNav(
  isCurrentWeek: boolean,
  data: (WeekState & { maxWeek: number; maxSeason: number }) | null | undefined,
  setWeekState: (s: WeekState | null) => void,
): CurrentWeekNavResult {
  const location = useLocation();
  const [currentWeekSnapshot, setCurrentWeekSnapshot] = useState<CurrentWeekSnapshot | null>(null);

  useEffect(() => {
    if (!isCurrentWeek || !data) return;
    // data gets a new reference on every poll/SSE tick (scores/clock change) — bail out unless
    // the fields this snapshot actually cares about moved, so pages don't re-render on ticks
    // that don't affect the selector cap or current-week identity.
    setCurrentWeekSnapshot(prev =>
      prev
        && prev.season === data.season && prev.week === data.week && prev.isPostSeason === data.isPostSeason
        && prev.maxWeek === data.maxWeek && prev.maxSeason === data.maxSeason
        ? prev
        : { season: data.season, week: data.week, isPostSeason: data.isPostSeason, maxWeek: data.maxWeek, maxSeason: data.maxSeason });
  }, [isCurrentWeek, data]);

  const routeToCurrentIfMatches = useCallback((candidate: WeekState): WeekState | null => {
    if (currentWeekSnapshot
      && candidate.season === currentWeekSnapshot.season
      && candidate.week === currentWeekSnapshot.week
      && candidate.isPostSeason === currentWeekSnapshot.isPostSeason) {
      return null;
    }
    return candidate;
  }, [currentWeekSnapshot]);

  useEffect(() => {
    const state = location.state as { resetToCurrent?: boolean } | null;
    if (state?.resetToCurrent) setWeekState(null);
    // Only re-run when the location itself changes (a real nav-link click), not when
    // setWeekState's identity changes — pages don't memoize it, so including it here would
    // refire this effect on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.key]);

  return {
    currentWeekSnapshot,
    maxWeek: currentWeekSnapshot?.maxWeek,
    maxSeason: currentWeekSnapshot?.maxSeason,
    routeToCurrentIfMatches,
  };
}
