import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { Wordmark } from '../components/Brand';
import { ErrorNote, Spinner } from '../components/ui';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  const request = useMutation({
    mutationFn: () => api.post('/api/auth/forgot-password', { email }),
    onSuccess: () => setSent(true),
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex justify-center">
          <Wordmark />
        </div>

        <div className="card p-6">
          {sent ? (
            <div className="text-center">
              <h1 className="text-xl font-semibold text-ink">Check your email</h1>
              {/* Deliberately generic: the API does not disclose whether the account exists. */}
              <p className="mt-2 text-sm text-ink-muted">
                If an account exists for {email}, we've sent a link to reset its password.
              </p>
              <Link to="/login" className="btn-secondary mt-5 w-full">
                Back to sign in
              </Link>
            </div>
          ) : (
            <>
              <h1 className="text-xl font-semibold text-ink">Reset your password</h1>
              <p className="mt-1 text-sm text-ink-muted">We'll email you a link to choose a new password.</p>

              <div className="mt-5 space-y-4">
                {error && <ErrorNote message={error} />}
                <div>
                  <label className="field-label" htmlFor="fp-email">
                    Email
                  </label>
                  <input
                    id="fp-email"
                    className="input"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </div>
                <button className="btn-primary w-full" disabled={!email || request.isPending} onClick={() => request.mutate()}>
                  {request.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Send reset link'}
                </button>
                <Link to="/login" className="block text-center text-sm text-ink-muted hover:text-ink">
                  Back to sign in
                </Link>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
