import { useState } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAuth } from '../auth/AuthContext';
import { apiErrorMessage } from '../lib/api';
import { BrandMark } from '../components/Brand';
import { Spinner, ErrorNote } from '../components/ui';

const schema = z.object({
  email: z.string().email('Enter a valid email.'),
  password: z.string().min(1, 'Password is required.'),
});
type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';

  const onSubmit = async (values: FormValues) => {
    setError(null);
    try {
      await login(values.email, values.password);
      navigate(from, { replace: true });
    } catch (e) {
      setError(apiErrorMessage(e, 'Unable to sign in. Check your credentials.'));
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand panel — the calm, open Fiji note */}
      <div className="relative hidden overflow-hidden bg-lagoon-700 lg:block">
        <div className="absolute inset-0 bg-gradient-to-br from-lagoon-600 to-lagoon-800" />
        <svg className="absolute bottom-0 left-0 w-full text-lagoon-500/40" viewBox="0 0 400 120" preserveAspectRatio="none">
          <path d="M0 60 C 60 20, 120 100, 200 60 S 340 20, 400 60 V120 H0 Z" fill="currentColor" />
        </svg>
        <svg className="absolute bottom-0 left-0 w-full text-lagoon-500/25" viewBox="0 0 400 120" preserveAspectRatio="none">
          <path d="M0 80 C 80 40, 140 110, 220 80 S 360 50, 400 80 V120 H0 Z" fill="currentColor" />
        </svg>
        <div className="relative flex h-full flex-col justify-between p-12">
          <div className="flex items-center gap-3">
            <BrandMark className="h-9 w-9" />
            <span className="font-display text-xl font-semibold text-white">Nautilus ERP</span>
          </div>
          <div className="max-w-md">
            <h1 className="font-display text-4xl font-semibold leading-tight text-white">
              Business management, built for Fiji.
            </h1>
            <p className="mt-4 text-lagoon-100">
              Inventory, sales, and purchasing in one considered, compliance-first platform —
              designed to grow with you.
            </p>
          </div>
          <p className="text-xs text-lagoon-200">© {new Date().getFullYear()} Nautilus ERP</p>
        </div>
      </div>

      {/* Form panel */}
      <div className="flex items-center justify-center bg-canvas px-6 py-12">
        <div className="w-full max-w-sm">
          <div className="mb-8 lg:hidden">
            <BrandMark className="h-10 w-10" />
          </div>
          <h2 className="text-2xl font-semibold text-ink">Sign in</h2>
          <p className="mt-1 text-sm text-ink-muted">Welcome back. Enter your details to continue.</p>

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-4">
            {error && <ErrorNote message={error} />}
            <div>
              <label className="field-label" htmlFor="email">
                Email
              </label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                className={`input ${errors.email ? 'input-error' : ''}`}
                placeholder="you@company.fj"
                {...register('email')}
              />
              {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
            </div>
            <div>
              <label className="field-label" htmlFor="password">
                Password
              </label>
              <input
                id="password"
                type="password"
                autoComplete="current-password"
                className={`input ${errors.password ? 'input-error' : ''}`}
                placeholder="••••••••"
                {...register('password')}
              />
              {errors.password && <p className="mt-1 text-xs text-danger">{errors.password.message}</p>}
            </div>
            <button type="submit" className="btn-primary w-full" disabled={isSubmitting}>
              {isSubmitting ? <Spinner className="h-4 w-4 text-white" /> : 'Sign in'}
            </button>
            <Link to="/forgot-password" className="block text-center text-sm text-ink-muted hover:text-ink">
              Forgot your password?
            </Link>
          </form>
        </div>
      </div>
    </div>
  );
}
