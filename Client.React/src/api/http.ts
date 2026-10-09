import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios';

let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

export const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  withCredentials: true,
  // On a weak phone signal a request can sit on a dead connection for a minute or more; fail it
  // instead, so callers can offer a retry rather than an endless blank loading state.
  timeout: 15_000,
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
      } catch {
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  }
);
