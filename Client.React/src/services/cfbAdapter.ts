import { getCfbCurrentSlate, getCfbSlates, getCfbSpreads, getCfbScores as getCfbDbScores, getCfbUserPicks, getCfbAllPicks, addCfbPicks, deleteCfbPicks } from '../api/cfb';
import { getCfbScoresForSlate, getCfbLiveGames } from '../api/espn';
import { cfbSlateNumberToWeek, cfbWeekToSlateNumber, getCfbWeekName, computeHomeCovers, computeOverWins, getCfbRequiredPicks, isGameLive } from '../utils/gameHelpers';
import type { CfbSlateDto, CfbSpreadDto, CfbScoreDto, CfbPickDto } from '../types/league';
import type { EspnScores } from '../types/espn';
import { getHomeTeamScore, getAwayTeamScore, toGameStatus, isHomeAway } from '../utils/gameHelpers';
import type { SportAdapter, GameView, GameStatusValue, PickView, PickType, WeekState } from './sportAdapter';
import { revealPicksForStartedGames, memoizeOnce } from './sportAdapter';

/** Map CFB backend status strings to canonical GameStatusValue */

// Fallback season shown when no slates are available at all (empty DB / first-run edge case)
const CFB_CONFIGURED_SEASON = 2026;
// Earliest supported CFB season (controls the lower bound of the season dropdown).
const CFB_FIRST_SEASON = 2025;
const CFB_REGULAR_WEEKS = Array.from({ length: 13 }, (_, i) => i + 1); // weeks 1-13
const CFB_POST_WEEKS = [1, 2, 3, 4, 5]; // Conf.Champs + CFP First Round/QF/SF/Championship

function slateToWeekState(slate: CfbSlateDto): WeekState {
  const { week, isPostSeason } = cfbSlateNumberToWeek(slate.slateNumber);
  return { season: slate.season, week, isPostSeason };
}

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
 * for a given team (e.g. off-season, demo mode, or before game is created).
 */
