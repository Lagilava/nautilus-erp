import { useParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Send, Ban, ScrollText } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { JournalEntryDetail } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { humanize } from '../../lib/status';
import { Loading, ErrorNote, Spinner } from '../../components/ui';
import { DetailScaffold, SummaryRow } from '../../components/DetailScaffold';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

export function JournalEntryDetailPage() {
  const { id = '' } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');

  const { data: je, isLoading, error } = useQuery({
    queryKey: ['journal-entry', id],
    queryFn: async () => (await api.get<JournalEntryDetail>(`/api/journal-entries/${id}`)).data,
  });

  const action = useMutation({
    mutationFn: (verb: string) => api.post(`/api/journal-entries/${id}/${verb}`),
    onSuccess: (_r, verb) => {
      qc.invalidateQueries({ queryKey: ['journal-entry', id] });
      qc.invalidateQueries({ queryKey: ['journal-entries'] });
      toast(verb === 'post' ? 'Journal entry posted.' : 'Journal entry voided.');
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !je) return <ErrorNote message={apiErrorMessage(error)} />;

  return (
    <DetailScaffold
      icon={ScrollText}
      backTo="/accounting/journal-entries"
      backLabel="Journal Entries"
      title={je.reference}
      status={je.status}
      actions={
        canWrite && (
          <>
            {je.status === 'Draft' && (
              <button className="btn-primary" disabled={action.isPending} onClick={() => action.mutate('post')}>
                <Send className="h-4 w-4" /> Post
              </button>
            )}
            {je.status === 'Posted' && (
              <button className="btn-ghost text-danger" disabled={action.isPending} onClick={() => action.mutate('void')}>
                <Ban className="h-4 w-4" /> Void
              </button>
            )}
            {action.isPending && <Spinner className="h-4 w-4" />}
          </>
        )
      }
    >
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <div className="card overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  <th className="table-head px-4 py-3">Account</th>
                  <th className="table-head px-4 py-3">Memo</th>
                  <th className="table-head px-4 py-3 text-right">Debit</th>
                  <th className="table-head px-4 py-3 text-right">Credit</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {je.lines.map((l) => (
                  <tr key={l.id}>
                    <td className="px-4 py-3 text-ink">
                      {l.accountCode} — {l.accountName}
                    </td>
                    <td className="px-4 py-3 text-ink-muted">{l.memo ?? '—'}</td>
                    <td className="px-4 py-3 text-right tabular text-ink">{l.debit ? fmtMoney(l.debit) : ''}</td>
                    <td className="px-4 py-3 text-right tabular text-ink">{l.credit ? fmtMoney(l.credit) : ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="space-y-4">
          <div className="card p-5">
            <SummaryRow label="Total debits" value={fmtMoney(je.totalDebits)} />
            <SummaryRow label="Total credits" value={fmtMoney(je.totalCredits)} strong />
          </div>

          <div className="card p-5">
            <div className="flex items-center justify-between">
              <span className="text-sm text-ink-muted">Entry date</span>
              <span className="tabular text-sm text-ink-soft">{fmtDate(je.entryDate)}</span>
            </div>
            <div className="mt-3 flex items-center justify-between">
              <span className="text-sm text-ink-muted">Source</span>
              <span className="text-sm text-ink-soft">{humanize(je.source)}</span>
            </div>
            {je.description && (
              <div className="mt-3">
                <span className="text-sm text-ink-muted">Description</span>
                <p className="mt-1 text-sm text-ink-soft">{je.description}</p>
              </div>
            )}
            {je.preparedBy && (
              <div className="mt-3 flex items-center justify-between">
                <span className="text-sm text-ink-muted">Prepared by</span>
                <span className="text-sm text-ink-soft">{je.preparedBy}</span>
              </div>
            )}
            {je.postedBy && (
              <div className="mt-3 flex items-center justify-between">
                <span className="text-sm text-ink-muted">Posted by</span>
                <span className="text-sm text-ink-soft">{je.postedBy}</span>
              </div>
            )}
          </div>
        </div>
      </div>
    </DetailScaffold>
  );
}
