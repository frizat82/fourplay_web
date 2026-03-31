import type { Competition, Competitor, EspnScores, Event, TypeName, HomeAway, EspnRecordType } from '../types/espn';
import { toLocalDisplay } from './time';
import type { PickType } from '../types/picks';

export function getWeekFromEspnWeek(week: number, isPostSeason = false) {
  return isPostSeason ? week + 18 : week;
}

export function getWeekName(week: number, isPostSeason = false) {
  if (!isPostSeason) return `Week ${week}`;
  switch (week) {
    case 1:
      return 'Wild Card';
    case 2:
      return 'Divisional Round';
    case 3:
      return 'Conference Championship';
    case 4:
      return 'Super Bowl';
    default:
      throw new Error('Invalid week number');
  }
}

export function getEspnRequiredPicks(week: number, isPostSeason = false) {
  if (!isPostSeason) return 4;
  switch (week) {
    case 1:
    case 2:
      return 3;
    case 3:
      return 2;
    case 4:
    case 5:
      return 1;
    default:
      throw new Error('Invalid week number');
  }
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

export function getAwayTeamLogo(competition: Competition) {
  return getTeamLogo(getAwayTeamAbbr(competition));
}

export function getHomeTeamLogo(competition: Competition) {
  return getTeamLogo(getHomeTeamAbbr(competition));
}

export function getTeamLogo(teamAbbr: string) {
  return `/Icons/Teams/${teamAbbr.toLowerCase()}.png`;
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

export function isPostSeason(scores: EspnScores | null) {
  if (!scores?.season) return false;
  return scores.season.type === 3;
}

export function getScoreEvents(scores: EspnScores | null | undefined): Event[] {
  return scores?.events ?? [];
}

export function getPickLabel(pickType: PickType) {
  return pickType === 'Spread' ? 'Spread' : pickType;
}

function isHomeAway(value: HomeAway, expected: 'home' | 'away') {
  if (typeof value === 'number') {
    return expected === 'away' ? value === 0 : value === 1;
  }
  return value === expected;
}

function isRecordType(value: EspnRecordType, expected: 'total' | 'road' | 'home') {
  if (typeof value === 'number') {
    if (expected === 'total') return value === 0;
    if (expected === 'road') return value === 1;
    return value === 2;
  }
  return value === expected;
}
