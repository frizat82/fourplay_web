import { lazy, Suspense, useEffect, type ComponentType } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './layouts/AppLayout';
import RouteFallback from './components/RouteFallback';
import HomePage from './pages/HomePage';
import PicksPage from './pages/PicksPage';
import ScoresPage from './pages/ScoresPage';
import LogoutPage from './pages/LogoutPage';
// Static, not lazy: HomePage already imports RulesContent from this module, so it's in the
// entry chunk either way.
import RulesPage from './pages/RulesPage';
import { RequireAdmin, RequireAuth, useAuth } from './services/auth';
import { useSportContext } from './services/sport';
import { createNflAdapter } from './services/nflAdapter';
import { createCfbAdapter } from './services/cfbAdapter';
import type { SportAdapter } from './services/sportAdapter';

// Route-level code splitting. Home, Picks, Scores and Rules (the pages people cold-load most, on phones)
// stay in the entry chunk; everything else — account/auth forms (which pull in zod and
// react-hook-form), admin tools, league management, and Leaderboard (html-to-image sharing) —
// loads on first visit. Leaderboard is a main nav tab, so App preloads its chunk once idle. A stale chunk after a deploy is handled by
// installChunkReloadGuard (main.tsx).
const JoinLeaguePage = lazy(() => import('./pages/JoinLeaguePage'));
const LeaguePickerPage = lazy(() => import('./pages/LeaguePickerPage'));
const loadLeaderboardPage = () => import('./pages/LeaderboardPage');
const LeaderboardPage = lazy(loadLeaderboardPage);
const LoginPage = lazy(() => import('./pages/account/LoginPage'));
const RegisterPage = lazy(() => import('./pages/account/RegisterPage'));
const RegisterConfirmationPage = lazy(() => import('./pages/account/RegisterConfirmationPage'));
const ForgotPasswordPage = lazy(() => import('./pages/account/ForgotPasswordPage'));
const ForgotPasswordConfirmationPage = lazy(() => import('./pages/account/ForgotPasswordConfirmationPage'));
const ResetPasswordPage = lazy(() => import('./pages/account/ResetPasswordPage'));
const ResetPasswordConfirmationPage = lazy(() => import('./pages/account/ResetPasswordConfirmationPage'));
const InvalidPasswordResetPage = lazy(() => import('./pages/account/InvalidPasswordResetPage'));
const ConfirmEmailPage = lazy(() => import('./pages/account/ConfirmEmailPage'));
const ResendEmailConfirmationPage = lazy(() => import('./pages/account/ResendEmailConfirmationPage'));
const InvalidUserPage = lazy(() => import('./pages/account/InvalidUserPage'));
const LockoutPage = lazy(() => import('./pages/account/LockoutPage'));
const ManageAccountPage = lazy(() => import('./pages/account/ManageAccountPage'));
const ChangePasswordPage = lazy(() => import('./pages/account/ChangePasswordPage'));
const ChangeUsernamePage = lazy(() => import('./pages/account/ChangeUsernamePage'));
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));
const AdminJobManagerPage = lazy(() => import('./pages/admin/JobManagerPage'));
const AdminUserManagementPage = lazy(() => import('./pages/admin/UserManagementPage'));
const AdminInvitationsPage = lazy(() => import('./pages/admin/InvitationsPage'));
const AdminLeagueCostsPage = lazy(() => import('./pages/admin/LeagueCostsPage'));
const AdminChangelogPage = lazy(() => import('./pages/admin/ChangelogPage'));
const AuthPage = lazy(() => import('./pages/AuthPage'));
const LeaguePortalPage = lazy(() => import('./pages/LeaguePortalPage'));

const nflAdapter = createNflAdapter();
const cfbAdapter = createCfbAdapter();

function RootRedirect() {
  const { user, loading } = useAuth();
  if (loading) return null;
  return user ? <Navigate to="/dashboard" replace /> : <HomePage />;
}

// Every sport-agnostic page gets the NFL or CFB adapter from the subdomain. Mounting also kicks
// off the adapter's current-week fetch right away — it doesn't depend on the user's leagues, so
// it runs in parallel with the session's league request instead of after it.
function AdapterRoute({ page: Page }: { page: ComponentType<{ adapter: SportAdapter }> }) {
  const { isCfb } = useSportContext();
  const adapter = isCfb ? cfbAdapter : nflAdapter;
  useEffect(() => adapter.prefetchCurrentWeek(), [adapter]);
  return <Page adapter={adapter} />;
}

