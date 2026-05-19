import { getCfbSlates, getCfbSpreads, getCfbScores, getCfbUserPicks, getCfbAllPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import { cfbSlateNumberToWeek, cfbWeekToSlateNumber, getCfbWeekName } from '../utils/gameHelpers';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import type { SportAdapter, GameView, GameStatusValue, PickView, WeekState, LoadedWeek, LoadedScores } from './sportAdapter';

/** Map CFB backend status strings to canonical GameStatusValue */
function toCfbGameStatus(gameStatus: string | null | undefined): GameStatusValue {
  if (gameStatus === 'StatusFinal') return 'final';
  if (gameStatus === 'StatusInProgress') return 'in_progress';
  if (gameStatus === 'StatusScheduled' || !gameStatus) return 'scheduled';
  return null;
}

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

function buildGames(spreads: CfbSpreadDto[], scores: CfbScoreDto[]): GameView[] {
  const scoreMap = new Map(scores.map(s => [s.espnEventId, s]));
  return spreads.map(sp => {
    const score = scoreMap.get(sp.espnEventId);
    const status = toCfbGameStatus(score?.gameStatus);
    const isFinal = status === 'final';
    const hs = score?.homeTeamScore ?? null;
    const as_ = score?.awayTeamScore ?? null;
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
      homeCovers: isFinal && hs != null && as_ != null ? (hs + sp.homeTeamSpread) > as_ : null,
      overWins: isFinal && hs != null && as_ != null ? (hs + as_) > sp.overUnder : null,
      weather: score?.weatherDisplayValue ? {
        displayValue: score.weatherDisplayValue,
        conditionId: score.weatherConditionId ?? undefined,
        temperatureF: score.weatherTemperatureF ?? undefined,
      } : undefined,
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

async function loadSlate(leagueId: number, userId: string, slateId: number): Promise<{ games: GameView[]; userPicks: PickView[] }> {
  const [spreads, scores, picks] = await Promise.all([
    getCfbSpreads(slateId),
    getCfbScores(slateId),
    getCfbUserPicks(leagueId, slateId),
  ]);
  return {
    games: buildGames(spreads, scores),
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
    const [spreads, scores, allPickDtos] = await Promise.all([
      getCfbSpreads(slate.id),
      getCfbScores(slate.id),
      getCfbAllPicks(leagueId, slate.id),
    ]);
    const games = buildGames(spreads, scores);
    const allPicks = allPickDtos.map(cfbPickToPickView);
    const userPicks = allPicks.filter(p => p.userId === userId);
    return { games, allPicks, userPicks };
  }

  return {
    pollIntervalMs: 0,
    supportsJerseys: false,
    supportsMatrix: false,
    supportsPickDialog: false,
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
        return { season: CFB_SEASON, week: 1, isPostSeason: false, games: [], userPicks: [], hasOdds: false, requiredPicks: 0 };
      }
      const weekState = slateToWeekState(active);
      const { games, userPicks } = await loadSlate(leagueId, userId, active.id);
      // maxWeek = latest slate with spreads seeded, for capping the WeekYearSelector
      const lastSlateWithData = [...slates].reverse().find(s => s.slateNumber <= 19);
      const { week: maxW, isPostSeason: maxPs } = cfbSlateNumberToWeek(lastSlateWithData?.slateNumber ?? active.slateNumber);
      return { ...weekState, games, userPicks, hasOdds: games.length > 0, requiredPicks: games.length, maxWeek: maxW, maxSeason: CFB_SEASON };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, userPicks } = await loadSlate(leagueId, userId, slate.id);
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
        return { season: CFB_SEASON, week: 1, isPostSeason: false, games: [], allPicks: [], userPicks: [], hasOdds: false, hasActiveGames: false };
      }
      const weekState = slateToWeekState(active);
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, active);
      const hasActiveGames = games.some(g => g.gameStatus === 'in_progress' || g.gameStatus === 'halftime');
      return { ...weekState, games, allPicks, userPicks, hasOdds: games.length > 0, hasActiveGames, maxWeek: weekState.week, maxSeason: CFB_SEASON };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const slates = await getSlates();
      const slateNum = cfbWeekToSlateNumber(week, isPostSeason);
      const slate = slates.find(s => s.slateNumber === slateNum && s.season === season);
      if (!slate) return null;
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, slate);
      if (games.length === 0) return null;
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds: true, hasActiveGames: false, maxWeek: week, maxSeason: season };
    },
  };
}
