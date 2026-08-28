import { Grid, Paper, Skeleton, Stack } from '@mui/material';

/** One placeholder game card, matching GameCard/ScoresPage's real two-team-row shape:
 * helmet circle + name/record line on the left, a value/button-shaped block on the right. */
function GameCardSkeleton() {
  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        {[0, 1].map(row => (
          <Stack key={row} direction="row" alignItems="center" spacing={1.5}>
            <Skeleton variant="circular" width={50} height={50} />
            <Stack spacing={0.5} sx={{ flexGrow: 1 }}>
              <Skeleton variant="text" width="40%" height={20} />
              <Skeleton variant="text" width="25%" height={16} />
            </Stack>
            <Skeleton variant="rounded" width={112} height={44} />
          </Stack>
        ))}
      </Stack>
    </Paper>
  );
}

/** Placeholder grid for ScoresPage/PicksPage's first-load state — same Grid sizing as the real
 * game-card grid, so nothing shifts position once real data replaces it. */
export default function GameCardGridSkeleton({ count = 4 }: { count?: number }) {
  return (
    // aria-hidden: decorative (no real data) — see LeaderboardSkeleton.tsx for why this matters
    // beyond a11y correctness (role-based test queries would otherwise match this too).
    <Grid container spacing={2} aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <Grid size={{ xs: 12, md: 6, lg: 4 }} key={i}>
          <GameCardSkeleton />
        </Grid>
      ))}
    </Grid>
  );
}
