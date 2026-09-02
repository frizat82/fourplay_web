import { useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import EmailIcon from '@mui/icons-material/Email';
import DeleteIcon from '@mui/icons-material/Delete';
import PageHeader from '../../components/PageHeader';
import { useToast } from '../../services/toast';
import { useAuth } from '../../services/auth';
import { createInvitation, deleteInvitation, getAllInvitations, resendInvitation } from '../../api/invitations';
import { getAllLeagues } from '../../api/league';
import type { InvitationDto, LeagueInfoDto } from '../../types/admin';
import { extractApiErrorMessage } from '../../utils/apiError';

function getInvitationStatusChip(invitation: InvitationDto): { label: string; color: 'success' | 'warning' | 'error' | 'info' } {
  if (invitation.isUsed) {
    return invitation.registeredUserEmailConfirmed
      ? { label: 'Confirmed', color: 'success' }
      : { label: 'Pending Confirmation', color: 'warning' };
  }
  if (invitation.isExpired) return { label: 'Expired', color: 'error' };
  return { label: 'Active', color: 'info' };
}

export default function AdminInvitationsPage() {
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showUsed, setShowUsed] = useState(true);
  const [showExpired, setShowExpired] = useState(true);
  const [email, setEmail] = useState('');
  const [selectedLeagueId, setSelectedLeagueId] = useState<number | ''>('');
  const [leagues, setLeagues] = useState<LeagueInfoDto[]>([]);
  const [creating, setCreating] = useState(false);
  const toast = useToast();
  const { user } = useAuth();

  const loadInvitations = async () => {
    setLoading(true);
    const data = await getAllInvitations();
    setInvitations(data ?? []);
    setLoading(false);
  };

  useEffect(() => {
    void loadInvitations();
    void getAllLeagues().then(setLeagues);
  }, []);

  const filteredInvitations = useMemo(
    () =>
      invitations.filter(
        (inv) => (!inv.isUsed || showUsed) && (inv.isUsed || !inv.isExpired || showExpired)
      ),
    [invitations, showUsed, showExpired]
  );

  // Path must match InvitationService.SendInvitationEmailAsync's registrationUrl (the emailed
  // link) — nothing enforces the two staying in sync, so update both if this route ever changes.
  const getInviteUrl = (invitation: InvitationDto) => {
    const url = new URL('/account/register', window.location.origin);
    url.searchParams.set('inviteCode', invitation.invitationCode);
    url.searchParams.set('returnUrl', '/');
    return url.toString();
  };

  const handleCreateInvitation = async () => {
    if (!email || !user?.userId) return;
    if (invitations.some((i) => i.email === email)) {
      toast.push(`An Invitation for ${email} already exists.`, 'warning');
      return;
    }
    setCreating(true);
    try {
      const leagueId = selectedLeagueId !== '' ? selectedLeagueId : null;
      // Email is sent server-side as part of creating the invitation — unless the address
      // already belongs to a registered user, in which case the backend creates a pending
      // membership invite instead (they'll see an in-app accept/decline banner, not an email).
      const result = await createInvitation(email, user.userId, leagueId);
      if (result.outcome === 'ExistingUserInvitePending') {
        toast.push(`${email} already has an account — they'll see a request to accept or decline in the app.`, 'success');
      } else {
        toast.push(`Invitation sent to ${email}`, 'success');
      }
      await loadInvitations();
      setEmail('');
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Error creating invitation'), 'error');
    } finally {
      setCreating(false);
    }
  };

  const handleCopy = async (invitation: InvitationDto) => {
    const url = getInviteUrl(invitation);
    await navigator.clipboard.writeText(url);
    toast.push(`Invitation URL copied`, 'info');
  };

  const handleDelete = async (invitation: InvitationDto) => {
    await deleteInvitation(invitation.id);
    toast.push(`Invitation for ${invitation.email} deleted.`, 'success');
    await loadInvitations();
  };

  const handleSendEmail = async (invitation: InvitationDto) => {
    await resendInvitation(invitation.id);
    toast.push(`Invitation e-mail sent to ${invitation.email}`, 'success');
  };

  const activeCount = invitations.filter((inv) => !inv.isUsed && !inv.isExpired).length;
  const usedCount = invitations.filter((inv) => inv.isUsed).length;
  const expiredCount = invitations.filter((inv) => inv.isExpired && !inv.isUsed).length;

  return (
    <Box>
      <PageHeader title="Manage Invitations" />

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3 }}>
        <Paper sx={{ flex: 1, p: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
          <Typography variant="h6" color="info.main" sx={{ fontWeight: 700 }}>
            {activeCount}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Active Codes
          </Typography>
        </Paper>
        <Paper sx={{ flex: 1, p: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
          <Typography variant="h6" color="success.main" sx={{ fontWeight: 700 }}>
            {usedCount}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Used
          </Typography>
        </Paper>
        <Paper sx={{ flex: 1, p: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
          <Typography variant="h6" color="error.main" sx={{ fontWeight: 700 }}>
            {expiredCount}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Expired
          </Typography>
        </Paper>
      </Stack>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h5" sx={{ mb: 2 }}>
            Create New Invitation
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems="center">
            <TextField
              label="Email Address"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              fullWidth
            />
            <FormControl sx={{ minWidth: 180 }}>
              <InputLabel>League (optional)</InputLabel>
              <Select
                value={selectedLeagueId}
                label="League (optional)"
                onChange={(e) => setSelectedLeagueId(e.target.value as number | '')}
              >
                <MenuItem value=""><em>No league</em></MenuItem>
                {leagues.map((l) => (
                  <MenuItem key={l.id} value={l.id}>
                    {l.leagueName} ({l.leagueType.toUpperCase()})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button variant="contained" onClick={handleCreateInvitation} disabled={creating}>
              {creating ? 'Inviting...' : 'Invite'}
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" sx={{ mb: 2 }}>
          All Invitations
        </Typography>
        <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
          <Button variant="outlined" onClick={() => setShowUsed((prev) => !prev)}>
            {showUsed ? 'Hide Used' : 'Show Used'}
          </Button>
          <Button variant="outlined" onClick={() => setShowExpired((prev) => !prev)}>
            {showExpired ? 'Hide Expired' : 'Show Expired'}
          </Button>
        </Stack>

        {loading ? (
          <Stack alignItems="center">
            <CircularProgress />
          </Stack>
        ) : (
          <Box sx={{ overflowX: 'auto' }}><Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date Created</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>League</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Expires</TableCell>
                <TableCell>Used By</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filteredInvitations.map((invitation) => (
                <TableRow key={invitation.id}>
                  <TableCell>{new Date(invitation.createdAt).toLocaleString()}</TableCell>
                  <TableCell>{invitation.email}</TableCell>
                  <TableCell>{invitation.leagueName ?? '-'}</TableCell>
                  <TableCell>
                    <Chip size="small" {...getInvitationStatusChip(invitation)} />
                  </TableCell>
                  <TableCell>
                    {invitation.expiresAt ? new Date(invitation.expiresAt).toLocaleString() : 'Never'}
                  </TableCell>
                  <TableCell>{invitation.registeredUserName ?? '-'}</TableCell>
                  <TableCell>
                    {!invitation.isUsed && !invitation.isExpired && (
                      <>
                        <IconButton aria-label={`Copy invite link for ${invitation.email}`} onClick={() => handleCopy(invitation)}>
                          <ContentCopyIcon />
                        </IconButton>
                        <IconButton aria-label={`Resend invitation to ${invitation.email}`} onClick={() => handleSendEmail(invitation)}>
                          <EmailIcon />
                        </IconButton>
                      </>
                    )}
                    <IconButton onClick={() => handleDelete(invitation)}>
                      <DeleteIcon color="error" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table></Box>
        )}
      </Paper>
    </Box>
  );
}
