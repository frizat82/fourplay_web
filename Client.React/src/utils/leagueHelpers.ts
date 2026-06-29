const LEAGUE_BASE_COST = 100;
const LEAGUE_BASE_MEMBERS = 10;
const LEAGUE_PER_HEAD = 10;

export function computeLeagueCost(memberCount: number): number {
  return LEAGUE_BASE_COST + Math.max(0, memberCount - LEAGUE_BASE_MEMBERS) * LEAGUE_PER_HEAD;
}
