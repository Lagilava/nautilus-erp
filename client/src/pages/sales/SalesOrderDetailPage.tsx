import { useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, PackageCheck, XCircle, FileText, ShoppingCart } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { SalesOrderDetail } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { Loading, ErrorNote, Spinner } from '../../components/ui';
import { DetailScaffold, SummaryRow } from '../../components/DetailScaffold';
import { AttachmentsPanel } from '../../components/AttachmentsPanel';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';
import { useProducts } from '../../lib/pickers';

export function SalesOrderDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');

  const products = useProducts();
  const productName = useMemo(
    () => new Map((products.data ?? []).map((p) => [p.id, `${p.sku} — ${p.name}`])),
    [products.data],
  );

  const { data: order, isLoading, error } = useQuery({
    queryKey: ['sales-order', id],
    queryFn: async () => (await api.get<SalesOrderDetail>(`/api/sales-orders/${id}`)).data,
  });

  const action = useMutation({
    mutationFn: (verb: string) => api.post(`/api/sales-orders/${id}/${verb}`),
    onSuccess: (_r, verb) => {
      qc.invalidateQueries({ queryKey: ['sales-order', id] });
      qc.invalidateQueries({ queryKey: ['sales-orders'] });
      toast(`Order ${verb === 'fulfill' ? 'fulfilled' : verb + 'ed'}.`);
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  const createInvoice = useMutation({
    mutationFn: async () => {
      const today = new Date().toISOString().slice(0, 10);
      return (await api.post<string>('/api/invoices/from-order', { salesOrderId: id, issueDate: today, dueDate: null }))
        .data;
    },
    onSuccess: (invoiceId) => {
      toast('Invoice created.');
      navigate(`/invoices/${invoiceId}`);
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !order) return <ErrorNote message={apiErrorMessage(error)} />;

  const busy = action.isPending || createInvoice.isPending;

  return (
    <DetailScaffold
      icon={ShoppingCart}
      backTo="/sales-orders"
      backLabel="Sales Orders"
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
            {order.status === 'Confirmed' && (
              <button className="btn-primary" disabled={busy} onClick={() => action.mutate('fulfill')}>
                <PackageCheck className="h-4 w-4" /> Fulfil (issue stock)
              </button>
            )}
            {(order.status === 'Confirmed' || order.status === 'Fulfilled') && (
              <button className="btn-secondary" disabled={busy} onClick={() => createInvoice.mutate()}>
                <FileText className="h-4 w-4" /> Create invoice
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
        <div className="space-y-4 lg:col-span-2">
          <div className="card overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  <th className="table-head px-4 py-3">Product</th>
                  <th className="table-head px-4 py-3 text-right">Qty</th>
                  <th className="table-head px-4 py-3 text-right">Unit price</th>
                  <th className="table-head px-4 py-3 text-right">Line total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {order.lines.map((l) => (
                  <tr key={l.id}>
                    <td className="px-4 py-3 text-ink-soft">{productName.get(l.productId) ?? l.productId.slice(0, 8)}</td>
                    <td className="px-4 py-3 text-right tabular">{l.quantity}</td>
                    <td className="px-4 py-3 text-right tabular">{fmtMoney(l.unitPrice)}</td>
                    <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <AttachmentsPanel entityType="SalesOrder" entityId={id} />
        </div>

        <div className="card h-fit p-5">
          <dl className="space-y-1">
            <div className="flex justify-between py-1 text-sm">
              <dt className="text-ink-muted">Order date</dt>
              <dd className="tabular text-ink-soft">{fmtDate(order.orderDate)}</dd>
            </div>
            <div className="my-2 border-t border-line" />
            <SummaryRow label="Subtotal (excl. tax)" value={fmtMoney(order.subTotal)} strong />
          </dl>
          <p className="mt-4 text-xs text-ink-muted">
            Tax is applied when an invoice is raised, at the rate in force on the invoice date.
          </p>
        </div>
      </div>
    </DetailScaffold>
  );
}
