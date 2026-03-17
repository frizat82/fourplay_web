export function getSituations(
  homeTeam: string,
  awayTeam: string,
  homeScore: number,
  awayScore: number,
  homeSpread?: number | null,
  awaySpread?: number | null,
  overLine?: number | null,
  underLine?: number | null
): string | null {
  let output = '';
  const total = homeScore + awayScore;

  if (homeSpread !== null && homeSpread !== undefined) {
    const coverMargin = homeScore + homeSpread - awayScore;
    if (coverMargin < 0) {
      const needed = Math.ceil(Math.abs(coverMargin));
      output = `${homeTeam} needs ${pointsToText(needed)} to cover the spread (${homeSpread}).`;
    }
  }

  if (awaySpread !== null && awaySpread !== undefined) {
    const coverMargin = awayScore + awaySpread - homeScore;
    if (coverMargin < 0) {
      const needed = Math.ceil(Math.abs(coverMargin));
      output = `${awayTeam} needs ${pointsToText(needed)} to cover the spread (${awaySpread}).`;
    }
  }

  if (overLine !== null && overLine !== undefined && total < overLine) {
    const needed = overLine - total;
    output = `The game needs ${needed} more points to hit the Over (${overLine}).`;
  }

  if (underLine !== null && underLine !== undefined && total > underLine) {
    const extra = total - underLine;
    output = `The game has exceeded the Under by ${extra} points (${underLine}).`;
  }

  return output || null;
}

function pointsToText(points: number) {
  if (points <= 3) return 'one FG';
  if (points <= 7) return 'one score';
  if (points <= 14) return 'two scores';
  if (points <= 21) return 'three scores';
  return `${Math.floor(points / 7) + 1} scores`;
}
