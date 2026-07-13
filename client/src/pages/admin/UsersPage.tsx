import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, UserCheck, UserX, UserCog } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { UserAccount } from '../../lib/types';
import { PageHeader, Loading, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';

const ALL_ROLES = ['Administrator', 'Manager', 'Staff'];

interface BranchOption {
  id: string;
  name: string;
}

export function UsersPage() {
  const qc = useQueryClient();
  const toast = useToast();
  const [creating, setCreating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ['users'],
    queryFn: async () => (await api.get<UserAccount[]>('/api/users')).data,
  });

  const branches = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await api.get<BranchOption[]>('/api/branches')).data,
  });

  const setBranch = useMutation({
    mutationFn: ({ id, branchId }: { id: string; branchId: string | null }) =>
      api.put(`/api/users/${id}/branch`, { userId: id, branchId }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] });
      toast('Branch scope updated.');
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      api.post(`/api/users/${id}/active`, active, { headers: { 'Content-Type': 'application/json' } }),
    onSuccess: (_r, v) => {
      qc.invalidateQueries({ queryKey: ['users'] });
      toast(v.active ? 'User activated.' : 'User deactivated.');
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  return (
    <>
      <PageHeader
        icon={UserCog}
        eyebrow="Administration"
        title="Users"
        subtitle="Manage who can access the system and what they can do."
        actions={
          <button className="btn-primary" onClick={() => setCreating(true)}>
            <Plus className="h-4 w-4" /> New user
          </button>
        }
      />

      <div className="card overflow-hidden">
        {isLoading ? (
          <Loading />
        ) : error ? (
          <div className="p-4">
            <ErrorNote message={apiErrorMessage(error)} />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  <th className="table-head px-4 py-3">Name</th>
                  <th className="table-head px-4 py-3">Email</th>
                  <th className="table-head px-4 py-3">Roles</th>
                  <th className="table-head px-4 py-3">Branch scope</th>
                  <th className="table-head px-4 py-3">Status</th>
                  <th className="table-head px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data?.map((u) => (
                  <tr key={u.id}>
                    <td className="px-4 py-3 font-medium text-ink">
                      {u.firstName} {u.lastName}
                    </td>
                    <td className="px-4 py-3 text-ink-soft">{u.email}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {u.roles.map((r) => (
                          <StatusPill key={r} label={r} tone="neutral" />
                        ))}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      {/* Record-level security: null = sees every branch. */}
                      <select
                        aria-label={`Branch scope for ${u.email}`}
                        className="input py-1 text-xs"
                        value={u.branchId ?? ''}
                        disabled={setBranch.isPending}
                        onChange={(e) => setBranch.mutate({ id: u.id, branchId: e.target.value || null })}
                      >
                        <option value="">All branches</option>
                        {branches.data?.map((b) => (
                          <option key={b.id} value={b.id}>
                            {b.name}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="px-4 py-3">
                      <StatusPill label={u.isActive ? 'Active' : 'Disabled'} tone={u.isActive ? 'success' : 'danger'} />
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        className="btn-ghost px-2 py-1 text-xs"
                        disabled={setActive.isPending}
                        onClick={() => setActive.mutate({ id: u.id, active: !u.isActive })}
                      >
                        {u.isActive ? (
                          <>
                            <UserX className="h-3.5 w-3.5" /> Disable
                          </>
                        ) : (
                          <>
                            <UserCheck className="h-3.5 w-3.5" /> Enable
                          </>
                        )}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {creating && <NewUserModal onClose={() => setCreating(false)} />}
    </>
  );
}

function NewUserModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [form, setForm] = useState({ email: '', password: '', firstName: '', lastName: '' });
  const [roles, setRoles] = useState<string[]>(['Staff']);
  const [branchId, setBranchId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const branches = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await api.get<BranchOption[]>('/api/branches')).data,
  });

  const toggleRole = (r: string) =>
    setRoles((prev) => (prev.includes(r) ? prev.filter((x) => x !== r) : [...prev, r]));

  const mutation = useMutation({
    mutationFn: () => api.post<string>('/api/users', { ...form, roles, branchId: branchId || null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] });
      toast('User created.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const valid = form.email && form.password.length >= 8 && form.firstName && form.lastName && roles.length > 0;

  return (
    <Modal
      open
      onClose={onClose}
      title="New user"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={!valid || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create user'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="first-name">First name</label>
            <input id="first-name" className="input" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
          </div>
          <div>
            <label className="field-label" htmlFor="last-name">Last name</label>
            <input id="last-name" className="input" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
          </div>
        </div>
        <div>
          <label className="field-label" htmlFor="email">Email</label>
          <input id="email" className="input" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        </div>
        <div>
          <label className="field-label" htmlFor="temporary-password-min-8-chars">Temporary password (min 8 chars)</label>
          <input id="temporary-password-min-8-chars"
            className="input"
            type="text"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
        </div>
        <div>
          <label className="field-label" htmlFor="branch-scope">Branch scope</label>
          <select id="branch-scope" className="input" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
            <option value="">All branches (unrestricted)</option>
            {branches.data?.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-ink-muted">
            Scoped users only see stock, sales, and purchasing for their branch.
          </p>
        </div>
        <fieldset>
          <legend className="field-label">Roles</legend>
          <div className="flex flex-wrap gap-2">
            {ALL_ROLES.map((r) => (
              <button
                key={r}
                type="button"
                aria-pressed={roles.includes(r)}
                onClick={() => toggleRole(r)}
                className={`pill border transition-colors ${
                  roles.includes(r)
                    ? 'border-lagoon-500 bg-lagoon-50 text-lagoon-700'
                    : 'border-line text-ink-muted hover:bg-lagoon-50/50'
                }`}
              >
                {r}
              </button>
            ))}
          </div>
        </fieldset>
      </div>
    </Modal>
  );
}
