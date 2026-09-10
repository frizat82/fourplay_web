import { loadScoresWithRetry, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, doOddsExist, spreadBatch, addPicks, getLeaguePicks, getNflCurrentWeek } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, Event } from '../types/espn';
import type { NflPickDto, SpreadResponse } from '../types/picks';
import {
  getHomeTeamAbbr, getAwayTeamAbbr,
  getHomeTeam, getAwayTeam,
  getHomeTeamScore, getAwayTeamScore,
  getTeamRecord,
  getWeekFromEspnWeek, getNflWeekName, getNflRequiredPicks,
  isPostSeason as isPostSeasonHelper,
  isGameOver, isGameStarted, toGameStatus,
  computeHomeCovers, computeAwayCovers, computeOverWins, computeUnderWins,
  mergeLiveSituation,
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, PickType } from './sportAdapter';
import { revealPicksForStartedGames, memoizeOnce } from './sportAdapter';

// Cap navigation at the real current week, not a hardcoded season length — once the season
// moves into the postseason the full regular season (18) is legitimately browsable/complete;
// mirrors cfbAdapter.ts's already-correct dynamic maxWeek.
function maxWeekFor(isPostSeason: boolean, nflWeek: number): number {
  return isPostSeason ? 18 : nflWeek;
}

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
  const overThresholdVal = spreadCache[homeAbbr]?.over ?? null;
  const underThresholdVal = spreadCache[homeAbbr]?.under ?? null;
  const status = toGameStatus(competition);
  return {
    id: competition.id,
    homeTeam: homeAbbr,
    awayTeam: awayAbbr,
    homeSpread: homeSpreadVal,
    awaySpread: awaySpreadVal,
    overThreshold: overThresholdVal,
    underThreshold: underThresholdVal,
    homeScore,
    awayScore,
    gameStatus: status,
    gameTime: competition.date,
    homeCovers: computeHomeCovers(status, homeSpreadVal, homeScore, awayScore),
    awayCovers: computeAwayCovers(status, awaySpreadVal, homeScore, awayScore),
    overWins: computeOverWins(status, overThresholdVal, homeScore, awayScore),
    underWins: computeUnderWins(status, underThresholdVal, homeScore, awayScore),
    weather: event.weather ? {
      displayValue: event.weather.displayValue,
      conditionId: event.weather.conditionId,
      temperatureF: event.weather.temperature,
    } : undefined,
    homeRecord: getTeamRecord(getHomeTeam(competition)),
    awayRecord: getTeamRecord(getAwayTeam(competition)),
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
        map.set(`${home}-${away}`, mergeLiveSituation(live));
      }
    }
  } catch { /* live games unavailable */ }
  return map;
}

// The frozen demo/replay fixture is real captured ESPN wire data (its own week.number is ESPN's
// raw numbering, not our internal WeekId) — this is the one place nflAdapter.ts still legitimately
// needs to interpret ESPN's own shape, since a frozen fixture genuinely IS an ESPN ingestion point.
function isFrozenWeekMatch(frozenData: Awaited<ReturnType<typeof loadScoresWithRetry>>, season: number, nflWeek: number, isPostSeason: boolean): boolean {
  if (!frozenData?.week || !frozenData.season) return false;
  const frozenIsPostSeason = isPostSeasonHelper(frozenData);
  const frozenNflWeek = getWeekFromEspnWeek(frozenData.week.number, frozenData.season.year, frozenIsPostSeason);
  return frozenData.season.year === season && frozenNflWeek === nflWeek && frozenIsPostSeason === isPostSeason;
}

