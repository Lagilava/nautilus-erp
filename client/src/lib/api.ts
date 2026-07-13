import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './tokenStore';
import type { AuthResult, ProblemDetails } from './types';

// Same-origin: the Vite dev server proxies /api to the backend (see vite.config.ts).
export const api = axios.create({ baseURL: '' });

api.interceptors.request.use((config) => {
  const token = tokenStore.accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// --- Silent refresh on 401, with a single in-flight refresh shared by concurrent requests ---
let refreshing: Promise<string | null> | null = null;

async function redeemRefreshToken(): Promise<string | null> {
  // Re-read at call time (not passed in): if another tab already rotated the token while
  // we were waiting for the cross-tab lock below, we must present the *current* one.
  const refreshToken = tokenStore.refreshToken;
  if (!refreshToken) return null;
  try {
    const { data } = await axios.post<AuthResult>('/api/auth/refresh', { refreshToken });
    tokenStore.set(data);
    return data.accessToken;
  } catch {
    tokenStore.clear();
    return null;
  }
}

async function refreshAccessToken(): Promise<string | null> {
  // Refresh tokens are single-use and rotate server-side (see RefreshTokenCommandHandler):
  // presenting an already-rotated token is treated as a theft signal and revokes the whole
  // session chain. Two browser tabs open to the app can otherwise both redeem the same
  // token at once. The Web Locks API serialises refreshes across tabs of this origin, so a
  // tab that loses the race simply waits its turn and then redeems the now-current token
  // instead of the stale one. Where Web Locks isn't available (older browsers), we fall
  // back to an unprotected refresh — the pre-existing behaviour.
  if (typeof navigator !== 'undefined' && navigator.locks) {
    return navigator.locks.request('nautilus-erp:refresh-token', redeemRefreshToken);
  }
  return redeemRefreshToken();
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retried?: boolean };
    const isAuthCall = original?.url?.includes('/api/auth/');

    if (error.response?.status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true;
      refreshing ??= refreshAccessToken().finally(() => (refreshing = null));
      const newToken = await refreshing;
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return api(original);
      }
      // Refresh failed — force re-login.
      window.dispatchEvent(new CustomEvent('erp:unauthorized'));
    }
    return Promise.reject(error);
  },
);

/** Extracts a human-readable message from an Axios error's problem-details body. */
export function apiErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (axios.isAxiosError(error)) {
    // No response at all → the API is unreachable (not running / wrong port), not a
    // credentials or validation problem. Say so clearly rather than blaming the user.
    if (!error.response) {
      return 'Cannot reach the server. Is the API running on http://localhost:5126?';
    }
    const problem = error.response.data as ProblemDetails | undefined;
    if (problem?.errors) {
      const first = Object.values(problem.errors)[0];
      if (first?.length) return first[0];
    }
    if (problem?.detail) return problem.detail;
    if (problem?.title) return problem.title;
  }
  return fallback;
}
