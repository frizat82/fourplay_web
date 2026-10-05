import { findGameIdForTeam } from '../utils/gameHelpers';

// Push notification deep links (/scores?team=KC) name a team, not a game — find its game card.
describe('findGameIdForTeam', () => {
  const games = [
    { id: 'g1', homeTeam: 'BUF', awayTeam: 'MIA' },
    { id: 'g2', homeTeam: 'DAL', awayTeam: 'NYG' },
  ];

  it('matches the home team', () => expect(findGameIdForTeam(games, 'DAL')).toBe('g2'));
  it('matches the away team', () => expect(findGameIdForTeam(games, 'MIA')).toBe('g1'));
  it('is case-insensitive', () => expect(findGameIdForTeam(games, 'nyg')).toBe('g2'));
  it('returns null when the team has no game this week', () => expect(findGameIdForTeam(games, 'KC')).toBeNull());
  it('returns null with no team', () => expect(findGameIdForTeam(games, null)).toBeNull());
});