export default function App() {
  // Warm the Leaderboard chunk after first paint so tapping its nav tab doesn't wait on a fetch.
  useEffect(() => {
    const id = setTimeout(() => void loadLeaderboardPage().catch(() => {}), 3000);
    return () => clearTimeout(id);
  }, []);

  return (
    // Pages outside AppLayout (account forms, join, 404). Pages inside it suspend at AppLayout's
    // own boundary around <Outlet/>, so the nav stays up while a route chunk loads.
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/" element={<RootRedirect />} />
        <Route path="/login" caseSensitive={false} element={<Navigate to="/account/login" replace />} />
        <Route path="/register" caseSensitive={false} element={<Navigate to="/account/register" replace />} />
        <Route path="/account/login" caseSensitive={false} element={<LoginPage />} />
        <Route path="/account/register" caseSensitive={false} element={<RegisterPage />} />
        <Route path="/join/:token" caseSensitive={false} element={<JoinLeaguePage />} />
        <Route path="/account/registerconfirmation" caseSensitive={false} element={<RegisterConfirmationPage />} />
        <Route path="/account/forgotpassword" caseSensitive={false} element={<ForgotPasswordPage />} />
        <Route path="/account/forgotpasswordconfirmation" caseSensitive={false} element={<ForgotPasswordConfirmationPage />} />
        <Route path="/account/resetpassword" caseSensitive={false} element={<ResetPasswordPage />} />
        <Route path="/account/resetpasswordconfirmation" caseSensitive={false} element={<ResetPasswordConfirmationPage />} />
        <Route path="/account/invalidpasswordreset" caseSensitive={false} element={<InvalidPasswordResetPage />} />
        <Route path="/account/confirmemail" caseSensitive={false} element={<ConfirmEmailPage />} />
        <Route path="/account/resendemailconfirmation" caseSensitive={false} element={<ResendEmailConfirmationPage />} />
        <Route path="/account/invaliduser" caseSensitive={false} element={<InvalidUserPage />} />
        <Route path="/account/lockout" caseSensitive={false} element={<LockoutPage />} />
        {/* Not RequireAuth-guarded on purpose: logout clears auth state as part of its own
            flow, which would otherwise race against RequireAuth's own reactive redirect to
            login and strand the user there instead of on home. See App.logout.test.tsx. */}
        <Route path="/logout" element={<LogoutPage />} />

        <Route
          element={
            <RequireAuth>
              <AppLayout />
            </RequireAuth>
          }
        >
          <Route path="/dashboard" element={<AdapterRoute page={HomePage} />} />
          <Route path="/leaguepicker" element={<LeaguePickerPage />} />
          <Route path="/picks" element={<AdapterRoute page={PicksPage} />} />
          <Route path="/scores" element={<AdapterRoute page={ScoresPage} />} />
          <Route path="/leaderboard" element={<AdapterRoute page={LeaderboardPage} />} />
          <Route path="/auth" element={<AuthPage />} />

          <Route
            path="/admin"
            element={
              <RequireAdmin>
                <Navigate to="/admin/jobManager" replace />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/jobManager"
            element={
              <RequireAdmin>
                <AdminJobManagerPage />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/users"
            element={
              <RequireAdmin>
                <AdminUserManagementPage />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/invitations"
            caseSensitive={false}
            element={
              <RequireAdmin>
                <AdminInvitationsPage />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/leagueCosts"
            caseSensitive={false}
            element={
              <RequireAdmin>
                <AdminLeagueCostsPage />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/changelog"
            caseSensitive={false}
            element={
              <RequireAdmin>
                <AdminChangelogPage />
              </RequireAdmin>
            }
          />
          <Route path="/account/manage" element={<ManageAccountPage />} />
          <Route path="/account/manage/changepassword" caseSensitive={false} element={<ChangePasswordPage />} />
          <Route path="/account/manage/changeusername" caseSensitive={false} element={<ChangeUsernamePage />} />
          <Route path="/rules" caseSensitive={false} element={<RulesPage />} />
          <Route path="/league/manage" element={<AdapterRoute page={LeaguePortalPage} />} />
        </Route>
        <Route path="/account" element={<Navigate to="/account/login" replace />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
