import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Search, Pencil, Package } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged, Product, Category, UnitOfMeasure, Tax } from '../lib/types';
import { fmtMoney } from '../lib/format';
import { PageHeader, Loading, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../components/ui';
import { Pagination } from '../components/Pagination';
import { Modal } from '../components/Modal';
import { useToast } from '../components/Toast';
import { useAuth } from '../auth/AuthContext';

export function ProductsPage() {
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const qc = useQueryClient();

  const { data, isLoading, error } = useQuery({
    queryKey: ['products', page, search],
    queryFn: async () =>
      (
        await api.get<Paged<Product>>('/api/products', {
          params: { page, pageSize: 15, search: search || undefined },
        })
      ).data,
  });

  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ entityType: string }>).detail;
      if (detail?.entityType === 'Product') qc.invalidateQueries({ queryKey: ['products'] });
    };
    window.addEventListener('erp:entity-updated', handler);
    return () => window.removeEventListener('erp:entity-updated', handler);
  }, [qc]);

  return (
    <>
      <PageHeader
        icon={Package}
        eyebrow="Catalog"
        title="Products"
        subtitle="Your item master — the shared vocabulary for sales, purchasing, and inventory."
        actions={
          canWrite && (
            <button className="btn-primary" onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4" /> New product
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
              placeholder="Search by SKU, name, or barcode…"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
        </div>

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
                    <th className="table-head px-4 py-3">SKU</th>
                    <th className="table-head px-4 py-3">Name</th>
                    <th className="table-head px-4 py-3">Category</th>
                    <th className="table-head px-4 py-3">Tax</th>
                    <th className="table-head px-4 py-3 text-right">Cost</th>
                    <th className="table-head px-4 py-3 text-right">Price</th>
                    <th className="table-head px-4 py-3">Status</th>
                    {canWrite && <th className="table-head px-4 py-3" />}
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((p) => (
                    <tr key={p.id} className="transition-colors hover:bg-lagoon-50/40">
                      <td className="px-4 py-3 font-medium text-ink tabular">{p.sku}</td>
                      <td className="px-4 py-3 text-ink-soft">{p.name}</td>
                      <td className="px-4 py-3 text-ink-muted">{p.categoryName}</td>
                      <td className="px-4 py-3 text-ink-muted">{p.taxCode}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-soft">{fmtMoney(p.costPrice)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(p.sellingPrice)}</td>
                      <td className="px-4 py-3">
                        <StatusPill
                          label={p.isActive ? 'Active' : 'Inactive'}
                          tone={p.isActive ? 'success' : 'neutral'}
                        />
                      </td>
                      {canWrite && (
                        <td className="px-4 py-3 text-right">
                          <button
                            className="rounded p-1 text-ink-muted hover:bg-canvas hover:text-ink"
                            aria-label={`Edit ${p.name}`}
                            onClick={() => setEditing(p)}
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
            <EmptyState
              title="No products yet"
              hint={canWrite ? 'Create your first product to get started.' : 'Ask a manager to add products.'}
            />
          </div>
        )}
      </div>

      {creating && <CreateProductModal onClose={() => setCreating(false)} />}
      {editing && <EditProductModal product={editing} onClose={() => setEditing(null)} />}
    </>
  );
}

const schema = z.object({
  sku: z.string().min(1, 'Required').max(64),
  name: z.string().min(1, 'Required').max(200),
  categoryId: z.string().min(1, 'Select a category'),
  unitOfMeasureId: z.string().min(1, 'Select a unit'),
  taxId: z.string().min(1, 'Select a tax'),
  costPrice: z.number().min(0),
  sellingPrice: z.number().min(0),
});
type FormValues = z.infer<typeof schema>;

function CreateProductModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);

  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: async () => (await api.get<Category[]>('/api/categories')).data,
  });
  const units = useQuery({
    queryKey: ['units'],
    queryFn: async () => (await api.get<UnitOfMeasure[]>('/api/units-of-measure')).data,
  });
  const taxes = useQuery({
    queryKey: ['taxes'],
    queryFn: async () => (await api.get<Tax[]>('/api/taxes')).data,
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { costPrice: 0, sellingPrice: 0 },
  });

  const mutation = useMutation({
    mutationFn: (values: FormValues) => api.post('/api/products', values),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] });
      toast('Product created.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const refsLoading = categories.isLoading || units.isLoading || taxes.isLoading;

  return (
    <Modal
      open
      onClose={onClose}
      title="New product"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            form="create-product"
            type="submit"
            disabled={mutation.isPending || refsLoading}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create product'}
          </button>
        </>
      }
    >
      {refsLoading ? (
        <Loading label="Loading reference data…" />
      ) : (
        <form id="create-product" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {error && <ErrorNote message={error} />}
          <div className="grid grid-cols-2 gap-4">
            <Field label="SKU" error={errors.sku?.message}>
              <input className="input" {...register('sku')} />
            </Field>
            <Field label="Name" error={errors.name?.message}>
              <input className="input" {...register('name')} />
            </Field>
          </div>
          <Field label="Category" error={errors.categoryId?.message}>
            <select className="input" {...register('categoryId')}>
              <option value="">Select…</option>
              {categories.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </Field>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Unit of measure" error={errors.unitOfMeasureId?.message}>
              <select className="input" {...register('unitOfMeasureId')}>
                <option value="">Select…</option>
                {units.data?.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Tax" error={errors.taxId?.message}>
              <select className="input" {...register('taxId')}>
                <option value="">Select…</option>
                {taxes.data?.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.code} ({t.currentRate}%)
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Cost price" error={errors.costPrice?.message}>
              <input type="number" step="0.01" className="input" {...register('costPrice', { valueAsNumber: true })} />
            </Field>
            <Field label="Selling price" error={errors.sellingPrice?.message}>
              <input type="number" step="0.01" className="input" {...register('sellingPrice', { valueAsNumber: true })} />
            </Field>
          </div>
        </form>
      )}
    </Modal>
  );
}

const editSchema = schema.extend({
  description: z.string().optional(),
  barcode: z.string().optional(),
  isActive: z.boolean(),
});
type EditFormValues = z.infer<typeof editSchema>;

function EditProductModal({ product, onClose }: { product: Product; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: async () => (await api.get<Category[]>('/api/categories')).data,
  });
  const units = useQuery({
    queryKey: ['units'],
    queryFn: async () => (await api.get<UnitOfMeasure[]>('/api/units-of-measure')).data,
  });
  const taxes = useQuery({
    queryKey: ['taxes'],
    queryFn: async () => (await api.get<Tax[]>('/api/taxes')).data,
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditFormValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      sku: product.sku,
      name: product.name,
      description: product.description ?? '',
      barcode: product.barcode ?? '',
      categoryId: product.categoryId,
      unitOfMeasureId: product.unitOfMeasureId,
      taxId: product.taxId,
      costPrice: product.costPrice,
      sellingPrice: product.sellingPrice,
      isActive: product.isActive,
    },
  });

  const mutation = useMutation({
    mutationFn: (values: EditFormValues) =>
      api.put(`/api/products/${product.id}`, { id: product.id, ...values, rowVersion: product.rowVersion }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] });
      toast('Product updated.');
      onClose();
    },
    onError: (e) => {
      if (apiErrorMessage(e).toLowerCase().includes('changed by someone else')) setConflict(true);
      else setError(apiErrorMessage(e));
    },
  });

  const refsLoading = categories.isLoading || units.isLoading || taxes.isLoading;

  return (
    <Modal
      open
      onClose={onClose}
      title={`Edit ${product.sku}`}
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            form="edit-product"
            type="submit"
            disabled={mutation.isPending || refsLoading || conflict}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Save changes'}
          </button>
        </>
      }
    >
      {refsLoading ? (
        <Loading label="Loading reference data…" />
      ) : (
        <form id="edit-product" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {conflict ? (
            <ErrorNote message="This product was changed by someone else since you opened this form. Close and reopen it to see their changes before editing." />
          ) : (
            error && <ErrorNote message={error} />
          )}
          <div className="grid grid-cols-2 gap-4">
            <Field label="SKU" error={errors.sku?.message}>
              <input className="input" {...register('sku')} />
            </Field>
            <Field label="Name" error={errors.name?.message}>
              <input className="input" {...register('name')} />
            </Field>
          </div>
          <Field label="Category" error={errors.categoryId?.message}>
            <select className="input" {...register('categoryId')}>
              <option value="">Select…</option>
              {categories.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </Field>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Unit of measure" error={errors.unitOfMeasureId?.message}>
              <select className="input" {...register('unitOfMeasureId')}>
                <option value="">Select…</option>
                {units.data?.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Tax" error={errors.taxId?.message}>
              <select className="input" {...register('taxId')}>
                <option value="">Select…</option>
                {taxes.data?.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.code} ({t.currentRate}%)
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Cost price" error={errors.costPrice?.message}>
              <input type="number" step="0.01" className="input" {...register('costPrice', { valueAsNumber: true })} />
            </Field>
            <Field label="Selling price" error={errors.sellingPrice?.message}>
              <input type="number" step="0.01" className="input" {...register('sellingPrice', { valueAsNumber: true })} />
            </Field>
          </div>
          <label className="flex items-center gap-2 text-sm text-ink-soft">
            <input type="checkbox" {...register('isActive')} />
            Active
          </label>
        </form>
      )}
    </Modal>
  );
}

function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <label className="field-label">{label}</label>
      {children}
      {error && <p className="mt-1 text-xs text-danger">{error}</p>}
    </div>
  );
}
