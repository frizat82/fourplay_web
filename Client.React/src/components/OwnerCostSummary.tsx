import { Chip } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useSession } from '../services/session';
import { getLeagueCost } from '../api/league';

/** Total season cost across every league the current user owns (frizat-d6l). */
export default function OwnerCostSummary() {
  const { ownedLeagues } = useSession();

  const { data: total } = useQuery({
    queryKey: ['owner-cost-summary', ownedLeagues.map(l => l.id)],
    queryFn: async (): Promise<number> => {
      const costs = await Promise.all(ownedLeagues.map(league => getLeagueCost(league.id)));
      return costs.reduce((sum, c) => sum + c.cost, 0);
    },
    enabled: ownedLeagues.length > 0,
  });

  if (ownedLeagues.length === 0 || total === undefined) return null;

  const count = ownedLeagues.length;
  return (
    <Chip
      data-testid="owner-cost-summary"
      // frizat: this chip renders on the Dashboard hero, which always has a dark navy background
      // regardless of the app's light/dark theme (see .hero-section in home.css — it isn't
      // theme-aware). An outlined chip has no fill, so in light mode primary.main (near-black
      // navy, tuned for text on a light background) was nearly invisible against that fixed-dark
      // backdrop. A filled chip always paints an opaque colored background with contrastText
      // (white), so it stays legible everywhere this component is used, in both themes.
      color="primary"
      variant="filled"
      label={`$${total} across ${count} league${count !== 1 ? 's' : ''}`}
    />
  );
}