function buildGamesFromEspn(
  spreads: CfbSpreadDto[],
  espnData: EspnScores | null,
  dbScores: CfbScoreDto[],
  situationMap: Map<string, import('../types/liveGame').GameSituation | null>,
): GameView[] {
  // frizat: joined by home team abbreviation, not an ESPN event id — a team plays at most one
  // game per slate (the scope every caller of this function already loads spreads/scores at), so
  // homeTeam alone is an unambiguous join key. Matches CfbSpreads/CfbScores' own natural key.
  const espnMap = new Map<string, import('../types/espn').Competition>();
  for (const event of espnData?.events ?? []) {
    for (const comp of event.competitions) {
      // isHomeAway handles both string ('home') and numeric (1) forms — our backend re-serializes
      // the ESPN homeAway enum as a number (see toGameStatus's status.type.name comment above for
      // the same pattern), so a bare `=== 'home'` string comparison never matches.
      const home = comp.competitors.find(c => isHomeAway(c.homeAway, 'home'));
      if (home) espnMap.set(home.team.abbreviation, comp);
    }
  }
  const dbMap = new Map(dbScores.map(s => [s.homeTeam, s]));

  return spreads.map(sp => {
    const comp = espnMap.get(sp.homeTeam);
    const db = dbMap.get(sp.homeTeam);

    let status: GameStatusValue;
    let hs: number | null;
    let as_: number | null;

    if (comp) {
      // ESPN has live data — use it. Same status derivation as NFL's toGameStatus (nflAdapter.ts):
      // isGameOver/isHalfTime/isGameStarted handle both the numeric and string forms of
      // status.type.name (our backend re-serializes the ESPN enum as a number), unlike a bare
      // string comparison against 'STATUS_HALFTIME' etc, which would never match.
      status = toGameStatus(comp);
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

    const key = `${sp.homeTeam}-${sp.awayTeam}`;
    return {
      // Unique within a single slate's spread list (the only scope this ever gets rendered in) —
      // a team plays at most one game per slate, same reasoning as the espnMap/dbMap join keys.
      id: sp.homeTeam,
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
      spreadPostedAt: sp.dateCreated,
      homeRank: sp.homeTeamRank,
      awayRank: sp.awayTeamRank,
      // No hardcoded fallback — matches NFL exactly. Real situation data comes from
      // situationMap (built from getCfbLiveGames(), see fetchCfbEspnData) when ESPN provides it;
      // otherwise honestly null rather than showing a fabricated down/distance.
      situation: situationMap.get(key) ?? null,
    };
  });
}

// frizat: CfbPicks.Team is whichever side the user picked — it can be the home OR away team, but
// GameView.id is always the game's home team (see buildGamesFromEspn). A pick on the away side
// still needs its PickView.gameId to resolve to the game's home team so it matches GameView.id
// (pickCountForTeam/didUserPick in ScoresPage.tsx key off exact gameId equality) — this map
// resolves either side back to that game's homeTeam, built once per slate's spread list.
function buildTeamToHomeTeamMap(spreads: CfbSpreadDto[]): Map<string, string> {
  const map = new Map<string, string>();
  for (const sp of spreads) {
    map.set(sp.homeTeam, sp.homeTeam);
    map.set(sp.awayTeam, sp.homeTeam);
  }
  return map;
}

function cfbPickToPickView(pick: CfbPickDto, teamToHomeTeam: Map<string, string>): PickView {
  return {
    gameId: teamToHomeTeam.get(pick.team) ?? pick.team,
    team: pick.team,
    pickType: pick.pickType as PickType,
    userId: pick.userId,
    userName: pick.userName ?? '',
  };
}

// Always keyed by the control-table-resolved slate id — never ESPN's own implicit "current"
// scoreboard, which has its own notion of "current" (e.g. the last-completed slate during any gap
// in play) that can disagree with CfbSeasonWeekConfigs. Same fix as nflAdapter.ts's loadCurrentGames.
async function fetchCfbEspnData(slate: CfbSlateDto): Promise<{ espn: EspnScores | null; situations: Map<string, import('../types/liveGame').GameSituation | null> }> {
  const [espn, liveGames] = await Promise.all([
    getCfbScoresForSlate(slate.id),
    getCfbLiveGames().catch(() => []),
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
    getCfbSpreads(leagueId, slateId),
    getCfbUserPicks(leagueId, slateId),
    getCfbDbScores(slateId),
    fetchCfbEspnData(slate),
  ]);
  const teamToHomeTeam = buildTeamToHomeTeamMap(spreads);
  return {
    games: buildGamesFromEspn(spreads, espn, dbScores, situations),
    userPicks: picks.map(p => cfbPickToPickView(p, teamToHomeTeam)),
  };
}

export function createCfbAdapter(): SportAdapter {
  let cachedSlates: CfbSlateDto[] = [];
  // A real null (empty CfbSlates table — legitimate bootstrap state, not an error; see
  // CfbCurrentSlateService) is a valid cacheable answer here, same as nflAdapter.ts's
  // getCurrentWeek(). A rejected fetch (network/DB failure) is not cached, so it retries.
  const getCurrentSlate = memoizeOnce(getCfbCurrentSlate);

  async function getSlates(): Promise<CfbSlateDto[]> {
    if (cachedSlates.length === 0) {
      const current = await getCurrentSlate();
      if (current) cachedSlates = await getCfbSlates(current.season);
    }
    return cachedSlates;
  }

  async function loadScoresForSlate(leagueId: number, userId: string, slate: CfbSlateDto): Promise<{ games: GameView[]; allPicks: PickView[]; userPicks: PickView[] }> {
    const [spreads, allPickDtos, dbScores, { espn, situations }] = await Promise.all([
      getCfbSpreads(leagueId, slate.id),
      getCfbAllPicks(leagueId, slate.id),
      getCfbDbScores(slate.id),
      fetchCfbEspnData(slate),
    ]);
    const games = buildGamesFromEspn(spreads, espn, dbScores, situations);
    const teamToHomeTeam = buildTeamToHomeTeamMap(spreads);
    const allPicks = allPickDtos.map(p => cfbPickToPickView(p, teamToHomeTeam));
    const userPicks = allPicks.filter(p => p.userId === userId);
    return { games, allPicks: revealPicksForStartedGames(allPicks, games, userId), userPicks };
  }

  return {
    sport: 'cfb',
    pollIntervalMs: 300_000,
    // Relative path — see nflAdapter.ts's sseUrl for why this must not be an absolute
    // VITE_API_TARGET URL (breaks the same-origin proxy path and the SameSite=Lax auth cookie).
    sseUrl: '/api/cfb/live-stream',
    weekSelectorConfig: {
      regularWeekOptions: CFB_REGULAR_WEEKS,
      postSeasonWeekOptions: CFB_POST_WEEKS,
      maxRegularSeasonWeek: 13,
      minSeason: CFB_FIRST_SEASON,
      weekLabelFn: getCfbWeekName,
    },

    async currentSeasonYear() {
      const current = await getCurrentSlate();
      return current?.season ?? CFB_CONFIGURED_SEASON;
    },

    async loadCurrentGames(leagueId, userId) {
      const active = await getCurrentSlate();
      if (!active) {
        return { season: CFB_CONFIGURED_SEASON, week: 1, isPostSeason: false, games: [], userPicks: [], hasOdds: false, requiredPicks: 0, maxWeek: 1, maxSeason: CFB_CONFIGURED_SEASON };
      }
      const [slates, { games, userPicks }] = await Promise.all([
        getSlates(),
        loadSlate(leagueId, userId, active.id, active),
      ]);
      const weekState = slateToWeekState(active);
      // maxWeek = max REGULAR season week with data (caps the regular season selector)
      const maxRegularSlate = slates
        .filter(s => s.slateType === 'RegularSeason')
        .reduce((max, s) => Math.max(max, s.slateNumber), 0);
      return { ...weekState, games, userPicks, hasOdds: games.length > 0, requiredPicks: getCfbRequiredPicks(active.slateNumber), maxWeek: maxRegularSlate || 13, maxSeason: active.season };
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
      const [fresh, spreads] = await Promise.all([
        getCfbUserPicks(leagueId, slate.id),
        getCfbSpreads(leagueId, slate.id),
      ]);
      const teamToHomeTeam = buildTeamToHomeTeamMap(spreads);
      return fresh.map(p => cfbPickToPickView(p, teamToHomeTeam));
    },

    // ─── Scores ─────────────────────────────────────────────────────────────

    async loadCurrentScores(leagueId, userId) {
      const active = await getCurrentSlate();
      if (!active) {
        return { season: CFB_CONFIGURED_SEASON, week: 1, isPostSeason: false, games: [], allPicks: [], userPicks: [], hasOdds: false, hasActiveGames: false, requiredPicks: 0, maxWeek: 1, maxSeason: CFB_CONFIGURED_SEASON };
      }
      const weekState = slateToWeekState(active);
      const { games, allPicks, userPicks } = await loadScoresForSlate(leagueId, userId, active);
      const hasActiveGames = games.some(g => isGameLive(g.gameStatus));
      return { ...weekState, games, allPicks, userPicks, hasOdds: games.length > 0, hasActiveGames, requiredPicks: getCfbRequiredPicks(active.slateNumber), maxWeek: weekState.week, maxSeason: active.season };
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
