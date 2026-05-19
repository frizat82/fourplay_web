import { loadScoresWithRetry, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, addPicks, getLeaguePicks } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, Event } from '../types/espn';
import type { NflPickDto, PickType } from '../types/picks';
import {
  getHomeTeamAbbr, getAwayTeamAbbr,
  getHomeTeam, getAwayTeam,
  getHomeTeamScore, getAwayTeamScore,
  getTeamRecord, getTeamLogo,
  getWeekFromEspnWeek, getEspnRequiredPicks,
  isPostSeason as isPostSeasonHelper,
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, WeekState, LoadedWeek, LoadedScores } from './sportAdapter';

function competitionToGameView(
  competition: Competition,
  event: Event,
  spreadCache: Record<string, { spread: string | null; over: string | null; under: string | null }>,
  situationMap?: Map<string, string | null>
): GameView {
  const homeAbbr = getHomeTeamAbbr(competition);
  const awayAbbr = getAwayTeamAbbr(competition);
  const parseSpread = (s: string | null | undefined): number | null => {
    if (!s) return null;
    const n = parseFloat(s);
    return isFinite(n) ? n : null;
  };
  const key = `${homeAbbr}-${awayAbbr}`;
  const homeScore = getHomeTeamScore(competition);
  const awayScore = getAwayTeamScore(competition);
  const homeSpreadVal = parseSpread(spreadCache[homeAbbr]?.spread);
  const overUnderVal = parseSpread(spreadCache[homeAbbr]?.over) ?? parseSpread(spreadCache[homeAbbr]?.under);
  const isFinal = (competition.status?.type?.name ?? '').includes('final');
  return {
    id: competition.id,
    homeTeam: homeAbbr,
    awayTeam: awayAbbr,
    homeSpread: homeSpreadVal,
    awaySpread: parseSpread(spreadCache[awayAbbr]?.spread),
    overUnder: overUnderVal,
    homeScore,
    awayScore,
    gameStatus: competition.status?.type?.name ?? null,
    gameTime: competition.date,
    homeCovers: isFinal && homeSpreadVal != null ? (homeScore + homeSpreadVal) > awayScore : null,
    overWins: isFinal && overUnderVal != null ? (homeScore + awayScore) > overUnderVal : null,
    weather: event.weather ? {
      displayValue: event.weather.displayValue,
      conditionId: event.weather.conditionId,
      temperatureF: event.weather.temperature,
    } : undefined,
    homeRecord: getTeamRecord(getHomeTeam(competition)),
    awayRecord: getTeamRecord(getAwayTeam(competition)),
    homeLogo: getTeamLogo(homeAbbr),
    awayLogo: getTeamLogo(awayAbbr),
    situation: situationMap?.get(key) ?? null,
  };
}

function nflPickToPickView(pick: NflPickDto, games: GameView[]): PickView | null {
  const game = games.find(g => g.homeTeam === pick.team || g.awayTeam === pick.team);
  if (!game) return null;
  return {
    gameId: game.id,
    team: pick.team,
    pickType: pick.pick as PickView['pickType'],
    userId: pick.userId,
    userName: pick.userName,
  };
}

async function buildSpreadCache(
  events: Event[],
  leagueId: number,
  season: number,
  nflWeek: number,
  hasOdds: boolean
): Promise<Record<string, { spread: string | null; over: string | null; under: string | null }>> {
  if (!hasOdds) return {};
  const teams: string[] = [];
  for (const event of events) {
    for (const comp of event.competitions) {
      teams.push(getHomeTeamAbbr(comp), getAwayTeamAbbr(comp));
    }
  }
  const resp = await spreadBatch(leagueId, season, nflWeek, { requests: teams.map(t => ({ team: t })) });
  return resp.responses ?? {};
}

async function buildSituationMap(events: Event[]): Promise<Map<string, string | null>> {
  const map = new Map<string, string | null>();
  try {
    const liveGames = await getLiveGames();
    for (const event of events) {
      for (const comp of event.competitions) {
        const home = getHomeTeamAbbr(comp);
        const away = getAwayTeamAbbr(comp);
        const live = liveGames.find(g => g.homeTeam === home && g.awayTeam === away);
        map.set(`${home}-${away}`, live?.situation?.downDistanceText ?? null);
      }
    }
  } catch { /* live games unavailable */ }
  return map;
}

