import { getWeekScores, getLiveGames } from '../api/espn';
import { getUserPicks, spreadBatch, addPicks, removeMyPick, getLeaguePicks, getLeaguePickCounts, getNflCurrentWeek, getNflSeasonWeeks } from '../api/league';
import { getAllJerseys } from '../api/jersey';
import type { Competition, Event } from '../types/espn';
import type { LiveGame } from '../types/liveGame';
import type { NflPickDto, SpreadResponse } from '../types/picks';
import type { NflSeasonWeekDto } from '../types/league';
import {
  getHomeTeamAbbr, getAwayTeamAbbr,
  getHomeTeam, getAwayTeam,
  getHomeTeamScore, getAwayTeamScore,
  getTeamRecord,
  isGameOver, isGameStarted, toGameStatus,
  computeHomeCovers, computeAwayCovers, computeOverWins, computeUnderWins,
} from '../utils/gameHelpers';
import type { SportAdapter, GameView, PickView, PickType } from './sportAdapter';
import { revealPicksForStartedGames, memoizeOnce, memoizeByKey, orFallback, pickCountsToMap } from './sportAdapter';
import { isNotFound } from '../utils/apiError';

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

// A past week's scoreboard with the requests that ride alongside it (picks, odds, the season's
// control-table rows): all go out together, but an empty week still resolves to null however
// those extras fare. The week's pick count and name are its row's (frizat-d47) — only looked up
// once the week has games, since a week the server served games for always has a row: a missing
// one is a real data problem, so it fails loudly rather than guessing.
// Only that week's own scores (by our WeekId) — never ESPN's live "current" scoreboard, whose own
// week numbering could disagree near a boundary. Demo and replay serve their frozen game through
// this same per-week endpoint for the current week (DemoEspnCacheService/ReplayCacheService).
async function loadPastWeek<P>(season: number, week: number, picks: Promise<P>,
  odds: Promise<WeekOdds>, seasonWeeks: Promise<NflSeasonWeekDto[]>) {
  const data = await getWeekScores(season, week);
  if (!data?.events?.length) { void Promise.allSettled([picks, odds, seasonWeeks]); return null; }
  const [pickDtos, weekOdds, rows] = await Promise.all([picks, odds, seasonWeeks]);
  const row = rows.find(w => w.weekId === week);
  if (!row) throw new Error(`No NFL control-table row for season ${season} week ${week}`);
  return { events: data.events, pickDtos, odds: weekOdds, row };
}

// One week's games (with teased spreads) and the given picks mapped onto them.
async function toGamesAndPicks(events: Event[], leagueId: number, season: number, week: number, odds: WeekOdds,
  pickDtos: NflPickDto[] | null | undefined, liveGameMap?: Map<string, LiveGame>) {
  const sc = await buildSpreadCache(events, leagueId, season, week, odds);
  const games: GameView[] = events.flatMap(ev => ev.competitions.map(c => competitionToGameView(c, ev, sc, liveGameMap)));
  const picks = (pickDtos ?? []).map(p => nflPickToPickView(p, games)).filter((p): p is PickView => p !== null);
  return { games, picks };
}

