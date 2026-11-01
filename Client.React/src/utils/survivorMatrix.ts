import type { GameView, PickView } from '../services/sportAdapter';
import type { PickType } from '../types/picks';
import { isGameFinal, isGameLocked, pickCovers } from './gameHelpers';

// won/lost = final. covering/trailing = live, "if it ended right now". pending = not started.
export type MatrixPickState = 'won' | 'lost' | 'covering' | 'trailing' | 'pending';

export type MatrixSlot =
  | { kind: 'pick'; team: string; pickType: PickType; state: MatrixPickState; margin: number | null }
  // Another member's pick on a game that hasn't kicked off (revealPicksForStartedGames withholds it).
  | { kind: 'hidden' }
  | { kind: 'empty' };

type PickSlot = Extract<MatrixSlot, { kind: 'pick' }>;

export type SurvivorStatus = 'alive' | 'onTheRopes' | 'out';

export interface SurvivorRow {
  userName: string;
  status: SurvivorStatus;
  slots: MatrixSlot[];
}

// How far a pick is past its line (positive = covering). Display only: covering itself always
// comes from pickCovers, so this never decides a result.
function pickMargin(game: GameView | undefined, team: string, pickType: PickType): number | null {
  if (!game || game.homeScore == null || game.awayScore == null) return null;
  const total = game.homeScore + game.awayScore;
  if (pickType === 'Over') return game.overThreshold == null ? null : total - game.overThreshold;
  if (pickType === 'Under') return game.underThreshold == null ? null : game.underThreshold - total;
  const isHome = team === game.homeTeam;
  const spread = isHome ? game.homeSpread : game.awaySpread;
  if (spread == null) return null;
  return isHome ? game.homeScore + spread - game.awayScore : game.awayScore + spread - game.homeScore;
}

function pickState(game: GameView | undefined, team: string, pickType: PickType): MatrixPickState {
  const covers = pickCovers(game, team, pickType);
  if (covers == null || !game) return 'pending';
  if (isGameFinal(game.gameStatus)) return covers ? 'won' : 'lost';
  return covers ? 'covering' : 'trailing';
}

export function formatMargin(margin: number): string {
  if (margin > 0) return `+${margin}`;
  if (margin < 0) return `−${Math.abs(margin)}`;
  return '0';
}

/**
 * One row per member with a revealed pick: picks ordered by kickoff, unrevealed slots filled in,
 * and whether the member is still alive. Rows still in it come first, then rows that are out.
 */
export function buildSurvivorRows(picks: PickView[], games: GameView[], requiredPicks: number, currentUserId?: string): SurvivorRow[] {
  const gameByTeam = new Map<string, GameView>();
  for (const g of games) { gameByTeam.set(g.homeTeam, g); gameByTeam.set(g.awayTeam, g); }
  const kickoff = (team: string) => {
    const g = gameByTeam.get(team);
    return g ? Date.parse(g.gameTime) : Number.POSITIVE_INFINITY;
  };
  // Until every game has kicked off, a missing slot may just be a pick we can't see yet.
  const allKickedOff = games.every(isGameLocked);

  const byUser = new Map<string, PickView[]>();
  for (const p of picks) byUser.set(p.userName, [...(byUser.get(p.userName) ?? []), p]);

  const rows = [...byUser].map(([userName, userPicks]): SurvivorRow => {
    const isViewer = userPicks.some(p => p.userId === currentUserId);
    const pickSlots: PickSlot[] = [...userPicks]
      .sort((a, b) => kickoff(a.team) - kickoff(b.team))
      .slice(0, requiredPicks)
      .map((p): PickSlot => {
        const game = gameByTeam.get(p.team);
        const state = pickState(game, p.team, p.pickType);
        const margin = state === 'covering' || state === 'trailing' ? pickMargin(game, p.team, p.pickType) : null;
        return { kind: 'pick', team: p.team, pickType: p.pickType, state, margin };
      });
    const unfilled = requiredPicks - pickSlots.length;
    const filler: MatrixSlot = !isViewer && !allKickedOff ? { kind: 'hidden' } : { kind: 'empty' };
    const slots = [...pickSlots, ...Array.from({ length: unfilled }, () => filler)];

    const states = pickSlots.map(s => s.state);
    const missedPick = unfilled > 0 && allKickedOff;
    const status: SurvivorStatus = states.includes('lost') || missedPick ? 'out'
      : states.includes('trailing') ? 'onTheRopes'
      : 'alive';
    return { userName, status, slots };
  });

  return rows.sort((a, b) =>
    Number(a.status === 'out') - Number(b.status === 'out') || a.userName.localeCompare(b.userName));
}
