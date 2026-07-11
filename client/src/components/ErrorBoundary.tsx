import * as Sentry from '@sentry/react';
import type { ReactNode } from 'react';
import { BrandMark } from './Brand';

/**
 * Top-level crash boundary. A render error anywhere in the tree lands here instead of a blank
 * white screen. Reports to Sentry (a no-op if VITE_SENTRY_DSN isn't set — see main.tsx).
 */
export function ErrorBoundary({ children }: { children: ReactNode }) {
  return (
    <Sentry.ErrorBoundary
      fallback={({ resetError }) => (
        <div className="flex min-h-screen items-center justify-center bg-canvas px-6">
          <div className="card w-full max-w-sm space-y-4 p-6 text-center">
            <BrandMark className="mx-auto h-10 w-10" />
            <h1 className="text-lg font-semibold text-ink">Something went wrong</h1>
            <p className="text-sm text-ink-muted">
              An unexpected error occurred. Reloading usually fixes it.
            </p>
            <button
              className="btn-primary w-full"
              onClick={() => {
                resetError();
                window.location.href = '/';
              }}
            >
              Reload
            </button>
          </div>
        </div>
      )}
    >
      {children}
    </Sentry.ErrorBoundary>
  );
}
