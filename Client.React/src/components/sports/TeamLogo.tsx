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
export default function TeamLogo({ abbr, sport, size = 56, showLabel = true }: TeamLogoProps) {
  return (
    <TeamArtImage
      abbr={abbr}
      src={`/Icons/Logos/${sport}/${abbr.toLowerCase()}.png`}
      size={size}
      showLabel={showLabel}
      cacheKey={`${sport}-${abbr}`}
    />
  );
}
