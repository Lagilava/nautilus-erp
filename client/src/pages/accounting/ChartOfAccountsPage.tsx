import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Landmark, Plus, Ban } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { Account, AccountType } from '../../lib/types';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

const ACCOUNT_TYPES: AccountType[] = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'];

export function ChartOfAccountsPage() {
  const { hasRole } = useAuth();
  const isAdmin = hasRole('Administrator');
  const toast = useToast();
  const qc = useQueryClient();

  const [typeFilter, setTypeFilter] = useState<AccountType | ''>('');
  const [activeOnly, setActiveOnly] = useState(false);
  const [creating, setCreating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ['accounts', activeOnly],
    queryFn: async () =>
      (await api.get<Account[]>('/api/chart-of-accounts', { params: { activeOnly: activeOnly || undefined } })).data,
  });

  const deactivate = useMutation({
    mutationFn: (id: string) => api.post(`/api/chart-of-accounts/${id}/deactivate`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['accounts'] });
      toast('Account deactivated.');
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  const filtered = (data ?? []).filter((a) => !typeFilter || a.type === typeFilter);

  return (
    <>
      <PageHeader
        icon={Landmark}
        eyebrow="Accounting"
        title="Chart of Accounts"
        subtitle="The structural skeleton every journal posting relies on."
        actions={
          isAdmin && (
            <button className="btn-primary" onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4" /> New Account
            </button>
          )
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <select
          className="input w-auto"
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value as AccountType | '')}
          aria-label="Filter by type"
        >
          <option value="">All types</option>
          {ACCOUNT_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <label className="flex items-center gap-2 text-sm text-ink-soft">
          <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
          Active only
        </label>
      </div>

      <div className="card overflow-hidden">
        {isLoading ? (
          <TableSkeleton cols={5} />
        ) : error ? (
          <div className="p-4">
            <ErrorNote message={apiErrorMessage(error)} />
          </div>
        ) : filtered.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  <th className="table-head px-4 py-3">Code</th>
                  <th className="table-head px-4 py-3">Name</th>
                  <th className="table-head px-4 py-3">Type</th>
                  <th className="table-head px-4 py-3">Status</th>
                  <th className="table-head px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {filtered.map((a) => (
                  <tr key={a.id}>
                    <td className="px-4 py-3 font-medium text-ink tabular">{a.code}</td>
                    <td className="px-4 py-3 text-ink-soft">
                      {a.name}
                      {a.isSystem && <span className="ml-2 text-xs text-ink-muted">(system)</span>}
                    </td>
                    <td className="px-4 py-3 text-ink-muted">{a.type}</td>
                    <td className="px-4 py-3">
                      <StatusPill label={a.isActive ? 'Active' : 'Inactive'} tone={a.isActive ? 'success' : 'neutral'} />
                    </td>
                    <td className="px-4 py-3 text-right">
                      {isAdmin && !a.isSystem && a.isActive && (
                        <button
                          className="btn-ghost text-danger"
                          disabled={deactivate.isPending}
                          onClick={() => deactivate.mutate(a.id)}
                        >
                          <Ban className="h-4 w-4" /> Deactivate
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-4">
            <EmptyState title="No accounts found" hint="Adjust the filters or add a new account." />
          </div>
        )}
      </div>

      {creating && <NewAccountModal onClose={() => setCreating(false)} />}
    </>
  );
}

function NewAccountModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [type, setType] = useState<AccountType>('Asset');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => api.post('/api/chart-of-accounts', { code, name, type }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['accounts'] });
      toast('Account created.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="New account"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            disabled={mutation.isPending || !code.trim() || !name.trim()}
            onClick={() => mutation.mutate()}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create account'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div>
          <label className="field-label" htmlFor="account-code">
            Code
          </label>
          <input id="account-code" className="input" value={code} onChange={(e) => setCode(e.target.value)} />
        </div>
        <div>
          <label className="field-label" htmlFor="account-name">
            Name
          </label>
          <input id="account-name" className="input" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <label className="field-label" htmlFor="account-type">
            Type
          </label>
          <select
            id="account-type"
            className="input"
            value={type}
            onChange={(e) => setType(e.target.value as AccountType)}
          >
            {ACCOUNT_TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
      </div>
    </Modal>
  );
}
