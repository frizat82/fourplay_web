import { loadScoresWithRetry, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, addPicks, getLeaguePicks, getNflCurrentWeek } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, Event } from '../types/espn';
import type { NflPickDto, SpreadResponse } from '../types/picks';
import {
  getHomeTeamAbbr, getAwayTeamAbbr,
  getHomeTeam, getAwayTeam,
  getHomeTeamScore, getAwayTeamScore,
  getTeamRecord, getTeamLogo,
  getWeekFromEspnWeek, getEspnRequiredPicks,
  isPostSeason as isPostSeasonHelper,
  isGameOver, isGameStarted, toGameStatus,
  computeHomeCovers, computeAwayCovers, computeOverWins,
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, PickType } from './sportAdapter';
import { revealPicksForStartedGames, memoizeOnce } from './sportAdapter';

function competitionToGameView(
  competition: Competition,
  event: Event,
  spreadCache: Record<string, SpreadResponse>,
  situationMap?: Map<string, import('../types/liveGame').GameSituation | null>
): GameView {
  const homeAbbr = getHomeTeamAbbr(competition);
  const awayAbbr = getAwayTeamAbbr(competition);
  const key = `${homeAbbr}-${awayAbbr}`;
  const homeScore = getHomeTeamScore(competition);
  const awayScore = getAwayTeamScore(competition);
  const homeSpreadVal = spreadCache[homeAbbr]?.spread ?? null;
  const awaySpreadVal = spreadCache[awayAbbr]?.spread ?? null;
  const overUnderVal = spreadCache[homeAbbr]?.over ?? null;
  const status = toGameStatus(competition);
  return {
    id: competition.id,
    homeTeam: homeAbbr,
    awayTeam: awayAbbr,
    homeSpread: homeSpreadVal,
    awaySpread: awaySpreadVal,
    overUnder: overUnderVal,
    homeScore,
    awayScore,
    gameStatus: status,
    gameTime: competition.date,
    homeCovers: computeHomeCovers(status, homeSpreadVal, homeScore, awayScore),
    awayCovers: computeAwayCovers(status, awaySpreadVal, homeScore, awayScore),
    overWins: computeOverWins(status, overUnderVal, homeScore, awayScore),
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
    spreadPostedAt: spreadCache[homeAbbr]?.dateCreated ?? spreadCache[awayAbbr]?.dateCreated ?? null,
  };
}

