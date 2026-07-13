import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Boxes,
  KeyRound,
  Lock,
  LogIn,
  Mail,
  ShieldCheck,
  ShoppingCart,
  Sparkles,
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { apiErrorMessage } from '../lib/api';
import { BrandMark, WaveMotif } from '../components/Brand';
import { Spinner, ErrorNote } from '../components/ui';

const schema = z.object({
  email: z.string().email('Enter a valid email.'),
  password: z.string().min(1, 'Password is required.'),
});
type FormValues = z.infer<typeof schema>;

const FEATURES = [
  { icon: Boxes, label: 'Real-time inventory across every warehouse' },
  { icon: ShoppingCart, label: 'Sales, purchasing and invoicing in one flow' },
  { icon: ShieldCheck, label: 'Role-based access with a full audit trail' },
];

const TRUST_BADGES = ['Encrypted in transit', 'Multi-factor auth', 'Full audit trail'];

export function LoginPage() {
  const { login, verifyMfa } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [challengeToken, setChallengeToken] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [verifying, setVerifying] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';

  const onSubmit = async (values: FormValues) => {
    setError(null);
    try {
      const outcome = await login(values.email, values.password);
      if (outcome.mfaRequired) {
        setChallengeToken(outcome.challengeToken);
        return;
      }
      navigate(from, { replace: true });
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to sign in. Check your credentials.'));
    }
  };

  const onVerifyMfa = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setVerifying(true);
    try {
      await verifyMfa(challengeToken!, code);
      navigate(from, { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, 'Invalid or expired code.'));
    } finally {
      setVerifying(false);
    }
  };

  return (
    <div className="relative grid min-h-screen overflow-hidden bg-canvas lg:grid-cols-2">
      {/* Brand panel — the calm, open Fiji note, now with real depth and motion */}
      <div className="relative hidden overflow-hidden bg-brand-gradient lg:block">
        {/* Drifting light — three soft blurred orbs, each on its own slow orbit. */}
        <div className="pointer-events-none absolute -left-16 -top-16 h-72 w-72 rounded-full bg-lagoon-300/30 blur-3xl animate-drift" aria-hidden="true" />
        <div className="pointer-events-none absolute -bottom-24 -right-10 h-96 w-96 rounded-full bg-[#2C63AB]/40 blur-3xl animate-drift-slow" aria-hidden="true" />
        <div className="pointer-events-none absolute right-1/3 top-1/3 h-56 w-56 rounded-full bg-sand-300/20 blur-3xl animate-drift" style={{ animationDelay: '3s' }} aria-hidden="true" />

        {/* Faint dot-grid for texture beneath everything else. */}
        <svg className="pointer-events-none absolute inset-0 h-full w-full opacity-[0.12]" aria-hidden="true">
          <pattern id="login-dots" width="22" height="22" patternUnits="userSpaceOnUse">
            <circle cx="1.5" cy="1.5" r="1.5" fill="white" />
          </pattern>
          <rect width="100%" height="100%" fill="url(#login-dots)" />
        </svg>
        <WaveMotif className="text-[#2C63AB]/30" />

        <div className="relative flex h-full flex-col justify-between p-12">
          <div className="flex animate-fade-in items-center gap-3">
            <div className="relative flex h-11 w-11 items-center justify-center rounded-xl bg-white p-1.5 shadow-lg ring-1 ring-white/40">
              <span className="absolute inset-0 -z-10 animate-pulse rounded-xl bg-white/40 blur-md" />
              <BrandMark className="h-full w-full" />
            </div>
            <span className="font-display text-xl font-semibold text-white">Nautilus ERP</span>
          </div>

          <div className="max-w-md">
            <span
              className="inline-flex animate-fade-in items-center gap-1.5 rounded-full border border-white/25 bg-white/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-lagoon-100 backdrop-blur-sm"
              style={{ animationDelay: '80ms' }}
            >
              <Sparkles className="h-3 w-3 text-sand-300" />
              Enterprise ERP · Fiji
            </span>
            <h1
              className="mt-5 animate-fade-in font-display text-4xl font-semibold leading-tight text-white xl:text-5xl"
              style={{ animationDelay: '160ms' }}
            >
              Business management,
              <br />
              built for{' '}
              <span className="bg-gradient-to-r from-white via-lagoon-100 to-white bg-clip-text text-transparent">
                Fiji.
              </span>
            </h1>
            <p className="mt-4 animate-fade-in text-lagoon-100" style={{ animationDelay: '240ms' }}>
              Inventory, sales, and purchasing in one considered, compliance-first platform —
              designed to grow with you.
            </p>

            {/* A "live view" glass panel — the product-preview flourish, but safely in the
                normal document flow so it can never drift over the sign-in form. */}
            <div
              className="mt-7 animate-fade-in overflow-hidden rounded-2xl border border-white/15 bg-white/[0.07] backdrop-blur-sm"
              style={{ animationDelay: '320ms' }}
            >
              <div className="flex items-center gap-1.5 border-b border-white/10 px-4 py-2.5">
                <span className="h-2 w-2 rounded-full bg-white/30" />
                <span className="h-2 w-2 rounded-full bg-white/30" />
                <span className="h-2 w-2 rounded-full bg-white/30" />
                <span className="ml-2 text-[10px] font-medium uppercase tracking-wider text-white/60">
                  Live view
                </span>
              </div>
              <ul className="divide-y divide-white/10">
                {FEATURES.map(({ icon: Icon, label }) => (
                  <li key={label} className="flex items-center gap-3 px-4 py-3 text-sm text-white">
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/10 ring-1 ring-white/20">
                      <Icon className="h-4 w-4" strokeWidth={2} />
                    </span>
                    {label}
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div className="flex animate-fade-in flex-wrap items-center gap-x-4 gap-y-2" style={{ animationDelay: '620ms' }}>
            {TRUST_BADGES.map((label) => (
              <span key={label} className="flex items-center gap-1.5 text-xs text-lagoon-200">
                <ShieldCheck className="h-3.5 w-3.5" />
                {label}
              </span>
            ))}
            <span className="text-xs text-lagoon-300">© {new Date().getFullYear()}</span>
          </div>
        </div>
      </div>

      {/* Form panel */}
      <div className="relative flex items-center justify-center px-6 py-12">
        {/* Faint echo of the brand motion on this side too, kept subtle over the canvas. */}
        <div className="pointer-events-none absolute -right-24 top-10 h-64 w-64 rounded-full bg-lagoon-300/10 blur-3xl animate-drift" aria-hidden="true" />
        <div className="pointer-events-none absolute -bottom-10 -left-16 h-56 w-56 rounded-full bg-[#2C63AB]/10 blur-3xl animate-drift-slow" aria-hidden="true" />

        <div className="relative w-full max-w-sm">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-surface p-1.5 shadow-card ring-1 ring-line">
              <BrandMark className="h-full w-full" />
            </div>
            <span className="font-display text-lg font-semibold text-ink">Nautilus ERP</span>
          </div>

          <div className="relative animate-fade-in" style={{ animationDelay: '120ms' }}>
            {/* Soft colour glow behind the card — the "impressive" halo. */}
            <div className="absolute -inset-4 -z-10 rounded-[2rem] bg-gradient-to-br from-lagoon-300/30 via-transparent to-[#2C63AB]/30 blur-2xl" aria-hidden="true" />

            <div className="rounded-2xl border border-line bg-surface p-7 shadow-lift sm:p-8">
              {challengeToken ? (
                <>
                  <div className="mb-1 flex items-center gap-2.5">
                    <span className="icon-badge h-9 w-9 shrink-0">
                      <ShieldCheck className="h-4 w-4" strokeWidth={2} />
                    </span>
                    <h2 className="font-display text-xl font-semibold text-ink">Verification code</h2>
                  </div>
                  <p className="mt-1 text-sm text-ink-muted">
                    Enter the 6-digit code from your authenticator app, or a recovery code.
                  </p>
                  <form onSubmit={onVerifyMfa} className="mt-7 space-y-4">
                    {error && <ErrorNote message={error} />}
                    <div>
                      <label className="field-label" htmlFor="code">
                        Code
                      </label>
                      <div className="group relative">
                        <KeyRound className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-muted transition-colors group-focus-within:text-lagoon-500" />
                        <input
                          id="code"
                          autoComplete="one-time-code"
                          className="input pl-9 tracking-[0.3em]"
                          placeholder="123456"
                          value={code}
                          onChange={(e) => setCode(e.target.value)}
                        />
                      </div>
                    </div>
                    <button type="submit" className="btn-primary w-full" disabled={verifying || !code}>
                      {verifying ? <Spinner className="h-4 w-4 text-white" /> : 'Verify'}
                    </button>
                    <button
                      type="button"
                      className="block w-full text-center text-sm text-ink-muted hover:text-ink"
                      onClick={() => {
                        setChallengeToken(null);
                        setCode('');
                        setError(null);
                      }}
                    >
                      Back to sign in
                    </button>
                  </form>
                </>
              ) : (
                <>
                  <h2 className="font-display text-2xl font-semibold text-ink">Sign in</h2>
                  <p className="mt-1 text-sm text-ink-muted">Welcome back. Enter your details to continue.</p>

                  <form onSubmit={handleSubmit(onSubmit)} className="mt-7 space-y-4">
                    {error && <ErrorNote message={error} />}
                    <div>
                      <label className="field-label" htmlFor="email">
                        Email
                      </label>
                      <div className="group relative">
                        <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-muted transition-colors group-focus-within:text-lagoon-500" />
                        <input
                          id="email"
                          type="email"
                          autoComplete="username"
                          className={`input pl-9 ${errors.email ? 'input-error' : ''}`}
                          placeholder="you@company.fj"
                          {...register('email')}
                        />
                      </div>
                      {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
                    </div>
                    <div>
                      <div className="flex items-center justify-between">
                        <label className="field-label" htmlFor="password">
                          Password
                        </label>
                        <Link to="/forgot-password" className="mb-1.5 text-xs font-medium text-lagoon-600 hover:text-lagoon-700">
                          Forgot?
                        </Link>
                      </div>
                      <div className="group relative">
                        <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-muted transition-colors group-focus-within:text-lagoon-500" />
                        <input
                          id="password"
                          type="password"
                          autoComplete="current-password"
                          className={`input pl-9 ${errors.password ? 'input-error' : ''}`}
                          placeholder="••••••••"
                          {...register('password')}
                        />
                      </div>
                      {errors.password && <p className="mt-1 text-xs text-danger">{errors.password.message}</p>}
                    </div>
                    <button type="submit" className="btn-primary w-full" disabled={isSubmitting}>
                      {isSubmitting ? (
                        <Spinner className="h-4 w-4 text-white" />
                      ) : (
                        <>
                          <LogIn className="h-4 w-4" /> Sign in
                        </>
                      )}
                    </button>
                  </form>
                </>
              )}
            </div>
          </div>

          <p
            className="mt-6 flex animate-fade-in items-center justify-center gap-1.5 text-center text-xs text-ink-muted"
            style={{ animationDelay: '280ms' }}
          >
            <ShieldCheck className="h-3.5 w-3.5" />
            Protected by multi-factor authentication
          </p>
        </div>
      </div>
    </div>
  );
}
