import type { GameView, PickView } from '../services/sportAdapter';
import type { PickType } from '../types/picks';
import { isGameDecided, isGameFinal, pickCovers, pickMargin } from './gameHelpers';

// won/lost = final. covering/trailing = live, "if it ended right now". pending = not started.
export type MatrixPickState = 'won' | 'lost' | 'covering' | 'trailing' | 'pending';

export type MatrixSlot =
  | { kind: 'pick'; team: string; pickType: PickType; state: MatrixPickState; margin: number | null }
  // Another member's pick on a game that hasn't kicked off (revealPicksForStartedGames withholds it).
  | { kind: 'hidden' }
  | { kind: 'empty' };

type PickSlot = Extract<MatrixSlot, { kind: 'pick' }>;

// alive/onTheRopes: still has games to play (the people you can root against). won: every pick
// final and covered. out: lost a pick or missed one.
export type SurvivorStatus = 'alive' | 'onTheRopes' | 'won' | 'out';

const GROUP_ORDER: Record<SurvivorStatus, number> = { alive: 0, onTheRopes: 0, won: 1, out: 2 };

export interface SurvivorRow {
  userId: string;
  userName: string;
  status: SurvivorStatus;
  slots: MatrixSlot[];
}

function pickState(game: GameView | undefined, team: string, pickType: PickType): MatrixPickState {
  const covers = pickCovers(game, team, pickType);
  if (covers == null || !game) return 'pending';
  if (isGameFinal(game.gameStatus)) return covers ? 'won' : 'lost';
  return covers ? 'covering' : 'trailing';
}

// Not spreadLabel: a margin of exactly 0 is "0" (on the line), not "PK", and uses a true minus sign.
export function formatMargin(margin: number): string {
  if (margin > 0) return `+${margin}`;
  if (margin < 0) return `−${Math.abs(margin)}`;
  return '0';
}

// Screen-reader wording for a pick's state, shared by the Scores matrix and the Scores cards.
export function describePick(state: MatrixPickState, margin: number | null): string {
  const marginText = margin == null ? '' : ` ${formatMargin(margin)}`;
  switch (state) {
    case 'won': return 'covered';
    case 'lost': return "didn't cover";
    case 'covering': return `covering${marginText} (live)`;
    case 'trailing': return `not covering${marginText} (live)`;
    default: return 'not started';
  }
}

/**
 * One row per member with a revealed pick: picks ordered by kickoff, unrevealed slots filled in,
 * and whether the member is still alive. Rows with games left come first, then winners, then out.
 */
export function buildSurvivorRows(picks: PickView[], games: GameView[], requiredPicks: number, currentUserId?: string): SurvivorRow[] {
  const gameByTeam = new Map<string, GameView>();
  const kickoffByTeam = new Map<string, number>();
  for (const g of games) {
    const kickoff = Date.parse(g.gameTime);
    for (const team of [g.homeTeam, g.awayTeam]) { gameByTeam.set(team, g); kickoffByTeam.set(team, kickoff); }
  }
  const kickoff = (team: string) => kickoffByTeam.get(team) ?? Number.POSITIVE_INFINITY;
  // Until every game has kicked off, a missing slot may just be a pick we can't see yet. Judged by
  // the status in the same fetch the picks came from (not the clock now), so a game that kicked off
  // since that fetch still reads as hidden rather than as a missed pick.
  const allKickedOff = games.every(g => isGameDecided(g.gameStatus));

  const byUser = new Map<string, PickView[]>();
  for (const p of picks) {
    const list = byUser.get(p.userId) ?? [];
    list.push(p);
    byUser.set(p.userId, list);
  }

  const rows = [...byUser].map(([userId, userPicks]): SurvivorRow => {
    const isViewer = userId === currentUserId;
    const pickSlots: PickSlot[] = userPicks
      .sort((a, b) => kickoff(a.team) - kickoff(b.team) || 0) // || 0: Infinity - Infinity is NaN
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
      : unfilled === 0 && states.every(st => st === 'won') ? 'won'
      : 'alive';
    return { userId, userName: userPicks[0].userName, status, slots };
  });

  return rows.sort((a, b) =>
    GROUP_ORDER[a.status] - GROUP_ORDER[b.status] || a.userName.localeCompare(b.userName));
}
