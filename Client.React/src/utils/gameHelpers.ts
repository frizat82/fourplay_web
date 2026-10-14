import type { Competition, Competitor, EspnScores, Event, TypeName, HomeAway, EspnRecordType } from '../types/espn';
import { toLocalDisplay } from './time';
import type { PickType } from '../types/picks';
import type { GameStatusValue, GameView } from '../services/sportAdapter';
import type { GameSituation } from '../types/liveGame';

/** True only once a game has finished — the canonical GameView.gameStatus check. */
export function isGameFinal(status: GameStatusValue): boolean {
  return status === 'final';
}

/** True while a game is underway (including halftime) — the canonical GameView.gameStatus check. */
export function isGameLive(status: GameStatusValue): boolean {
  return status === 'in_progress' || status === 'halftime';
}

/** True once a game has started or finished — i.e. no longer just "scheduled". */
export function isGameDecided(status: GameStatusValue): boolean {
  return isGameFinal(status) || isGameLive(status);
}

/**
 * True once a pick for this game can no longer be changed. Prefers the live ESPN status
 * (isGameDecided), but falls back to the game's own scheduled kickoff time already having
 * passed — covers the gap right after kickoff where ESPN's status hasn't caught up yet.
 * Shared by PicksPage (blocks selecting/unselecting) and PicksIsland (shows a locked pick).
 */
export function isGameLocked(game: GameView): boolean {
  if (isGameDecided(game.gameStatus)) return true;
  return new Date(game.gameTime) <= new Date();
}

/**
 * Mirrors Shared/Helpers/GameHelpers.cs's IsWeekExcludedFromSeason exactly — a league's
 * configured StartWeek (frizat-o3x) excludes any earlier week/slate from scoring. `week` is
 * whichever week/slate number the currently-viewed page is on (NFL week or CFB slate number,
 * both already unified under GameView/WeekState — no sport branching needed here).
 */
export function isWeekExcludedFromSeason(week: number, startWeek: number): boolean {
  return week < startWeek;
}

