import { lazy, Suspense, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '../services/auth';
import type { SportAdapter } from '../services/sportAdapter';
import { useLeagueLeaderboards, type LeagueLeaderboard } from '../utils/useLeagueLeaderboards';
import { celebrationPicks, findLatestWeekWin, type CelebrationPick } from '../utils/weekWin';
import { hasSeenWeekWin, markWeekWinSeen } from '../utils/weekWinSeen';

// Only winners ever load the animation, so it stays out of the entry chunk. It is a bonus, so a
// chunk that fails to download (flaky signal, a deploy mid-session) shows nothing rather than
// letting the lazy rejection take the whole dashboard down with it.
const loadCelebration = () => import('./WeekWinCelebration');
const WeekWinCelebration = lazy(() => loadCelebration().catch((): Awaited<ReturnType<typeof loadCelebration>> => ({ default: () => <></> })));

/** The first league whose latest final week the viewer won and hasn't been shown yet. */
function firstUnseenWin(leaderboards: LeagueLeaderboard[] | undefined, userId: string | undefined, dismissed: number[]) {
  if (!userId || !leaderboards) return null;
  for (const league of leaderboards) {
    if (dismissed.includes(league.leagueId)) continue;
    const latest = findLatestWeekWin(league.leaderboard, userId);
    if (latest && !hasSeenWeekWin(userId, league.leagueId, league.season, latest.week)) return { ...league, ...latest };
  }
  return null;
}

interface WeekWinCelebrationHostProps {
  adapter: SportAdapter;
}

/**
 * Plays the week-win celebration once per league, the first time the viewer opens the dashboard
 * after winning the latest final week. Reads the leaderboards the dashboard already loads, so
 * nobody who didn't win pays for it.
 */
export default function WeekWinCelebrationHost({ adapter }: WeekWinCelebrationHostProps) {
  const userId = useAuth().user?.userId;
  const leaderboards = useLeagueLeaderboards(adapter);
  // Dismissals this session. The stored marker covers later visits, but may fail to save.
  const [dismissed, setDismissed] = useState<number[]>([]);

  const win = useMemo(() => firstUnseenWin(leaderboards, userId, dismissed), [leaderboards, userId, dismissed]);

  // The week's name and the viewer's covered picks. Without them (a failed load, or a week with
  // no stored games) the celebration still plays, minus the picks.
  const { data: details, isPending } = useQuery({
    queryKey: [adapter.sport, 'weekWinDetails', win?.leagueId, win?.season, win?.week, userId],
    queryFn: async (): Promise<{ weekLabel: string; picks: CelebrationPick[] } | null> => {
      void loadCelebration().catch(() => {}); // fetch the animation while the picks load
      // Both adapters find a past week by season and number alone; isPostSeason is not read.
      const scores = await adapter.loadHistoricalScores(win!.leagueId, userId!, { season: win!.season, week: win!.week, isPostSeason: false });
      if (!scores) return null;
      return { weekLabel: scores.weekLabel, picks: celebrationPicks(scores.userPicks, scores.games) };
    },
    enabled: !!win && !!userId,
    staleTime: Infinity,
    retry: false,
  });

  if (!win || !userId || isPending) return null;

  return (
    <Suspense fallback={null}>
      <WeekWinCelebration
        key={win.leagueId}
        sport={adapter.sport}
        leagueName={win.leagueName}
        weekLabel={details?.weekLabel || `Week ${win.week}`}
        amount={win.amount}
        picks={details?.picks ?? []}
        onDone={() => {
          markWeekWinSeen(userId, win.leagueId, win.season, win.week);
          setDismissed(ids => [...ids, win.leagueId]);
        }}
      />
    </Suspense>
  );
}
