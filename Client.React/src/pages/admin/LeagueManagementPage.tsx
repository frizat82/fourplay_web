import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
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
import PageHeader from '../../components/PageHeader';
import { useSession } from '../../services/session';
import { useAuth } from '../../services/auth';
import { useToast } from '../../services/toast';
import {
  addLeagueUserMapping,
  assignLeagueOwner,
  createLeague,
  getLeagueJuice,
  getLeagueUserMappingsForUser,
  getUsers,
} from '../../api/league';
import type {
  LeagueCreateDto,
  LeagueInfoDto,
  LeagueJuiceMappingDto,
  LeagueUserMappingDto,
  UserSummaryDto,
} from '../../types/admin';

export default function AdminLeagueManagementPage() {
  const { currentLeague } = useSession();
  const { user } = useAuth();
  const toast = useToast();

  const [userMapping, setUserMapping] = useState<LeagueUserMappingDto[]>([]);
  const [leagueJuiceMapping, setLeagueJuiceMapping] = useState<LeagueJuiceMappingDto[]>([]);
  const [loadingMappings, setLoadingMappings] = useState(false);
  const [loadingJuice, setLoadingJuice] = useState(false);

  const [mappingDialogOpen, setMappingDialogOpen] = useState(false);
  const [leagueDialogOpen, setLeagueDialogOpen] = useState(false);

  const [availableUsers, setAvailableUsers] = useState<UserSummaryDto[]>([]);
  const [selectedUser, setSelectedUser] = useState<UserSummaryDto | null>(null);
  const [selectedLeague, setSelectedLeague] = useState<LeagueInfoDto | null>(null);

  const [leagueForm, setLeagueForm] = useState<Omit<LeagueCreateDto, 'ownerUserId'> & { ownerUserId: string }>({
    leagueName: '',
    leagueType: 'Nfl',
    ownerUserId: '',
    season: new Date().getFullYear(),
    juice: 0,
    juiceDivisional: 0,
    juiceConference: 0,
    weeklyCost: 0,
  });

  // Assign-owner state
  const [ownerDialogOpen, setOwnerDialogOpen] = useState(false);
  const [ownerTargetLeague, setOwnerTargetLeague] = useState<LeagueInfoDto | null>(null);
  const [newOwnerId, setNewOwnerId] = useState('');

  const leagueList = useMemo(() => {
    const list: LeagueInfoDto[] = [];
    userMapping.forEach((m) => {
      if (!list.find((l) => l.id === m.leagueId)) {
        list.push({
          id: m.leagueId,
          leagueName: m.leagueName ?? 'Unknown',
          dateCreated: new Date().toISOString(),
          ownerUserId: m.leagueOwnerUserId ?? '',
          leagueType: 'Nfl',
        });
      }
    });
    return list.sort((a, b) => a.leagueName.localeCompare(b.leagueName));
  }, [userMapping]);

  const loadUserLeagueMappings = useCallback(async () => {
    if (!user?.userId) return;
    setLoadingMappings(true);
    try {
      const mappings = await getLeagueUserMappingsForUser(user.userId);
      setUserMapping(mappings);
    } catch {
      toast.push('Error loading league mappings', 'error');
    } finally {
      setLoadingMappings(false);
    }
  }, [toast, user?.userId]);

  const loadMappings = useCallback(async () => {
    if (!currentLeague) return;
    setLoadingJuice(true);
    try {
      const juice = await getLeagueJuice(currentLeague);
      setLeagueJuiceMapping(juice);
    } catch {
      toast.push('Error loading league juice mappings', 'error');
    } finally {
      setLoadingJuice(false);
    }
  }, [currentLeague, toast]);

  useEffect(() => {
    void loadUserLeagueMappings();
    if (currentLeague) {
      void loadMappings();
    }
  }, [currentLeague, loadMappings, loadUserLeagueMappings]);

  const loadAllUsers = useCallback(async () => {
    const users = await getUsers();
    setAvailableUsers(users);
  }, []);

  const openAddMapping = async () => {
    if (!user?.userId) return;
    await loadAllUsers();
    setAvailableUsers((prev) => prev.filter((u) => u.id !== user.userId));
    setSelectedUser(null);
    setSelectedLeague(null);
    setMappingDialogOpen(true);
  };

  const openAssignOwner = async (league: LeagueInfoDto) => {
    await loadAllUsers();
    setOwnerTargetLeague(league);
    setNewOwnerId('');
    setOwnerDialogOpen(true);
  };

  const handleAddMapping = async () => {
    if (!selectedUser || !selectedLeague) return;
    if (userMapping.some((m) => m.userId === selectedUser.id && m.leagueId === selectedLeague.id)) {
      toast.push('User Already Exists In League', 'error');
      return;
    }
    await addLeagueUserMapping({
      id: 0,
      leagueId: selectedLeague.id,
      userId: selectedUser.id,
      leagueOwnerUserId: user?.userId ?? '',
      userName: selectedUser.userName ?? '',
      leagueName: selectedLeague.leagueName,
      leagueType: 0,
      dateCreated: new Date().toISOString(),
    });
    toast.push('User Added To League', 'success');
    setMappingDialogOpen(false);
    await loadUserLeagueMappings();
  };

  const handleAddLeague = async () => {
    if (!user?.userId) return;
    try {
      await createLeague({ ...leagueForm, ownerUserId: leagueForm.ownerUserId || user.userId });
      toast.push('League created', 'success');
      await loadUserLeagueMappings();
      await loadMappings();
    } catch {
      toast.push('Error creating league', 'error');
    } finally {
      setLeagueDialogOpen(false);
    }
  };

  const handleAssignOwner = async () => {
    if (!ownerTargetLeague || !newOwnerId) return;
    try {
      await assignLeagueOwner(ownerTargetLeague.id, newOwnerId);
      toast.push('Owner updated', 'success');
      setOwnerDialogOpen(false);
      setOwnerTargetLeague(null);
      setNewOwnerId('');
      await loadUserLeagueMappings();
    } catch {
      toast.push('Failed to assign owner', 'error');
    }
  };

  return (
    <Box>
      <PageHeader title="League Management" />

      <Grid container spacing={3}>
        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Stack spacing={2}>
              <Typography variant="h4" align="center">
                Assign a User to a League
              </Typography>
              {loadingMappings ? (
                <CircularProgress />
              ) : (
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>League</TableCell>
                        <TableCell>User</TableCell>
                        <TableCell>Owner ID</TableCell>
                        <TableCell>Actions</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {userMapping.map((mapping) => (
                        <TableRow key={`${mapping.leagueId}-${mapping.userId}`}>
                          <TableCell>{mapping.leagueName}</TableCell>
                          <TableCell>{mapping.userName}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem', maxWidth: 180, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {mapping.leagueOwnerUserId ?? '—'}
                          </TableCell>
                          <TableCell>
                            <Button
                              size="small"
                              variant="outlined"
                              onClick={() => void openAssignOwner({
                                id: mapping.leagueId,
                                leagueName: mapping.leagueName ?? '',
                                ownerUserId: mapping.leagueOwnerUserId ?? '',
                                leagueType: 'Nfl',
                                dateCreated: mapping.dateCreated,
                              })}
                            >
                              Assign Owner
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
              <Button disabled={loadingMappings || userMapping.length === 0} variant="contained" onClick={openAddMapping}>
                Add User to a League
              </Button>
            </Stack>
          </Paper>
        </Grid>

        <Grid size={12}>
          <Paper sx={{ p: 3 }}>
            <Stack spacing={2}>
              <Typography variant="h4" align="center">
                Setup a New League
              </Typography>
              {loadingJuice ? (
                <CircularProgress />
              ) : (
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>League</TableCell>
                        <TableCell>Juice</TableCell>
                        <TableCell>Div</TableCell>
                        <TableCell>Conf</TableCell>
                        <TableCell>Cost</TableCell>
                        <TableCell>Season</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {leagueJuiceMapping.map((mapping) => (
                        <TableRow key={`${mapping.leagueId}-${mapping.season}`}>
                          <TableCell>{mapping.leagueName}</TableCell>
                          <TableCell>{mapping.juice}</TableCell>
                          <TableCell>{mapping.juiceDivisional}</TableCell>
                          <TableCell>{mapping.juiceConference}</TableCell>
                          <TableCell>{mapping.weeklyCost}</TableCell>
                          <TableCell>{mapping.season}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
              <Button disabled={loadingJuice} variant="contained" onClick={() => setLeagueDialogOpen(true)}>
                Add League
              </Button>
            </Stack>
          </Paper>
        </Grid>
      </Grid>

      <Dialog open={mappingDialogOpen} onClose={() => setMappingDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Add Mapping</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              select
              label="User Email"
              SelectProps={{ native: true }}
              value={selectedUser?.id ?? ''}
              onChange={(event) =>
                setSelectedUser(availableUsers.find((u) => u.id === event.target.value) ?? null)
              }
            >
              <option value="" />
              {availableUsers.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.email}
                </option>
              ))}
            </TextField>
            <TextField
              select
              label="League Name"
              SelectProps={{ native: true }}
              value={selectedLeague?.id ?? ''}
              onChange={(event) =>
                setSelectedLeague(leagueList.find((l) => String(l.id) === event.target.value) ?? null)
              }
            >
              <option value="" />
              {leagueList.map((league) => (
                <option key={league.id} value={league.id}>
                  {league.leagueName}
                </option>
              ))}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMappingDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" color="error" disabled={!selectedUser || !selectedLeague} onClick={handleAddMapping}>
            Add Mapping
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={leagueDialogOpen} onClose={() => setLeagueDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Create League</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="League Name"
              value={leagueForm.leagueName}
              onChange={(e) => setLeagueForm({ ...leagueForm, leagueName: e.target.value })}
            />
            <FormControl size="small">
              <InputLabel>Sport</InputLabel>
              <Select
                value={leagueForm.leagueType}
                label="Sport"
                onChange={(e) => setLeagueForm({ ...leagueForm, leagueType: e.target.value })}
              >
                <MenuItem value="Nfl">NFL</MenuItem>
                <MenuItem value="Cfb">CFB</MenuItem>
              </Select>
            </FormControl>
            <TextField
              label="Owner User ID"
              value={leagueForm.ownerUserId}
              onChange={(e) => setLeagueForm({ ...leagueForm, ownerUserId: e.target.value })}
              helperText="Leave blank to assign yourself as owner"
            />
            <TextField
              label="Season (Year)"
              type="number"
              value={leagueForm.season}
              onChange={(e) => setLeagueForm({ ...leagueForm, season: Number(e.target.value) })}
              inputProps={{ min: new Date().getFullYear() }}
            />
            <TextField
              label="Juice (Teaser)"
              type="number"
              value={leagueForm.juice}
              onChange={(e) => setLeagueForm({ ...leagueForm, juice: Number(e.target.value) })}
            />
            <TextField
              label="Juice WildCard + Divisional"
              type="number"
              value={leagueForm.juiceDivisional}
              onChange={(e) => setLeagueForm({ ...leagueForm, juiceDivisional: Number(e.target.value) })}
            />
            <TextField
              label="Juice Conference"
              type="number"
              value={leagueForm.juiceConference}
              onChange={(e) => setLeagueForm({ ...leagueForm, juiceConference: Number(e.target.value) })}
            />
            <TextField
              label="Weekly Cost"
              type="number"
              value={leagueForm.weeklyCost}
              onChange={(e) => setLeagueForm({ ...leagueForm, weeklyCost: Number(e.target.value) })}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLeagueDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => void handleAddLeague()}
            disabled={!leagueForm.leagueName || !leagueForm.season}
          >
            Create League
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={ownerDialogOpen} onClose={() => setOwnerDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Assign Owner — {ownerTargetLeague?.leagueName}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Current owner: {ownerTargetLeague?.ownerUserId || 'none'}
            </Typography>
            <TextField
              select
              label="New Owner"
              SelectProps={{ native: true }}
              value={newOwnerId}
              onChange={(e) => setNewOwnerId(e.target.value)}
            >
              <option value="" />
              {availableUsers.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.email ?? u.userName ?? u.id}
                </option>
              ))}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOwnerDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => void handleAssignOwner()} disabled={!newOwnerId}>
            Assign
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
