import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { StatusPill } from './ui';
import { statusTone, humanize } from '../lib/status';

export function DetailScaffold({
  backTo,
  backLabel,
  title,
  status,
  actions,
  icon: Icon,
  children,
}: {
  backTo: string;
  backLabel: string;
  title: string;
  status?: string;
  actions?: ReactNode;
  icon?: LucideIcon;
  children: ReactNode;
}) {
  return (
    <>
      <Link
        to={backTo}
        className="group mb-4 inline-flex items-center gap-1.5 text-sm text-ink-muted transition-colors hover:text-ink"
      >
        <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" /> {backLabel}
      </Link>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3.5">
          {Icon && (
            <span className="icon-badge hidden h-11 w-11 shrink-0 sm:inline-flex">
              <Icon className="h-5 w-5" strokeWidth={2} />
            </span>
          )}
          <h1 className="font-display text-2xl font-semibold text-ink tabular">{title}</h1>
          {status && <StatusPill label={humanize(status)} tone={statusTone(status)} />}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </>
  );
}

/** A labelled figure used in the totals summary of a document. */
export function SummaryRow({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-center justify-between py-1.5">
      <span className={strong ? 'font-medium text-ink' : 'text-sm text-ink-muted'}>{label}</span>
      <span className={`tabular ${strong ? 'text-lg font-semibold text-ink' : 'text-sm text-ink-soft'}`}>{value}</span>
    </div>
  );
}
