import TeamHelmet from './TeamHelmet';
import TeamLogo from './TeamLogo';
import { getTeamArtMode } from '../../utils/teamArt';

interface TeamArtProps {
  abbr: string;
  sport: 'nfl' | 'cfb';
  size?: number;
  showLabel?: boolean;
}

// Single call site every page/component uses to render a team's identity — picks TeamHelmet
// (synthetic badge) or TeamLogo (real ESPN crest) based on VITE_TEAM_ART_MODE, so the mode
// decision lives in exactly one place. `sport` only matters for TeamLogo's sport-scoped logo
// folder (see TeamLogo.tsx) — TeamHelmet's flat Helmets folder is unaffected.
export default function TeamArt({ abbr, sport, size, showLabel }: TeamArtProps) {
  return getTeamArtMode() === 'logos'
    ? <TeamLogo abbr={abbr} sport={sport} size={size} showLabel={showLabel} />
    : <TeamHelmet abbr={abbr} size={size} showLabel={showLabel} />;
}
