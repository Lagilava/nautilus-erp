import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import { useAuth } from '../auth/AuthContext';
import type { LoginOutcome } from '../auth/AuthContext';

const navigate = vi.fn();

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => navigate };
});

vi.mock('../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}));

function renderLoginPage() {
  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  const login = vi.fn<(email: string, password: string) => Promise<LoginOutcome>>();
  const verifyMfa = vi.fn<(challengeToken: string, code: string) => Promise<void>>();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuth).mockReturnValue({
      user: null,
      ready: true,
      isAuthenticated: false,
      hasRole: () => false,
      login,
      verifyMfa,
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });
  });

  it('rejects an invalid email without calling login', async () => {
    const user = userEvent.setup();
    renderLoginPage();

    // Passes the native <input type="email"> constraint (has an "@", no TLD required there)
    // but fails zod's stricter pattern, so the submit event reaches RHF/zod for a real check —
    // a value with no "@" at all would be blocked by the native constraint before that.
    await user.type(screen.getByLabelText('Email'), 'user@domain');
    await user.type(screen.getByLabelText('Password'), 'whatever');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('signs in and navigates home when no second factor is required', async () => {
    login.mockResolvedValueOnce({ mfaRequired: false });
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByLabelText('Email'), 'staff@erp.local');
    await user.type(screen.getByLabelText('Password'), 'Str0ng#Pass1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(login).toHaveBeenCalledWith('staff@erp.local', 'Str0ng#Pass1'));
    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/', { replace: true }));
  });

  it('shows a server-side error message and does not navigate on failed sign-in', async () => {
    login.mockRejectedValueOnce(new Error('bad credentials'));
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByLabelText('Email'), 'staff@erp.local');
    await user.type(screen.getByLabelText('Password'), 'WrongPass1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Unable to sign in. Check your credentials.')).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('switches to the code-entry step when the account requires MFA, then verifies', async () => {
    login.mockResolvedValueOnce({ mfaRequired: true, challengeToken: 'challenge-abc' });
    verifyMfa.mockResolvedValueOnce(undefined);
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByLabelText('Email'), 'staff@erp.local');
    await user.type(screen.getByLabelText('Password'), 'Str0ng#Pass1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Verification code')).toBeInTheDocument();
    // Login navigated nowhere yet — the second factor is still owed.
    expect(navigate).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText('Code'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    await waitFor(() => expect(verifyMfa).toHaveBeenCalledWith('challenge-abc', '123456'));
    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/', { replace: true }));
  });

  it('shows an error and stays on the code step when the MFA code is rejected', async () => {
    login.mockResolvedValueOnce({ mfaRequired: true, challengeToken: 'challenge-abc' });
    verifyMfa.mockRejectedValueOnce(new Error('invalid code'));
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByLabelText('Email'), 'staff@erp.local');
    await user.type(screen.getByLabelText('Password'), 'Str0ng#Pass1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByText('Verification code');

    await user.type(screen.getByLabelText('Code'), '000000');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByText('Invalid or expired code.')).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('"Back to sign in" returns to the password step', async () => {
    login.mockResolvedValueOnce({ mfaRequired: true, challengeToken: 'challenge-abc' });
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByLabelText('Email'), 'staff@erp.local');
    await user.type(screen.getByLabelText('Password'), 'Str0ng#Pass1');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await screen.findByText('Verification code');

    await user.click(screen.getByRole('button', { name: 'Back to sign in' }));

    expect(await screen.findByText('Welcome back. Enter your details to continue.')).toBeInTheDocument();
  });
});