export function createNflAdapter(): SportAdapter {
  return {
    pollIntervalMs: 30_000,
    supportsJerseys: true,
    supportsMatrix: true,
    supportsPickDialog: true,
    weekSelectorConfig: { maxRegularSeasonWeek: 18, minSeason: 2020 },

    async currentSeasonYear() {
      const data = await loadScoresWithRetry();
      return data?.season?.year ?? new Date().getFullYear();
    },

    // ─── Picks ──────────────────────────────────────────────────────────────

    async loadCurrentGames(leagueId, userId) {
      const data = await loadScoresWithRetry();
      if (!data?.season || !data.week) {
        return { season: new Date().getFullYear(), week: 1, isPostSeason: false, games: [], userPicks: [], hasOdds: false, requiredPicks: 4 };
      }
      const postSeason = isPostSeasonHelper(data);
      const weekNum = data.week.number;
      const season = data.season.year;
      const nflWeek = getWeekFromEspnWeek(weekNum, postSeason);
      const [picksResult, hasOdds] = await Promise.all([getUserPicks(userId, leagueId, season, nflWeek), doOddsExist(leagueId, season, nflWeek)]);
      const sc = await buildSpreadCache(data.events ?? [], leagueId, season, nflWeek, hasOdds);
      const games: GameView[] = (data.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week: weekNum, isPostSeason: postSeason, games, userPicks, hasOdds, requiredPicks: getEspnRequiredPicks(weekNum, postSeason) };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      const data = await getWeekScores(week, season, isPostSeason);
      if (!data?.events?.length) return null;
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const [picksResult, hasOdds] = await Promise.all([getUserPicks(userId, leagueId, season, nflWeek), doOddsExist(leagueId, season, nflWeek)]);
      const sc = await buildSpreadCache(data.events, leagueId, season, nflWeek, hasOdds);
      const games: GameView[] = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week, isPostSeason, games, userPicks, hasOdds, requiredPicks: getEspnRequiredPicks(week, isPostSeason) };
    },

    async submitPicks(leagueId, { season, week, isPostSeason }, picks) {
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      await addPicks(picks.map(p => ({ id: 0, leagueId, userId: '', userName: '', team: p.team, pick: p.pickType as PickType, nflWeek, season, dateCreated: new Date().toISOString() } as NflPickDto)));
    },

    async clearPicks(_leagueId, _state) { return []; },

    async loadJerseys(season, week) { return (await getAllJerseys(season, week)) ?? {}; },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const data = await loadScoresWithRetry();
      if (!data?.season || !data.week) {
        return { season: new Date().getFullYear(), week: 1, isPostSeason: false, games: [], allPicks: [], userPicks: [], hasOdds: false, hasActiveGames: false };
      }
      const postSeason = isPostSeasonHelper(data);
      const weekNum = data.week.number;
      const season = data.season.year;
      const nflWeek = getWeekFromEspnWeek(weekNum, postSeason);
      const hasOdds = await doOddsExist(leagueId, season, nflWeek);
      const sc = await buildSpreadCache(data.events ?? [], leagueId, season, nflWeek, hasOdds);
      const situationMap = await buildSituationMap(data.events ?? []);
      const games = (data.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc, situationMap)));
      const hasActiveGames = games.some(g => g.gameStatus === 'status_in_progress' || g.gameStatus === 'status_halftime');
      const allPicksDtos = await getLeaguePicks(leagueId, season, nflWeek);
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week: weekNum, isPostSeason: postSeason, games, allPicks, userPicks, hasOdds, hasActiveGames };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const data = await getWeekScores(week, season, isPostSeason);
      if (!data?.events?.length) return null;
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const hasOdds = await doOddsExist(leagueId, season, nflWeek);
      const sc = await buildSpreadCache(data.events, leagueId, season, nflWeek, hasOdds);
      const games = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const allPicksDtos = await getLeaguePicks(leagueId, season, nflWeek);
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds, hasActiveGames: false };
    },
  };
}
