import { Typography } from '@mui/material';

interface RankBadgeProps {
  rank: number | null | undefined;
}

/**
 * AP Top 25 rank (e.g. "#3") shown next to a ranked team's name — CFB only, since NFL has no
 * polls. Renders nothing for an unranked team (null/undefined), rather than an "unranked"
 * placeholder. Shared by GameCard (Picks page) and ScoresPage so a future style tweak only
 * needs one edit.
 */
export default function RankBadge({ rank }: RankBadgeProps) {
  if (rank == null) return null;
  return (
    <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
      #{rank}
    </Typography>
  );
}