// Week names, postseason flags and pick counts are never computed here: they're each week's /
// slate's control-table row, served by the API (frizat-d47). ESPN's own week numbering is never
// translated either — weeks are our control table's.
export function spreadLabel(spread: number): string {
  if (spread === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
}

const STATUS_FINAL = 0;
const STATUS_HALFTIME = 1;
const STATUS_IN_PROGRESS = 2;
const STATUS_SCHEDULED = 3;

function isStatus(status: TypeName | string | number, code: number, text: string) {
  if (typeof status === 'number') return status === code;
  return status === text;
}

export function displayDetails(competition: Competition): string {
  const status = competition.status.type.name;
  if (isStatus(status, STATUS_SCHEDULED, 'status_scheduled')) {
    return toLocalDisplay(competition.date, {
      weekday: 'short',
      hour: 'numeric',
      minute: '2-digit',
    });
  }
  if (isStatus(status, STATUS_HALFTIME, 'status_halftime')) return 'Half Time';
  if (isStatus(status, STATUS_IN_PROGRESS, 'status_in_progress')) {
    return `Q${competition.status.period} ${competition.status.displayClock}`;
  }
  if (isStatus(status, STATUS_FINAL, 'status_final')) return 'Final';
  return '';
}

export function getCompetitionFromHomeAwayAbbr(home: string, away: string, scores: EspnScores) {
  for (const scoreEvent of scores.events ?? []) {
    for (const competition of scoreEvent.competitions) {
      if (getHomeTeamAbbr(competition) === home && getAwayTeamAbbr(competition) === away) {
        return competition;
      }
    }
  }
  throw new Error('Competition not found');
}

export function getAwayTeamAbbr(competition: Competition) {
  return getTeamAbbr(getAwayTeam(competition));
}

export function getHomeTeamAbbr(competition: Competition) {
  return getTeamAbbr(getHomeTeam(competition));
}

export function getAwayTeam(competition: Competition) {
  const found = competition.competitors.find((c) => isHomeAway(c.homeAway, 'away'));
  if (!found) throw new Error('Away team missing');
  return found;
}

export function getHomeTeam(competition: Competition) {
  const found = competition.competitors.find((c) => isHomeAway(c.homeAway, 'home'));
  if (!found) throw new Error('Home team missing');
  return found;
}

export function getTeamScore(competitor: Competitor): number {
  const raw = competitor.score as unknown;
  if (typeof raw === 'number') return raw;
  const parsed = Number(raw);
  return Number.isFinite(parsed) ? parsed : 0;
}

export function getHomeTeamScore(competition: Competition) {
  return getTeamScore(getHomeTeam(competition));
}

export function getAwayTeamScore(competition: Competition) {
  return getTeamScore(getAwayTeam(competition));
}

export function isGameStarted(competition: Competition) {
  return !isStatus(competition.status.type.name, STATUS_SCHEDULED, 'status_scheduled');
}

export function isGameOver(competition: Competition) {
  return isStatus(competition.status.type.name, STATUS_FINAL, 'status_final');
}

export function getTeamAbbr(competitor: Competitor | undefined) {
  return competitor?.team?.abbreviation ?? '';
}

export function getTeamRecord(competitor: Competitor) {
  return competitor.records?.find((r) => isRecordType(r.type, 'total'))?.summary;
}

export function getDownDistance(competition: Competition) {
  return competition.situation?.downDistanceText;
}

export function isRedZone(competition: Competition) {
  return Boolean(competition.situation?.isRedZone);
}

export function getPossessionTeamAbbr(competition: Competition) {
  const possessionId = competition.situation?.possession;
  if (!possessionId) return null;
  const team = competition.competitors.find((c) => c.id === possessionId);
  return team?.team?.abbreviation ?? null;
}

export function isHalfTime(competition: Competition) {
  return isStatus(competition.status.type.name, STATUS_HALFTIME, 'status_halftime');
}

/** Map ESPN TypeName (number or string) to canonical GameStatusValue — shared by both adapters. */
export function toGameStatus(competition: Competition): GameStatusValue {
  if (isGameOver(competition)) return 'final';
  if (!isGameStarted(competition)) return 'scheduled';
  return isHalfTime(competition) ? 'halftime' : 'in_progress';
}

export function hasPossession(competition: Competition, teamAbbr: string) {
  const possession = getPossessionTeamAbbr(competition);
  return possession === teamAbbr;
}

export function isAfterKickoff(competition: Competition, now: Date = new Date()): boolean {
  return now >= new Date(competition.date);
}

export function shouldShowGamePicks(competition: Competition) {
  return isGameStarted(competition) || isAfterKickoff(competition);
}

export function getScoreEvents(scores: EspnScores | null | undefined): Event[] {
  return scores?.events ?? [];
}

export function getPickLabel(pickType: PickType) {
  return pickType === 'Spread' ? 'Spread' : pickType;
}

// ─── Shared cover/over computation used by all sport adapters ────────────────
// Both NFL and CFB adapters should call these instead of duplicating the logic.

/**
 * Returns whether the home team is covering the spread, or null if the game
 * hasn't started or spread data isn't available.
 * Works for final, in-progress, and halftime.
 */
// This league's spread is "teased" (juice added per-team, see SpreadCalculator.GetSpread on the
// backend) — home/away spreads are NOT mirror images of each other once juice is nonzero, so one
// side's cover can't be derived by negating the other's. Each must be computed independently from
// its own spread, sharing this one guard/formula so they can't drift apart.
function computeCovers(
  status: GameStatusValue,
  teamSpread: number | null,
  teamScore: number | null,
  opponentScore: number | null,
): boolean | null {
  if (!isGameDecided(status) || teamSpread == null || teamScore == null || opponentScore == null) return null;
  return (teamScore + teamSpread) > opponentScore;
}

export function computeHomeCovers(
  status: GameStatusValue,
  homeSpread: number | null,
  homeScore: number | null,
  awayScore: number | null,
): boolean | null {
  return computeCovers(status, homeSpread, homeScore, awayScore);
}

export function computeAwayCovers(
  status: GameStatusValue,
  awaySpread: number | null,
  homeScore: number | null,
  awayScore: number | null,
): boolean | null {
  return computeCovers(status, awaySpread, awayScore, homeScore);
}

/**
 * Returns whether the total is over the Over threshold, or null if unavailable.
 * The league's juice is applied independently per pick type (SpreadCalculator.GetOverUnder on the
 * backend: Over threshold = rawTotal - juice, Under threshold = rawTotal + juice) — these are NOT
 * the same number once juice is nonzero, so computeUnderWins below must never be derived by
 * negating this result (same reasoning as computeHomeCovers/computeAwayCovers above).
 */
export function computeOverWins(
  status: GameStatusValue,
  overThreshold: number | null,
  homeScore: number | null,
  awayScore: number | null,
): boolean | null {
  if (!isGameDecided(status) || overThreshold == null || homeScore == null || awayScore == null) return null;
  return (homeScore + awayScore) > overThreshold;
}

/** Returns whether the total is under the Under threshold, or null if unavailable. */
export function computeUnderWins(
  status: GameStatusValue,
  underThreshold: number | null,
  homeScore: number | null,
  awayScore: number | null,
): boolean | null {
  if (!isGameDecided(status) || underThreshold == null || homeScore == null || awayScore == null) return null;
  return (homeScore + awayScore) < underThreshold;
}

export function isHomeAway(value: HomeAway, expected: 'home' | 'away') {
  if (typeof value === 'number') {
    return expected === 'away' ? value === 0 : value === 1;
  }
  return value === expected;
}

// ESPN's live situation.isRedZone flag has been observed true with a yardLine nowhere near
// either goal (e.g. yardLine=35 mid-drive, likely a momentarily-stale value right after a play).
// The actual red zone is the 20 yards nearest whichever goal the ball is closest to, so cross-
// check the flag against yardLine before trusting it — anything that draws a "red zone" UI
// element (the ScoresPage card border, FieldPosition's stripe) must use this, not the raw flag,
// so the two can never visually disagree with each other the way they did before this existed.
export function isConsistentRedZone(situation: Pick<GameSituation, 'isRedZone' | 'yardLine'>): boolean {
  return situation.isRedZone && (situation.yardLine <= 20 || situation.yardLine >= 80);
}

function isRecordType(value: EspnRecordType, expected: 'total' | 'road' | 'home') {
  if (typeof value === 'number') {
    if (expected === 'total') return value === 0;
    if (expected === 'road') return value === 1;
    return value === 2;
  }
  return value === expected;
}

/** Push notification deep links (/scores?team=KC) name a team; returns the id of its game card. */
export function findGameIdForTeam<T extends { id: string; homeTeam: string; awayTeam: string }>(
  games: readonly T[],
  team: string | null | undefined,
): string | null {
  if (!team) return null;
  const t = team.toUpperCase();
  return games.find((g) => g.homeTeam.toUpperCase() === t || g.awayTeam.toUpperCase() === t)?.id ?? null;
}
