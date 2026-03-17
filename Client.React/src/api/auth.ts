import { http } from './http';
import type {
  ChangePasswordRequest,
  ConfirmEmailRequest,
  CreateUserRequest,
  CreateUserResponse,
  ForgotPasswordRequest,
  LoginRequest,
  RequestEmailConfirmation,
  ResetPasswordRequest,
  SignInResultDto,
  UserInfo,
} from '../types/auth';

export async function login(payload: LoginRequest) {
  const { data } = await http.post<SignInResultDto>('/api/auth/login', payload);
  return data;
}

export async function logout() {
  await http.post('/api/auth/logout');
}

export async function me() {
  const { data } = await http.get<UserInfo>('/api/auth/me');
  return data;
}

export async function refresh() {
  await http.post('/api/auth/refresh');
}

export async function createUser(payload: CreateUserRequest) {
  const { data } = await http.post<CreateUserResponse>('/api/auth/create-user', payload);
  return data;
}

export async function requestConfirmEmail(payload: RequestEmailConfirmation) {
  const { data } = await http.post<string>('/api/auth/request-email-confirmation', payload);
  return data;
}

export async function confirmEmail(payload: ConfirmEmailRequest) {
  await http.post('/api/auth/confirm-email', payload);
}

export async function forgotPassword(payload: ForgotPasswordRequest) {
  await http.post('/api/auth/forgot-password', payload);
}

export async function resetPassword(payload: ResetPasswordRequest) {
  await http.post('/api/auth/reset-password', payload);
}

export async function changePassword(payload: ChangePasswordRequest) {
  await http.post('/api/auth/change-password', payload);
}

export async function doesUserExist(email: string) {
  const { data } = await http.post<boolean>(`/api/auth/does-user-exist/${encodeURIComponent(email)}`);
  return data;
}

export async function assignUserRole(userId: string) {
  const { data } = await http.post<string>('/api/auth/assign-user-role', { userId, role: 'Administrator' });
  return data;
}

export async function deleteUser(userId: string) {
  const { data } = await http.post<string>(`/api/auth/delete-user/${encodeURIComponent(userId)}`);
  return data;
}

export async function confirmEmailAdmin(userId: string) {
  const { data } = await http.get<string>(`/api/auth/admin-confirm-email/${encodeURIComponent(userId)}`);
  return data;
}
