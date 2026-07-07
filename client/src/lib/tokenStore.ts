// Access token lives in memory (not readable via XSS from storage); the refresh token is
// persisted so a page reload can silently re-authenticate. Trade-off documented in docs/API.md.
import type { AuthResult } from './types';

const REFRESH_KEY = 'erp.refreshToken';

let accessToken: string | null = null;
let currentUser: Pick<AuthResult, 'userId' | 'email' | 'roles'> | null = null;

export const tokenStore = {
  get accessToken() {
    return accessToken;
  },
  get refreshToken() {
    return localStorage.getItem(REFRESH_KEY);
  },
  get user() {
    return currentUser;
  },
  set(auth: AuthResult) {
    accessToken = auth.accessToken;
    currentUser = { userId: auth.userId, email: auth.email, roles: auth.roles };
    localStorage.setItem(REFRESH_KEY, auth.refreshToken);
  },
  clear() {
    accessToken = null;
    currentUser = null;
    localStorage.removeItem(REFRESH_KEY);
  },
};
