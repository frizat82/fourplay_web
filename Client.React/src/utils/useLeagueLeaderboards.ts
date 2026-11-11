import { useQuery } from '@tanstack/react-query';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { getLeaderboard } from '../api/leaderboard';
import type { SportAdapter } from '../services/sportAdapter';
import type { LeaderboardDto } from '../types/leaderboard';

export interface LeagueLeaderboard {
  leagueId: number;
  leagueName: string;
  season: number;
  leaderboard: LeaderboardDto[];
}

/**
 * The current season's leaderboard for each of the viewer's leagues in this sport. One query,
 * shared by everything on the dashboard that reads standings, so the leaderboards load once.
 * Undefined until loaded.
 */
export function useLeagueLeaderboards(adapter: SportAdapter): LeagueLeaderboard[] | undefined {
  const { availableLeagues, leaguesLoaded } = useSession();
  const { user } = useAuth();

  return useQuery({
    queryKey: [adapter.sport, 'dashboard-leaderboards', availableLeagues.map(l => l.leagueId), user?.userId],
    queryFn: async (): Promise<LeagueLeaderboard[]> => {
      const season = await adapter.currentSeasonYear();
      return Promise.all(availableLeagues.map(async league => ({
        leagueId: league.leagueId,
        leagueName: league.leagueName ?? 'League',
        season,
        leaderboard: (await getLeaderboard(league.leagueId, season)) ?? [],
      })));
    },
    enabled: leaguesLoaded && availableLeagues.length > 0 && !!user?.userId,
    staleTime: 60_000,
  }).data;
}
