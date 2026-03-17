import type { Competition } from '../types/espn';
import { getAwayTeamAbbr, getAwayTeamScore, getHomeTeamAbbr, getHomeTeamScore, isGameOver, isGameStarted } from '../utils/gameHelpers';
import { getSituations } from '../utils/gameSituationHelper';

interface ScoreTickerProps {
  competition: Competition;
  over?: number | null;
  under?: number | null;
  homeSpread?: number | null;
  awaySpread?: number | null;
  isPostSeason: boolean;
}

export default function ScoreTicker({ competition, over, under, homeSpread, awaySpread, isPostSeason }: ScoreTickerProps) {
  if (!isGameStarted(competition)) return null;
  if (isGameOver(competition)) return null;

  const awayTeam = getAwayTeamAbbr(competition);
  const homeTeam = getHomeTeamAbbr(competition);
  const awayScore = getAwayTeamScore(competition);
  const homeScore = getHomeTeamScore(competition);

  if (awayScore === 0 && homeScore === 0) return null;

  const text = getSituations(
    homeTeam,
    awayTeam,
    homeScore,
    awayScore,
    homeSpread,
    awaySpread,
    isPostSeason ? over ?? null : null,
    isPostSeason ? under ?? null : null
  );

  if (!text) return null;

  return (
    <div className="score-ticker">
      <div className="ticker-text">{text}</div>
    </div>
  );
}
