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
import DeleteIcon from '@mui/icons-material/Delete';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import PageHeader from '../components/PageHeader';
import { useSession } from '../services/session';
import { useToast } from '../services/toast';
import {
  getLeagueUserMappings,
  getLeagueJuice,
  getLeagueCost,
  updateLeagueJuice,
  rollForwardJuice,
  removeLeagueMember,
  inviteToLeague,
} from '../api/league';
import type { LeagueInfoDto, LeagueJuiceMappingDto, LeagueCostDto } from '../types/admin';
import type { LeagueUserMappingDto } from '../types/league';
import { computeLeagueCost } from '../utils/leagueHelpers';

const CURRENT_SEASON = new Date().getFullYear();

export default function LeaguePortalPage() {
  const { ownedLeagues, isLeagueOwner } = useSession();
  const toast = useToast();

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

  useEffect(() => {
    if (ownedLeagues.length > 0 && !selectedLeague) {
      setSelectedLeague(ownedLeagues[0]);
    }
  }, [ownedLeagues, selectedLeague]);

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

  const currentJuiceMapping = juiceMappings.find((m) => m.season === selectedSeason);
  const availableSeasons = juiceMappings.map((m) => m.season).sort((a, b) => b - a);
  if (!availableSeasons.includes(CURRENT_SEASON)) availableSeasons.unshift(CURRENT_SEASON);

  if (!isLeagueOwner) {
    return (
      <Box sx={{ p: 4, textAlign: 'center' }}>
        <Typography variant="h6" color="text.secondary">
          You don&apos;t own any leagues for this sport.
        </Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      <PageHeader title="My Leagues" subtitle="Commissioner portal" />

      {ownedLeagues.length > 1 && (
        <FormControl sx={{ mb: 3, minWidth: 240 }} size="small">
          <InputLabel>League</InputLabel>
          <Select
            value={selectedLeague?.id ?? ''}
            label="League"
            onChange={(e) => {
              const league = ownedLeagues.find((l) => l.id === Number(e.target.value));
              if (league) setSelectedLeague(league);
            }}
          >
            {ownedLeagues.map((l) => (
              <MenuItem key={l.id} value={l.id}>
                {l.leagueName}
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
              onRemove={setRemoveTarget}
              onInvite={() => setInviteOpen(true)}
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
              onSave={handleSaveJuice}
              onRollForward={handleRollForward}
              saving={savingJuice}
              rollingForward={rollingForward}
            />
          )}
          {tab === 2 && <InfoTab league={selectedLeague} />}
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
    </Box>
  );
}

interface MembersTabProps {
  members: LeagueUserMappingDto[];
  loading: boolean;
  costDto: LeagueCostDto | null;
  onRemove: (m: LeagueUserMappingDto) => void;
  onInvite: () => void;
}

function MembersTab({ members, loading, costDto, onRemove, onInvite }: MembersTabProps) {
  const count = costDto?.memberCount ?? members.length;
  const cost = computeLeagueCost(count);

  return (
    <Box>
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
        <Chip label={`${count} member${count !== 1 ? 's' : ''} · $${cost}/season`} color="primary" variant="outlined" />
        <Button startIcon={<PersonAddIcon />} variant="outlined" size="small" onClick={onInvite}>
          Invite Player
        </Button>
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
                  <TableCell>{m.userId}</TableCell>
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
  onSave: () => void;
  onRollForward: () => void;
  saving: boolean;
  rollingForward: boolean;
}

function JuiceTab({
  availableSeasons, selectedSeason, onSeasonChange, juiceForm, onJuiceFormChange,
  hasMappingForSeason, onSave, onRollForward, saving, rollingForward,
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

      <Stack spacing={2} sx={{ maxWidth: 400 }}>
        <TextField
          label="Tease Pts (Regular Season)"
          type="number"
          size="small"
          value={juiceForm.juice}
          onChange={(e) => onJuiceFormChange('juice', Number(e.target.value))}
        />
        <TextField
          label="Tease Pts (Divisional)"
          type="number"
          size="small"
          value={juiceForm.juiceDivisional}
          onChange={(e) => onJuiceFormChange('juiceDivisional', Number(e.target.value))}
        />
        <TextField
          label="Tease Pts (Conference)"
          type="number"
          size="small"
          value={juiceForm.juiceConference}
          onChange={(e) => onJuiceFormChange('juiceConference', Number(e.target.value))}
        />
        <TextField
          label="Weekly Cost ($)"
          type="number"
          size="small"
          value={juiceForm.weeklyCost}
          onChange={(e) => onJuiceFormChange('weeklyCost', Number(e.target.value))}
        />
        <Button variant="contained" onClick={onSave} disabled={saving} sx={{ alignSelf: 'flex-start' }}>
          {saving ? <CircularProgress size={18} /> : 'Save'}
        </Button>
      </Stack>
    </Box>
  );
}

function InfoTab({ league }: { league: LeagueInfoDto }) {
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
        <Stack direction="row" justifyContent="space-between">
          <Typography color="text.secondary">Owner ID</Typography>
          <Typography fontWeight={600} sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
            {league.ownerUserId}
          </Typography>
        </Stack>
      </Stack>
    </Box>
  );
}
