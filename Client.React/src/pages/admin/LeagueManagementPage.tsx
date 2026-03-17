import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
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
import PageHeader from '../../components/PageHeader';
import { useSession } from '../../services/session';
import { useAuth } from '../../services/auth';
import { useToast } from '../../services/toast';
import {
  addLeagueInfo,
  addLeagueJuiceMapping,
  addLeagueUserMapping,
  getLeagueByName,
  getLeagueJuice,
  getLeagueUserMappingsForUser,
  getUsers,
  leagueExists,
} from '../../api/league';
import type {
  CreateLeagueModel,
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

  const [leagueForm, setLeagueForm] = useState<CreateLeagueModel>({
    leagueName: '',
    juice: 0,
    juiceDivisional: 0,
    juiceConference: 0,
    season: new Date().getFullYear(),
    weeklyCost: 0,
  });

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

  const openAddMapping = async () => {
    if (!user?.userId) return;
    const users = await getUsers();
    const list = users.filter((u) => u.id !== user.userId);
    setAvailableUsers(list);
    setSelectedUser(null);
    setSelectedLeague(null);
    setMappingDialogOpen(true);
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
      dateCreated: new Date().toISOString(),
    });
    toast.push('User Added To League', 'success');
    setMappingDialogOpen(false);
    await loadUserLeagueMappings();
  };

  const ensureUserExists = async (existingLeague: LeagueInfoDto) => {
    if (!user?.userId) return;
    const leagueUserInfo = await getLeagueUserMappingsForUser(user.userId);
    if (!leagueUserInfo.find((l) => l.leagueName === existingLeague.leagueName)) {
      await addLeagueUserMapping({
        id: 0,
        leagueId: existingLeague.id,
        leagueName: existingLeague.leagueName,
        userId: user.userId,
        userName: user.name ?? '',
        leagueOwnerUserId: user.userId,
        dateCreated: new Date().toISOString(),
      });
    }
    await loadUserLeagueMappings();
  };

  const handleAddLeague = async () => {
    if (!user?.userId) return;
    try {
      const exists = await leagueExists(leagueForm.leagueName);
      if (!exists) {
        await addLeagueInfo({
          id: 0,
          leagueName: leagueForm.leagueName,
          ownerUserId: user.userId,
          leagueType: 'Nfl',
          dateCreated: new Date().toISOString(),
        });
      }

    const leagueExistsForSeason = await leagueExists(leagueForm.leagueName, leagueForm.season);
    if (leagueExistsForSeason) {
      const existing = await getLeagueByName(leagueForm.leagueName);
      toast.push(`League Already Exists ${leagueForm.leagueName}:${leagueForm.season}`, 'error');
      if (existing) {
        await ensureUserExists(existing);
      }
      return;
    }

      const existingLeague = await getLeagueByName(leagueForm.leagueName);
      if (existingLeague) {
        await addLeagueJuiceMapping({
          id: 0,
          leagueId: existingLeague.id,
          leagueName: existingLeague.leagueName,
          season: leagueForm.season,
          juice: leagueForm.juice,
          juiceDivisional: leagueForm.juiceDivisional,
          juiceConference: leagueForm.juiceConference,
          weeklyCost: leagueForm.weeklyCost,
          dateCreated: new Date().toISOString(),
        });
        toast.push('League Season Added', 'success');
        await ensureUserExists(existingLeague);
        await loadMappings();
      } else {
        toast.push(`Error Loading League ${leagueForm.leagueName}:${leagueForm.season}`, 'error');
      }
    } catch {
      toast.push('Error adding league', 'error');
    } finally {
      setLeagueDialogOpen(false);
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
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {userMapping.map((mapping) => (
                        <TableRow key={`${mapping.leagueId}-${mapping.userId}`}>
                          <TableCell>{mapping.leagueName}</TableCell>
                          <TableCell>{mapping.userName}</TableCell>
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
        <DialogTitle>Add League</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="League"
              value={leagueForm.leagueName}
              onChange={(e) => setLeagueForm({ ...leagueForm, leagueName: e.target.value })}
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
            <TextField
              label="Season (Year)"
              type="number"
              value={leagueForm.season}
              onChange={(e) => setLeagueForm({ ...leagueForm, season: Number(e.target.value) })}
              inputProps={{ min: new Date().getFullYear() }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLeagueDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            color="error"
            onClick={handleAddLeague}
            disabled={!leagueForm.leagueName || !leagueForm.season}
          >
            Add League
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
