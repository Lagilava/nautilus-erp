import type { ReactNode } from 'react';
import { Loader2, Inbox } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export function Spinner({ className = 'h-5 w-5' }: { className?: string }) {
  return <Loader2 className={`animate-spin text-lagoon-500 ${className}`} />;
}

export function PageHeader({
  title,
  subtitle,
  actions,
  icon: Icon,
  eyebrow,
}: {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  /** Optional module icon, shown in a gradient badge beside the title. */
  icon?: LucideIcon;
  /** Optional small uppercase label above the title. */
  eyebrow?: string;
}) {
  return (
    <div className="mb-6 flex items-end justify-between gap-4">
      <div className="flex min-w-0 items-center gap-3.5">
        {Icon && (
          <span className="icon-badge hidden h-11 w-11 shrink-0 sm:inline-flex">
            <Icon className="h-5 w-5" strokeWidth={2} />
          </span>
        )}
        <div className="min-w-0">
          {eyebrow && (
            <p className="mb-0.5 text-[11px] font-semibold uppercase tracking-[0.16em] text-lagoon-600">
              {eyebrow}
            </p>
          )}
          <h1 className="truncate text-2xl font-semibold text-ink">{title}</h1>
          {subtitle && <p className="mt-1 text-sm text-ink-muted">{subtitle}</p>}
        </div>
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}

export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-20 text-ink-muted">
      <Spinner />
      <span className="text-sm">{label}</span>
    </div>
  );
}

/** A shimmering placeholder bar. */
export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`shimmer rounded bg-line/60 ${className}`} />;
}

/** Content-shaped loading state for data tables. */
export function TableSkeleton({ rows = 6, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="divide-y divide-line">
      <div className="flex gap-4 px-4 py-3">
        {Array.from({ length: cols }).map((_, i) => (
          <Skeleton key={i} className="h-3 flex-1" />
        ))}
      </div>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex gap-4 px-4 py-4">
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} className={`h-4 flex-1 ${c === 0 ? 'max-w-[8rem]' : ''}`} />
          ))}
        </div>
      ))}
    </div>
  );
}

export function EmptyState({
  title,
  hint,
  icon: Icon = Inbox,
  action,
}: {
  title: string;
  hint?: string;
  icon?: LucideIcon;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-line bg-lagoon-50/20 py-16 text-center">
      <span className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-full bg-lagoon-50 text-lagoon-500">
        <Icon className="h-6 w-6" strokeWidth={1.75} />
      </span>
      <p className="font-medium text-ink-soft">{title}</p>
      {hint && <p className="mt-1 max-w-sm text-sm text-ink-muted">{hint}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

export function ErrorNote({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-danger/30 bg-danger/5 px-3 py-2 text-sm text-danger">
      {message}
    </div>
  );
}

/**
 * A headline figure card — the shared building block for dashboard-style stats
 * and page summaries. Optionally shows a module icon and a caption.
 */
export function StatTile({
  label,
  value,
  hint,
  icon: Icon,
}: {
  label: string;
  value: string;
  hint?: string;
  icon?: LucideIcon;
}) {
  return (
    <div className="card card-hover p-5">
      <div className="flex items-start justify-between gap-3">
        <p className="text-xs font-medium uppercase tracking-wider text-ink-muted">{label}</p>
        {Icon && (
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-lagoon-50 text-lagoon-500">
            <Icon className="h-4 w-4" />
          </span>
        )}
      </div>
      <p className="mt-2 font-display text-3xl font-semibold tabular text-ink">{value}</p>
      {hint && <p className="mt-1 text-xs text-ink-muted">{hint}</p>}
    </div>
  );
}

/** A card with a titled header row, used to frame page sections. */
export function SectionCard({
  title,
  icon: Icon,
  actions,
  children,
  bodyClassName = '',
}: {
  title: string;
  icon?: LucideIcon;
  actions?: ReactNode;
  children: ReactNode;
  bodyClassName?: string;
}) {
  return (
    <div className="card overflow-hidden">
      <div className="flex items-center justify-between gap-3 border-b border-line px-5 py-4">
        <div className="flex items-center gap-2">
          {Icon && <Icon className="h-4 w-4 text-lagoon-500" />}
          <h2 className="text-base font-semibold text-ink">{title}</h2>
        </div>
        {actions}
      </div>
      <div className={bodyClassName}>{children}</div>
    </div>
  );
}

const PILL_TONES: Record<string, string> = {
  neutral: 'bg-lagoon-50 text-lagoon-700',
  success: 'bg-success/10 text-success',
  warning: 'bg-warning/10 text-warning',
  danger: 'bg-danger/10 text-danger',
  gold: 'bg-sand-100 text-sand-700',
  azure: 'bg-azure-50 text-azure-700',
};

export function StatusPill({ label, tone = 'neutral' }: { label: string; tone?: keyof typeof PILL_TONES }) {
  return <span className={`pill ${PILL_TONES[tone] ?? PILL_TONES.neutral}`}>{label}</span>;
}
