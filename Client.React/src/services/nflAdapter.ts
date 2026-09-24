import { getScores, getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, spreadBatch, addPicks, removeMyPick, getLeaguePicks, getLeaguePickCounts, getNflCurrentWeek } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, EspnScores, Event } from '../types/espn';
import type { LiveGame } from '../types/liveGame';
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
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, PickType } from './sportAdapter';
import { revealPicksForStartedGames, memoizeOnce, orFallback, pickCountsToMap } from './sportAdapter';

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
  liveGameMap?: Map<string, LiveGame>
): GameView {
  const homeAbbr = getHomeTeamAbbr(competition);
  const awayAbbr = getAwayTeamAbbr(competition);
  const key = `${homeAbbr}-${awayAbbr}`;
  const live = liveGameMap?.get(key);
  const homeScore = getHomeTeamScore(competition);
  const awayScore = getAwayTeamScore(competition);
  const homeSpreadVal = spreadCache[homeAbbr]?.spread ?? null;
  const awaySpreadVal = spreadCache[awayAbbr]?.spread ?? null;
  const overThresholdVal = spreadCache[homeAbbr]?.over ?? null;
  const underThresholdVal = spreadCache[homeAbbr]?.under ?? null;
  const status = toGameStatus(competition);
  return {
    // frizat-z3a: never ESPN's own competition.id — a team plays at most one game per week, so
    // the team-abbreviation pair is already a complete, stable key, matching CfbAdapter's
    // GameView.id (homeTeam) and the backend's own natural-key migration off EspnEventId. ESPN's
    // numeric id is an implementation detail of one specific data source, not our identity.
    id: `${homeAbbr}vs${awayAbbr}`,
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
    // situation and period/displayClock are two independently-nullable concerns (frizat-66c) —
    // situation is ball/down-distance detail for FieldPosition, genuinely null whenever ESPN's
    // live feed omits it (between snaps, right after a play); period/displayClock are the
    // status-line clock, sourced from LiveGame's own top-level fields and populated separately
    // even when situation itself is null (e.g. halftime, where there's no active down at all).
    situation: live?.situation ?? null,
    period: live?.period,
    displayClock: live?.displayClock,
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

// The week's odds in one request: an empty team list = every team with odds, so it goes out
// alongside getWeekScores instead of waiting on it for the team list. It also answers "have odds
// posted?" — the endpoint 404s until they have — so no separate /odds/exists round trip is needed.
// Any other failure is real and propagates (surfaced by the query's error state).
async function fetchWeekOdds(leagueId: number, season: number, nflWeek: number): Promise<WeekOdds> {
  try {
    const resp = await spreadBatch(leagueId, season, nflWeek, { requests: [] });
    return { hasOdds: true, spreads: resp.responses ?? {} };
  } catch (err) {
    if (isNotFound(err)) return { hasOdds: false, spreads: {} };
    throw err;
  }
}

interface WeekOdds { hasOdds: boolean; spreads: Record<string, SpreadResponse> }

function isNotFound(err: unknown): boolean {
  return (err as { response?: { status?: number } } | null)?.response?.status === 404;
}

async function buildSpreadCache(
  events: Event[],
  leagueId: number,
  season: number,
  nflWeek: number,
  { hasOdds, spreads: prefetched }: WeekOdds,
): Promise<Record<string, SpreadResponse>> {
  if (!hasOdds) return {};
  if (Object.keys(prefetched).length > 0) return prefetched;
  // Odds exist but the all-teams request came back empty — a backend that predates it (deploy
  // skew) — so ask for the scoreboard's teams explicitly, the original way.
  const teams: string[] = [];
  for (const event of events) {
    for (const comp of event.competitions) {
      teams.push(getHomeTeamAbbr(comp), getAwayTeamAbbr(comp));
    }
  }
  const resp = await spreadBatch(leagueId, season, nflWeek, { requests: teams.map(t => ({ team: t })) });
  return resp.responses ?? {};
}

// frizat-66c: returns the raw LiveGame per matchup (not a pre-merged/derived situation) so
// competitionToGameView can read situation and period/displayClock as the two independently-
// nullable concerns they actually are — never fabricating one from the presence of the other.
// liveGames is fetched by the caller alongside the scoreboard (it doesn't depend on it), with a
// failure treated as "none live" (orFallback) — same as cfbAdapter's live games.
function buildLiveGameMap(events: Event[], liveGames: LiveGame[]): Map<string, LiveGame> {
  const map = new Map<string, LiveGame>();
  for (const event of events) {
    for (const comp of event.competitions) {
      const home = getHomeTeamAbbr(comp);
      const away = getAwayTeamAbbr(comp);
      const live = liveGames.find(g => g.homeTeam === home && g.awayTeam === away);
      if (live) map.set(`${home}-${away}`, live);
    }
  }
  return map;
}

// The frozen demo/replay fixture is real captured ESPN wire data (its own week.number is ESPN's
// raw numbering, not our internal WeekId) — this is the one place nflAdapter.ts still legitimately
// needs to interpret ESPN's own shape, since a frozen fixture genuinely IS an ESPN ingestion point.
function isFrozenWeekMatch(frozenData: EspnScores | null, season: number, nflWeek: number, isPostSeason: boolean): boolean {
  if (!frozenData?.week || !frozenData.season) return false;
  const frozenIsPostSeason = isPostSeasonHelper(frozenData);
  const frozenNflWeek = getWeekFromEspnWeek(frozenData.week.number, frozenData.season.year, frozenIsPostSeason);
  return frozenData.season.year === season && frozenNflWeek === nflWeek && frozenIsPostSeason === isPostSeason;
}

// A past week's scoreboard. Demo mode serves a frozen in-progress fixture as its "current"
// scoreboard, used instead when it's the week being asked for — so both are fetched together, and
// the current one exactly once: it used to go first, with up to 5 retries 500ms apart whenever it
// had no events (every off-season view of a past week waited 2.5s+ before starting its own fetch).
async function loadPastWeekScores(season: number, nflWeek: number, isPostSeason: boolean): Promise<EspnScores | null> {
  const [frozenData, weekData] = await Promise.all([orFallback(getScores, null), getWeekScores(season, nflWeek)]);
  return isFrozenWeekMatch(frozenData, season, nflWeek, isPostSeason) ? frozenData : weekData;
}

export function createNflAdapter(): SportAdapter {
  // The control table (NflSeasonWeekConfigs, via SeasonWindowResolver/NflCurrentWeekService) is
  // the SOLE source of truth for which week is "current" — mirrors cfbAdapter.ts's
  // getCurrentSlate(). ESPN's own implicit "current" scoreboard (getScores
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

    prefetchCurrentWeek() {
      getCurrentWeek().catch(() => {});
    },

    // ─── Picks ──────────────────────────────────────────────────────────────

    async loadCurrentGames(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, weekId: nflWeek, isPostSeason: postSeason } = current;
      const [data, picksResult, odds] = await Promise.all([
        getWeekScores(season, nflWeek),
        getUserPicks(userId, leagueId, season, nflWeek),
        fetchWeekOdds(leagueId, season, nflWeek),
      ]);
      const { hasOdds } = odds;
      const sc = await buildSpreadCache(data?.events ?? [], leagueId, season, nflWeek, odds);
      const games: GameView[] = (data?.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week: nflWeek, isPostSeason: postSeason, games, userPicks, hasOdds, requiredPicks: getNflRequiredPicks(nflWeek), maxWeek: maxWeekFor(postSeason, nflWeek), maxSeason: season };
    },

    async loadHistoricalGames(leagueId, userId, { season, week, isPostSeason }) {
      // Picks and odds go out alongside the scoreboard, but an empty week still resolves to null
      // regardless of how those extra requests fare (awaited only once there's a week to show).
      const picksPromise = getUserPicks(userId, leagueId, season, week);
      const oddsPromise = fetchWeekOdds(leagueId, season, week);
      const data = await loadPastWeekScores(season, week, isPostSeason);
      if (!data?.events?.length) { void Promise.allSettled([picksPromise, oddsPromise]); return null; }
      const [picksResult, odds] = await Promise.all([picksPromise, oddsPromise]);
      const { hasOdds } = odds;
      const sc = await buildSpreadCache(data.events, leagueId, season, week, odds);
      const games: GameView[] = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const userPicks = picksResult.map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      return { season, week, isPostSeason, games, userPicks, hasOdds, requiredPicks: getNflRequiredPicks(week), maxWeek: maxWeekFor(isPostSeason, week), maxSeason: season };
    },

    async submitPicks(leagueId, { season, week }, picks) {
      await addPicks(picks.map(p => ({ id: 0, leagueId, userId: '', userName: '', team: p.team, pick: p.pickType as PickType, nflWeek: week, season, dateCreated: new Date().toISOString() } as NflPickDto)));
    },

    async removePick(leagueId, { season, week }, pick) {
      await removeMyPick({ id: 0, leagueId, userId: '', userName: '', team: pick.team, pick: pick.pickType as PickType, nflWeek: week, season, dateCreated: new Date().toISOString() } as NflPickDto);
    },

    async clearPicks() { return []; },

    async loadJerseys(season, week) { return (await getAllJerseys(season, week)) ?? {}; },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, weekId: nflWeek, isPostSeason: postSeason } = current;
      const [data, allPicksDtos, odds, liveGames] = await Promise.all([
        getWeekScores(season, nflWeek),
        getLeaguePicks(leagueId, season, nflWeek),
        fetchWeekOdds(leagueId, season, nflWeek),
        orFallback(getLiveGames, []),
      ]);
      const { hasOdds } = odds;
      const sc = await buildSpreadCache(data?.events ?? [], leagueId, season, nflWeek, odds);
      const liveGameMap = buildLiveGameMap(data?.events ?? [], liveGames);
      const games = (data?.events ?? []).flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc, liveGameMap)));
      // Use typed helpers on raw competitions — not string comparison on already-mapped GameView
      const hasActiveGames = (data?.events ?? []).some(ev =>
        ev.competitions.some(c => isGameStarted(c) && !isGameOver(c))
      );
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week: nflWeek, isPostSeason: postSeason, games, allPicks: revealPicksForStartedGames(allPicks, games, userId), userPicks, hasOdds, hasActiveGames, requiredPicks: getNflRequiredPicks(nflWeek), maxWeek: maxWeekFor(postSeason, nflWeek), maxSeason: season };
    },

    async loadHistoricalScores(leagueId, userId, { season, week, isPostSeason }) {
      // Same shape as loadHistoricalGames: parallel, but an empty week is still just null.
      const picksPromise = getLeaguePicks(leagueId, season, week);
      const oddsPromise = fetchWeekOdds(leagueId, season, week);
      const data = await loadPastWeekScores(season, week, isPostSeason);
      if (!data?.events?.length) { void Promise.allSettled([picksPromise, oddsPromise]); return null; }
      const [allPicksDtos, odds] = await Promise.all([picksPromise, oddsPromise]);
      const { hasOdds } = odds;
      const sc = await buildSpreadCache(data.events, leagueId, season, week, odds);
      const games = data.events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc)));
      const allPicks = (allPicksDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
      const userPicks = allPicks.filter(p => p.userId === userId);
      return { season, week, isPostSeason, games, allPicks, userPicks, hasOdds, hasActiveGames: false, requiredPicks: getNflRequiredPicks(week), maxWeek: maxWeekFor(isPostSeason, week), maxSeason: season };
    },

    // ─── Commissioner missing-picks (frizat-8ni) ───────────────────────────────

    async getMissingPicks(leagueId) {
      const current = await getCurrentWeek();
      // frizat-xbq: submitted counts, not getLeaguePicks (which hides unstarted games' picks).
      const counts = await getLeaguePickCounts(leagueId, current.season, current.weekId);
      return {
        picksByUser: pickCountsToMap(counts),
        requiredPicks: getNflRequiredPicks(current.weekId),
        weekLabel: current.weekLabel,
      };
    },
  };
}
