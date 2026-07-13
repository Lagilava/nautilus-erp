import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ReceiptText } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { Paged, SupplierInvoiceSummary } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize } from '../../lib/status';
import { PageHeader, TableSkeleton, EmptyState, ErrorNote, StatusPill } from '../../components/ui';
import { Pagination } from '../../components/Pagination';
import { useSuppliers } from '../../lib/pickers';

export function SupplierInvoicesPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const suppliers = useSuppliers();
  const supplierName = useMemo(
    () => new Map((suppliers.data ?? []).map((s) => [s.id, s.name])),
    [suppliers.data],
  );

  const { data, isLoading, error } = useQuery({
    queryKey: ['supplier-invoices', page],
    queryFn: async () =>
      (await api.get<Paged<SupplierInvoiceSummary>>('/api/supplier-invoices', { params: { page, pageSize: 15 } })).data,
  });

  return (
    <>
      <PageHeader
        icon={ReceiptText}
        eyebrow="Purchasing"
        title="Supplier Invoices"
        subtitle="Approve supplier bills and record payments (accounts payable)."
      />

      <div className="card overflow-hidden">
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
                    <th className="table-head px-4 py-3">Number</th>
                    <th className="table-head px-4 py-3">Supplier</th>
                    <th className="table-head px-4 py-3">Issued</th>
                    <th className="table-head px-4 py-3 text-right">Total</th>
                    <th className="table-head px-4 py-3 text-right">Balance</th>
                    <th className="table-head px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((inv) => (
                    <tr
                      key={inv.id}
                      className="cursor-pointer transition-colors hover:bg-lagoon-50/40"
                      onClick={() => navigate(`/supplier-invoices/${inv.id}`)}
                    >
                      <td className="px-4 py-3 font-medium text-ink tabular">{inv.number}</td>
                      <td className="px-4 py-3 text-ink-soft">{supplierName.get(inv.supplierId) ?? '—'}</td>
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(inv.issueDate)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(inv.total)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-soft">{fmtMoney(inv.balance)}</td>
                      <td className="px-4 py-3">
                        <StatusPill label={humanize(inv.status)} tone={statusTone(inv.status)} />
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
            <EmptyState title="No supplier invoices yet" hint="Create a bill from a purchase order." />
          </div>
        )}
      </div>
    </>
  );
}
