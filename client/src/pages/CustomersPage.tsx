import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Search } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged, Customer } from '../lib/types';
import { fmtMoney } from '../lib/format';
import { PageHeader, Loading, EmptyState, ErrorNote, StatusPill, Spinner } from '../components/ui';
import { Pagination } from '../components/Pagination';
import { Modal } from '../components/Modal';
import { useToast } from '../components/Toast';
import { useAuth } from '../auth/AuthContext';

export function CustomersPage() {
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false);

  const { data, isLoading, error } = useQuery({
    queryKey: ['customers', page, search],
    queryFn: async () =>
      (await api.get<Paged<Customer>>('/api/customers', { params: { page, pageSize: 15, search: search || undefined } }))
        .data,
  });

  return (
    <>
      <PageHeader
        title="Customers"
        subtitle="The parties you sell to."
        actions={
          canWrite && (
            <button className="btn-primary" onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4" /> New customer
            </button>
          )
        }
      />

      <div className="card overflow-hidden">
        <div className="border-b border-line p-3">
          <div className="relative max-w-sm">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-muted" />
            <input
              className="input pl-9"
              placeholder="Search by code or name…"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>

        {isLoading ? (
          <Loading />
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
                    <th className="table-head px-4 py-3">Code</th>
                    <th className="table-head px-4 py-3">Name</th>
                    <th className="table-head px-4 py-3">Email</th>
                    <th className="table-head px-4 py-3">TIN</th>
                    <th className="table-head px-4 py-3 text-right">Credit limit</th>
                    <th className="table-head px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((c) => (
                    <tr key={c.id} className="transition-colors hover:bg-lagoon-50/40">
                      <td className="px-4 py-3 font-medium text-ink tabular">{c.code}</td>
                      <td className="px-4 py-3 text-ink-soft">{c.name}</td>
                      <td className="px-4 py-3 text-ink-muted">{c.email ?? '—'}</td>
                      <td className="px-4 py-3 text-ink-muted tabular">{c.taxIdentificationNumber ?? '—'}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-soft">{fmtMoney(c.creditLimit)}</td>
                      <td className="px-4 py-3">
                        <StatusPill label={c.isActive ? 'Active' : 'Inactive'} tone={c.isActive ? 'success' : 'neutral'} />
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
            <EmptyState title="No customers yet" hint={canWrite ? 'Add your first customer.' : undefined} />
          </div>
        )}
      </div>

      {creating && <CreateCustomerModal onClose={() => setCreating(false)} />}
    </>
  );
}

const schema = z.object({
  code: z.string().min(1, 'Required').max(32),
  name: z.string().min(1, 'Required').max(200),
  email: z.string().email('Invalid email').optional().or(z.literal('')),
  phone: z.string().optional(),
  country: z.string().optional(),
  taxIdentificationNumber: z.string().optional(),
  creditLimit: z.number().min(0),
});
type FormValues = z.infer<typeof schema>;

function CreateCustomerModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { creditLimit: 0 } });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      api.post('/api/customers', { ...values, email: values.email || null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['customers'] });
      toast('Customer created.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="New customer"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" form="create-customer" type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create customer'}
          </button>
        </>
      }
    >
      <form id="create-customer" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">Code</label>
            <input className="input" {...register('code')} />
            {errors.code && <p className="mt-1 text-xs text-danger">{errors.code.message}</p>}
          </div>
          <div>
            <label className="field-label">Name</label>
            <input className="input" {...register('name')} />
            {errors.name && <p className="mt-1 text-xs text-danger">{errors.name.message}</p>}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">Email</label>
            <input className="input" {...register('email')} />
            {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
          </div>
          <div>
            <label className="field-label">Phone</label>
            <input className="input" {...register('phone')} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">TIN</label>
            <input className="input" {...register('taxIdentificationNumber')} />
          </div>
          <div>
            <label className="field-label">Credit limit (FJD)</label>
            <input type="number" step="0.01" className="input" {...register('creditLimit', { valueAsNumber: true })} />
          </div>
        </div>
      </form>
    </Modal>
  );
}
