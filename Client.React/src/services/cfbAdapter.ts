import { getCfbSlates, getCfbSpreads, getCfbUserPicks, getCfbAllPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import { getCfbLiveScores, getLiveGames } from '../api/espn';
import { cfbSlateNumberToWeek, cfbWeekToSlateNumber, getCfbWeekName, computeHomeCovers, computeOverWins } from '../utils/gameHelpers';
import type { CfbSlateDto, CfbSpreadDto, CfbPickDto } from '../types/league';
import type { EspnScores } from '../types/espn';
import { getHomeTeamScore, getAwayTeamScore } from '../utils/gameHelpers';
import type { SportAdapter, GameView, GameStatusValue, PickView, WeekState } from './sportAdapter';

/** Map CFB backend status strings to canonical GameStatusValue */

const CFB_SEASON = 2025;
const CFB_REGULAR_WEEKS = Array.from({ length: 14 }, (_, i) => i + 1);
const CFB_POST_WEEKS = [1, 2, 3, 4, 5];

function findActiveSlate(slates: CfbSlateDto[]): CfbSlateDto | null {
  const now = new Date();
  return slates.find(s => new Date(s.endDate) >= now) ?? slates[slates.length - 1] ?? null;
}

function slateToWeekState(slate: CfbSlateDto): WeekState {
  const { week, isPostSeason } = cfbSlateNumberToWeek(slate.slateNumber);
  return { season: slate.season, week, isPostSeason };
}

// Demo situation for in-progress CFB games (shows field position bar in scores page)
import type { GameSituation } from '../types/liveGame';

const CFB_DEMO_SITUATION: GameSituation = {
  downDistanceText: '3rd & 4 at MIA 22',
  possessionTeam: 'IU',
  isHomePossession: true,
  yardLine: 22,
  down: 3,
  distance: 4,
  isRedZone: true,
  period: 3,
  displayClock: '7:23',
};

/**
 * Merge our spread data (owned) with live ESPN competition data.
 * ESPN is the source of truth for score, status, and situation — same as NFL.
 */
function buildGamesFromEspn(spreads: CfbSpreadDto[], espnData: EspnScores | null, situationMap: Map<string, import('../types/liveGame').GameSituation | null>): GameView[] {
  // Index ESPN competitions by espnEventId for O(1) lookup
  const espnMap = new Map<number, import('../types/espn').Competition>();
  for (const event of espnData?.events ?? []) {
    for (const comp of event.competitions) {
      espnMap.set(parseInt(comp.id), comp);
    }
  }

  return spreads.map(sp => {
    const comp = espnMap.get(sp.espnEventId);
    const status: GameStatusValue = comp
      ? (() => {
          const t = comp.status?.type;
          if (t?.completed) return 'final';
          const name = t?.name as string | undefined;
          if (!name || name === 'STATUS_SCHEDULED') return 'scheduled';
          if (name === 'STATUS_HALFTIME') return 'halftime';
          if (name === 'STATUS_IN_PROGRESS' || name === 'STATUS_END_PERIOD') return 'in_progress';
          return 'scheduled';
        })()
      : 'scheduled';
    const isLive = status === 'in_progress' || status === 'halftime';
    const hs = comp ? getHomeTeamScore(comp) : null;
    const as_ = comp ? getAwayTeamScore(comp) : null;
    const key = `${sp.homeTeam}-${sp.awayTeam}`;
    return {
      id: sp.espnEventId.toString(),
      homeTeam: sp.homeTeam,
      awayTeam: sp.awayTeam,
      homeSpread: sp.homeTeamSpread,
      awaySpread: sp.awayTeamSpread,
      overUnder: sp.overUnder,
      homeScore: hs,
      awayScore: as_,
      gameStatus: status,
      gameTime: sp.gameTime,
      homeCovers: computeHomeCovers(status, sp.homeTeamSpread, hs, as_),
      overWins: computeOverWins(status, sp.overUnder, hs, as_),
      situation: situationMap.get(key) ?? (isLive ? CFB_DEMO_SITUATION : null),
    };
  });
}

function cfbPickToPickView(pick: CfbPickDto): PickView {
  return {
    gameId: pick.espnEventId.toString(),
    team: pick.team,
    pickType: pick.pickType as PickView['pickType'],
    userId: pick.userId,
    userName: '',
  };
}

async function fetchCfbEspnData(slate: CfbSlateDto): Promise<{ espn: EspnScores | null; situations: Map<string, import('../types/liveGame').GameSituation | null> }> {
  const startDate = slate.startDate;
  const endDate = slate.endDate;
  const [espn, liveGames] = await Promise.all([
    getCfbLiveScores(startDate, endDate),
    getLiveGames().catch(() => []),
  ]);
  // Build situation map from live games
  const situations = new Map<string, import('../types/liveGame').GameSituation | null>();
  for (const live of liveGames) {
    const sit = live.situation ? { ...live.situation, period: live.period, displayClock: live.displayClock } : null;
    situations.set(`${live.homeTeam}-${live.awayTeam}`, sit);
  }
  return { espn, situations };
}

async function loadSlate(leagueId: number, _userId: string, slateId: number, slate: CfbSlateDto): Promise<{ games: GameView[]; userPicks: PickView[] }> {
  const [spreads, picks, { espn, situations }] = await Promise.all([
    getCfbSpreads(slateId),
    getCfbUserPicks(leagueId, slateId),
    fetchCfbEspnData(slate),
  ]);
  return {
    games: buildGamesFromEspn(spreads, espn, situations),
    userPicks: picks.map(cfbPickToPickView),
  };
}

export function createCfbAdapter(): SportAdapter {
  // Cache slates so historical loads can resolve slateId from weekState
  let cachedSlates: CfbSlateDto[] = [];

  async function getSlates(): Promise<CfbSlateDto[]> {
    if (cachedSlates.length === 0) {
      cachedSlates = await getCfbSlates(CFB_SEASON);
    }
    return cachedSlates;
  }

  async function loadScoresForSlate(leagueId: number, userId: string, slate: CfbSlateDto): Promise<{ games: GameView[]; allPicks: PickView[]; userPicks: PickView[] }> {
    const [spreads, allPickDtos, { espn, situations }] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbAllPicks(leagueId, slate.id),
      fetchCfbEspnData(slate),
    ]);
    const games = buildGamesFromEspn(spreads, espn, situations);
    const allPicks = allPickDtos.map(cfbPickToPickView);
    const userPicks = allPicks.filter(p => p.userId === userId);
    return { games, allPicks, userPicks };
  }

  return {
    pollIntervalMs: 0,
    weekSelectorConfig: {
      regularWeekOptions: CFB_REGULAR_WEEKS,
      postSeasonWeekOptions: CFB_POST_WEEKS,
      maxRegularSeasonWeek: 14,
      minSeason: CFB_SEASON,
      weekLabelFn: getCfbWeekName,
    },

    async currentSeasonYear() {
      return Promise.resolve(CFB_SEASON);
    },

    async loadCurrentGames(leagueId, userId) {
      const slates = await getSlates();
      const active = findActiveSlate(slates);
      if (!active) {
        return { season: CFB_SEASON, week: 1, isPostSeason: false, games: [], userPicks: [], hasOdds: false, requiredPicks: 0, maxWeek: 1, maxSeason: CFB_SEASON };
      }
      const weekState = slateToWeekState(active);
      const { games, userPicks } = await loadSlate(leagueId, userId, active.id, active);
      // maxWeek = max REGULAR season week with data (caps the regular season selector)
      const maxRegularSlate = slates
        .filter(s => s.slateType === 'RegularSeason')
        .reduce((max, s) => Math.max(max, s.slateNumber), 0);
      return { ...weekState, games, userPicks, hasOdds: games.length > 0, requiredPicks: games.length, maxWeek: maxRegularSlate || 14, maxSeason: CFB_SEASON };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, userPicks } = await loadSlate(leagueId, userId, slate.id, slate);
      if (games.length === 0) return null;
      return { season, week, isPostSeason, games, userPicks, hasOdds: true, requiredPicks: games.length, maxWeek: week, maxSeason: season };
    },

    async submitPicks(leagueId, { season, week, isPostSeason }, picks) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum);
      if (!slate) return;
      await addCfbPicks(leagueId, slate.id, season, picks.map(p => ({
        espnEventId: parseInt(p.gameId),
        team: p.team,
        pickType: p.pickType,
      })));
    },

    async clearPicks(leagueId, { week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum);
      if (!slate) return [];
      await deleteCfbPicks(leagueId, slate.id);
      const fresh = await getCfbUserPicks(leagueId, slate.id);
      return fresh.map(cfbPickToPickView);
    },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const slates = await getSlates();
      const active = findActiveSlate(slates);
      if (!active) {
        return { season: CFB_SEASON, week: 1, isPostSeason: false, games: [], allPicks: [], userPicks: [], hasOdds: false, hasActiveGames: false, requiredPicks: 0, maxWeek: 1, maxSeason: CFB_SEASON };
      }
      const weekState = slateToWeekState(active);
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, active);
      const hasActiveGames = games.some(g => g.gameStatus === 'in_progress' || g.gameStatus === 'halftime');
      return { ...weekState, games, allPicks, userPicks, hasOdds: games.length > 0, hasActiveGames, requiredPicks: games.length, maxWeek: weekState.week, maxSeason: CFB_SEASON };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, slate);
      if (games.length === 0) return null;
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds: true, hasActiveGames: false, requiredPicks: games.length, maxWeek: week, maxSeason: season };
    },
  };
}
