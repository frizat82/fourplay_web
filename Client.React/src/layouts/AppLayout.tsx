import { useMemo, useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  AppBar,
  Box,
  Collapse,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Stack,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import MenuIcon from '@mui/icons-material/Menu';
import HomeIcon from '@mui/icons-material/Home';
import AddToPhotosIcon from '@mui/icons-material/AddToPhotos';
import ScoreboardIcon from '@mui/icons-material/Scoreboard';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import MenuBookIcon from '@mui/icons-material/MenuBook';
import SettingsIcon from '@mui/icons-material/Settings';
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import LogoutIcon from '@mui/icons-material/Logout';
import WorkIcon from '@mui/icons-material/Work';
import GroupsIcon from '@mui/icons-material/Groups';
import PersonIcon from '@mui/icons-material/Person';
import MailIcon from '@mui/icons-material/Mail';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useThemeMode } from '../services/theme';
import { isAdmin } from '../utils/auth';

const drawerWidth = 260;

const navItemSx = {
  mx: 1,
  borderRadius: 2,
} as const;

const adminNavItemSx = {
  mx: 3,
  borderRadius: 2,
  pl: 2,
} as const;

export default function AppLayout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'), { noSsr: true });
  const [open, setOpen] = useState(!isMobile);
  const [adminOpen, setAdminOpen] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const { availableLeagues, currentLeague, selectLeague } = useSession();
  const { user } = useAuth();
  const { mode, toggleTheme } = useThemeMode();
  const navigate = useNavigate();

  const handleNavClick = (to: string) => {
    if (isMobile) setOpen(false);
    navigate(to);
  };

  const leagueLabel = useMemo(() => {
    const match = availableLeagues.find((l) => l.leagueId === currentLeague);
    return match?.leagueName ?? 'Select League';
  }, [availableLeagues, currentLeague]);

  const showAdmin = isAdmin(user);

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar sx={{ gap: 2 }}>
          <IconButton color="inherit" edge="start" onClick={() => setOpen(!open)}>
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            FourPlay
          </Typography>
          <Stack direction="row" spacing={1} alignItems="center">
            <Typography variant="body2" sx={{ opacity: 0.8 }}>
              {leagueLabel}
            </Typography>
            <IconButton color="inherit" onClick={(e) => setMenuAnchor(e.currentTarget)}>
              <SettingsIcon />
            </IconButton>
            <IconButton color="inherit" onClick={toggleTheme} aria-label="toggle dark mode">
              {mode === 'dark' ? <Brightness7Icon /> : <Brightness4Icon />}
            </IconButton>
          </Stack>
          <Menu
            anchorEl={menuAnchor}
            open={Boolean(menuAnchor)}
            onClose={() => setMenuAnchor(null)}
            PaperProps={{ sx: { minWidth: 220 } }}
          >
            <Typography variant="caption" sx={{ px: 2, py: 1, opacity: 0.7 }}>
              League Selection
            </Typography>
            <Divider />
            {availableLeagues.length === 0 && (
              <MenuItem disabled>No leagues assigned yet</MenuItem>
            )}
            {availableLeagues.map((league) => (
              <MenuItem
                key={league.leagueId}
                selected={league.leagueId === currentLeague}
                onClick={() => {
                  selectLeague(league.leagueId);
                  setMenuAnchor(null);
                }}
              >
                {league.leagueName}
              </MenuItem>
            ))}
            <MenuItem component={NavLink} to="/leaguepicker" onClick={() => setMenuAnchor(null)}>
              Open League Picker
            </MenuItem>
            <Divider />
            <MenuItem component={NavLink} to="/account/manage" onClick={() => setMenuAnchor(null)}>
              {user?.name ?? 'Account'}
            </MenuItem>
            <MenuItem component={NavLink} to="/logout" onClick={() => setMenuAnchor(null)}>
              <ListItemIcon>
                <LogoutIcon fontSize="small" />
              </ListItemIcon>
              Logout
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Drawer
        variant={isMobile ? 'temporary' : 'persistent'}
        open={open}
        onClose={() => setOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{
          width: open ? drawerWidth : 0,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            boxSizing: 'border-box',
            background: (theme) =>
              theme.palette.mode === 'dark'
                ? 'linear-gradient(180deg, #1a2440 0%, #0f1a2e 100%)'
                : 'linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.98))',
            borderRight: '1px solid',
            borderColor: 'divider',
          },
        }}
      >
        <Toolbar />
        <Box sx={{ overflow: 'auto' }}>
          <List>
            <ListItemButton
              component={NavLink}
              to="/dashboard"
              end
              sx={navItemSx}
              onClick={() => handleNavClick('/dashboard')}
            >
              <ListItemIcon>
                <HomeIcon />
              </ListItemIcon>
              <ListItemText primary="Dashboard" />
            </ListItemButton>
            <ListItemButton
              component={NavLink}
              to="/picks"
              sx={navItemSx}
              onClick={() => handleNavClick('/picks')}
            >
              <ListItemIcon>
                <AddToPhotosIcon />
              </ListItemIcon>
              <ListItemText primary="My Picks" />
            </ListItemButton>
            <ListItemButton
              component={NavLink}
              to="/scores"
              sx={navItemSx}
              onClick={() => handleNavClick('/scores')}
            >
              <ListItemIcon>
                <ScoreboardIcon />
              </ListItemIcon>
              <ListItemText primary="Scores" />
            </ListItemButton>
            <ListItemButton
              component={NavLink}
              to="/leaderboard"
              sx={navItemSx}
              onClick={() => handleNavClick('/leaderboard')}
            >
              <ListItemIcon>
                <LeaderboardIcon />
              </ListItemIcon>
              <ListItemText primary="Leaderboard" />
            </ListItemButton>
            <ListItemButton
              component={NavLink}
              to="/rules"
              sx={navItemSx}
              onClick={() => handleNavClick('/rules')}
            >
              <ListItemIcon>
                <MenuBookIcon />
              </ListItemIcon>
              <ListItemText primary="Rules" />
            </ListItemButton>
          </List>
          {showAdmin && (
            <>
              <Divider sx={{ my: 1 }} />
              <List disablePadding>
                <ListItemButton
                  onClick={() => setAdminOpen(!adminOpen)}
                  sx={{
                    mx: 1,
                    borderRadius: 2,
                    backgroundColor: 'rgba(59, 130, 246, 0.05)',
                    '&:hover': {
                      backgroundColor: 'rgba(59, 130, 246, 0.1)',
                    },
                  }}
                >
                  <ListItemIcon>
                    <AdminPanelSettingsIcon sx={{ color: '#3b82f6' }} />
                  </ListItemIcon>
                  <ListItemText 
                    primary="Admin" 
                    primaryTypographyProps={{ fontWeight: 600 }}
                  />
                  {adminOpen ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                </ListItemButton>
                <Collapse in={adminOpen} timeout="auto" unmountOnExit>
                  <List component="div" disablePadding>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/jobManagement"
                      sx={adminNavItemSx}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <WorkIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="Job Manager" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/leagueManagement"
                      sx={adminNavItemSx}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <GroupsIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="League Management" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/users"
                      sx={adminNavItemSx}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <PersonIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="User Management" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/invitations"
                      sx={adminNavItemSx}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <MailIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="Invitations" />
                    </ListItemButton>
                  </List>
                </Collapse>
              </List>
            </>
          )}
        </Box>
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: { xs: 0, sm: 3 },
          width: { xs: '100%', md: `calc(100% - ${open ? drawerWidth : 0}px)` },
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.leavingScreen,
          }),
        }}
      >
        <Toolbar />
        <Box className="page-shell">
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}
