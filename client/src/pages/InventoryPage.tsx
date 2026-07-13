import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList, Boxes } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged, StockLevel } from '../lib/types';
import { fmtMoney, fmtNumber } from '../lib/format';
import { useSuppliers, useWarehouses } from '../lib/pickers';
import { useAuth } from '../auth/AuthContext';
import { PageHeader, Loading, EmptyState, ErrorNote, StatusPill, Spinner } from '../components/ui';
import { Pagination } from '../components/Pagination';
import { Modal } from '../components/Modal';
import { useToast } from '../components/Toast';

interface ReorderDraftResult {
  purchaseOrderId: string;
  number: string;
  lineCount: number;
}

/**
 * One-click replenishment: pick a supplier and warehouse, and the server drafts a purchase
 * order covering everything at or below its reorder level. The draft opens for review.
 */
function ReorderModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const toast = useToast();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const suppliers = useSuppliers();
  const warehouses = useWarehouses();
  const [supplierId, setSupplierId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () =>
      (
        await api.post<ReorderDraftResult>('/api/purchase-orders/reorder-draft', {
          supplierId,
          warehouseId,
        })
      ).data,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['purchase-orders'] });
      toast(`Draft ${result.number} created with ${result.lineCount} line${result.lineCount === 1 ? '' : 's'}.`);
      onClose();
      navigate(`/purchase-orders/${result.purchaseOrderId}`);
    },
    onError: (e) => setError(apiErrorMessage(e, 'Could not create the reorder draft.')),
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Create reorder PO"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            disabled={!supplierId || !warehouseId || mutation.isPending}
            onClick={() => {
              setError(null);
              mutation.mutate();
            }}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create draft'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <p className="text-sm text-ink-muted">
          Drafts a purchase order for every product in the warehouse at or below its reorder level,
          topped up to twice that level. You review and confirm the draft before anything is ordered.
        </p>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="reorder-supplier">
              Supplier
            </label>
            <select
              id="reorder-supplier"
              className="input"
              value={supplierId}
              onChange={(e) => setSupplierId(e.target.value)}
            >
              <option value="">Select…</option>
              {suppliers.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="field-label" htmlFor="reorder-warehouse">
              Warehouse
            </label>
            <select
              id="reorder-warehouse"
              className="input"
              value={warehouseId}
              onChange={(e) => setWarehouseId(e.target.value)}
            >
              <option value="">Select…</option>
              {warehouses.data?.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </Modal>
  );
}

export function InventoryPage() {
  const { hasRole } = useAuth();
  const [page, setPage] = useState(1);
  const [lowOnly, setLowOnly] = useState(false);
  const [reorderOpen, setReorderOpen] = useState(false);

  const canReorder = hasRole('Administrator') || hasRole('Manager');

  const { data, isLoading, error } = useQuery({
    queryKey: ['stock-levels', page, lowOnly],
    queryFn: async () =>
      (
        await api.get<Paged<StockLevel>>('/api/inventory/levels', {
          params: { page, pageSize: 15, lowStockOnly: lowOnly || undefined },
        })
      ).data,
  });

  return (
    <>
      <PageHeader
        icon={Boxes}
        eyebrow="Catalog"
        title="Inventory"
        subtitle="Stock on hand and valuation across warehouses (FIFO costed)."
        actions={
          <div className="flex items-center gap-4">
            <label className="flex cursor-pointer items-center gap-2 text-sm text-ink-soft">
              <input
                type="checkbox"
                className="h-4 w-4 rounded border-line text-lagoon-500 focus:ring-lagoon-400"
                checked={lowOnly}
                onChange={(e) => {
                  setLowOnly(e.target.checked);
                  setPage(1);
                }}
              />
              Low stock only
            </label>
            {canReorder && (
              <button className="btn-primary" onClick={() => setReorderOpen(true)}>
                <ClipboardList className="h-4 w-4" />
                Reorder low stock
              </button>
            )}
          </div>
        }
      />

      <div className="card overflow-hidden">
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
                    <th className="table-head px-4 py-3">SKU</th>
                    <th className="table-head px-4 py-3">Product</th>
                    <th className="table-head px-4 py-3">Warehouse</th>
                    <th className="table-head px-4 py-3 text-right">On hand</th>
                    <th className="table-head px-4 py-3 text-right">Reorder</th>
                    <th className="table-head px-4 py-3 text-right">Value</th>
                    <th className="table-head px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((s) => (
                    <tr key={`${s.productId}-${s.warehouseId}`} className="transition-colors hover:bg-lagoon-50/40">
                      <td className="px-4 py-3 font-medium text-ink tabular">{s.sku}</td>
                      <td className="px-4 py-3 text-ink-soft">{s.productName}</td>
                      <td className="px-4 py-3 text-ink-muted">{s.warehouseName}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtNumber(s.quantityOnHand)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-muted">{fmtNumber(s.reorderLevel)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-soft">{fmtMoney(s.stockValue)}</td>
                      <td className="px-4 py-3">
                        {s.isBelowReorder ? (
                          <StatusPill label="Reorder" tone="warning" />
                        ) : (
                          <StatusPill label="OK" tone="success" />
                        )}
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
            <EmptyState
              title={lowOnly ? 'Nothing below reorder level' : 'No stock recorded yet'}
              hint={lowOnly ? undefined : 'Stock appears here once goods are received.'}
            />
          </div>
        )}
      </div>

      {reorderOpen && <ReorderModal open={reorderOpen} onClose={() => setReorderOpen(false)} />}
    </>
  );
}
