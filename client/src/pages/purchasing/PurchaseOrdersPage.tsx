import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { Paged, PurchaseOrderSummary } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize } from '../../lib/status';
import { PageHeader, Loading, TableSkeleton, EmptyState, ErrorNote, StatusPill, Spinner } from '../../components/ui';
import { Pagination } from '../../components/Pagination';
import { Modal } from '../../components/Modal';
import { LineItemsEditor, type LineDraft } from '../../components/LineItemsEditor';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';
import { useSuppliers, useProducts, useWarehouses } from '../../lib/pickers';

export function PurchaseOrdersPage() {
  const { hasRole } = useAuth();
  const navigate = useNavigate();
  const canWrite = hasRole('Administrator', 'Manager');
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);

  const suppliers = useSuppliers();
  const supplierName = useMemo(
    () => new Map((suppliers.data ?? []).map((s) => [s.id, s.name])),
    [suppliers.data],
  );

  const { data, isLoading, error } = useQuery({
    queryKey: ['purchase-orders', page],
    queryFn: async () =>
      (await api.get<Paged<PurchaseOrderSummary>>('/api/purchase-orders', { params: { page, pageSize: 15 } })).data,
  });

  return (
    <>
      <PageHeader
        title="Purchase Orders"
        subtitle="Order from suppliers, then receive goods into stock."
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
                    <th className="table-head px-4 py-3">Supplier</th>
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
                      onClick={() => navigate(`/purchase-orders/${o.id}`)}
                    >
                      <td className="px-4 py-3 font-medium text-ink tabular">{o.number}</td>
                      <td className="px-4 py-3 text-ink-soft">{supplierName.get(o.supplierId) ?? '—'}</td>
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
            <EmptyState title="No purchase orders yet" hint={canWrite ? 'Create your first order.' : undefined} />
          </div>
        )}
      </div>

      {creating && (
        <NewPurchaseOrderModal
          onClose={() => setCreating(false)}
          onCreated={(id) => {
            setCreating(false);
            navigate(`/purchase-orders/${id}`);
          }}
        />
      )}
    </>
  );
}

function NewPurchaseOrderModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const suppliers = useSuppliers();
  const warehouses = useWarehouses();
  const products = useProducts();

  const [supplierId, setSupplierId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10));
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [error, setError] = useState<string | null>(null);

  const refsLoading = suppliers.isLoading || warehouses.isLoading || products.isLoading;

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        supplierId,
        warehouseId,
        orderDate,
        lines: lines
          .filter((l) => l.productId && l.quantity > 0)
          .map((l) => ({ productId: l.productId, quantity: l.quantity, unitCost: l.price })),
        notes: null,
      };
      return (await api.post<string>('/api/purchase-orders', payload)).data;
    },
    onSuccess: (id) => {
      qc.invalidateQueries({ queryKey: ['purchase-orders'] });
      toast('Purchase order created.');
      onCreated(id);
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const valid = supplierId && warehouseId && lines.some((l) => l.productId && l.quantity > 0);

  return (
    <Modal
      open
      onClose={onClose}
      title="New purchase order"
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
              <label className="field-label" htmlFor="supplier">Supplier</label>
              <select id="supplier" className="input" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
                <option value="">Select…</option>
                {suppliers.data?.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="field-label" htmlFor="receiving-warehouse">Receiving warehouse</label>
              <select id="receiving-warehouse" className="input" value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)}>
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
            <label className="field-label" htmlFor="order-date">Order date</label>
            <input id="order-date" type="date" className="input" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} />
          </div>
          <LineItemsEditor products={products.data ?? []} priceLabel="unit cost" lines={lines} onChange={setLines} />
        </div>
      )}
    </Modal>
  );
}
