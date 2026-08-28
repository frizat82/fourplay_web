import { Box, Skeleton, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';

/** Placeholder for LeaderboardPage's first-load state — same Rank/User/Total/Week table shape as
 * the real standings table, so nothing shifts position once real data replaces it. */
export default function LeaderboardSkeleton({ rows = 6, weekColumns = 4 }: { rows?: number; weekColumns?: number }) {
  return (
    // aria-hidden: this is decorative (no real data), and MUI's Table renders a real role="table"
    // element that would otherwise collide with the real table's role once data loads — hiding it
    // keeps it out of the accessibility tree and out of role-based test/assistive-tech queries.
    <Box sx={{ overflowX: 'auto' }} aria-hidden="true">
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Rank</TableCell>
            <TableCell>User</TableCell>
            <TableCell>Total</TableCell>
            {Array.from({ length: weekColumns }, (_, i) => (
              <TableCell key={i}><Skeleton variant="text" width={24} /></TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {Array.from({ length: rows }, (_, row) => (
            <TableRow key={row}>
              <TableCell><Skeleton variant="text" width={20} /></TableCell>
              <TableCell><Skeleton variant="text" width={100} /></TableCell>
              <TableCell><Skeleton variant="text" width={40} /></TableCell>
              {Array.from({ length: weekColumns }, (_, i) => (
                <TableCell key={i}><Skeleton variant="rounded" width={36} height={24} /></TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}
