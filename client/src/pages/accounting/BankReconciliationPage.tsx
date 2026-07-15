import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, Link2, Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { BankStatementLine, BankStatementLineSource, UnreconciledJournalLine } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, Spinner } from '../../components/ui';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

/**
 * Manual click-to-match reconciliation: unreconciled bank statement lines on the left,
 * unreconciled Cash-account journal lines on the right. Selecting one of each and clicking
 * "Match" calls POST /api/bank-reconciliation/match; the backend enforces the amounts agree.
 */
export function BankReconciliationPage() {
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const toast = useToast();
  const qc = useQueryClient();
  const [selectedStatementLine, setSelectedStatementLine] = useState<string | null>(null);
  const [selectedJournalLine, setSelectedJournalLine] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);

  const statementLines = useQuery({
    queryKey: ['bank-reconciliation', 'statement-lines'],
    queryFn: async () =>
      (await api.get<BankStatementLine[]>('/api/bank-reconciliation/statement-lines/unreconciled')).data,
  });

  const journalLines = useQuery({
    queryKey: ['bank-reconciliation', 'journal-lines'],
    queryFn: async () =>
      (await api.get<UnreconciledJournalLine[]>('/api/bank-reconciliation/journal-lines/unreconciled')).data,
  });

  const matchMutation = useMutation({
    mutationFn: async () =>
      api.post('/api/bank-reconciliation/match', {
        bankStatementLineId: selectedStatementLine,
        journalLineId: selectedJournalLine,
      }),
    onSuccess: () => {
      toast('Statement line matched.');
      setSelectedStatementLine(null);
      setSelectedJournalLine(null);
      qc.invalidateQueries({ queryKey: ['bank-reconciliation'] });
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  const canMatch = !!selectedStatementLine && !!selectedJournalLine;

  return (
    <>
      <PageHeader
        icon={Landmark}
        eyebrow="Accounting"
        title="Bank Reconciliation"
        subtitle="Match imported or manually entered bank statement lines to posted Cash-account journal lines."
        actions={
          <div className="flex items-center gap-2">
            {canWrite && (
              <button className="btn-secondary" onClick={() => setAdding(true)}>
                <Plus className="h-4 w-4" /> Add Statement Line
              </button>
            )}
            {canWrite && (
              <button className="btn-primary" disabled={!canMatch || matchMutation.isPending} onClick={() => matchMutation.mutate()}>
                {matchMutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : <Link2 className="h-4 w-4" />}
                Match
              </button>
            )}
          </div>
        }
      />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="card overflow-hidden">
          <div className="border-b border-line px-4 py-3 font-medium text-ink">Unreconciled statement lines</div>
          {statementLines.isLoading ? (
            <TableSkeleton cols={3} />
          ) : statementLines.error ? (
            <div className="p-4">
              <ErrorNote message={apiErrorMessage(statementLines.error)} />
            </div>
          ) : statementLines.data && statementLines.data.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line">
                    <th className="table-head px-4 py-3">Date</th>
                    <th className="table-head px-4 py-3">Description</th>
                    <th className="table-head px-4 py-3 text-right">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {statementLines.data.map((l) => (
                    <tr
                      key={l.id}
                      className={`cursor-pointer transition-colors hover:bg-lagoon-50/40 ${
                        selectedStatementLine === l.id ? 'bg-lagoon-100/60' : ''
                      }`}
                      onClick={() => setSelectedStatementLine(l.id)}
                    >
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(l.statementDate)}</td>
                      <td className="px-4 py-3 text-ink-soft">{l.description ?? '—'}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="p-4">
              <EmptyState title="Nothing to reconcile" hint="All statement lines are matched." />
            </div>
          )}
        </div>

        <div className="card overflow-hidden">
          <div className="border-b border-line px-4 py-3 font-medium text-ink">Unreconciled Cash journal lines</div>
          {journalLines.isLoading ? (
            <TableSkeleton cols={3} />
          ) : journalLines.error ? (
            <div className="p-4">
              <ErrorNote message={apiErrorMessage(journalLines.error)} />
            </div>
          ) : journalLines.data && journalLines.data.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line">
                    <th className="table-head px-4 py-3">Date</th>
                    <th className="table-head px-4 py-3">Reference</th>
                    <th className="table-head px-4 py-3 text-right">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {journalLines.data.map((l) => (
                    <tr
                      key={l.journalLineId}
                      className={`cursor-pointer transition-colors hover:bg-lagoon-50/40 ${
                        selectedJournalLine === l.journalLineId ? 'bg-lagoon-100/60' : ''
                      }`}
                      onClick={() => setSelectedJournalLine(l.journalLineId)}
                    >
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(l.entryDate)}</td>
                      <td className="px-4 py-3 font-medium text-ink tabular">{l.reference}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.debit - l.credit)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="p-4">
              <EmptyState title="Nothing to reconcile" hint="All Cash journal lines are matched." />
            </div>
          )}
        </div>
      </div>

      {adding && <AddStatementLineModal onClose={() => setAdding(false)} />}
    </>
  );
}

function AddStatementLineModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [statementDate, setStatementDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [source, setSource] = useState<BankStatementLineSource>('Manual');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () =>
      api.post('/api/bank-reconciliation/statement-lines', {
        statementDate,
        amount: Number(amount),
        description: description || null,
        source,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['bank-reconciliation'] });
      toast('Statement line added.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  function submit() {
    setError(null);
    if (!amount || Number(amount) === 0) {
      setError('Enter a non-zero amount (positive for a deposit, negative for a withdrawal).');
      return;
    }
    mutation.mutate();
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Add bank statement line"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={mutation.isPending} onClick={submit}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Add'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="bsl-date">
              Statement date
            </label>
            <input
              id="bsl-date"
              type="date"
              className="input"
              value={statementDate}
              onChange={(e) => setStatementDate(e.target.value)}
            />
          </div>
          <div>
            <label className="field-label" htmlFor="bsl-amount">
              Amount
            </label>
            <input
              id="bsl-amount"
              type="number"
              step="0.01"
              className="input"
              placeholder="Positive = deposit, negative = withdrawal"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
          </div>
        </div>
        <div>
          <label className="field-label" htmlFor="bsl-description">
            Description
          </label>
          <input
            id="bsl-description"
            className="input"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
        <div>
          <label className="field-label" htmlFor="bsl-source">
            Source
          </label>
          <select
            id="bsl-source"
            className="input"
            value={source}
            onChange={(e) => setSource(e.target.value as BankStatementLineSource)}
          >
            <option value="Manual">Manual</option>
            <option value="Imported">Imported</option>
          </select>
        </div>
      </div>
    </Modal>
  );
}
