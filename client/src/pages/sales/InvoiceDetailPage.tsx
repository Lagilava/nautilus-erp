import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Send, Ban, Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { InvoiceDetail, PaymentRecord, PaymentMethod } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize, PAYMENT_METHODS } from '../../lib/status';
import { Loading, ErrorNote, Spinner, StatusPill } from '../../components/ui';
import { DetailScaffold, SummaryRow } from '../../components/DetailScaffold';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';
import { useAuth } from '../../auth/AuthContext';

export function InvoiceDetailPage() {
  const { id = '' } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const { hasRole } = useAuth();
  const canWrite = hasRole('Administrator', 'Manager');
  const [paying, setPaying] = useState(false);

  const { data: inv, isLoading, error } = useQuery({
    queryKey: ['invoice', id],
    queryFn: async () => (await api.get<InvoiceDetail>(`/api/invoices/${id}`)).data,
  });

  const payments = useQuery({
    queryKey: ['invoice-payments', id],
    queryFn: async () => (await api.get<PaymentRecord[]>(`/api/invoices/${id}/payments`)).data,
    enabled: !!inv && inv.status !== 'Draft',
  });

  const action = useMutation({
    mutationFn: (verb: string) => api.post(`/api/invoices/${id}/${verb}`),
    onSuccess: (_r, verb) => {
      qc.invalidateQueries({ queryKey: ['invoice', id] });
      qc.invalidateQueries({ queryKey: ['invoices'] });
      toast(verb === 'issue' ? 'Invoice issued.' : 'Invoice voided.');
    },
    onError: (e) => toast(apiErrorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !inv) return <ErrorNote message={apiErrorMessage(error)} />;

  const canPay = inv.status === 'Issued' || inv.status === 'PartiallyPaid';

  return (
    <DetailScaffold
      backTo="/invoices"
      backLabel="Invoices"
      title={inv.number}
      status={inv.status}
      actions={
        canWrite && (
          <>
            {inv.status === 'Draft' && (
              <button className="btn-primary" disabled={action.isPending} onClick={() => action.mutate('issue')}>
                <Send className="h-4 w-4" /> Issue
              </button>
            )}
            {canPay && (
              <button className="btn-primary" onClick={() => setPaying(true)}>
                <Plus className="h-4 w-4" /> Record payment
              </button>
            )}
            {inv.status === 'Issued' && inv.amountPaid === 0 && (
              <button className="btn-ghost text-danger" disabled={action.isPending} onClick={() => action.mutate('void')}>
                <Ban className="h-4 w-4" /> Void
              </button>
            )}
            {action.isPending && <Spinner className="h-4 w-4" />}
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
                  <th className="table-head px-4 py-3">Description</th>
                  <th className="table-head px-4 py-3 text-right">Qty</th>
                  <th className="table-head px-4 py-3 text-right">Unit</th>
                  <th className="table-head px-4 py-3 text-right">Tax</th>
                  <th className="table-head px-4 py-3 text-right">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {inv.lines.map((l) => (
                  <tr key={l.id}>
                    <td className="px-4 py-3 text-ink-soft">{l.description}</td>
                    <td className="px-4 py-3 text-right tabular">{l.quantity}</td>
                    <td className="px-4 py-3 text-right tabular">{fmtMoney(l.unitPrice)}</td>
                    <td className="px-4 py-3 text-right tabular text-ink-muted">{l.taxRate}%</td>
                    <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(l.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {inv.status !== 'Draft' && (
            <div className="card overflow-hidden">
              <div className="border-b border-line px-4 py-3">
                <h2 className="text-sm font-semibold text-ink">Payments</h2>
              </div>
              {payments.isLoading ? (
                <Loading />
              ) : payments.data && payments.data.length > 0 ? (
                <table className="w-full text-sm">
                  <tbody className="divide-y divide-line">
                    {payments.data.map((p) => (
                      <tr key={p.id}>
                        <td className="px-4 py-3 font-medium text-ink tabular">{p.number}</td>
                        <td className="px-4 py-3 text-ink-muted">{fmtDate(p.paymentDate)}</td>
                        <td className="px-4 py-3 text-ink-soft">{humanize(p.method)}</td>
                        <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(p.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="px-4 py-6 text-center text-sm text-ink-muted">No payments recorded.</p>
              )}
            </div>
          )}
        </div>

        <div className="space-y-4">
          <div className="card p-5">
            <SummaryRow label="Subtotal" value={fmtMoney(inv.subTotal)} />
            <SummaryRow label="Tax" value={fmtMoney(inv.taxTotal)} />
            <div className="my-1 border-t border-line" />
            <SummaryRow label="Total" value={fmtMoney(inv.total)} strong />
            <SummaryRow label="Paid" value={fmtMoney(inv.amountPaid)} />
            <SummaryRow label="Balance" value={fmtMoney(inv.balance)} strong />
          </div>

          <div className="card p-5">
            <div className="flex items-center justify-between">
              <span className="text-sm text-ink-muted">Issue date</span>
              <span className="tabular text-sm text-ink-soft">{fmtDate(inv.issueDate)}</span>
            </div>
            <div className="mt-3 flex items-center justify-between">
              <span className="text-sm text-ink-muted">Fiscalization (FRCS/VMS)</span>
              <StatusPill label={humanize(inv.fiscalStatus)} tone={statusTone(inv.fiscalStatus)} />
            </div>
            {inv.fiscalStatus === 'NotSubmitted' && (
              <p className="mt-2 text-xs text-ink-muted">
                Not submitted — the VMS integration is a verified-only boundary and is not enabled in this build.
              </p>
            )}
          </div>
        </div>
      </div>

      {paying && <RecordPaymentModal invoiceId={id} balance={inv.balance} onClose={() => setPaying(false)} />}
    </DetailScaffold>
  );
}

function RecordPaymentModal({
  invoiceId,
  balance,
  onClose,
}: {
  invoiceId: string;
  balance: number;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const toast = useToast();
  const [amount, setAmount] = useState(balance);
  const [method, setMethod] = useState<PaymentMethod>('Cash');
  const [reference, setReference] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/invoices/${invoiceId}/payments`, {
        invoiceId,
        amount,
        paymentDate: new Date().toISOString().slice(0, 10),
        method,
        reference: reference || null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['invoice', invoiceId] });
      qc.invalidateQueries({ queryKey: ['invoice-payments', invoiceId] });
      qc.invalidateQueries({ queryKey: ['invoices'] });
      toast('Payment recorded.');
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="Record payment"
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