// The week's postseason flag, pick count and name are its control-table row's (frizat-d47) —
// never computed from the week number, nor echoed back from the request.
function weekMeta(season: number, week: number,
  { isPostSeason, requiredPicks, weekLabel }: { isPostSeason: boolean; requiredPicks: number; weekLabel: string },
  { hasOdds }: WeekOdds) {
  return { season, week, isPostSeason, hasOdds, requiredPicks, weekLabel };
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

  // A season's control-table rows (frizat-d47), fetched once per season per adapter.
  const getSeasonWeeks = memoizeByKey(getNflSeasonWeeks);

  return {
    sport: 'nfl',
    pollIntervalMs: 300_000,
    // Relative path, not an absolute VITE_API_TARGET URL — every other API call in this app goes
    // through the same proxy (Vite locally, Vercel's /api/:path* rewrite in prod, see
    // Client.React/vercel.json) and relies on same-origin cookies. An absolute cross-origin URL
    // here would bypass that proxy and drop the SameSite=Lax auth cookie on non-HTTPS origins.
    sseUrl: '/api/espn/live-stream',
    weekSelectorConfig: {
      // The first season with control-table rows (frizat-d47) — earlier seasons have no weeks.
      minSeason: 2025,
    },

    async loadSeasonWeeks(season) {
      return (await getSeasonWeeks(season)).map(w => ({ week: w.weekId, label: w.weekLabel, isPostSeason: w.isPostSeason }));
    },

    async currentSeasonYear() {
      const current = await getCurrentWeek();
      return current.season;
    },

    // Also warms the season's rows, so the week selector's list doesn't wait behind the whole
    // current-week load (its query resolves from this memo).
    prefetchCurrentWeek() {
      getCurrentWeek().then(c => getSeasonWeeks(c.season)).catch(() => {});
    },

    // ─── Picks ──────────────────────────────────────────────────────────────

    async loadCurrentGames(leagueId, userId) {
      const current = await getCurrentWeek();
      const { season, weekId: week } = current;
      const [data, pickDtos, odds] = await Promise.all([
        getWeekScores(season, week),
        getUserPicks(userId, leagueId, season, week),
        fetchWeekOdds(leagueId, season, week),
      ]);
      const { games, picks } = await toGamesAndPicks(data?.events ?? [], leagueId, season, week, odds, pickDtos);
      return { ...weekMeta(season, week, current, odds), games, userPicks: picks };
    },

    async loadHistoricalGames(leagueId, userId, { season, week }) {
      const past = await loadPastWeek(season, week,
        getUserPicks(userId, leagueId, season, week), fetchWeekOdds(leagueId, season, week), getSeasonWeeks(season));
      if (!past) return null;
      const { games, picks } = await toGamesAndPicks(past.events, leagueId, season, week, past.odds, past.pickDtos);
      return { ...weekMeta(season, week, past.row, past.odds), games, userPicks: picks };
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
      const { season, weekId: week } = current;
      const [data, pickDtos, odds, liveGames] = await Promise.all([
        getWeekScores(season, week),
        getLeaguePicks(leagueId, season, week),
        fetchWeekOdds(leagueId, season, week),
        orFallback(getLiveGames, []),
      ]);
      const events = data?.events ?? [];
      const { games, picks } = await toGamesAndPicks(events, leagueId, season, week, odds, pickDtos, buildLiveGameMap(events, liveGames));
      // Use typed helpers on raw competitions — not string comparison on already-mapped GameView
      const hasActiveGames = events.some(ev => ev.competitions.some(c => isGameStarted(c) && !isGameOver(c)));
      return {
        ...weekMeta(season, week, current, odds), games, hasActiveGames,
        allPicks: revealPicksForStartedGames(picks, games, userId), userPicks: picks.filter(p => p.userId === userId),
      };
    },

    async loadHistoricalScores(leagueId, userId, { season, week }) {
      const past = await loadPastWeek(season, week,
        getLeaguePicks(leagueId, season, week), fetchWeekOdds(leagueId, season, week), getSeasonWeeks(season));
      if (!past) return null;
      const { games, picks } = await toGamesAndPicks(past.events, leagueId, season, week, past.odds, past.pickDtos);
      return { ...weekMeta(season, week, past.row, past.odds), games, hasActiveGames: false, allPicks: picks, userPicks: picks.filter(p => p.userId === userId) };
    },

    // ─── Commissioner missing-picks (frizat-8ni) ───────────────────────────────

    async getMissingPicks(leagueId) {
      const current = await getCurrentWeek();
      // frizat-xbq: submitted counts, not getLeaguePicks (which hides unstarted games' picks).
      const counts = await getLeaguePickCounts(leagueId, current.season, current.weekId);
      return {
        picksByUser: pickCountsToMap(counts),
        requiredPicks: current.requiredPicks,
        weekLabel: current.weekLabel,
      };
    },
  };
}
