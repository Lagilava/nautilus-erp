import { useEffect, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Search, Pencil, Users } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged, Customer } from '../lib/types';
import { fmtMoney } from '../lib/format';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../components/ui';
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
  const [editing, setEditing] = useState<Customer | null>(null);
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['customers', page, search],
    queryFn: async () =>
      (await api.get<Paged<Customer>>('/api/customers', { params: { page, pageSize: 15, search: search || undefined } }))
        .data,
  });

  // Another admin/manager saved a change to a customer — refresh so this list doesn't go stale
  // silently, the same problem that motivated real-time updates in the first place.
  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ entityType: string }>).detail;
      if (detail?.entityType === 'Customer') qc.invalidateQueries({ queryKey: ['customers'] });
    };
    window.addEventListener('erp:entity-updated', handler);
    return () => window.removeEventListener('erp:entity-updated', handler);
  }, [qc]);

  return (
    <>
      <PageHeader
        icon={Users}
        eyebrow="Sales"
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
          <TableSkeleton cols={5} />
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
                    {canWrite && <th className="table-head px-4 py-3" />}
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
                      {canWrite && (
                        <td className="px-4 py-3 text-right">
                          <button
                            className="rounded p-1 text-ink-muted hover:bg-canvas hover:text-ink"
                            aria-label={`Edit ${c.name}`}
                            onClick={() => setEditing(c)}
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                        </td>
                      )}
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
      {editing && <EditCustomerModal customer={editing} onClose={() => setEditing(null)} />}
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
            <label className="field-label" htmlFor="code">Code</label>
            <input id="code" className="input" {...register('code')} />
            {errors.code && <p className="mt-1 text-xs text-danger">{errors.code.message}</p>}
          </div>
          <div>
            <label className="field-label" htmlFor="name">Name</label>
            <input id="name" className="input" {...register('name')} />
            {errors.name && <p className="mt-1 text-xs text-danger">{errors.name.message}</p>}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="email">Email</label>
            <input id="email" className="input" {...register('email')} />
            {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
          </div>
          <div>
            <label className="field-label" htmlFor="phone">Phone</label>
            <input id="phone" className="input" {...register('phone')} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="tin">TIN</label>
            <input id="tin" className="input" {...register('taxIdentificationNumber')} />
          </div>
          <div>
            <label className="field-label" htmlFor="credit-limit-fjd">Credit limit (FJD)</label>
            <input id="credit-limit-fjd" type="number" step="0.01" className="input" {...register('creditLimit', { valueAsNumber: true })} />
          </div>
        </div>
      </form>
    </Modal>
  );
}

const editSchema = schema.omit({ code: true });
type EditFormValues = z.infer<typeof editSchema>;

function EditCustomerModal({ customer, onClose }: { customer: Customer; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditFormValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      name: customer.name,
      email: customer.email ?? '',
      phone: customer.phone ?? '',
      taxIdentificationNumber: customer.taxIdentificationNumber ?? '',
      creditLimit: customer.creditLimit,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: EditFormValues) =>
      api.put(`/api/customers/${customer.id}`, {
        id: customer.id,
        ...values,
        email: values.email || null,
        rowVersion: customer.rowVersion,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['customers'] });
      toast('Customer updated.');
      onClose();
    },
    onError: (e) => {
      // A conflicting edit landed first — the stale rowVersion this form was opened with no
      // longer matches. Resubmitting would silently overwrite that edit, so make the user
      // reload and re-apply their change instead.
      if (apiErrorMessage(e).toLowerCase().includes('changed by someone else')) setConflict(true);
      else setError(apiErrorMessage(e));
    },
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={`Edit ${customer.code}`}
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            form="edit-customer"
            type="submit"
            disabled={mutation.isPending || conflict}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Save changes'}
          </button>
        </>
      }
    >
      <form id="edit-customer" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {conflict ? (
          <ErrorNote message="This customer was changed by someone else since you opened this form. Close and reopen it to see their changes before editing." />
        ) : (
          error && <ErrorNote message={error} />
        )}
        <div>
          <label className="field-label" htmlFor="edit-name">Name</label>
          <input id="edit-name" className="input" {...register('name')} />
          {errors.name && <p className="mt-1 text-xs text-danger">{errors.name.message}</p>}
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="edit-email">Email</label>
            <input id="edit-email" className="input" {...register('email')} />
            {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
          </div>
          <div>
            <label className="field-label" htmlFor="edit-phone">Phone</label>
            <input id="edit-phone" className="input" {...register('phone')} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="edit-tin">TIN</label>
            <input id="edit-tin" className="input" {...register('taxIdentificationNumber')} />
          </div>
          <div>
            <label className="field-label" htmlFor="edit-credit-limit-fjd">Credit limit (FJD)</label>
            <input
              id="edit-credit-limit-fjd"
              type="number"
              step="0.01"
              className="input"
              {...register('creditLimit', { valueAsNumber: true })}
            />
          </div>
        </div>
      </form>
    </Modal>
  );
}
