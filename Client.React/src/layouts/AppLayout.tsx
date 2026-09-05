import { useMemo, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AppBar,
  Box,
  Button,
  Chip,
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
import ArrowDropDownIcon from '@mui/icons-material/ArrowDropDown';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import MenuIcon from '@mui/icons-material/Menu';
import HomeIcon from '@mui/icons-material/Home';
import AddToPhotosIcon from '@mui/icons-material/AddToPhotos';
import ScoreboardIcon from '@mui/icons-material/Scoreboard';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import MenuBookIcon from '@mui/icons-material/MenuBook';
import AdminPanelSettingsIcon from '@mui/icons-material/AdminPanelSettings';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import LogoutIcon from '@mui/icons-material/Logout';
import WorkIcon from '@mui/icons-material/Work';
import PersonIcon from '@mui/icons-material/Person';
import MailIcon from '@mui/icons-material/Mail';
import AttachMoneyIcon from '@mui/icons-material/AttachMoney';
import HistoryIcon from '@mui/icons-material/History';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { useSession } from '../services/session';
import { useAuth } from '../services/auth';
import { useSportContext } from '../services/sport';
import { useThemeMode } from '../services/theme';
import { isAdmin } from '../utils/auth';
import { isStandalonePwa } from '../utils/pwa';
import { useSafeAreaRefresh } from '../utils/useSafeAreaRefresh';
import PendingInviteBanner from '../components/PendingInviteBanner';
import VersionFooter from '../components/VersionFooter';

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

// Single source for the safe-area CSS value — referenced by both the fixed AppBar's own padding
// and toolbarSpacerSx below, so the two can't drift out of sync (see the AppBar's comment for why
// they must move together).
const safeInsetTop = 'var(--safe-inset-top)';

// Reserves exactly the fixed AppBar's real (safe-area-inset-aware) height — see the AppBar's
// own comment below for why. Used by both spacer <Toolbar /> placeholders.
const toolbarSpacerSx = { mt: safeInsetTop } as const;

export default function AppLayout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'), { noSsr: true });
  const [open, setOpen] = useState(!isMobile);
  const [adminOpen, setAdminOpen] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const { availableLeagues, currentLeague, selectLeague, hasNflAccess, hasCfbAccess, leaguesLoaded } = useSession();
  const { isCfb } = useSportContext();
  const { user } = useAuth();
  const { mode, toggleTheme } = useThemeMode();
  const navigate = useNavigate();
  const location = useLocation();
  useSafeAreaRefresh();

  const getOtherSportUrl = () => {
    const { hostname, port, protocol } = window.location;
    const portSuffix = port ? `:${port}` : '';
    const otherHost = hostname.startsWith('cfb.') ? hostname.slice(4) : `cfb.${hostname}`;
    return `${protocol}//${otherHost}${portSuffix}`;
  };

  // NFL and CFB are separate origins, so each is its own installed PWA — a cross-origin link
  // from one always drops a standalone-mode install into the regular browser, on every platform
  // (see utils/pwa.ts). Not fixable via routing or an in-app dialog, so the switch-sport controls
  // below are hidden entirely in standalone mode rather than showing a control whose only
  // possible action is an unexpected app-to-browser jump. This control is only ever shown to
  // users who already have access to both sports (hasOther), so hiding it doesn't hide the other
  // sport's existence from anyone — they already know it exists.
  const inStandalonePwa = isStandalonePwa();

  const handleNavClick = (to: string) => {
    if (isMobile) setOpen(false);
    navigate(to);
  };

  const leagueLabel = useMemo(() => {
    const match = availableLeagues.find((l) => l.leagueId === currentLeague);
    return match?.leagueName ?? 'Select League';
  }, [availableLeagues, currentLeague]);

  const showAdmin = isAdmin(user);

  const hasCurrent = isCfb ? hasCfbAccess : hasNflAccess;
  const hasOther = isCfb ? hasNflAccess : hasCfbAccess;
  const otherSport = isCfb ? 'NFL' : 'CFB';
  // /code-review: named predicate instead of accreting `&&` clauses on the gate condition itself
  // — each exemption reason gets its own line here rather than a growing inline expression.
  const isLeaguePortalRoute = location.pathname.startsWith('/league/manage');
  const isExemptFromSportAccessGate =
    // League Portal is the one page that already handles "no leagues yet" correctly (a "Create
    // League" empty state, since creating a league needs no prior access). Without this, a user
    // with only NFL leagues could never reach League Portal on the CFB site to create their first
    // CFB league at all — the "no access" block replaced every routed page, including the one
    // page designed to get them out of that exact state.
    isLeaguePortalRoute
    // Admins are exempt everywhere, not just League Portal — an admin's own personal league
    // membership has nothing to do with whether they should be able to reach the admin panel or
    // manage the platform on a given sport's site. Requiring them to first self-serve a league of
    // their own via League Portal, just to unblock the admin pages they actually needed, was a
    // needless detour that only happened to work by accident.
    || showAdmin;
  const noAccessContent = !isExemptFromSportAccessGate && leaguesLoaded && currentLeague === null && !hasCurrent ? (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '50vh', textAlign: 'center', p: 4 }}>
      <Typography variant="h5" fontWeight={700} gutterBottom>
        No {isCfb ? 'CFB' : 'NFL'} access
      </Typography>
      {hasOther ? (
        <>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Your account has {isCfb ? 'NFL' : 'CFB'} leagues but not {isCfb ? 'CFB' : 'NFL'}.
          </Typography>
          {/* /code-review: unlike the toolbar chip (a convenience shortcut on an otherwise fully
              functional page), this is the ONLY link on this screen — there's no nav, no content,
              nothing else to do here. Hiding it in standalone mode would strand a user who
              installed the wrong sport's PWA with a dead end and no way out. Keep it visible
              always, even though it still has to open the system browser (same platform
              constraint as the toolbar chip). */}
          <Button variant="contained" href={getOtherSportUrl()}>
            Go to {isCfb ? 'NFL' : 'CFB'} site
          </Button>
        </>
      ) : (
        <Typography color="text.secondary">
          You haven&apos;t been invited to any {isCfb ? 'college football' : 'NFL'} leagues yet.
          Ask your commissioner for an invite link.
        </Typography>
      )}
    </Box>
  ) : null;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {/* frizat: iOS "Add to Home Screen -> Open as Web App" launches in standalone mode, which
          (via index.html's viewport-fit=cover + apple-mobile-web-app-status-bar-style=black-
          translucent) renders content edge-to-edge under the status bar/Dynamic Island — without
          this, the fixed AppBar's content sat directly behind it. env(safe-area-inset-top)
          evaluates to 0px in a normal browser tab, so this is a no-op everywhere else. The two
          spacer <Toolbar /> elements below must gain the same extra margin to keep reserving
          exactly the AppBar's real (now taller) height, or content directly under it either hides
          under the bar or leaves a gap. */}
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, pt: safeInsetTop }}>
        <Toolbar sx={{ gap: 2 }}>
          <IconButton color="inherit" edge="start" onClick={() => setOpen(!open)}>
            <MenuIcon />
          </IconButton>
          {/* frizat: at 390px (this app's primary viewport) the hamburger + CFB-switch chip +
              league chip + dark-mode toggle already claim most of the Toolbar's width, leaving
              so little room for this title that `noWrap` collapsed it down to "I…" — illegible
              and pointless. The remaining controls (sport chip, league name, drawer) already
              orient the user, so the wordmark text is dropped below `sm` instead of rendering
              unreadable — the wrapping Box keeps flexGrow so the right-hand controls stay
              pinned to the edge either way. */}
          <Box sx={{ flexGrow: 1 }}>
            <Typography variant="h6" noWrap sx={{ display: { xs: 'none', sm: 'block' } }}>
              IV League
            </Typography>
          </Box>
          <Stack direction="row" spacing={1} alignItems="center">
            {hasOther && !noAccessContent && !inStandalonePwa && (
              <Chip
                component="a"
                href={getOtherSportUrl()}
                icon={<SwapHorizIcon />}
                label={otherSport}
                aria-label={`Switch to ${otherSport} site`}
                clickable
                variant="outlined"
                sx={{
                  color: 'inherit',
                  borderColor: 'rgba(255,255,255,0.4)',
                  height: 44,
                  '& .MuiChip-icon': { color: 'inherit', opacity: 0.7 },
                  '&:hover': { borderColor: 'rgba(255,255,255,0.8)', bgcolor: 'rgba(255,255,255,0.08)' },
                }}
              />
            )}
            <Chip
              label={leagueLabel}
              onClick={(e) => setMenuAnchor(e.currentTarget)}
              onDelete={(e) => setMenuAnchor(e.currentTarget)}
              deleteIcon={<ArrowDropDownIcon />}
              variant="outlined"
              // frizat: MUI's small Chip is only 24px tall — well under the 44px minimum touch
              // target (see CLAUDE.md "Dev Environment"). This chip doubles as the league
              // switcher button in the header, so bump its height explicitly rather than using
              // size="small"/"medium" (neither MUI Chip size meets 44px).
              sx={{
                color: 'inherit',
                borderColor: 'rgba(255,255,255,0.4)',
                maxWidth: 140,
                height: 44,
                '& .MuiChip-label': { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
                '& .MuiChip-deleteIcon': { color: 'inherit', opacity: 0.7 },
                '& .MuiChip-deleteIcon:hover': { color: 'inherit', opacity: 1 },
                '&:hover': { borderColor: 'rgba(255,255,255,0.8)', bgcolor: 'rgba(255,255,255,0.08)' },
              }}
            />
            <IconButton color="inherit" onClick={toggleTheme} aria-label="toggle dark mode">
              {mode === 'dark' ? <Brightness7Icon /> : <Brightness4Icon />}
            </IconButton>
          </Stack>
          <Menu
            anchorEl={menuAnchor}
            open={Boolean(menuAnchor)}
            onClose={() => setMenuAnchor(null)}
            slotProps={{ paper: { sx: { minWidth: 220 } } }}
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
        <Toolbar sx={toolbarSpacerSx} />
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
            <ListItemButton
              component={NavLink}
              to="/league/manage"
              sx={navItemSx}
              onClick={() => handleNavClick('/league/manage')}
            >
              <ListItemIcon>
                <EmojiEventsIcon />
              </ListItemIcon>
              <ListItemText primary="My Leagues" />
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
                      to="/admin/jobManager"
                      sx={adminNavItemSx}
                      onClick={() => handleNavClick('/admin/jobManager')}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <WorkIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="Job Manager" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/users"
                      sx={adminNavItemSx}
                      onClick={() => handleNavClick('/admin/users')}
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
                      onClick={() => handleNavClick('/admin/invitations')}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <MailIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="Invitations" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/leagueCosts"
                      sx={adminNavItemSx}
                      onClick={() => handleNavClick('/admin/leagueCosts')}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <AttachMoneyIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="League Costs" />
                    </ListItemButton>
                    <ListItemButton
                      component={NavLink}
                      to="/admin/changelog"
                      sx={adminNavItemSx}
                      onClick={() => handleNavClick('/admin/changelog')}
                    >
                      <ListItemIcon sx={{ minWidth: 36 }}>
                        <HistoryIcon sx={{ fontSize: 20 }} />
                      </ListItemIcon>
                      <ListItemText primary="Changelog" />
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
          display: 'flex',
          flexDirection: 'column',
          p: { xs: 0, sm: 3 },
          width: { xs: '100%', md: `calc(100% - ${open ? drawerWidth : 0}px)` },
          minHeight: '100vh',
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.leavingScreen,
          }),
        }}
      >
        <Toolbar sx={toolbarSpacerSx} />
        <Box className="page-shell" sx={{ flex: 1 }}>
          <PendingInviteBanner />
          {noAccessContent ?? <Outlet />}
        </Box>
        <VersionFooter />
      </Box>
    </Box>
  );
}
