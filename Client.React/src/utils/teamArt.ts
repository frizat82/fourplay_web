export type TeamArtMode = 'badges' | 'logos';

// VITE_TEAM_ART_MODE toggles between the synthetic SVG shield badges (default, "badges") and
// real downloaded ESPN team logos ("logos") — see TeamHelmet.tsx / TeamLogo.tsx and
// scripts/download-team-logos.js. Any other/missing value falls back to "badges" so an unset env
// var never silently changes existing behavior.
export function getTeamArtMode(): TeamArtMode {
  return import.meta.env.VITE_TEAM_ART_MODE === 'logos' ? 'logos' : 'badges';
}
