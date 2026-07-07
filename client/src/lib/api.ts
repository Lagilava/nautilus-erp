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

async function refreshAccessToken(): Promise<string | null> {
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
