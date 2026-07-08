import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { Paged, SalesOrderSummary } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize } from '../../lib/status';
import { PageHeader, Loading, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { Pagination } from '../../components/Pagination';
import { Modal } from '../../components/Modal';
import { LineItemsEditor, type LineDraft } from '../../components/LineItemsEditor';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';
import { useCustomers, useProducts, useWarehouses } from '../../lib/pickers';

export function SalesOrdersPage() {
  const { hasRole } = useAuth();
  const navigate = useNavigate();
  const canWrite = hasRole('Administrator', 'Manager');
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);

  const customers = useCustomers();
  const customerName = useMemo(
    () => new Map((customers.data ?? []).map((c) => [c.id, c.name])),
    [customers.data],
  );

  const { data, isLoading, error } = useQuery({
    queryKey: ['sales-orders', page],
    queryFn: async () =>
      (await api.get<Paged<SalesOrderSummary>>('/api/sales-orders', { params: { page, pageSize: 15 } })).data,
  });

  return (
    <>
      <PageHeader
        title="Sales Orders"
        subtitle="From order to fulfilment — confirm, then issue stock to the customer."
        actions={
          canWrite && (
            <button className="btn-primary" onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4" /> New order
            </button>
          )
        }
      />

      <div className="card overflow-hidden">
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
                    <th className="table-head px-4 py-3">Number</th>
                    <th className="table-head px-4 py-3">Customer</th>
                    <th className="table-head px-4 py-3">Date</th>
                    <th className="table-head px-4 py-3 text-right">Subtotal</th>
                    <th className="table-head px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((o) => (
                    <tr
                      key={o.id}
                      className="cursor-pointer transition-colors hover:bg-lagoon-50/40"
                      onClick={() => navigate(`/sales-orders/${o.id}`)}
                    >
                      <td className="px-4 py-3 font-medium text-ink tabular">{o.number}</td>
                      <td className="px-4 py-3 text-ink-soft">{customerName.get(o.customerId) ?? '—'}</td>
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(o.orderDate)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(o.subTotal)}</td>
                      <td className="px-4 py-3">
                        <StatusPill label={humanize(o.status)} tone={statusTone(o.status)} />
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
            <EmptyState title="No sales orders yet" hint={canWrite ? 'Create your first order.' : undefined} />
          </div>
        )}
      </div>

      {creating && (
        <NewSalesOrderModal
          onClose={() => setCreating(false)}
          onCreated={(id) => {
            setCreating(false);
            navigate(`/sales-orders/${id}`);
          }}
        />
      )}
    </>
  );
}

function NewSalesOrderModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const customers = useCustomers();
  const warehouses = useWarehouses();
  const products = useProducts();

  const [customerId, setCustomerId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10));
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [error, setError] = useState<string | null>(null);

  const refsLoading = customers.isLoading || warehouses.isLoading || products.isLoading;

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        customerId,
        warehouseId,
        orderDate,
        lines: lines
          .filter((l) => l.productId && l.quantity > 0)
          .map((l) => ({ productId: l.productId, quantity: l.quantity, unitPrice: l.price })),
        notes: null,
      };
      return (await api.post<string>('/api/sales-orders', payload)).data;
    },
    onSuccess: (id) => {
      qc.invalidateQueries({ queryKey: ['sales-orders'] });
      toast('Sales order created.');
      onCreated(id);
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const valid = customerId && warehouseId && lines.some((l) => l.productId && l.quantity > 0);

  return (
    <Modal
      open
      onClose={onClose}
      title="New sales order"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={!valid || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create order'}
          </button>
        </>
      }
    >
      {refsLoading ? (
        <Loading label="Loading…" />
      ) : (
        <div className="space-y-4">
          {error && <ErrorNote message={error} />}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="field-label">Customer</label>
              <select className="input" value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
                <option value="">Select…</option>
                {customers.data?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="field-label">Warehouse</label>
              <select className="input" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
                <option value="">Select…</option>
                {warehouses.data?.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div>
            <label className="field-label">Order date</label>
            <input type="date" className="input" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} />
          </div>
          <LineItemsEditor products={products.data ?? []} priceLabel="unit price" lines={lines} onChange={setLines} />
        </div>
      )}
    </Modal>
  );
}
