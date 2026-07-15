import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ScrollText, Plus, Trash2 } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type {
  Account,
  JournalEntryStatus,
  JournalEntrySource,
  JournalEntrySummary,
  ManualJournalLineInput,
  Paged,
} from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize } from '../../lib/status';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { Pagination } from '../../components/Pagination';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

const STATUSES: JournalEntryStatus[] = ['Draft', 'Posted', 'Voided'];
const SOURCES: JournalEntrySource[] = ['Manual', 'SalesInvoice', 'SupplierInvoice', 'Payment'];

export function JournalEntriesPage() {
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<JournalEntryStatus | ''>('');
  const [source, setSource] = useState<JournalEntrySource | ''>('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [creating, setCreating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ['journal-entries', page, status, source, fromDate, toDate],
    queryFn: async () =>
      (
        await api.get<Paged<JournalEntrySummary>>('/api/journal-entries', {
          params: {
            page,
            pageSize: 15,
            status: status || undefined,
            source: source || undefined,
            fromDate: fromDate || undefined,
            toDate: toDate || undefined,
          },
        })
      ).data,
  });

  return (
    <>
      <PageHeader
        icon={ScrollText}
        eyebrow="Accounting"
        title="Journal Entries"
        subtitle="Manual and system-posted double-entry transactions."
        actions={
          canWrite && (
            <button className="btn-primary" onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4" /> New Journal Entry
            </button>
          )
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <select
          className="input w-auto"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as JournalEntryStatus | '');
            setPage(1);
          }}
          aria-label="Filter by status"
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {humanize(s)}
            </option>
          ))}
        </select>
        <select
          className="input w-auto"
          value={source}
          onChange={(e) => {
            setSource(e.target.value as JournalEntrySource | '');
            setPage(1);
          }}
          aria-label="Filter by source"
        >
          <option value="">All sources</option>
          {SOURCES.map((s) => (
            <option key={s} value={s}>
              {humanize(s)}
            </option>
          ))}
        </select>
        <input
          type="date"
          className="input w-auto"
          value={fromDate}
          onChange={(e) => {
            setFromDate(e.target.value);
            setPage(1);
          }}
          aria-label="From date"
        />
        <input
          type="date"
          className="input w-auto"
          value={toDate}
          onChange={(e) => {
            setToDate(e.target.value);
            setPage(1);
          }}
          aria-label="To date"
        />
      </div>

      <div className="card overflow-hidden">
        {isLoading ? (
          <TableSkeleton cols={6} />
        ) : error ? (
          <div className="p-4">
            <ErrorNote message={apiErrorMessage(error)} />
          </div>
        ) : data && data.items.length > 0 ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line">
                    <th className="table-head px-4 py-3">Date</th>
                    <th className="table-head px-4 py-3">Reference</th>
                    <th className="table-head px-4 py-3">Source</th>
                    <th className="table-head px-4 py-3 text-right">Debits</th>
                    <th className="table-head px-4 py-3 text-right">Credits</th>
                    <th className="table-head px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((je) => (
                    <tr
                      key={je.id}
                      className="cursor-pointer transition-colors hover:bg-lagoon-50/40"
                      onClick={() => navigate(`/accounting/journal-entries/${je.id}`)}
                    >
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(je.entryDate)}</td>
                      <td className="px-4 py-3 font-medium text-ink tabular">{je.reference}</td>
                      <td className="px-4 py-3 text-ink-soft">{humanize(je.source)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(je.totalDebits)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(je.totalCredits)}</td>
                      <td className="px-4 py-3">
                        <StatusPill label={humanize(je.status)} tone={statusTone(je.status)} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} onPage={setPage} data={data} />
          </>
        ) : (
          <div className="p-4">
            <EmptyState title="No journal entries" hint="Create a manual entry, or issue an invoice to auto-post one." />
          </div>
        )}
      </div>

      {creating && <NewJournalEntryModal onClose={() => setCreating(false)} />}
    </>
  );
}

interface DraftLine extends ManualJournalLineInput {
  key: number;
}

let lineKeySeq = 0;
function emptyLine(): DraftLine {
  return { key: lineKeySeq++, accountId: '', debit: 0, credit: 0, memo: '' };
}

function NewJournalEntryModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const navigate = useNavigate();
  const [entryDate, setEntryDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [reference, setReference] = useState('');
  const [description, setDescription] = useState('');
  const [lines, setLines] = useState<DraftLine[]>([emptyLine(), emptyLine()]);
  const [error, setError] = useState<string | null>(null);

  const accounts = useQuery({
    queryKey: ['accounts', true],
    queryFn: async () => (await api.get<Account[]>('/api/chart-of-accounts', { params: { activeOnly: true } })).data,
  });

  const totalDebit = useMemo(() => lines.reduce((s, l) => s + (Number(l.debit) || 0), 0), [lines]);
  const totalCredit = useMemo(() => lines.reduce((s, l) => s + (Number(l.credit) || 0), 0), [lines]);
  const balanced = lines.length > 0 && totalDebit === totalCredit && totalDebit > 0;

  function updateLine(key: number, patch: Partial<DraftLine>) {
    setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  }

  function removeLine(key: number) {
    setLines((ls) => ls.filter((l) => l.key !== key));
  }

  const mutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post<string>('/api/journal-entries', {
        entryDate,
        reference,
        description: description || null,
        lines: lines.map(({ accountId, debit, credit, memo }) => ({
          accountId,
          debit: Number(debit) || 0,
          credit: Number(credit) || 0,
          memo: memo || null,
        })),
      });
      return data;
    },
    onSuccess: (id) => {
      qc.invalidateQueries({ queryKey: ['journal-entries'] });
      toast('Journal entry created as draft.');
      onClose();
      navigate(`/accounting/journal-entries/${id}`);
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  function submit() {
    setError(null);
    if (!reference.trim()) {
      setError('Reference is required.');
      return;
    }
    if (lines.some((l) => !l.accountId)) {
      setError('Every line needs an account.');
      return;
    }
    if (!balanced) {
      setError('Total debits must equal total credits.');
      return;
    }
    mutation.mutate();
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="New journal entry"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={mutation.isPending} onClick={submit}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create draft'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="je-date">
              Entry date
            </label>
            <input
              id="je-date"
              type="date"
              className="input"
              value={entryDate}
              onChange={(e) => setEntryDate(e.target.value)}
            />
          </div>
          <div>
            <label className="field-label" htmlFor="je-reference">
              Reference
            </label>
            <input
              id="je-reference"
              className="input"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
            />
          </div>
        </div>
        <div>
          <label className="field-label" htmlFor="je-description">
            Description
          </label>
          <input
            id="je-description"
            className="input"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="field-label mb-0">Lines</span>
            <button type="button" className="btn-secondary px-2 py-1 text-xs" onClick={() => setLines((ls) => [...ls, emptyLine()])}>
              <Plus className="h-3.5 w-3.5" /> Add line
            </button>
          </div>
          {lines.map((line) => (
            <div key={line.key} className="grid grid-cols-12 items-center gap-2">
              <select
                className="input col-span-5"
                aria-label="Account"
                value={line.accountId}
                onChange={(e) => updateLine(line.key, { accountId: e.target.value })}
              >
                <option value="">Select account…</option>
                {(accounts.data ?? []).map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.code} — {a.name}
                  </option>
                ))}
              </select>
              <input
                type="number"
                step="0.01"
                className="input col-span-2"
                aria-label="Debit"
                placeholder="Debit"
                value={line.debit || ''}
                onChange={(e) => updateLine(line.key, { debit: Number(e.target.value) })}
              />
              <input
                type="number"
                step="0.01"
                className="input col-span-2"
                aria-label="Credit"
                placeholder="Credit"
                value={line.credit || ''}
                onChange={(e) => updateLine(line.key, { credit: Number(e.target.value) })}
              />
              <input
                className="input col-span-2"
                aria-label="Memo"
                placeholder="Memo"
                value={line.memo ?? ''}
                onChange={(e) => updateLine(line.key, { memo: e.target.value })}
              />
              <button
                type="button"
                className="btn-ghost col-span-1 text-danger"
                aria-label="Remove line"
                onClick={() => removeLine(line.key)}
                disabled={lines.length <= 2}
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))}
          <div className="flex items-center justify-end gap-6 pt-2 text-sm">
            <span className={totalDebit === totalCredit ? 'text-ink-soft' : 'text-danger'}>
              Debits {fmtMoney(totalDebit)}
            </span>
            <span className={totalDebit === totalCredit ? 'text-ink-soft' : 'text-danger'}>
              Credits {fmtMoney(totalCredit)}
            </span>
          </div>
        </div>
      </div>
    </Modal>
  );
}
