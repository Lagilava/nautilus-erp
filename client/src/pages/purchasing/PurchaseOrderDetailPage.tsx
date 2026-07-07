import { useMemo, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, PackagePlus, XCircle, FileText } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { PurchaseOrderDetail } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { Loading, ErrorNote, Spinner } from '../../components/ui';
import { DetailScaffold, SummaryRow } from '../../components/DetailScaffold';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';
import { useProducts } from '../../lib/pickers';

export function PurchaseOrderDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [receiving, setReceiving] = useState(false);

  const products = useProducts();
  const productName = useMemo(
    () => new Map((products.data ?? []).map((p) => [p.id, `${p.sku} — ${p.name}`])),
    [products.data],
  );

  const { data: order, isLoading, error } = useQuery({
    queryKey: ['purchase-order', id],
    queryFn: async () => (await api.get<PurchaseOrderDetail>(`/api/purchase-orders/${id}`)).data,
  });

  const action = useMutation({
    mutationFn: (verb: string) => api.post(`/api/purchase-orders/${id}/${verb}`),
    onSuccess: (_r, verb) => {
      qc.invalidateQueries({ queryKey: ['purchase-order', id] });
      qc.invalidateQueries({ queryKey: ['purchase-orders'] });
      toast(`Order ${verb}ed.`);
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  const createBill = useMutation({
    mutationFn: async () => {
      const today = new Date().toISOString().slice(0, 10);
      return (
        await api.post<string>('/api/supplier-invoices/from-order', {
          purchaseOrderId: id,
          issueDate: today,
          dueDate: null,
          supplierReference: null,
        })
      ).data;
    },
    onSuccess: (sid) => {
      toast('Supplier invoice created.');
      navigate(`/supplier-invoices/${sid}`);
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !order) return <ErrorNote message={apiErrorMessage(error)} />;

  const busy = action.isPending || createBill.isPending;
  const canReceive = order.status === 'Confirmed' || order.status === 'PartiallyReceived';

  return (
    <DetailScaffold
      backTo="/purchase-orders"
      backLabel="Purchase Orders"
      title={order.number}
      status={order.status}
      actions={
        canWrite && (
          <>
            {order.status === 'Draft' && (
              <button className="btn-primary" disabled={busy} onClick={() => action.mutate('confirm')}>
                <CheckCircle2 className="h-4 w-4" /> Confirm
              </button>
            )}
            {canReceive && (
              <button className="btn-primary" disabled={busy} onClick={() => setReceiving(true)}>
                <PackagePlus className="h-4 w-4" /> Receive goods
              </button>
            )}
            {order.status !== 'Draft' && order.status !== 'Cancelled' && (
              <button className="btn-secondary" disabled={busy} onClick={() => createBill.mutate()}>
                <FileText className="h-4 w-4" /> Create bill
              </button>
            )}
            {(order.status === 'Draft' || order.status === 'Confirmed') && (
              <button className="btn-ghost text-danger" disabled={busy} onClick={() => action.mutate('cancel')}>
                <XCircle className="h-4 w-4" /> Cancel
              </button>
            )}
            {busy && <Spinner className="h-4 w-4" />}
          </>
        )
      }
    >
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="card overflow-hidden lg:col-span-2">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-line">
                <th className="table-head px-4 py-3">Product</th>
                <th className="table-head px-4 py-3 text-right">Ordered</th>
                <th className="table-head px-4 py-3 text-right">Received</th>
                <th className="table-head px-4 py-3 text-right">Unit cost</th>
                <th className="table-head px-4 py-3 text-right">Line total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {order.lines.map((l) => (
                <tr key={l.id}>
                  <td className="px-4 py-3 text-ink-soft">{productName.get(l.productId) ?? l.productId.slice(0, 8)}</td>
                  <td className="px-4 py-3 text-right tabular">{l.quantity}</td>
                  <td className="px-4 py-3 text-right tabular">
                    <span className={l.outstandingQuantity > 0 ? 'text-warning' : 'text-success'}>
                      {l.quantityReceived}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right tabular">{fmtMoney(l.unitCost)}</td>
                  <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card h-fit p-5">
          <div className="flex justify-between py-1 text-sm">
            <span className="text-ink-muted">Order date</span>
            <span className="tabular text-ink-soft">{fmtDate(order.orderDate)}</span>
          </div>
          <div className="my-2 border-t border-line" />
          <SummaryRow label="Subtotal (excl. tax)" value={fmtMoney(order.subTotal)} strong />
        </div>
      </div>

      {receiving && <ReceiveGoodsModal order={order} onClose={() => setReceiving(false)} />}
    </DetailScaffold>
  );
}

function ReceiveGoodsModal({ order, onClose }: { order: PurchaseOrderDetail; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const outstanding = order.lines.filter((l) => l.outstandingQuantity > 0);
  const [qty, setQty] = useState<Record<string, number>>(
    Object.fromEntries(outstanding.map((l) => [l.id, l.outstandingQuantity])),
  );
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/purchase-orders/${order.id}/receipts`, {
        purchaseOrderId: order.id,
        receivedDate: new Date().toISOString().slice(0, 10),
        lines: outstanding
          .filter((l) => (qty[l.id] ?? 0) > 0)
          .map((l) => ({ purchaseOrderLineId: l.id, quantity: qty[l.id] })),
        notes: null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['purchase-order', order.id] });
      qc.invalidateQueries({ queryKey: ['purchase-orders'] });
      qc.invalidateQueries({ queryKey: ['stock-levels'] });
      toast('Goods received into stock.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="Receive goods"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Post receipt'}
          </button>
        </>
      }
    >
      <div className="space-y-3">
        {error && <ErrorNote message={error} />}
        <p className="text-sm text-ink-muted">Enter the quantity received for each line (default is the outstanding amount).</p>
        {outstanding.map((l) => (
          <div key={l.id} className="flex items-center justify-between gap-3">
            <span className="text-sm text-ink-soft">
              {l.productId.slice(0, 8)} · outstanding <span className="tabular">{l.outstandingQuantity}</span>
            </span>
            <input
              type="number"
              min="0"
              max={l.outstandingQuantity}
              step="1"
              className="input w-28"
              value={qty[l.id] ?? 0}
              onChange={(e) => setQty((q) => ({ ...q, [l.id]: Number(e.target.value) }))}
            />
          </div>
        ))}
      </div>
    </Modal>
  );
}
