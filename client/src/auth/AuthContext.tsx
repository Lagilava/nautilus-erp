import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../lib/api';
import { tokenStore } from '../lib/tokenStore';
import type { AuthResult, LoginResult, UserIdentity } from '../lib/types';

/** Result of a login attempt: either it completed, or a second factor is still owed. */
export type LoginOutcome = { mfaRequired: false } | { mfaRequired: true; challengeToken: string };

interface AuthState {
  user: UserIdentity | null;
  ready: boolean;
  isAuthenticated: boolean;
  hasRole: (...roles: string[]) => boolean;
  refreshUser: () => Promise<void>;
  login: (email: string, password: string) => Promise<LoginOutcome>;
  verifyMfa: (challengeToken: string, code: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserIdentity | null>(null);
  const [ready, setReady] = useState(false);

  const loadMe = useCallback(async () => {
    const { data } = await api.get<UserIdentity>('/api/auth/me');
    setUser(data);
  }, []);

  // On boot, if a refresh token exists, silently re-authenticate and load the profile.
  useEffect(() => {
    let active = true;
    (async () => {
      if (tokenStore.refreshToken) {
        try {
          await loadMe();
        } catch {
          tokenStore.clear();
        }
      }
      if (active) setReady(true);
    })();
    return () => {
      active = false;
    };
  }, [loadMe]);

  // The API client signals an unrecoverable 401 (refresh failed) via this event.
  useEffect(() => {
    const handler = () => {
      tokenStore.clear();
      setUser(null);
    };
    window.addEventListener('erp:unauthorized', handler);
    return () => window.removeEventListener('erp:unauthorized', handler);
  }, []);

  const login = useCallback(
    async (email: string, password: string): Promise<LoginOutcome> => {
      const { data } = await api.post<LoginResult>('/api/auth/login', { email, password });
      if (data.mfaRequired) return { mfaRequired: true, challengeToken: data.mfaChallengeToken! };
      tokenStore.set(data.tokens!);
      await loadMe();
      return { mfaRequired: false };
    },
    [loadMe],
  );

  const verifyMfa = useCallback(
    async (challengeToken: string, code: string) => {
      const { data } = await api.post<AuthResult>('/api/auth/mfa/verify', { challengeToken, code });
      tokenStore.set(data);
      await loadMe();
    },
    [loadMe],
  );

  const logout = useCallback(async () => {
    const refreshToken = tokenStore.refreshToken;
    try {
      if (refreshToken) await api.post('/api/auth/logout', { refreshToken });
    } catch {
      /* best-effort */
    }
    tokenStore.clear();
    setUser(null);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      ready,
      isAuthenticated: !!user,
      hasRole: (...roles) => !!user && roles.some((r) => user.roles.includes(r)),
      refreshUser: loadMe,
      login,
      verifyMfa,
      logout,
    }),
    [user, ready, loadMe, login, verifyMfa, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
