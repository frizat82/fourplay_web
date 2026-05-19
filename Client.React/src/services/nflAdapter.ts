import { loadScoresWithRetry, getWeekScores } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, addPicks } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, Event } from '../types/espn';
import type { NflPickDto, PickType } from '../types/picks';
import {
  getHomeTeamAbbr, getAwayTeamAbbr,
  getHomeTeam, getAwayTeam,
  getHomeTeamScore, getAwayTeamScore,
  getTeamRecord,
  getWeekFromEspnWeek, getEspnRequiredPicks,
  isPostSeason as isPostSeasonHelper,
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, WeekState, LoadedWeek } from './sportAdapter';

function competitionToGameView(competition: Competition, event: Event, spreadCache: Record<string, { spread: string | null; over: string | null; under: string | null }>): GameView {
  const homeAbbr = getHomeTeamAbbr(competition);
  const awayAbbr = getAwayTeamAbbr(competition);
  const parseSpread = (s: string | null | undefined): number | null => {
    if (!s) return null;
    const n = parseFloat(s);
    return isFinite(n) ? n : null;
  };
  return {
    id: competition.id,
    homeTeam: homeAbbr,
    awayTeam: awayAbbr,
    homeSpread: parseSpread(spreadCache[homeAbbr]?.spread),
    awaySpread: parseSpread(spreadCache[awayAbbr]?.spread),
    overUnder: parseSpread(spreadCache[homeAbbr]?.over) ?? parseSpread(spreadCache[homeAbbr]?.under),
    homeScore: getHomeTeamScore(competition),
    awayScore: getAwayTeamScore(competition),
    gameStatus: competition.status?.type?.name ?? null,
    gameTime: competition.date,
    weather: event.weather ? {
      displayValue: event.weather.displayValue,
      conditionId: event.weather.conditionId,
      temperatureF: event.weather.temperature,
    } : undefined,
    homeRecord: getTeamRecord(getHomeTeam(competition)),
    awayRecord: getTeamRecord(getAwayTeam(competition)),
  };
}

function nflPickToPickView(pick: NflPickDto, games: GameView[]): PickView | null {
  // Find the game that matches this pick by team abbreviation
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

export function createNflAdapter(): SportAdapter {
  return {
    pollIntervalMs: 30_000,
    supportsJerseys: true,
    weekSelectorConfig: {
      maxRegularSeasonWeek: 18,
      minSeason: 2020,
    },

    async currentSeasonYear() {
      const data = await loadScoresWithRetry();
      return data?.season?.year ?? new Date().getFullYear();
    },

    async loadCurrentGames(leagueId, userId) {
      const data = await loadScoresWithRetry();
      if (!data?.season || !data.week) {
        return { season: new Date().getFullYear(), week: 1, isPostSeason: false, games: [], userPicks: [], hasOdds: false, requiredPicks: 4 };
      }

      const postSeason = isPostSeasonHelper(data);
      const weekNum = data.week.number;
      const season = data.season.year;
      const nflWeek = getWeekFromEspnWeek(weekNum, postSeason);

      const [picksResult, hasOdds] = await Promise.all([
        getUserPicks(userId, leagueId, season, nflWeek),
        doOddsExist(leagueId, season, nflWeek),
      ]);

      let spreadCache: Record<string, { spread: string | null; over: string | null; under: string | null }> = {};
      if (hasOdds) {
        const teams: string[] = [];
        for (const event of data.events ?? []) {
          for (const comp of event.competitions) {
            teams.push(getHomeTeamAbbr(comp), getAwayTeamAbbr(comp));
          }
        }
        const resp = await spreadBatch(leagueId, season, nflWeek, { requests: teams.map(t => ({ team: t })) });
        spreadCache = resp.responses ?? {};
      }

      const games: GameView[] = [];
      for (const event of data.events ?? []) {
        for (const comp of event.competitions) {
          games.push(competitionToGameView(comp, event, spreadCache));
        }
      }

      const userPicks: PickView[] = picksResult
        .map(p => nflPickToPickView(p, games))
        .filter((p): p is PickView => p !== null);

      return {
        season,
        week: weekNum,
        isPostSeason: postSeason,
        games,
        userPicks,
        hasOdds,
        requiredPicks: getEspnRequiredPicks(weekNum, postSeason),
      };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      const data = await getWeekScores(week, season, isPostSeason);
      if (!data?.events?.length) return null;

      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const [picksResult, hasOdds] = await Promise.all([
        getUserPicks(userId, leagueId, season, nflWeek),
        doOddsExist(leagueId, season, nflWeek),
      ]);

      let spreadCache: Record<string, { spread: string | null; over: string | null; under: string | null }> = {};
      if (hasOdds) {
        const teams: string[] = [];
        for (const event of data.events) {
          for (const comp of event.competitions) {
            teams.push(getHomeTeamAbbr(comp), getAwayTeamAbbr(comp));
          }
        }
        const resp = await spreadBatch(leagueId, season, nflWeek, { requests: teams.map(t => ({ team: t })) });
        spreadCache = resp.responses ?? {};
      }

      const games: GameView[] = [];
      for (const event of data.events) {
        for (const comp of event.competitions) {
          games.push(competitionToGameView(comp, event, spreadCache));
        }
      }

      const userPicks: PickView[] = picksResult
        .map(p => nflPickToPickView(p, games))
        .filter((p): p is PickView => p !== null);

      return { season, week, isPostSeason, games, userPicks, hasOdds, requiredPicks: getEspnRequiredPicks(week, isPostSeason) };
    },

    async submitPicks(leagueId, { season, week, isPostSeason }, picks) {
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const dtos: NflPickDto[] = picks.map(p => ({
        id: 0,
        leagueId,
        userId: '',
        userName: '',
        team: p.team,
        pick: p.pickType as PickType,
        nflWeek,
        season,
        dateCreated: new Date().toISOString(),
      }));
      await addPicks(dtos);
    },

    async clearPicks(_leagueId, _state) {
      // NFL: picks are server-side and locked — clearing is local only
      // Return empty to signal all picks cleared locally
      return [];
    },

    async loadJerseys(season, week) {
      return (await getAllJerseys(season, week)) ?? {};
    },
  };
}
