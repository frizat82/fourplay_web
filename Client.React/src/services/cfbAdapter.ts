import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks, getCfbAllPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import { getCfbLiveScores, getLiveGames } from '../api/espn';
import { cfbSlateNumberToWeek, cfbWeekToSlateNumber, getCfbWeekName, computeHomeCovers, computeOverWins, getCfbRequiredPicks } from '../utils/gameHelpers';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
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
function toCfbGameStatusFromString(s: string | null | undefined): GameStatusValue {
  if (!s) return 'scheduled';
  if (s === 'StatusFinal') return 'final';
  if (s === 'StatusInProgress') return 'in_progress';
  if (s === 'StatusHalftime') return 'halftime';
  return 'scheduled';
}

/**
 * Build GameView from spread data + live ESPN data.
 * ESPN is the primary source. Falls back to dbScores when ESPN has no event
 * for a given espnEventId (e.g. off-season, demo mode, or before game is created).
 */
function buildGamesFromEspn(
  spreads: CfbSpreadDto[],
  espnData: EspnScores | null,
  dbScores: CfbScoreDto[],
  situationMap: Map<string, import('../types/liveGame').GameSituation | null>,
): GameView[] {
  const espnMap = new Map<number, import('../types/espn').Competition>();
  for (const event of espnData?.events ?? []) {
    for (const comp of event.competitions) {
      espnMap.set(parseInt(comp.id), comp);
    }
  }
  const dbMap = new Map(dbScores.map(s => [s.espnEventId, s]));

  return spreads.map(sp => {
    const comp = espnMap.get(sp.espnEventId);
    const db = dbMap.get(sp.espnEventId);

    let status: GameStatusValue;
    let hs: number | null;
    let as_: number | null;

    if (comp) {
      // ESPN has live data — use it
      const t = comp.status?.type;
      if (t?.completed) status = 'final';
      else {
        const name = t?.name as string | undefined;
        if (!name || name === 'STATUS_SCHEDULED') status = 'scheduled';
        else if (name === 'STATUS_HALFTIME') status = 'halftime';
        else if (name === 'STATUS_IN_PROGRESS' || name === 'STATUS_END_PERIOD') status = 'in_progress';
        else status = 'scheduled';
      }
      hs = getHomeTeamScore(comp);
      as_ = getAwayTeamScore(comp);
    } else if (db) {
      // ESPN has no data yet — fall back to DB (covers demo mode + seeded final scores)
      status = toCfbGameStatusFromString(db.gameStatus);
      hs = db.homeTeamScore ?? null;
      as_ = db.awayTeamScore ?? null;
    } else {
      status = 'scheduled';
      hs = null;
      as_ = null;
    }

    const isLive = status === 'in_progress' || status === 'halftime';
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
    userName: pick.userName ?? '',
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
  const [spreads, picks, dbScores, { espn, situations }] = await Promise.all([
    getCfbSpreads(slateId),
    getCfbUserPicks(leagueId, slateId),
    getCfbScores(slateId),
    fetchCfbEspnData(slate),
  ]);
  return {
    games: buildGamesFromEspn(spreads, espn, dbScores, situations),
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
    const [spreads, allPickDtos, dbScores, { espn, situations }] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbAllPicks(leagueId, slate.id),
      getCfbScores(slate.id),
      fetchCfbEspnData(slate),
    ]);
    const games = buildGamesFromEspn(spreads, espn, dbScores, situations);
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
      return { ...weekState, games, userPicks, hasOdds: games.length > 0, requiredPicks: getCfbRequiredPicks(active.slateNumber), maxWeek: maxRegularSlate || 14, maxSeason: CFB_SEASON };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, userPicks } = await loadSlate(leagueId, userId, slate.id, slate);
      if (games.length === 0) return null;
      return { season, week, isPostSeason, games, userPicks, hasOdds: true, requiredPicks: getCfbRequiredPicks(slateNum), maxWeek: week, maxSeason: season };
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
      return { ...weekState, games, allPicks, userPicks, hasOdds: games.length > 0, hasActiveGames, requiredPicks: getCfbRequiredPicks(active.slateNumber), maxWeek: weekState.week, maxSeason: CFB_SEASON };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, slate);
      if (games.length === 0) return null;
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds: true, hasActiveGames: false, requiredPicks: getCfbRequiredPicks(slateNum), maxWeek: week, maxSeason: season };
    },
  };
}
