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
      color="primary"
      variant="outlined"
      label={`$${total} across ${count} league${count !== 1 ? 's' : ''}`}
    />
  );
}
