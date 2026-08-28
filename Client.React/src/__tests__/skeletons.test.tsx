import { render } from '@testing-library/react';
import GameCardGridSkeleton from '../components/GameCardSkeleton';
import LeaderboardSkeleton from '../components/LeaderboardSkeleton';

// frizat: both skeletons render real semantic elements MUI reuses for the loaded content (a
// <table> for LeaderboardSkeleton, via MUI's Table component) — aria-hidden keeps them out of the
// accessibility tree AND out of role-based queries, so `findByRole('table')` etc. in the real
// page's own tests wait for the actual data-populated element, not this decorative placeholder.
// This regressed real leaderboard.test.tsx tests once before being caught; these tests pin it.
describe('loading skeletons', () => {
  it('GameCardGridSkeleton renders without a real "button"/interactive role and is aria-hidden', () => {
    const { container } = render(<GameCardGridSkeleton />);
    const root = container.firstElementChild!;
    expect(root.getAttribute('aria-hidden')).toBe('true');
  });

  it('LeaderboardSkeleton renders a table shape but is aria-hidden so it never satisfies findByRole("table")', () => {
    const { container } = render(<LeaderboardSkeleton />);
    const root = container.firstElementChild!;
    expect(root.getAttribute('aria-hidden')).toBe('true');
    expect(root.querySelector('table')).not.toBeNull();
  });
});
