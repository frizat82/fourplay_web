export interface ClaimDto {
  type: string;
  value: string;
}

export interface UserInfo {
  userId: string;
  name: string;
  claims: ClaimDto[];
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe: boolean;
}

export interface SignInResultDto {
  succeeded: boolean;
  isLockedOut: boolean;
  requiresTwoFactor: boolean;
  isNotAllowed: boolean;
  accessFailedCount: number;
  lockoutEnd?: string | null;
  message?: string | null;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  code: string;
}

export interface CreateUserResponse {
  isSuccess: boolean;
  userId?: string | null;
  errors: string[];
}

export interface ForgotPasswordRequest {
  email: string;
  resetUrl: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  password: string;
}

export interface ChangePasswordRequest {
  email: string;
  currentPassword: string;
  password: string;
}

export interface RequestEmailConfirmation {
  email: string;
  confirmationUrl: string;
}

export interface ConfirmEmailRequest {
  userId: string;
  token: string;
}
