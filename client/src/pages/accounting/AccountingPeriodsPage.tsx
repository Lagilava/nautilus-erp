import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Lock } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { AccountingPeriod } from '../../lib/types';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/**
 * Accounting period locking. Closing a period (Administrator only) blocks any further journal
 * entry — manual or auto-posted — from being dated inside it. The backend rejects the close if
 * unposted (Draft) entries remain in the period, forcing cleanup first.
 */
export function AccountingPeriodsPage() {
  const { hasRole } = useAuth();
  const isAdmin = hasRole('Administrator');
  const toast = useToast();
  const qc = useQueryClient();
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);

  const periods = useQuery({
    queryKey: ['accounting-periods'],
    queryFn: async () => (await api.get<AccountingPeriod[]>('/api/accounting-periods')).data,
  });

  const closeMutation = useMutation({
    mutationFn: async () => api.post('/api/accounting-periods/close', { year, month }),
    onSuccess: () => {
      toast(`Period ${year}-${String(month).padStart(2, '0')} closed.`);
      qc.invalidateQueries({ queryKey: ['accounting-periods'] });
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  return (
    <>
      <PageHeader
        icon={CalendarClock}
        eyebrow="Accounting"
        title="Accounting Periods"
        subtitle="Close a period to prevent any further journal entry from posting into it."
      />

      {isAdmin && (
        <div className="card mb-4 flex flex-wrap items-end gap-3 p-4">
          <div>
            <label className="field-label" htmlFor="period-year">
              Year
            </label>
            <input
              id="period-year"
              type="number"
              className="input w-28"
              value={year}
              onChange={(e) => setYear(Number(e.target.value))}
            />
          </div>
          <div>
            <label className="field-label" htmlFor="period-month">
              Month
            </label>
            <select
              id="period-month"
              className="input w-40"
              value={month}
              onChange={(e) => setMonth(Number(e.target.value))}
            >
              {MONTH_NAMES.map((name, idx) => (
                <option key={name} value={idx + 1}>
                  {name}
                </option>
              ))}
            </select>
          </div>
          <button className="btn-primary" disabled={closeMutation.isPending} onClick={() => closeMutation.mutate()}>
            {closeMutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : <Lock className="h-4 w-4" />}
            Close period
          </button>
        </div>
      )}

      <div className="card overflow-hidden">
        {periods.isLoading ? (
          <TableSkeleton cols={3} />
        ) : periods.error ? (
          <div className="p-4">
            <ErrorNote message={apiErrorMessage(periods.error)} />
          </div>
        ) : periods.data && periods.data.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  <th className="table-head px-4 py-3">Period</th>
                  <th className="table-head px-4 py-3">Status</th>
                  <th className="table-head px-4 py-3">Closed by</th>
                  <th className="table-head px-4 py-3">Closed at</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {periods.data.map((p) => (
                  <tr key={p.id}>
                    <td className="px-4 py-3 font-medium text-ink tabular">
                      {p.year}-{String(p.month).padStart(2, '0')}
                    </td>
                    <td className="px-4 py-3">
                      <StatusPill label={p.isClosed ? 'Closed' : 'Open'} tone={p.isClosed ? 'danger' : 'success'} />
                    </td>
                    <td className="px-4 py-3 text-ink-soft">{p.closedBy ?? '—'}</td>
                    <td className="px-4 py-3 text-ink-muted">
                      {p.closedAt ? new Date(p.closedAt).toLocaleString() : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-4">
            <EmptyState title="No periods closed yet" hint="Closing a period only creates a record for it here." />
          </div>
        )}
      </div>
    </>
  );
}
