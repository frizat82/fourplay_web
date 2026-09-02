import { useCallback, useEffect, useState, type ReactNode } from 'react';
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import AddCircleIcon from '@mui/icons-material/AddCircle';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteIcon from '@mui/icons-material/Delete';
import IosShareIcon from '@mui/icons-material/IosShare';
import LinkIcon from '@mui/icons-material/Link';
import LinkOffIcon from '@mui/icons-material/LinkOff';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import PageHeader from '../components/PageHeader';
import OwnerCostSummary from '../components/OwnerCostSummary';
import { useSession } from '../services/session';
import { useSportContext } from '../services/sport';
import { useAuth } from '../services/auth';
import { useToast } from '../services/toast';
import { isAdmin } from '../utils/auth';
import { extractApiErrorMessage } from '../utils/apiError';
import { useShareLink } from '../utils/useShareLink';
import { useNumericField } from '../utils/useNumericField';
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  updateLeagueJuice,
  rollForwardJuice,
  removeLeagueMember,
  inviteToLeague,
  generateInviteLink,
  revokeInviteLink,
  getCurrentInviteLink,
  getLeagueInvitations,
  getLeagueMembershipInvites,
  cancelMembershipInvite,
  getAllLeagues,
  getUsers,
  createLeague,
  addLeagueUserMapping,
  assignLeagueOwner,
  deleteLeague,
  type LeagueInviteLinkDto,
  type InvitationDto,
  type MembershipInviteStatusDto,
} from '../api/league';
import type { LeagueInfoDto, LeagueJuiceMappingDto, LeagueCostDto, UserSummaryDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';
import { computeLeagueCost } from '../utils/leagueHelpers';
import { stickyColumnSx } from '../utils/tableStyles';

const CURRENT_SEASON = new Date().getFullYear();

const userLabel = (u: UserSummaryDto) => u.email ?? u.userName ?? u.id;

/** Shared <option> list for the three admin user-picker dialogs (Create League owner, Add User, Assign Owner). */
function UserOptions({ users }: { users: UserSummaryDto[] }) {
  return (
    <>
      {users.map((u) => (
        <option key={u.id} value={u.id}>
          {userLabel(u)}
        </option>
      ))}
    </>
  );
}

export default function LeaguePortalPage() {
  const { ownedLeagues, leaguesLoaded, reloadLeagues, currentLeague } = useSession();
  const { isCfb } = useSportContext();
  const { user } = useAuth();
  const admin = isAdmin(user);
  const toast = useToast();
  // The wire-format LeagueType for whichever sport's subdomain we're currently on — shared by
  // the platform-wide league filter below and Create League's locked Sport field.
  const currentLeagueType = isCfb ? 'Cfb' : 'Nfl';

  const [allLeagues, setAllLeagues] = useState<LeagueInfoDto[]>([]);
  const [allLeaguesLoaded, setAllLeaguesLoaded] = useState(false);
  // Admins see every league platform-wide (allLeagues), but still only for the sport they're
  // currently on — ownedLeagues already applies this same filter for non-admins (session.tsx).
  const leagueOptions = admin
    ? allLeagues.filter((l) => l.leagueType === currentLeagueType)
    : ownedLeagues;
  // Guards the empty state against the pre-fetch window — without it, an admin with leagues
  // platform-wide would see a false "no leagues yet" flash while allLeagues is still [].
  const optionsLoaded = admin ? allLeaguesLoaded : leaguesLoaded;

  const [selectedLeague, setSelectedLeague] = useState<LeagueInfoDto | null>(null);
  const [tab, setTab] = useState(0);

  // Members
  const [members, setMembers] = useState<LeagueUserMappingDto[]>([]);
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<LeagueUserMappingDto | null>(null);
  const [removing, setRemoving] = useState(false);

  // Email invite dialog
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviting, setInviting] = useState(false);

  // Shareable invite link
  const [generatingLink, setGeneratingLink] = useState(false);
  const [revokingLink, setRevokingLink] = useState(false);
  const [inviteLink, setInviteLink] = useState<LeagueInviteLinkDto | null>(null);

  // Email invitations sent to this league
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  // Pending/accepted/declined invites sent to already-registered users for this league
  const [membershipInvites, setMembershipInvites] = useState<MembershipInviteStatusDto[]>([]);
  const [cancelingMembershipInviteId, setCancelingMembershipInviteId] = useState<number | null>(null);

  // Juice settings
  const [juiceMappings, setJuiceMappings] = useState<LeagueJuiceMappingDto[]>([]);
  const [selectedSeason, setSelectedSeason] = useState(CURRENT_SEASON);
  const [juiceForm, setJuiceForm] = useState({ juice: 0, juiceDivisional: 0, juiceConference: 0, weeklyCost: 0 });
  const [savingJuice, setSavingJuice] = useState(false);
  const [rollingForward, setRollingForward] = useState(false);

  // Cost
  const [costDto, setCostDto] = useState<LeagueCostDto | null>(null);

  // Admin: platform user list, backs Create League / Add User / Assign Owner (see effect below)
  const [availableUsers, setAvailableUsers] = useState<UserSummaryDto[]>([]);

  // Admin: Create League dialog
  const [createLeagueOpen, setCreateLeagueOpen] = useState(false);
  const [newLeagueForm, setNewLeagueForm] = useState({ leagueName: '', leagueType: 'Nfl', ownerUserId: '' });
  const [creatingLeague, setCreatingLeague] = useState(false);

  // Admin: Add User dialog (Members tab)
  const [addUserOpen, setAddUserOpen] = useState(false);
  const [addUserTarget, setAddUserTarget] = useState<UserSummaryDto | null>(null);
  const [addingUser, setAddingUser] = useState(false);

  // Admin: Assign Owner dialog (Info tab)
  const [assignOwnerOpen, setAssignOwnerOpen] = useState(false);
  const [newOwnerId, setNewOwnerId] = useState('');
  const [assigningOwner, setAssigningOwner] = useState(false);

  // Owner or admin: Delete League dialog (Info tab) — type-to-confirm since this permanently
  // removes the league's members, payout settings, picks, and invitations along with it.
  const [deleteLeagueOpen, setDeleteLeagueOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deletingLeague, setDeletingLeague] = useState(false);

  const loadAllLeagues = useCallback(async () => {
    if (!admin) return;
    const leagues = await getAllLeagues();
    setAllLeagues(leagues);
    setAllLeaguesLoaded(true);
  }, [admin]);

  useEffect(() => { void loadAllLeagues(); }, [loadAllLeagues]);

  // Platform user list backs all three admin dialogs (Create League, Add User, Assign Owner) —
  // fetched once per admin visit rather than on every dialog open. Without a .catch(), a failed
  // fetch left availableUsers at [] for the whole session with no error and no retry — all three
  // pickers looked "broken" with nothing to select and no indication why.
  useEffect(() => {
    if (!admin) return;
    getUsers()
      .then(setAvailableUsers)
      .catch((err) => {
        console.error('Failed to load platform user list', err);
        toast.push('Failed to load users — try reloading the page', 'error');
      });
  }, [admin, toast]);

  useEffect(() => {
    if (leagueOptions.length > 0 && !selectedLeague) {
      // Default to whichever league is active in the top-right league switcher, if you happen to
      // administer it — so "My Leagues" opens already showing the league you were just looking
      // at instead of an arbitrary one. Falls back to the first owned league if you're not
      // currently in one you administer (e.g. picking in a league you're a member of, but not
      // its commissioner).
      const matchingCurrent = leagueOptions.find((l) => l.id === currentLeague);
      setSelectedLeague(matchingCurrent ?? leagueOptions[0]);
    }
  }, [leagueOptions, selectedLeague, currentLeague]);

  const loadMembers = useCallback(async (leagueId: number) => {
    setLoadingMembers(true);
    try {
      const [m, cost] = await Promise.all([
        getLeagueUserMappings(leagueId),
        getLeagueCost(leagueId),
      ]);
      setMembers(m);
      setCostDto(cost);
    } finally {
      setLoadingMembers(false);
    }
  }, []);

  const loadJuice = useCallback(async (leagueId: number) => {
    const mappings = await getLeagueJuice(leagueId);
    setJuiceMappings(mappings);
    const mapping = mappings.find((m) => m.season === selectedSeason);
    if (mapping) {
      setJuiceForm({
        juice: mapping.juice,
        juiceDivisional: mapping.juiceDivisional,
        juiceConference: mapping.juiceConference,
        weeklyCost: mapping.weeklyCost,
      });
    } else {
      setJuiceForm({ juice: 0, juiceDivisional: 0, juiceConference: 0, weeklyCost: 0 });
    }
  }, [selectedSeason]);

  useEffect(() => {
    if (!selectedLeague) return;
    void loadMembers(selectedLeague.id);
    void loadJuice(selectedLeague.id);
    getCurrentInviteLink(selectedLeague.id).then(setInviteLink).catch(() => setInviteLink(null));
    getLeagueInvitations(selectedLeague.id).then(setInvitations).catch(() => setInvitations([]));
    getLeagueMembershipInvites(selectedLeague.id).then(setMembershipInvites).catch(() => setMembershipInvites([]));
  }, [selectedLeague, loadMembers, loadJuice]);

  const handleRemove = async () => {
    if (!removeTarget || !selectedLeague) return;
    setRemoving(true);
    try {
      await removeLeagueMember(selectedLeague.id, removeTarget.userId);
      toast.push(`${removeTarget.userName ?? removeTarget.userId} removed`, 'success');
      setRemoveTarget(null);
      await loadMembers(selectedLeague.id);
    } catch {
      toast.push('Failed to remove member', 'error');
    } finally {
      setRemoving(false);
    }
  };

  const handleInvite = async () => {
    if (!selectedLeague || !inviteEmail.trim()) return;
    setInviting(true);
    try {
      const result = await inviteToLeague(selectedLeague.id, inviteEmail.trim());
      if (result.outcome === 'ExistingUserInvitePending') {
        toast.push(`Invite sent to ${inviteEmail} — pending their acceptance`, 'success');
        getLeagueMembershipInvites(selectedLeague.id).then(setMembershipInvites).catch(() => {});
      } else {
        toast.push(`Invitation sent to ${inviteEmail}`, 'success');
        getLeagueInvitations(selectedLeague.id).then(setInvitations).catch(() => {});
      }
      setInviteEmail('');
      setInviteOpen(false);
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Failed to send invitation'), 'error');
    } finally {
      setInviting(false);
    }
  };

  const handleCancelMembershipInvite = async (id: number) => {
    if (!selectedLeague) return;
    setCancelingMembershipInviteId(id);
    try {
      await cancelMembershipInvite(id);
      toast.push('Invite canceled', 'success');
      getLeagueMembershipInvites(selectedLeague.id).then(setMembershipInvites).catch(() => {});
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Failed to cancel invite'), 'error');
    } finally {
      setCancelingMembershipInviteId(null);
    }
  };

  const handleSaveJuice = async () => {
    if (!selectedLeague) return;
    setSavingJuice(true);
    try {
      await updateLeagueJuice(selectedLeague.id, selectedSeason, juiceForm);
      toast.push('Juice settings saved', 'success');
      await loadJuice(selectedLeague.id);
    } catch {
      toast.push('Failed to save juice settings', 'error');
    } finally {
      setSavingJuice(false);
    }
  };

  const handleGenerateInviteLink = async () => {
    if (!selectedLeague) return;
    setGeneratingLink(true);
    try {
      const link = await generateInviteLink(selectedLeague.id);
      setInviteLink(link);
    } catch {
      toast.push('Failed to generate invite link', 'error');
    } finally {
      setGeneratingLink(false);
    }
  };

  const handleRevokeInviteLink = async () => {
    if (!selectedLeague) return;
    setRevokingLink(true);
    try {
      await revokeInviteLink(selectedLeague.id);
      setInviteLink(null);
      toast.push('Invite link revoked', 'success');
    } catch (error) {
      toast.push(extractApiErrorMessage(error, 'Failed to revoke invite link'), 'error');
    } finally {
      setRevokingLink(false);
    }
  };

  const handleRollForward = async () => {
    if (!selectedLeague) return;
    setRollingForward(true);
    try {
      await rollForwardJuice(selectedLeague.id, selectedSeason);
      toast.push(`Juice copied to ${selectedSeason}`, 'success');
      await loadJuice(selectedLeague.id);
    } catch {
      toast.push('Failed to roll forward juice', 'error');
    } finally {
      setRollingForward(false);
    }
  };

  const openCreateLeague = () => {
    setNewLeagueForm({ leagueName: '', leagueType: currentLeagueType, ownerUserId: user?.userId ?? '' });
    setCreateLeagueOpen(true);
  };

  const handleCreateLeague = async () => {
    if (!newLeagueForm.leagueName.trim() || !newLeagueForm.ownerUserId) return;
    setCreatingLeague(true);
    try {
      const created = await createLeague({
        leagueName: newLeagueForm.leagueName.trim(),
        leagueType: newLeagueForm.leagueType,
        ownerUserId: newLeagueForm.ownerUserId,
        season: CURRENT_SEASON,
        juice: 0,
        juiceDivisional: 0,
        juiceConference: 0,
        weeklyCost: 0,
      });
      toast.push(`League "${created.leagueName}" created`, 'success');
      setCreateLeagueOpen(false);
      await Promise.all([loadAllLeagues(), reloadLeagues()]);
      setSelectedLeague(created);
    } catch {
      toast.push('Failed to create league', 'error');
    } finally {
      setCreatingLeague(false);
    }
  };

  const openAddUser = () => {
    setAddUserTarget(null);
    setAddUserOpen(true);
  };

  const handleAddUser = async () => {
    if (!addUserTarget || !selectedLeague) return;
    if (members.some((m) => m.userId === addUserTarget.id)) {
      toast.push(`${addUserTarget.email ?? addUserTarget.userName} is already in this league`, 'warning');
      return;
    }
    setAddingUser(true);
    try {
      await addLeagueUserMapping(selectedLeague.id, addUserTarget.id);
      toast.push(`${addUserTarget.email ?? addUserTarget.userName} added to league`, 'success');
      setAddUserOpen(false);
      await loadMembers(selectedLeague.id);
    } catch {
      toast.push('Failed to add user', 'error');
    } finally {
      setAddingUser(false);
    }
  };

  const openAssignOwner = () => {
    setNewOwnerId('');
    setAssignOwnerOpen(true);
  };

  const handleAssignOwner = async () => {
    if (!selectedLeague || !newOwnerId) return;
    setAssigningOwner(true);
    try {
      await assignLeagueOwner(selectedLeague.id, newOwnerId);
      toast.push('Owner updated', 'success');
      setAssignOwnerOpen(false);
      await loadAllLeagues();
    } catch {
      toast.push('Failed to assign owner', 'error');
    } finally {
      setAssigningOwner(false);
    }
  };

  const openDeleteLeague = () => {
    setDeleteConfirmText('');
    setDeleteLeagueOpen(true);
  };

  const handleDeleteLeague = async () => {
    if (!selectedLeague) return;
    setDeletingLeague(true);
    try {
      await deleteLeague(selectedLeague.id);
      toast.push(`League "${selectedLeague.leagueName}" deleted`, 'success');
      setDeleteLeagueOpen(false);
      setSelectedLeague(null);
      await Promise.all([loadAllLeagues(), reloadLeagues()]);
    } catch {
      toast.push('Failed to delete league', 'error');
    } finally {
      setDeletingLeague(false);
    }
  };

  const currentJuiceMapping = juiceMappings.find((m) => m.season === selectedSeason);
  const availableSeasons = juiceMappings.map((m) => m.season).sort((a, b) => b - a);
  if (!availableSeasons.includes(CURRENT_SEASON)) availableSeasons.unshift(CURRENT_SEASON);

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      <Stack direction="row" alignItems="flex-start" justifyContent="space-between" flexWrap="wrap" gap={2}>
        <PageHeader
          title="My Leagues"
          subtitle={selectedLeague ? selectedLeague.leagueName : 'Commissioner portal'}
        />
        <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
          <OwnerCostSummary />
          <Button startIcon={<AddCircleIcon />} variant="outlined" onClick={openCreateLeague}>
            Create League
          </Button>
        </Stack>
      </Stack>

      {optionsLoaded && leagueOptions.length === 0 && (
        <Box sx={{ p: 4, textAlign: 'center' }}>
          <Typography variant="h6" color="text.secondary">
            You don&apos;t have any leagues yet.
          </Typography>
          <Typography color="text.secondary">
            Create one to start picking against your friends — you&apos;ll be its commissioner.
          </Typography>
        </Box>
      )}

      {leagueOptions.length > 1 && (
        // frizat: mt matches the header Stack's own gap={2} above — without it, this sits flush
        // against whatever the header wraps to at narrow widths (its floating "League" label was
        // reported overlapping the Create League button on iOS, since this select renders above
        // all three tabs and is visible regardless of which one is active).
        <FormControl sx={{ mt: 2, mb: 3, minWidth: 240 }} size="small">
          <InputLabel>League</InputLabel>
          <Select
            value={selectedLeague?.id ?? ''}
            label="League"
            onChange={(e) => {
              const league = leagueOptions.find((l) => l.id === Number(e.target.value));
              if (league) setSelectedLeague(league);
            }}
          >
            {leagueOptions.map((l) => (
              <MenuItem key={l.id} value={l.id}>
                {l.leagueName} {admin && `(${l.leagueType.toUpperCase()})`}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      )}

      {selectedLeague && (
        <>
          <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ mb: 2 }}>
            <Tab label="Members" />
            <Tab label="Settings" />
            <Tab label="Info" />
          </Tabs>

          {tab === 0 && (
            <MembersTab
              members={members}
              loading={loadingMembers}
              costDto={costDto}
              isAdmin={admin}
              onRemove={setRemoveTarget}
              onInvite={() => setInviteOpen(true)}
              onAddUser={openAddUser}
              inviteLink={inviteLink}
              generatingLink={generatingLink}
              revokingLink={revokingLink}
              onGenerateInviteLink={() => void handleGenerateInviteLink()}
              onRevokeInviteLink={() => void handleRevokeInviteLink()}
              invitations={invitations}
              membershipInvites={membershipInvites}
              cancelingMembershipInviteId={cancelingMembershipInviteId}
              onCancelMembershipInvite={(id) => void handleCancelMembershipInvite(id)}
            />
          )}
          {tab === 1 && (
            <JuiceTab
              availableSeasons={availableSeasons}
              selectedSeason={selectedSeason}
              onSeasonChange={setSelectedSeason}
              juiceForm={juiceForm}
              onJuiceFormChange={(field, value) => setJuiceForm((f) => ({ ...f, [field]: value }))}
              hasMappingForSeason={!!currentJuiceMapping}
              locked={selectedSeason < CURRENT_SEASON}
              onSave={handleSaveJuice}
              onRollForward={handleRollForward}
              saving={savingJuice}
              rollingForward={rollingForward}
            />
          )}
          {tab === 2 && (
            <InfoTab
              league={selectedLeague}
              isAdmin={admin}
              canDelete={admin || selectedLeague.ownerUserId === user?.userId}
              onChangeOwner={openAssignOwner}
              onDeleteLeague={openDeleteLeague}
            />
          )}
        </>
      )}

      <Dialog open={!!removeTarget} onClose={() => setRemoveTarget(null)}>
        <DialogTitle>Remove Member</DialogTitle>
        <DialogContent>
          <Typography>
            Remove <strong>{removeTarget?.userName ?? removeTarget?.userId}</strong> from{' '}
            <strong>{selectedLeague?.leagueName}</strong>? Their pick history is kept, and they
            can be re-added later.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRemoveTarget(null)}>Cancel</Button>
          <Button color="error" onClick={() => void handleRemove()} disabled={removing}>
            {removing ? <CircularProgress size={18} /> : 'Remove'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={inviteOpen} onClose={() => setInviteOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Invite Player</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="Email address"
            type="email"
            fullWidth
            variant="outlined"
            sx={{ mt: 1 }}
            value={inviteEmail}
            onChange={(e) => setInviteEmail(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') void handleInvite(); }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setInviteOpen(false); setInviteEmail(''); }}>Cancel</Button>
          <Button variant="contained" onClick={() => void handleInvite()} disabled={inviting || !inviteEmail.trim()}>
            {inviting ? <CircularProgress size={18} /> : 'Send Invite'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={createLeagueOpen} onClose={() => setCreateLeagueOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Create League</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              autoFocus
              label="League Name"
              value={newLeagueForm.leagueName}
              onChange={(e) => setNewLeagueForm((f) => ({ ...f, leagueName: e.target.value }))}
            />
            {/* frizat: the site you're on IS the sport you're creating for — locked, not a
                choice, so there's no way to accidentally create a CFB league from the NFL
                site (or vice versa) the way the old editable dropdown allowed. */}
            <TextField
              label="Sport"
              value={isCfb ? 'CFB' : 'NFL'}
              disabled
            />
            {admin && (
              <TextField
                select
                label="Owner"
                SelectProps={{ native: true }}
                value={newLeagueForm.ownerUserId}
                onChange={(e) => setNewLeagueForm((f) => ({ ...f, ownerUserId: e.target.value }))}
              >
                <UserOptions users={availableUsers} />
              </TextField>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateLeagueOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => void handleCreateLeague()}
            disabled={creatingLeague || !newLeagueForm.leagueName.trim() || !newLeagueForm.ownerUserId}
          >
            {creatingLeague ? <CircularProgress size={18} /> : 'Create League'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={addUserOpen} onClose={() => setAddUserOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Add User to {selectedLeague?.leagueName}</DialogTitle>
        <DialogContent>
          <TextField
            select
            label="User"
            SelectProps={{ native: true }}
            sx={{ mt: 1 }}
            fullWidth
            value={addUserTarget?.id ?? ''}
            onChange={(e) => setAddUserTarget(availableUsers.find((u) => u.id === e.target.value) ?? null)}
          >
            <option value="" />
            <UserOptions users={availableUsers} />
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddUserOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => void handleAddUser()} disabled={addingUser || !addUserTarget}>
            {addingUser ? <CircularProgress size={18} /> : 'Add User'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={assignOwnerOpen} onClose={() => setAssignOwnerOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Change Owner — {selectedLeague?.leagueName}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Current owner: {selectedLeague?.ownerUserId || 'none'}
            </Typography>
            <TextField
              select
              label="New Owner"
              SelectProps={{ native: true }}
              value={newOwnerId}
              onChange={(e) => setNewOwnerId(e.target.value)}
            >
              <option value="" />
              <UserOptions users={availableUsers} />
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssignOwnerOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => void handleAssignOwner()} disabled={assigningOwner || !newOwnerId}>
            {assigningOwner ? <CircularProgress size={18} /> : 'Assign'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={deleteLeagueOpen} onClose={() => setDeleteLeagueOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Delete League</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography>
              Deleting <strong>{selectedLeague?.leagueName}</strong> permanently removes its members,
              payout settings, picks, and invitations. This cannot be undone.
            </Typography>
            <TextField
              autoFocus
              label="Type the league name to confirm"
              value={deleteConfirmText}
              onChange={(e) => setDeleteConfirmText(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteLeagueOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            color="error"
            onClick={() => void handleDeleteLeague()}
            disabled={deletingLeague || deleteConfirmText !== selectedLeague?.leagueName}
          >
            {deletingLeague ? <CircularProgress size={18} /> : 'Delete League'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

interface MembersTabProps {
  members: LeagueUserMappingDto[];
  loading: boolean;
  costDto: LeagueCostDto | null;
  isAdmin: boolean;
  onRemove: (m: LeagueUserMappingDto) => void;
  onInvite: () => void;
  onAddUser: () => void;
  inviteLink: LeagueInviteLinkDto | null;
  generatingLink: boolean;
  revokingLink: boolean;
  onGenerateInviteLink: () => void;
  onRevokeInviteLink: () => void;
  invitations: InvitationDto[];
  membershipInvites: MembershipInviteStatusDto[];
  cancelingMembershipInviteId: number | null;
  onCancelMembershipInvite: (id: number) => void;
}

function MembersTab({ members, loading, costDto, isAdmin: admin, onRemove, onInvite, onAddUser, inviteLink, generatingLink, revokingLink, onGenerateInviteLink, onRevokeInviteLink, invitations, membershipInvites, cancelingMembershipInviteId, onCancelMembershipInvite }: MembersTabProps) {
  const count = costDto?.memberCount ?? members.length;
  const cost = computeLeagueCost(count);
  const { share, copy } = useShareLink();

  const inviteUrl = inviteLink ? `${window.location.origin}/join/${inviteLink.token}` : '';

  const linkExpired = inviteLink ? new Date(inviteLink.expiresAt) < new Date() : false;
  const expiresLabel = inviteLink
    ? linkExpired
      ? `Expired ${new Date(inviteLink.expiresAt).toLocaleString()}`
      : `Expires ${new Date(inviteLink.expiresAt).toLocaleString()}`
    : '';

  return (
    <Box>
      {/* useFlexGap: Stack's default margin-based spacing only separates items within the same
          flex line — with flexWrap="wrap" on a narrow (mobile) viewport, buttons that wrap onto
          their own line end up touching with zero gap. useFlexGap switches to real CSS `gap`,
          which applies between wrapped lines too. */}
      <Stack direction="row" spacing={2} useFlexGap alignItems="center" sx={{ mb: 2 }} flexWrap="wrap">
        <Chip label={`${count} member${count !== 1 ? 's' : ''} · $${cost}/season`} color="primary" variant="outlined" />
        <Button startIcon={<PersonAddIcon />} variant="outlined" size="small" onClick={onInvite}>
          Invite Player
        </Button>
        <Button
          startIcon={generatingLink ? <CircularProgress size={14} /> : <LinkIcon />}
          variant="outlined"
          size="small"
          onClick={onGenerateInviteLink}
          disabled={generatingLink}
        >
          {inviteLink ? 'Regenerate Link' : 'Generate Invite Link'}
        </Button>
        {admin && (
          <Button startIcon={<AddCircleIcon />} variant="outlined" size="small" onClick={onAddUser}>
            Add User
          </Button>
        )}
      </Stack>

      {/* frizat: real incident — an owner generated a share link meaning "blast it to my whole
          group," a member clicked it, and the owner was confused about what would happen next.
          Always-visible text, not a hover tooltip, since this is mobile-first and the confusion
          happens exactly when someone is about to click one of these buttons. Both mechanisms
          apply the identical accept/decline-vs-register rule (LeagueController.JoinViaLink
          routes an existing user through the same membershipInviteService.CreateOrReopenAsync
          as InviteToLeague) — the real difference is targeting: one email vs. one shareable
          link for a whole group. Don't say the link joins existing members instantly; it
          doesn't (only a brand-new visitor registering through either path does). */}
      <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5, maxWidth: 640 }}>
        Invite Player sends a request to one email — existing members get a request to accept or decline, new visitors register to join.
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3, maxWidth: 640 }}>
        Invite Link works the same way, but gives you one shareable link for your whole group instead of one email at a time.
      </Typography>

      {inviteLink && (
        <Box sx={{ mb: 3, p: 2, border: 1, borderColor: linkExpired ? 'warning.main' : 'divider', borderRadius: 1, maxWidth: 520 }}>
          <Stack spacing={1}>
            {linkExpired ? (
              <Typography variant="caption" color="warning.main" fontWeight="bold">Link expired — regenerate to share a new one</Typography>
            ) : (
              <Typography variant="body2" sx={{ wordBreak: 'break-all', fontFamily: 'monospace', fontSize: '0.78rem' }}>
                {inviteUrl}
              </Typography>
            )}
            {!linkExpired && (
              <Stack direction="row" spacing={1} flexWrap="wrap">
                <Button size="small" startIcon={<ContentCopyIcon />} variant="outlined" onClick={() => void copy(inviteUrl)}>
                  Copy
                </Button>
                <Button size="small" startIcon={<IosShareIcon />} variant="contained" color="secondary" onClick={() => share('Join my league', inviteUrl)}>
                  Share
                </Button>
                <Button
                  size="small"
                  startIcon={revokingLink ? <CircularProgress size={14} /> : <LinkOffIcon />}
                  variant="outlined"
                  color="error"
                  onClick={onRevokeInviteLink}
                  disabled={revokingLink}
                >
                  Revoke Link
                </Button>
              </Stack>
            )}
            <Typography variant="caption" color={linkExpired ? 'warning.main' : 'text.secondary'}>{expiresLabel}</Typography>
          </Stack>
        </Box>
      )}

      {loading ? (
        <CircularProgress />
      ) : (
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={stickyColumnSx}>Name</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Joined</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {members.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4}>
                    <Typography color="text.secondary" variant="body2">No members yet</Typography>
                  </TableCell>
                </TableRow>
              )}
              {members.map((m) => (
                <TableRow key={m.id}>
                  <TableCell sx={stickyColumnSx}>{m.userName ?? m.userId}</TableCell>
                  <TableCell>{m.email ?? m.userId}</TableCell>
                  <TableCell>{new Date(m.dateCreated).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
                      color="error"
                      startIcon={<DeleteIcon />}
                      onClick={() => onRemove(m)}
                    >
                      Remove
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      <InviteStatusTable
        title="Sent Invitations"
        rows={invitations.map((inv) => ({
          id: inv.id,
          email: inv.email,
          createdAt: inv.createdAt,
          chip: inv.isUsed
            ? (inv.registeredUserEmailConfirmed
              ? { label: 'Confirmed', color: 'success' as const }
              : { label: 'Pending Confirmation', color: 'warning' as const })
            : inv.isExpired
              ? { label: 'Expired', color: 'default' as const }
              : { label: 'Pending', color: 'warning' as const },
        }))}
      />

      <InviteStatusTable
        title="Invites to Existing Users"
        rows={membershipInvites.map((inv) => ({
          id: inv.id,
          email: inv.invitedUserEmail,
          createdAt: inv.createdAt,
          chip: inv.status === 'Accepted'
            ? { label: 'Accepted', color: 'success' as const }
            : inv.status === 'Declined'
              ? { label: 'Declined', color: 'error' as const }
              : { label: 'Pending', color: 'warning' as const },
          action: inv.status === 'Pending' ? (
            <Button
              size="small"
              color="error"
              disabled={cancelingMembershipInviteId === inv.id}
              onClick={() => onCancelMembershipInvite(inv.id)}
            >
              Cancel
            </Button>
          ) : null,
        }))}
        showActions
      />
    </Box>
  );
}

interface InviteStatusRow {
  id: number;
  email: string;
  createdAt: string;
  chip: { label: string; color: 'success' | 'warning' | 'error' | 'default' };
  action?: ReactNode;
}

/** Shared "Email / Sent / Status[ / Actions]" table for both the Sent Invitations (email invites
 * to new users) and Invites to Existing Users (membership invites) sections of the Members tab. */
function InviteStatusTable({ title, rows, showActions = false }: { title: string; rows: InviteStatusRow[]; showActions?: boolean }) {
  if (rows.length === 0) return null;
  return (
    <Box sx={{ mt: 3 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>{title}</Typography>
      <Box sx={{ overflowX: 'auto' }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Email</TableCell>
              <TableCell>Sent</TableCell>
              <TableCell>Status</TableCell>
              {showActions && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.id}>
                <TableCell>{row.email}</TableCell>
                <TableCell>{new Date(row.createdAt).toLocaleDateString()}</TableCell>
                <TableCell><Chip label={row.chip.label} color={row.chip.color} size="small" /></TableCell>
                {showActions && <TableCell align="right">{row.action}</TableCell>}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </Box>
  );
}

interface JuiceTabProps {
  availableSeasons: number[];
  selectedSeason: number;
  onSeasonChange: (s: number) => void;
  juiceForm: { juice: number; juiceDivisional: number; juiceConference: number; weeklyCost: number };
  onJuiceFormChange: (field: string, value: number) => void;
  hasMappingForSeason: boolean;
  locked: boolean;
  onSave: () => void;
  onRollForward: () => void;
  saving: boolean;
  rollingForward: boolean;
}

function JuiceTab({
  availableSeasons, selectedSeason, onSeasonChange, juiceForm, onJuiceFormChange,
  hasMappingForSeason, locked, onSave, onRollForward, saving, rollingForward,
}: JuiceTabProps) {
  // All 4 fields are backend `int` DTOs (Shared/Models/Data/Dtos/LeagueCreateDto.cs) — teaser
  // points and weekly cost are always whole numbers in this domain, so integerOnly strips a
  // typed decimal point instead of letting it through to a save that 400s.
  const juiceField = useNumericField(juiceForm.juice, (n) => onJuiceFormChange('juice', n), { integerOnly: true });
  const juiceDivisionalField = useNumericField(juiceForm.juiceDivisional, (n) => onJuiceFormChange('juiceDivisional', n), { integerOnly: true });
  const juiceConferenceField = useNumericField(juiceForm.juiceConference, (n) => onJuiceFormChange('juiceConference', n), { integerOnly: true });
  const weeklyCostField = useNumericField(juiceForm.weeklyCost, (n) => onJuiceFormChange('weeklyCost', n), { integerOnly: true });

  return (
    <Box>
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 3 }}>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel>Season</InputLabel>
          <Select
            value={selectedSeason}
            label="Season"
            onChange={(e) => onSeasonChange(Number(e.target.value))}
          >
            {availableSeasons.map((s) => (
              <MenuItem key={s} value={s}>{s}</MenuItem>
            ))}
          </Select>
        </FormControl>
        {!hasMappingForSeason && availableSeasons.length > 1 && (
          <Button variant="outlined" size="small" onClick={onRollForward} disabled={rollingForward}>
            {rollingForward ? <CircularProgress size={16} /> : `Copy from prior season`}
          </Button>
        )}
      </Stack>

      {locked && (
        <Typography color="text.secondary" variant="body2" sx={{ mb: 2 }}>
          {selectedSeason} has already been played — juice settings are locked to protect past results.
        </Typography>
      )}

      <Stack spacing={2} sx={{ maxWidth: 400 }}>
        <TextField
          label="Tease Pts (Regular Season)"
          type="number"
          size="small"
          disabled={locked}
          slotProps={{ htmlInput: { inputMode: 'numeric' } }}
          {...juiceField}
        />
        <TextField
          label="Tease Pts (Divisional)"
          type="number"
          size="small"
          disabled={locked}
          slotProps={{ htmlInput: { inputMode: 'numeric' } }}
          {...juiceDivisionalField}
        />
        <TextField
          label="Tease Pts (Conference)"
          type="number"
          size="small"
          disabled={locked}
          slotProps={{ htmlInput: { inputMode: 'numeric' } }}
          {...juiceConferenceField}
        />
        <TextField
          label="Cost Per Week ($)"
          type="number"
          size="small"
          disabled={locked}
          slotProps={{ htmlInput: { inputMode: 'numeric' } }}
          {...weeklyCostField}
        />
        <Button variant="contained" onClick={onSave} disabled={saving || locked} sx={{ alignSelf: 'flex-start' }}>
          {saving ? <CircularProgress size={18} /> : 'Save'}
        </Button>
      </Stack>
    </Box>
  );
}

function InfoTab({
  league, isAdmin: admin, canDelete, onChangeOwner, onDeleteLeague,
}: {
  league: LeagueInfoDto; isAdmin: boolean; canDelete: boolean; onChangeOwner: () => void; onDeleteLeague: () => void;
}) {
  return (
    <Box sx={{ maxWidth: 400 }}>
      <Stack spacing={1.5} divider={<Divider />}>
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">League name</Typography>
          <Typography fontWeight={600}>{league.leagueName}</Typography>
        </Stack>
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">Sport</Typography>
          <Typography fontWeight={600}>{league.leagueType.toUpperCase()}</Typography>
        </Stack>
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">Created</Typography>
          <Typography fontWeight={600}>{new Date(league.dateCreated).toLocaleDateString()}</Typography>
        </Stack>
        {admin && (
          <Stack direction="row" justifyContent="flex-end">
            <Button size="small" variant="outlined" onClick={onChangeOwner}>
              Change Owner
            </Button>
          </Stack>
        )}
        {canDelete && (
          <Stack direction="row" justifyContent="flex-end">
            <Button size="small" color="error" startIcon={<DeleteIcon />} onClick={onDeleteLeague}>
              Delete League
            </Button>
          </Stack>
        )}
      </Stack>
    </Box>
  );
}
