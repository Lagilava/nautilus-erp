import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, XCircle, Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { SupplierInvoiceDetail, PaymentMethod } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { PAYMENT_METHODS } from '../../lib/status';
import { Loading, ErrorNote, Spinner } from '../../components/ui';
import { DetailScaffold, SummaryRow } from '../../components/DetailScaffold';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

export function SupplierInvoiceDetailPage() {
  const { id = '' } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [paying, setPaying] = useState(false);

  const { data: inv, isLoading, error } = useQuery({
    queryKey: ['supplier-invoice', id],
    queryFn: async () => (await api.get<SupplierInvoiceDetail>(`/api/supplier-invoices/${id}`)).data,
  });

  const action = useMutation({
    mutationFn: (verb: string) => api.post(`/api/supplier-invoices/${id}/${verb}`),
    onSuccess: (_r, verb) => {
      qc.invalidateQueries({ queryKey: ['supplier-invoice', id] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices'] });
      toast(`Invoice ${verb === 'approve' ? 'approved' : 'cancelled'}.`);
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !inv) return <ErrorNote message={apiErrorMessage(error)} />;

  const canPay = inv.status === 'Approved' || inv.status === 'PartiallyPaid';

  return (
    <DetailScaffold
      backTo="/supplier-invoices"
      backLabel="Supplier Invoices"
      title={inv.number}
      status={inv.status}
      actions={
        canWrite && (
          <>
            {inv.status === 'Draft' && (
              <button className="btn-primary" disabled={action.isPending} onClick={() => action.mutate('approve')}>
                <CheckCircle2 className="h-4 w-4" /> Approve
              </button>
            )}
            {canPay && (
              <button className="btn-primary" onClick={() => setPaying(true)}>
                <Plus className="h-4 w-4" /> Record payment
              </button>
            )}
            {inv.amountPaid === 0 && inv.status !== 'Cancelled' && (
              <button className="btn-ghost text-danger" disabled={action.isPending} onClick={() => action.mutate('cancel')}>
                <XCircle className="h-4 w-4" /> Cancel
              </button>
            )}
            {action.isPending && <Spinner className="h-4 w-4" />}
          </>
        )
      }
    >
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="card overflow-hidden lg:col-span-2">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-line">
                <th className="table-head px-4 py-3">Description</th>
                <th className="table-head px-4 py-3 text-right">Qty</th>
                <th className="table-head px-4 py-3 text-right">Unit cost</th>
                <th className="table-head px-4 py-3 text-right">Tax</th>
                <th className="table-head px-4 py-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {inv.lines.map((l) => (
                <tr key={l.id}>
                  <td className="px-4 py-3 text-ink-soft">{l.description}</td>
                  <td className="px-4 py-3 text-right tabular">{l.quantity}</td>
                  <td className="px-4 py-3 text-right tabular">{fmtMoney(l.unitCost)}</td>
                  <td className="px-4 py-3 text-right tabular text-ink-muted">{l.taxRate}%</td>
                  <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card h-fit p-5">
          <div className="flex justify-between py-1 text-sm">
            <span className="text-ink-muted">Issue date</span>
            <span className="tabular text-ink-soft">{fmtDate(inv.issueDate)}</span>
          </div>
          {inv.supplierReference && (
            <div className="flex justify-between py-1 text-sm">
              <span className="text-ink-muted">Supplier ref</span>
              <span className="tabular text-ink-soft">{inv.supplierReference}</span>
            </div>
          )}
          <div className="my-2 border-t border-line" />
          <SummaryRow label="Subtotal" value={fmtMoney(inv.subTotal)} />
          <SummaryRow label="Tax" value={fmtMoney(inv.taxTotal)} />
          <SummaryRow label="Total" value={fmtMoney(inv.total)} strong />
          <SummaryRow label="Paid" value={fmtMoney(inv.amountPaid)} />
          <SummaryRow label="Balance" value={fmtMoney(inv.balance)} strong />
        </div>
      </div>

      {paying && <PaySupplierModal invoiceId={id} balance={inv.balance} onClose={() => setPaying(false)} />}
    </DetailScaffold>
  );
}

function PaySupplierModal({ invoiceId, balance, onClose }: { invoiceId: string; balance: number; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [amount, setAmount] = useState(balance);
  const [method, setMethod] = useState<PaymentMethod>('BankTransfer');
  const [reference, setReference] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/supplier-invoices/${invoiceId}/payments`, {
        supplierInvoiceId: invoiceId,
        amount,
        paymentDate: new Date().toISOString().slice(0, 10),
        method,
        reference: reference || null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['supplier-invoice', invoiceId] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices'] });
      toast('Payment recorded.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="Record supplier payment"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn-primary"
            disabled={mutation.isPending || amount <= 0 || amount > balance}
            onClick={() => mutation.mutate()}
          >
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Record payment'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">Amount (max {fmtMoney(balance)})</label>
            <input
              type="number"
              step="0.01"
              className="input"
              value={amount}
              onChange={(e) => setAmount(Number(e.target.value))}
            />
          </div>
          <div>
            <label className="field-label">Method</label>
            <select className="input" value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
              {PAYMENT_METHODS.map((m) => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div>
          <label className="field-label">Reference (optional)</label>
          <input className="input" value={reference} onChange={(e) => setReference(e.target.value)} />
        </div>
      </div>
    </Modal>
  );
}
