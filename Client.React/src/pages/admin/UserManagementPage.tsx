import { useEffect, useState } from 'react';
import {
  Box,
  CircularProgress,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Chip,
} from '@mui/material';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import PageHeader from '../../components/PageHeader';
import { getUsers } from '../../api/league';
import { confirmEmailAdmin, deleteUser, assignUserRole } from '../../api/auth';
import { getInvitationsByUser, deleteInvitation } from '../../api/invitations';
import type { UserSummaryDto } from '../../types/admin';
import { useToast } from '../../services/toast';

export default function AdminUserManagementPage() {
  const [users, setUsers] = useState<UserSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const [selectedUser, setSelectedUser] = useState<UserSummaryDto | null>(null);
  const toast = useToast();

  const loadUsers = async () => {
    setLoading(true);
    const data = await getUsers();
    setUsers(data.sort((a, b) => (a.email ?? '').localeCompare(b.email ?? '')));
    setLoading(false);
  };

  useEffect(() => {
    void loadUsers();
  }, []);

  const openMenu = (event: React.MouseEvent<HTMLButtonElement>, user: UserSummaryDto) => {
    setAnchorEl(event.currentTarget);
    setSelectedUser(user);
  };

  const closeMenu = () => {
    setAnchorEl(null);
    setSelectedUser(null);
  };

  const handleConfirmEmail = async () => {
    if (!selectedUser) return;
    try {
      await confirmEmailAdmin(selectedUser.id);
      toast.push(`Email confirmed for ${selectedUser.email}`, 'success');
      await loadUsers();
    } catch {
      toast.push('Failed to confirm email', 'error');
    } finally {
      closeMenu();
    }
  };

  const handleAssignAdmin = async () => {
    if (!selectedUser) return;
    try {
      await assignUserRole(selectedUser.id);
      toast.push('User added as Administrator successfully.', 'success');
      await loadUsers();
    } catch {
      toast.push('Error adding Administrator user', 'error');
    } finally {
      closeMenu();
    }
  };

  const handleRemoveUser = async () => {
    if (!selectedUser) return;
    try {
      const invitations = await getInvitationsByUser(selectedUser.id);
      for (const invitation of invitations) {
        await deleteInvitation(invitation.id);
      }
      await deleteUser(selectedUser.id);
      toast.push('User deleted successfully.', 'success');
      await loadUsers();
    } catch {
      toast.push('Error deleting user', 'error');
    } finally {
      closeMenu();
    }
  };

  return (
    <Box>
      <PageHeader title="User Management" />
      <Paper sx={{ p: 2 }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </Box>
        ) : (
          <Box sx={{ overflowX: 'auto' }}><Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>User Name</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Email Confirmed</TableCell>
                <TableCell>Admin</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map((user) => (
                <TableRow key={user.id}>
                  <TableCell>{user.userName}</TableCell>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>
                    {user.emailConfirmed ? (
                      <Chip size="small" label="Confirmed" color="success" />
                    ) : (
                      <Chip size="small" label="Not Confirmed" color="error" />
                    )}
                  </TableCell>
                  <TableCell>
                    {user.isAdmin ? (
                      <Chip size="small" label="Admin" color="error" />
                    ) : (
                      <Chip size="small" label="User" />
                    )}
                  </TableCell>
                  <TableCell>
                    <IconButton onClick={(event) => openMenu(event, user)}>
                      <MoreVertIcon />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table></Box>
        )}
      </Paper>
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={closeMenu}>
        {selectedUser && !selectedUser.emailConfirmed && (
          <MenuItem onClick={handleConfirmEmail}>Confirm Email</MenuItem>
        )}
        <MenuItem onClick={handleRemoveUser}>Remove User</MenuItem>
        <MenuItem onClick={handleAssignAdmin}>Assign Admin Perms</MenuItem>
      </Menu>
    </Box>
  );
}
