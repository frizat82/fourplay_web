import { Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './layouts/AppLayout';
import HomePage from './pages/HomePage';
import { useAuth } from './services/auth';

function RootRedirect() {
  const { user, loading } = useAuth();
  if (loading) return null;
  return user ? <Navigate to="/dashboard" replace /> : <HomePage />;
}
import LeaguePickerPage from './pages/LeaguePickerPage';
import PicksPage from './pages/PicksPage';
import ScoresPage from './pages/ScoresPage';
import LeaderboardPage from './pages/LeaderboardPage';
import LoginPage from './pages/account/LoginPage';
import RegisterPage from './pages/account/RegisterPage';
import RegisterConfirmationPage from './pages/account/RegisterConfirmationPage';
import ForgotPasswordPage from './pages/account/ForgotPasswordPage';
import ForgotPasswordConfirmationPage from './pages/account/ForgotPasswordConfirmationPage';
import ResetPasswordPage from './pages/account/ResetPasswordPage';
import ResetPasswordConfirmationPage from './pages/account/ResetPasswordConfirmationPage';
import InvalidPasswordResetPage from './pages/account/InvalidPasswordResetPage';
import ConfirmEmailPage from './pages/account/ConfirmEmailPage';
import ResendEmailConfirmationPage from './pages/account/ResendEmailConfirmationPage';
import InvalidUserPage from './pages/account/InvalidUserPage';
import LockoutPage from './pages/account/LockoutPage';
import ManageAccountPage from './pages/account/ManageAccountPage';
import ChangePasswordPage from './pages/account/ChangePasswordPage';
import NotFoundPage from './pages/NotFoundPage';
import { RequireAdmin, RequireAuth } from './services/auth';
import AdminLeagueManagementPage from './pages/admin/LeagueManagementPage';
import AdminJobManagerPage from './pages/admin/JobManagerPage';
import AdminUserManagementPage from './pages/admin/UserManagementPage';
import AdminInvitationsPage from './pages/admin/InvitationsPage';
import LogoutPage from './pages/LogoutPage';
import AuthPage from './pages/AuthPage';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" caseSensitive={false} element={<Navigate to="/account/login" replace />} />
      <Route path="/register" caseSensitive={false} element={<Navigate to="/account/register" replace />} />
      <Route path="/account/login" caseSensitive={false} element={<LoginPage />} />
      <Route path="/account/register" caseSensitive={false} element={<RegisterPage />} />
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

      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route path="/dashboard" element={<HomePage />} />
        <Route path="/leaguepicker" element={<LeaguePickerPage />} />
        <Route path="/picks" element={<PicksPage />} />
        <Route path="/scores" element={<ScoresPage />} />
        <Route path="/leaderboard" element={<LeaderboardPage />} />
        <Route path="/auth" element={<AuthPage />} />
        <Route path="/logout" element={<LogoutPage />} />

        <Route
          path="/admin"
          element={
            <RequireAdmin>
              <Navigate to="/admin/jobManagement" replace />
            </RequireAdmin>
          }
        />
        <Route
          path="/admin/leagueManagement"
          element={
            <RequireAdmin>
              <AdminLeagueManagementPage />
            </RequireAdmin>
          }
        />
        <Route
          path="/admin/jobManagement"
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
        <Route path="/account/manage" element={<ManageAccountPage />} />
        <Route path="/account/manage/changepassword" caseSensitive={false} element={<ChangePasswordPage />} />
      </Route>

      <Route path="/account" element={<Navigate to="/account/login" replace />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
