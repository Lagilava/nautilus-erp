import { useState } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { Wordmark } from '../components/Brand';
import { ErrorNote, Spinner } from '../components/ui';

/** Target of the emailed reset link: /reset-password?email=…&token=… */
export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';

  const [newPassword, setNewPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const reset = useMutation({
    mutationFn: () => api.post('/api/auth/reset-password', { email, token, newPassword }),
    onSuccess: () => setDone(true),
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const valid = email && token && newPassword.length >= 8 && newPassword === confirm;

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex justify-center">
          <Wordmark />
        </div>

        <div className="card p-6">
          {done ? (
            <div className="text-center">
              <h1 className="text-xl font-semibold text-ink">Password reset</h1>
              <p className="mt-2 text-sm text-ink-muted">You can now sign in with your new password.</p>
              <button className="btn-primary mt-5 w-full" onClick={() => navigate('/login')}>
                Go to sign in
              </button>
            </div>
          ) : !email || !token ? (
            <div className="text-center">
              <h1 className="text-xl font-semibold text-ink">Invalid reset link</h1>
              <p className="mt-2 text-sm text-ink-muted">
                This link is incomplete or has expired. Request a new one from the sign-in page.
              </p>
              <Link to="/login" className="btn-secondary mt-5 w-full">
                Back to sign in
              </Link>
            </div>
          ) : (
            <>
              <h1 className="text-xl font-semibold text-ink">Choose a new password</h1>
              <p className="mt-1 text-sm text-ink-muted">Resetting the password for {email}.</p>

              <div className="mt-5 space-y-4">
                {error && <ErrorNote message={error} />}
                <div>
                  <label className="field-label">New password</label>
                  <input
                    className="input"
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                  />
                </div>
                <div>
                  <label className="field-label">Confirm password</label>
                  <input
                    className={`input ${confirm && confirm !== newPassword ? 'input-error' : ''}`}
                    type="password"
                    value={confirm}
                    onChange={(e) => setConfirm(e.target.value)}
                  />
                </div>
                <button className="btn-primary w-full" disabled={!valid || reset.isPending} onClick={() => reset.mutate()}>
                  {reset.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Reset password'}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