export function createNflAdapter(): SportAdapter {
  // The control table (NflSeasonWeekConfigs, via SeasonWindowResolver/NflCurrentWeekService) is
  // the SOLE source of truth for which week is "current" — mirrors cfbAdapter.ts's
  // getCurrentSlate(). ESPN's own implicit "current" scoreboard (getScores/loadScoresWithRetry
  // with no week param) must never be used to decide season/week: it has its own notion of
  // "current" — e.g. during any gap in play it returns the last-completed event — which can
  // disagree with the league's actual spread-release schedule. Once resolved here, the specific
  // week is always fetched by (season, weekId) — our own control table, never ESPN's own week
  // numbering (frizat-3nv) — same as historical navigation.
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
      // Our own internal WeekId (19-22) — contiguous, no ESPN Pro-Bowl-skip gap to work around
      // here at all (frizat-3nv/frizat-4k9: that quirk is normalized once, at the ESPN-ingestion
      // boundary, and never leaks past it).
      postSeasonWeekOptions: [19, 20, 21, 22],
      weekLabelFn: getNflWeekName,
    },

    async currentSeasonYear() {
      const current = await getCurrentWeek();
      return current.season;
    },

    // ─── Picks ──────────────────────────────────────────────────────────────

    async loadCurrentGames(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, weekId: nflWeek, isPostSeason: postSeason } = current;
      const [data, picksResult, hasOdds] = await Promise.all([
        getWeekScores(season, nflWeek),
        getUserPicks(userId, leagueId, season, nflWeek),
        doOddsExist(leagueId, season, nflWeek),
      ]);
      const sc = await buildSpreadCache(data?.events ?? [], leagueId, season, nflWeek, hasOdds);
      const games: GameView[] = (data?.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week: nflWeek, isPostSeason: postSeason, games, userPicks, hasOdds, requiredPicks: getNflRequiredPicks(nflWeek), maxWeek: maxWeekFor(postSeason, nflWeek), maxSeason: season };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      // Use frozen JSON when requesting the current demo week for consistent in-progress state
      const frozenData = await loadScoresWithRetry();
      const isFrozenWeek = isFrozenWeekMatch(frozenData, season, week, isPostSeason);
      const data = isFrozenWeek ? frozenData : await getWeekScores(season, week);
      if (!data?.events?.length) return null;
      const [picksResult, hasOdds] = await Promise.all([getUserPicks(userId, leagueId, season, week), doOddsExist(leagueId, season, week)]);
      const sc = await buildSpreadCache(data.events, leagueId, season, week, hasOdds);
      const games: GameView[] = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week, isPostSeason, games, userPicks, hasOdds, requiredPicks: getNflRequiredPicks(week), maxWeek: maxWeekFor(isPostSeason, week), maxSeason: season };
    },

    async submitPicks(leagueId, { season, week }, picks) {
      await addPicks(picks.map(p => ({ id: 0, leagueId, userId: '', userName: '', team: p.team, pick: p.pickType as PickType, nflWeek: week, season, dateCreated: new Date().toISOString() } as NflPickDto)));
    },

    async clearPicks() { return []; },

    async loadJerseys(season, week) { return (await getAllJerseys(season, week)) ?? {}; },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, weekId: nflWeek, isPostSeason: postSeason } = current;
      const [data, hasOdds, allPicksDtos] = await Promise.all([
        getWeekScores(season, nflWeek),
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
      return { season, week: nflWeek, isPostSeason: postSeason, games, allPicks: revealPicksForStartedGames(allPicks, games, userId), userPicks, hasOdds, hasActiveGames, requiredPicks: getNflRequiredPicks(nflWeek), maxWeek: maxWeekFor(postSeason, nflWeek), maxSeason: season };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      const frozenData = await loadScoresWithRetry();
      const isFrozenWeek = isFrozenWeekMatch(frozenData, season, week, isPostSeason);
      const data = isFrozenWeek ? frozenData : await getWeekScores(season, week);
      if (!data?.events?.length) return null;
      const hasOdds = await doOddsExist(leagueId, season, week);
      const sc = await buildSpreadCache(data.events, leagueId, season, week, hasOdds);
      const games = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const allPicksDtos = await getLeaguePicks(leagueId, season, week);
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds, hasActiveGames: false, requiredPicks: getNflRequiredPicks(week), maxWeek: maxWeekFor(isPostSeason, week), maxSeason: season };
    },
  };
}
