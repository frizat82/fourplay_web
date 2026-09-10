import TeamArtImage from './TeamArtImage';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, showLabel = true }: TeamHelmetProps) {
  // frizat: a handful of teams (e.g. Illinois) only ever had the neon PNG set, never a flat SVG —
  // falling back to that PNG silently brought the neon look back for exactly those teams even
  // after the dark-mode neon revert. TeamArtImage falls back to a plain text badge instead so no
  // team can ever render the neon asset again, regardless of SVG coverage.
  return (
    <TeamArtImage
      abbr={abbr}
      src={`/Icons/Helmets/${abbr.toLowerCase()}.svg`}
      size={size}
      height={Math.round(size * 1.1)}
      showLabel={showLabel}
    />
  );
}
