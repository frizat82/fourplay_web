import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios';
import { isNetworkError } from '../utils/apiError';

// On a weak phone signal a request can sit on a dead connection for a minute or more; fail reads
// after this instead, so callers can offer a retry rather than an endless blank loading state.
// Writes get no timeout: one the server already completed must not be reported as failed (the
// user would think their picks weren't saved and resubmit).
const READ_TIMEOUT_MS = 15_000;

let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

export const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

async function refreshAuth(): Promise<void> {
  if (isRefreshing && refreshPromise) {
    return refreshPromise;
  }

  isRefreshing = true;
  refreshPromise = http.post('/api/auth/refresh').then(() => undefined).finally(() => {
    isRefreshing = false;
  });

  return refreshPromise;
}

http.interceptors.request.use((config) => {
  const method = (config.method ?? 'get').toLowerCase();
  if (!config.timeout && (method === 'get' || method === 'head')) config.timeout = READ_TIMEOUT_MS;
  return config;
});

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined;
    const requestUrl = originalRequest?.url ?? '';
    const isAuthEndpoint =
      requestUrl.includes('/api/auth/login') ||
      requestUrl.includes('/api/auth/logout') ||
      requestUrl.includes('/api/auth/refresh');

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry && !isAuthEndpoint) {
      originalRequest._retry = true;
      try {
        await refreshAuth();
        return http.request(originalRequest);
      } catch (refreshError) {
        // A refresh that never got an answer says nothing about the session — surface it as the
        // connection problem it is, not as the 401 (which reads as "signed out").
        return Promise.reject(isNetworkError(refreshError) ? refreshError : error);
      }
    }

    return Promise.reject(error);
  }
);
