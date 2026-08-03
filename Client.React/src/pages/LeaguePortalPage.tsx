import { useCallback, useEffect, useState } from 'react';
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
import DeleteIcon from '@mui/icons-material/Delete';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import PageHeader from '../components/PageHeader';
import OwnerCostSummary from '../components/OwnerCostSummary';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useToast } from '../services/toast';
import { isAdmin } from '../utils/auth';
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  updateLeagueJuice,
  rollForwardJuice,
  removeLeagueMember,
  inviteToLeague,
  getAllLeagues,
  getUsers,
  createLeague,
  addLeagueUserMapping,
  assignLeagueOwner,
} from '../api/league';
import type { LeagueInfoDto, LeagueJuiceMappingDto, LeagueCostDto, UserSummaryDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';
import { computeLeagueCost } from '../utils/leagueHelpers';

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
  const { ownedLeagues, reloadLeagues } = useSession();
  const { user } = useAuth();
  const admin = isAdmin(user);
  const toast = useToast();

  const [allLeagues, setAllLeagues] = useState<LeagueInfoDto[]>([]);
  const leagueOptions = admin ? allLeagues : ownedLeagues;

  const [selectedLeague, setSelectedLeague] = useState<LeagueInfoDto | null>(null);
  const [tab, setTab] = useState(0);

  // Members
  const [members, setMembers] = useState<LeagueUserMappingDto[]>([]);
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [removeTarget, setRemoveTarget] = useState<LeagueUserMappingDto | null>(null);
  const [removing, setRemoving] = useState(false);

  // Invite dialog
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviting, setInviting] = useState(false);

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

  const loadAllLeagues = useCallback(async () => {
    if (!admin) return;
    const leagues = await getAllLeagues();
    setAllLeagues(leagues);
  }, [admin]);

  useEffect(() => { void loadAllLeagues(); }, [loadAllLeagues]);

  // Platform user list backs all three admin dialogs (Create League, Add User, Assign Owner) —
  // fetched once per admin visit rather than on every dialog open.
  useEffect(() => {
    if (!admin) return;
    void getUsers().then(setAvailableUsers);
  }, [admin]);

  useEffect(() => {
    if (leagueOptions.length > 0 && !selectedLeague) {
      setSelectedLeague(leagueOptions[0]);
    }
  }, [leagueOptions, selectedLeague]);

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
      await inviteToLeague(selectedLeague.id, inviteEmail.trim());
      toast.push(`Invitation sent to ${inviteEmail}`, 'success');
      setInviteEmail('');
      setInviteOpen(false);
    } catch {
      toast.push('Failed to send invitation', 'error');
    } finally {
      setInviting(false);
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
    setNewLeagueForm({ leagueName: '', leagueType: 'Nfl', ownerUserId: user?.userId ?? '' });
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

  const currentJuiceMapping = juiceMappings.find((m) => m.season === selectedSeason);
  const availableSeasons = juiceMappings.map((m) => m.season).sort((a, b) => b - a);
  if (!availableSeasons.includes(CURRENT_SEASON)) availableSeasons.unshift(CURRENT_SEASON);

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      <Stack direction="row" alignItems="flex-start" justifyContent="space-between" flexWrap="wrap" gap={2}>
        <PageHeader title="My Leagues" subtitle="Commissioner portal" />
        <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
          <OwnerCostSummary />
          <Button startIcon={<AddCircleIcon />} variant="outlined" onClick={openCreateLeague}>
            Create League
          </Button>
        </Stack>
      </Stack>

      {leagueOptions.length === 0 && (
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
        <FormControl sx={{ mb: 3, minWidth: 240 }} size="small">
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
                {l.leagueName} {admin && `(${l.leagueType})`}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      )}

      {selectedLeague && (
        <>
          <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ mb: 2 }}>
            <Tab label="Members" />
            <Tab label="Juice Settings" />
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
            <InfoTab league={selectedLeague} isAdmin={admin} onChangeOwner={openAssignOwner} />
          )}
        </>
      )}

      <Dialog open={!!removeTarget} onClose={() => setRemoveTarget(null)}>
        <DialogTitle>Remove Member</DialogTitle>
        <DialogContent>
          <Typography>
            Remove <strong>{removeTarget?.userName ?? removeTarget?.userId}</strong> from{' '}
            <strong>{selectedLeague?.leagueName}</strong>? This cannot be undone.
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
            <FormControl size="small">
              <InputLabel>Sport</InputLabel>
              <Select
                value={newLeagueForm.leagueType}
                label="Sport"
                onChange={(e) => setNewLeagueForm((f) => ({ ...f, leagueType: e.target.value }))}
              >
                <MenuItem value="Nfl">NFL</MenuItem>
                <MenuItem value="Cfb">CFB</MenuItem>
              </Select>
            </FormControl>
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
}

function MembersTab({ members, loading, costDto, isAdmin: admin, onRemove, onInvite, onAddUser }: MembersTabProps) {
  const count = costDto?.memberCount ?? members.length;
  const cost = computeLeagueCost(count);

  return (
    <Box>
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }} flexWrap="wrap">
        <Chip label={`${count} member${count !== 1 ? 's' : ''} · $${cost}/season`} color="primary" variant="outlined" />
        <Button startIcon={<PersonAddIcon />} variant="outlined" size="small" onClick={onInvite}>
          Invite Player
        </Button>
        {admin && (
          <Button startIcon={<AddCircleIcon />} variant="outlined" size="small" onClick={onAddUser}>
            Add User
          </Button>
        )}
      </Stack>
      {loading ? (
        <CircularProgress />
      ) : (
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
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
                  <TableCell>{m.userName ?? m.userId}</TableCell>
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
          value={juiceForm.juice}
          onChange={(e) => onJuiceFormChange('juice', Number(e.target.value))}
        />
        <TextField
          label="Tease Pts (Divisional)"
          type="number"
          size="small"
          disabled={locked}
          value={juiceForm.juiceDivisional}
          onChange={(e) => onJuiceFormChange('juiceDivisional', Number(e.target.value))}
        />
        <TextField
          label="Tease Pts (Conference)"
          type="number"
          size="small"
          disabled={locked}
          value={juiceForm.juiceConference}
          onChange={(e) => onJuiceFormChange('juiceConference', Number(e.target.value))}
        />
        <TextField
          label="Weekly Cost ($)"
          type="number"
          size="small"
          disabled={locked}
          value={juiceForm.weeklyCost}
          onChange={(e) => onJuiceFormChange('weeklyCost', Number(e.target.value))}
        />
        <Button variant="contained" onClick={onSave} disabled={saving || locked} sx={{ alignSelf: 'flex-start' }}>
          {saving ? <CircularProgress size={18} /> : 'Save'}
        </Button>
      </Stack>
    </Box>
  );
}

function InfoTab({ league, isAdmin: admin, onChangeOwner }: { league: LeagueInfoDto; isAdmin: boolean; onChangeOwner: () => void }) {
  return (
    <Box sx={{ maxWidth: 400 }}>
      <Stack spacing={1.5} divider={<Divider />}>
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">League name</Typography>
          <Typography fontWeight={600}>{league.leagueName}</Typography>
        </Stack>
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">Sport</Typography>
          <Typography fontWeight={600}>{league.leagueType}</Typography>
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
      </Stack>
    </Box>
  );
}
