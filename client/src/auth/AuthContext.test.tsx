import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';
import { api } from '../lib/api';
import type { AuthResult, UserIdentity, LoginResult } from '../lib/types';

// The real tokenStore exposes refreshToken as a getter-only accessor; tests need to set it
// freely between cases, so the mock is a plain mutable object built via vi.hoisted (its type
// is whatever this literal is, not the real module's), imported directly rather than through
// the '../lib/tokenStore' binding, which would still carry the real (read-only) type.
const tokenStore = vi.hoisted(() => ({
  accessToken: null as string | null,
  refreshToken: null as string | null,
  set: vi.fn(),
  clear: vi.fn(),
}));

vi.mock('../lib/api', () => ({
  api: { get: vi.fn(), post: vi.fn() },
}));

vi.mock('../lib/tokenStore', () => ({ tokenStore }));

const user: UserIdentity = {
  id: 'u1',
  email: 'staff@erp.local',
  firstName: 'Staff',
  lastName: 'Member',
  roles: ['Staff'],
  mfaEnabled: false,
};

const tokens: AuthResult = {
  userId: 'u1',
  email: 'staff@erp.local',
  roles: ['Staff'],
  accessToken: 'access-token',
  accessTokenExpiresAt: '2026-01-01T00:00:00Z',
  refreshToken: 'refresh-token',
};

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    tokenStore.refreshToken = null;
  });

  it('becomes ready without loading a profile when there is no stored refresh token', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.ready).toBe(true));

    expect(api.get).not.toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('silently re-authenticates on boot when a refresh token is already stored', async () => {
    tokenStore.refreshToken = 'stored-refresh-token';
    vi.mocked(api.get).mockResolvedValueOnce({ data: user });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.ready).toBe(true));

    expect(api.get).toHaveBeenCalledWith('/api/auth/me');
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.user?.email).toBe('staff@erp.local');
  });

  it('clears the stored token when silent re-authentication fails', async () => {
    tokenStore.refreshToken = 'stale-refresh-token';
    vi.mocked(api.get).mockRejectedValueOnce(new Error('401'));

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });

    await waitFor(() => expect(result.current.ready).toBe(true));

    expect(tokenStore.clear).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('login() with a completed sign-in stores tokens and loads the profile', async () => {
    const loginResult: LoginResult = { mfaRequired: false, tokens };
    vi.mocked(api.post).mockResolvedValueOnce({ data: loginResult });
    vi.mocked(api.get).mockResolvedValueOnce({ data: user });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    let outcome: Awaited<ReturnType<typeof result.current.login>> | undefined;
    await act(async () => {
      outcome = await result.current.login('staff@erp.local', 'Str0ng#Pass1');
    });

    expect(outcome).toEqual({ mfaRequired: false });
    expect(tokenStore.set).toHaveBeenCalledWith(tokens);
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('login() that requires MFA returns the challenge without storing tokens', async () => {
    const loginResult: LoginResult = { mfaRequired: true, mfaChallengeToken: 'challenge-abc' };
    vi.mocked(api.post).mockResolvedValueOnce({ data: loginResult });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    let outcome: Awaited<ReturnType<typeof result.current.login>> | undefined;
    await act(async () => {
      outcome = await result.current.login('staff@erp.local', 'Str0ng#Pass1');
    });

    expect(outcome).toEqual({ mfaRequired: true, challengeToken: 'challenge-abc' });
    expect(tokenStore.set).not.toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('verifyMfa() redeems the challenge, stores tokens and loads the profile', async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: tokens });
    vi.mocked(api.get).mockResolvedValueOnce({ data: user });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(async () => {
      await result.current.verifyMfa('challenge-abc', '123456');
    });

    expect(api.post).toHaveBeenCalledWith('/api/auth/mfa/verify', {
      challengeToken: 'challenge-abc',
      code: '123456',
    });
    expect(tokenStore.set).toHaveBeenCalledWith(tokens);
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('logout() revokes the refresh token server-side and clears local state', async () => {
    tokenStore.refreshToken = 'refresh-token';
    vi.mocked(api.post).mockResolvedValueOnce({ data: undefined });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(async () => {
      await result.current.logout();
    });

    expect(api.post).toHaveBeenCalledWith('/api/auth/logout', { refreshToken: 'refresh-token' });
    expect(tokenStore.clear).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('logout() clears local state even if the server call fails (best-effort)', async () => {
    tokenStore.refreshToken = 'refresh-token';
    vi.mocked(api.post).mockRejectedValueOnce(new Error('network error'));

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(async () => {
      await result.current.logout();
    });

    expect(tokenStore.clear).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('clears state when the API client dispatches an unrecoverable erp:unauthorized event', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.ready).toBe(true));

    act(() => {
      window.dispatchEvent(new CustomEvent('erp:unauthorized'));
    });

    expect(tokenStore.clear).toHaveBeenCalled();
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('hasRole() checks the loaded user against the given roles', async () => {
    tokenStore.refreshToken = 'stored-refresh-token';
    vi.mocked(api.get).mockResolvedValueOnce({ data: user });

    const { result } = renderHook(() => useAuth(), { wrapper: AuthProvider });
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true));

    expect(result.current.hasRole('Staff')).toBe(true);
    expect(result.current.hasRole('Administrator')).toBe(false);
    expect(result.current.hasRole('Administrator', 'Staff')).toBe(true);
  });
});
