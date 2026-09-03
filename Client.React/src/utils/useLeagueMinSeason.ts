import { useEffect, useState } from 'react';
import { getLeagueJuice } from '../api/league';

/**
 * The earliest season a league actually has data for, per the shared source of truth
 * (LeagueJuiceMapping) — not the sport-wide adapter default. NFL's minSeason=2020 and CFB's
 * minSeason=2025 are hardcoded floors covering every league on that sport, so a brand-new
 * league's season selector was offering years before that league (or CFB itself) existed
 * (e.g. "OG FourPlayaz" showing 2020-2024, "CFB Beta Testers" showing 2025). One shared hook —
 * not a per-sport fix — since the bug and the fix are identical for NFL and CFB.
 * Falls back to `fallbackMinSeason` while loading or when the league has no juice mapping yet.
 */
export function useLeagueMinSeason(leagueId: number | null, fallbackMinSeason: number): number {
  const [minSeason, setMinSeason] = useState(fallbackMinSeason);

  useEffect(() => {
    if (leagueId == null) { setMinSeason(fallbackMinSeason); return; }
    let ignore = false;
    void getLeagueJuice(leagueId)
      .then((mappings) => {
        if (ignore) return;
        setMinSeason(mappings.length > 0 ? Math.min(...mappings.map((m) => m.season)) : fallbackMinSeason);
      })
      .catch(() => { if (!ignore) setMinSeason(fallbackMinSeason); });
    return () => { ignore = true; };
  }, [leagueId, fallbackMinSeason]);

  return minSeason;
}
