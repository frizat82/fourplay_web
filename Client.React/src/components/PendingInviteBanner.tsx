import { useState } from 'react';
import { Alert, Box, Button, Stack } from '@mui/material';
import { acceptMembershipInvite, declineMembershipInvite } from '../api/league';
import { useSession } from '../services/session';
import { useToast } from '../services/toast';
import { extractApiErrorMessage } from '../utils/apiError';

export default function PendingInviteBanner() {
  const { pendingMembershipInvites, refreshPendingInvites, reloadLeagues } = useSession();
  const toast = useToast();
  const [respondingId, setRespondingId] = useState<number | null>(null);

  if (pendingMembershipInvites.length === 0) return null;

  const handleAccept = async (id: number) => {
    setRespondingId(id);
    try {
      await acceptMembershipInvite(id);
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Failed to accept invite'), 'error');
      setRespondingId(null);
      return;
    }
    // The accept already succeeded server-side — a failed refresh here just means stale league/
    // invite state until the next natural reload, not a failed accept. Don't tell the user
    // otherwise.
    await Promise.all([reloadLeagues(), refreshPendingInvites()]).catch(() => {});
    setRespondingId(null);
  };

  const handleDecline = async (id: number) => {
    setRespondingId(id);
    try {
      await declineMembershipInvite(id);
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Failed to decline invite'), 'error');
      setRespondingId(null);
      return;
    }
    await refreshPendingInvites().catch(() => {});
    setRespondingId(null);
  };

  return (
    <Stack spacing={1} sx={{ mb: 2 }}>
      {pendingMembershipInvites.map((invite) => (
        <Alert key={invite.id} severity="info">
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
            <span>You&apos;re being asked to join <strong>{invite.leagueName}</strong>.</span>
            <Stack direction="row" spacing={1}>
              <Button
                size="small"
                variant="contained"
                disabled={respondingId === invite.id}
                onClick={() => void handleAccept(invite.id)}
              >
                Accept
              </Button>
              <Button
                size="small"
                variant="outlined"
                disabled={respondingId === invite.id}
                onClick={() => void handleDecline(invite.id)}
              >
                Decline
              </Button>
            </Stack>
          </Box>
        </Alert>
      ))}
    </Stack>
  );
}
