import { useTheme } from '@mui/material/styles';
import TeamArtImage from './TeamArtImage';

interface TeamLogoProps {
  abbr: string;
  sport: 'nfl' | 'cfb';
  size?: number;
  showLabel?: boolean;
}

// Real ESPN team crest, downloaded once into public/Icons/Logos/{nfl,cfb}/
// (scripts/download-team-logos.js) — no runtime ESPN dependency. Used instead of TeamHelmet's
// synthetic SVG shield when VITE_TEAM_ART_MODE=logos (see utils/teamArt.ts). Sport-scoped
// subfolders, not a flat abbr-keyed one, because a few abbreviations are shared by an NFL team
// and a CFB team (MIA: Dolphins/Hurricanes, CIN: Bengals/Bearcats, TEN: Titans/Volunteers). A
// team with no downloaded logo (a rare opponent outside the curated TEAMS lists in
// generate-helmets.js) falls back to the same plain text badge TeamHelmet falls back to.
// In dark mode, ESPN's dark-background variant ({sport}-dark/) is preferred — e.g. Ohio State's
// black lettering is unreadable on a dark card — falling back to the regular logo if missing.
export default function TeamLogo({ abbr, sport, size = 56, showLabel = true }: TeamLogoProps) {
  const isDark = useTheme().palette.mode === 'dark';
  const file = `${abbr.toLowerCase()}.png`;
  const regular = `/Icons/Logos/${sport}/${file}`;
  return (
    <TeamArtImage
      abbr={abbr}
      src={isDark ? `/Icons/Logos/${sport}-dark/${file}` : regular}
      fallbackSrc={isDark ? regular : undefined}
      size={size}
      showLabel={showLabel}
      cacheKey={`${sport}-${abbr}`}
    />
  );
}