function nflPickToPickView(pick: NflPickDto, games: GameView[]): PickView | null {
  const game = games.find(g => g.homeTeam === pick.team || g.awayTeam === pick.team);
  if (!game) return null;
  return {
    gameId: game.id,
    team: pick.team,
    pickType: pick.pick as PickType,
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
): Promise<Record<string, SpreadResponse>> {
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

async function buildSituationMap(events: Event[]): Promise<Map<string, import('../types/liveGame').GameSituation | null>> {
  const map = new Map<string, import('../types/liveGame').GameSituation | null>();
  try {
    const liveGames = await getLiveGames();
    for (const event of events) {
      for (const comp of event.competitions) {
        const home = getHomeTeamAbbr(comp);
        const away = getAwayTeamAbbr(comp);
        const live = liveGames.find(g => g.homeTeam === home && g.awayTeam === away);
        // Merge period/clock from LiveGame into the situation so ScoresPage can display "Q3 8:42"
        const sit = live?.situation ?? null;
        map.set(`${home}-${away}`, sit || live?.period ? { ...(sit ?? { possessionTeam: null, isHomePossession: false, yardLine: 0, down: 0, distance: 0, isRedZone: false, downDistanceText: '' }), period: live?.period, displayClock: live?.displayClock } : null);
      }
    }
  } catch { /* live games unavailable */ }
  return map;
}

export function createNflAdapter(): SportAdapter {
  // The control table (NflSeasonWeekConfigs, via SeasonWindowResolver/NflCurrentWeekService) is
  // the SOLE source of truth for which week is "current" — mirrors cfbAdapter.ts's
  // getCurrentSlate(). ESPN's own implicit "current" scoreboard (getScores/loadScoresWithRetry
  // with no week param) must never be used to decide season/week: it has its own notion of
  // "current" — e.g. during any gap in play it returns the last-completed event — which can
  // disagree with the league's actual spread-release schedule. Once resolved here, the specific
  // week is always fetched by week (getWeekScores), same as historical navigation.
  //
  // NflCurrentWeekService NEVER legitimately resolves to "nothing" — it either returns a real
  // week or throws (e.g. no NflSeasonWeekConfig rows seeded at all, a genuine data-integrity
  // problem, not a normal state). memoizeOnce doesn't cache a throw, so getNflCurrentWeek()
  // failing here — for that reason, or a DB/network outage — propagates rather than being
  // swallowed into a fake empty week: a caught-and-hidden failure previously rendered as an
  // ordinary "no games this week" page, masking a real outage as normal off-season behavior.
  // Letting it throw surfaces it through useQuery's isError -> QueryErrorAlert, same as any
  // other failed fetch.
  const getCurrentWeek = memoizeOnce(getNflCurrentWeek);

  return {
    sport: 'nfl',
    pollIntervalMs: 300_000,
    // Relative path, not an absolute VITE_API_TARGET URL — every other API call in this app goes
    // through the same proxy (Vite locally, Vercel's /api/:path* rewrite in prod, see
    // Client.React/vercel.json) and relies on same-origin cookies. An absolute cross-origin URL
    // here would bypass that proxy and drop the SameSite=Lax auth cookie on non-HTTPS origins.
    sseUrl: '/api/espn/live-stream',
    weekSelectorConfig: {
      maxRegularSeasonWeek: 18,
      minSeason: 2020,
      // Skip week 4 (Pro Bowl) — Super Bowl is week 5 in ESPN's 2025 postseason
      postSeasonWeekOptions: [1, 2, 3, 5],
      weekLabelFn: (week, isPostSeason) => {
        if (!isPostSeason) return `Week ${week}`;
        switch (week) {
          case 1: return 'Wild Card';
          case 2: return 'Divisional Round';
          case 3: return 'Conference Championship';
          case 5: return 'Super Bowl';
          default: return `Postseason Week ${week}`;
        }
      },
    },

    async currentSeasonYear() {
      const current = await getCurrentWeek();
      return current.season;
    },

    // ─── Picks ──────────────────────────────────────────────────────────────

    async loadCurrentGames(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, espnWeek: weekNum, isPostSeason: postSeason } = current;
      const nflWeek = getWeekFromEspnWeek(weekNum, postSeason);
      const [data, picksResult, hasOdds] = await Promise.all([
        getWeekScores(weekNum, season, postSeason),
        getUserPicks(userId, leagueId, season, nflWeek),
        doOddsExist(leagueId, season, nflWeek),
      ]);
      const sc = await buildSpreadCache(data?.events ?? [], leagueId, season, nflWeek, hasOdds);
      const games: GameView[] = (data?.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week: weekNum, isPostSeason: postSeason, games, userPicks, hasOdds, requiredPicks: getEspnRequiredPicks(weekNum, postSeason), maxWeek: 18, maxSeason: season };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      // Use frozen JSON when requesting the current demo week for consistent in-progress state
      const frozenData = await loadScoresWithRetry();
      const frozenIsPostSeason = isPostSeasonHelper(frozenData);
      const isFrozenWeek = frozenData?.season?.year === season && frozenData?.week?.number === week && frozenIsPostSeason === isPostSeason;
      const data = isFrozenWeek ? frozenData : await getWeekScores(week, season, isPostSeason);
      if (!data?.events?.length) return null;
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const [picksResult, hasOdds] = await Promise.all([getUserPicks(userId, leagueId, season, nflWeek), doOddsExist(leagueId, season, nflWeek)]);
      const sc = await buildSpreadCache(data.events, leagueId, season, nflWeek, hasOdds);
      const games: GameView[] = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week, isPostSeason, games, userPicks, hasOdds, requiredPicks: getEspnRequiredPicks(week, isPostSeason), maxWeek: 18, maxSeason: season };
    },

    async submitPicks(leagueId, { season, week, isPostSeason }, picks) {
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      await addPicks(picks.map(p => ({ id: 0, leagueId, userId: '', userName: '', team: p.team, pick: p.pickType as PickType, nflWeek, season, dateCreated: new Date().toISOString() } as NflPickDto)));
    },

    async clearPicks() { return []; },

    async loadJerseys(season, week) { return (await getAllJerseys(season, week)) ?? {}; },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, espnWeek: weekNum, isPostSeason: postSeason } = current;
      const nflWeek = getWeekFromEspnWeek(weekNum, postSeason);
      const [data, hasOdds, allPicksDtos] = await Promise.all([
        getWeekScores(weekNum, season, postSeason),
        doOddsExist(leagueId, season, nflWeek),
        getLeaguePicks(leagueId, season, nflWeek),
      ]);
      const [sc, situationMap] = await Promise.all([
        buildSpreadCache(data?.events ?? [], leagueId, season, nflWeek, hasOdds),
        buildSituationMap(data?.events ?? []),
      ]);
      const games = (data?.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc, situationMap)));
      // Use typed helpers on raw competitions — not string comparison on already-mapped GameView
      const hasActiveGames = (data?.events ?? []).some(ev =>
        ev.competitions.some(c => isGameStarted(c) && !isGameOver(c))
      );
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week: weekNum, isPostSeason: postSeason, games, allPicks: revealPicksForStartedGames(allPicks, games, userId), userPicks, hasOdds, hasActiveGames, requiredPicks: getEspnRequiredPicks(weekNum, postSeason), maxWeek: 18, maxSeason: season };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const frozenData = await loadScoresWithRetry();
      const frozenIsPostSeason = isPostSeasonHelper(frozenData);
      const isFrozenWeek = frozenData?.season?.year === season && frozenData?.week?.number === week && frozenIsPostSeason === isPostSeason;
      const data = isFrozenWeek ? frozenData : await getWeekScores(week, season, isPostSeason);
      if (!data?.events?.length) return null;
      const nflWeek = getWeekFromEspnWeek(week, isPostSeason);
      const hasOdds = await doOddsExist(leagueId, season, nflWeek);
      const sc = await buildSpreadCache(data.events, leagueId, season, nflWeek, hasOdds);
      const games = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const allPicksDtos = await getLeaguePicks(leagueId, season, nflWeek);
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds, hasActiveGames: false, requiredPicks: getEspnRequiredPicks(week, isPostSeason), maxWeek: 18, maxSeason: season };
    },
  };
}
