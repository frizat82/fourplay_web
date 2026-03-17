import { useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  IconButton,
  Paper,
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
import { createInvitation, deleteInvitation, getAllInvitations, sendEmail } from '../../api/invitations';
import type { InvitationDto } from '../../types/admin';

export default function AdminInvitationsPage() {
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showUsed, setShowUsed] = useState(true);
  const [showExpired, setShowExpired] = useState(true);
  const [email, setEmail] = useState('');
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
  }, []);

  const filteredInvitations = useMemo(
    () =>
      invitations.filter(
        (inv) => (!inv.isUsed || showUsed) && (inv.isUsed || !inv.isExpired || showExpired)
      ),
    [invitations, showUsed, showExpired]
  );

  const getInviteUrl = (invitation: InvitationDto) => {
    const url = new URL('/account/register', window.location.origin);
    url.searchParams.set('inviteCode', invitation.invitationCode);
    url.searchParams.set('returnUrl', '/');
    return url.toString();
  };

  const generateInvitationEmailHtml = (invitation: InvitationDto) => {
    const registrationUrl = getInviteUrl(invitation);
    return `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>You're Invited</title>
  <style>
    body { font-family: Arial, sans-serif; background-color: #f4f4f7; margin: 0; padding: 0; }
    .container { max-width: 600px; margin: auto; background-color: #ffffff; padding: 40px; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }
    h1 { color: #333333; }
    p { color: #555555; line-height: 1.6; }
    .button { display: inline-block; padding: 12px 24px; margin-top: 20px; background-color: #1976d2; color: #ffffff !important; text-decoration: none !important; border-radius: 4px; font-weight: bold; }
    .footer { margin-top: 40px; font-size: 12px; color: #999999; text-align: center; }
  </style>
</head>
<body>
  <div class="container">
    <h1>You're Invited to Join!</h1>
    <p>Hello,</p>
    <p>
      You've been invited to join FourPlay at <strong>${window.location.origin}</strong>.
      Click the button below to create your account and get started.
    </p>
    <a href="${registrationUrl}" class="button">Create Your Account</a>
    <p>If you didn’t request this invite, you can safely ignore this email.</p>
    <div class="footer">&copy; 2025 YourWebsite. All rights reserved.</div>
  </div>
</body>
</html>`;
  };

  const handleCreateInvitation = async () => {
    if (!email || !user?.userId) return;
    if (invitations.some((i) => i.email === email)) {
      toast.push(`An Invitation for ${email} already exists.`, 'warning');
      return;
    }
    setCreating(true);
    try {
      const invitation = await createInvitation(email, user.userId);
      toast.push(`Invitation created for ${email}`, 'success');
      await sendEmail({
        toEmail: invitation.email,
        subject: 'FourPlay Invitation',
        htmlBody: generateInvitationEmailHtml(invitation),
      });
      await loadInvitations();
      setEmail('');
    } catch {
      toast.push('Error creating invitation', 'error');
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
    await sendEmail({
      toEmail: invitation.email,
      subject: 'FourPlay Invitation',
      htmlBody: generateInvitationEmailHtml(invitation),
    });
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
            <Button variant="contained" onClick={handleCreateInvitation} disabled={creating}>
              {creating ? 'Creating...' : 'Create Invitation'}
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
                  <TableCell>
                    {invitation.isUsed ? (
                      <Chip size="small" label="Used" color="success" />
                    ) : invitation.isExpired ? (
                      <Chip size="small" label="Expired" color="error" />
                    ) : (
                      <Chip size="small" label="Active" color="info" />
                    )}
                  </TableCell>
                  <TableCell>
                    {invitation.expiresAt ? new Date(invitation.expiresAt).toLocaleString() : 'Never'}
                  </TableCell>
                  <TableCell>{invitation.registeredUserName ?? '-'}</TableCell>
                  <TableCell>
                    {!invitation.isUsed && !invitation.isExpired && (
                      <>
                        <IconButton onClick={() => handleCopy(invitation)}>
                          <ContentCopyIcon />
                        </IconButton>
                        <IconButton onClick={() => handleSendEmail(invitation)}>
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
