import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { api, apiErrorMessage } from '../../lib/api';
import type { Paged, InvoiceSummary } from '../../lib/types';
import { fmtMoney, fmtDate } from '../../lib/format';
import { statusTone, humanize } from '../../lib/status';
import { PageHeader, Loading, EmptyState, ErrorNote, StatusPill } from '../../components/ui';
import { Pagination } from '../../components/Pagination';
import { useCustomers } from '../../lib/pickers';

export function InvoicesPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const customers = useCustomers();
  const customerName = useMemo(
    () => new Map((customers.data ?? []).map((c) => [c.id, c.name])),
    [customers.data],
  );

  const { data, isLoading, error } = useQuery({
    queryKey: ['invoices', page],
    queryFn: async () =>
      (await api.get<Paged<InvoiceSummary>>('/api/invoices', { params: { page, pageSize: 15 } })).data,
  });

  return (
    <>
      <PageHeader title="Invoices" subtitle="Issue tax invoices and record customer payments." />

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
                    <th className="table-head px-4 py-3">Number</th>
                    <th className="table-head px-4 py-3">Customer</th>
                    <th className="table-head px-4 py-3">Issued</th>
                    <th className="table-head px-4 py-3 text-right">Total</th>
                    <th className="table-head px-4 py-3 text-right">Balance</th>
                    <th className="table-head px-4 py-3">Status</th>
                    <th className="table-head px-4 py-3">Fiscal</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((inv) => (
                    <tr
                      key={inv.id}
                      className="cursor-pointer transition-colors hover:bg-lagoon-50/40"
                      onClick={() => navigate(`/invoices/${inv.id}`)}
                    >
                      <td className="px-4 py-3 font-medium text-ink tabular">{inv.number}</td>
                      <td className="px-4 py-3 text-ink-soft">{customerName.get(inv.customerId) ?? '—'}</td>
                      <td className="px-4 py-3 text-ink-muted">{fmtDate(inv.issueDate)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink">{fmtMoney(inv.total)}</td>
                      <td className="px-4 py-3 text-right tabular text-ink-soft">{fmtMoney(inv.balance)}</td>
                      <td className="px-4 py-3">
                        <StatusPill label={humanize(inv.status)} tone={statusTone(inv.status)} />
                      </td>
                      <td className="px-4 py-3">
                        <StatusPill label={humanize(inv.fiscalStatus)} tone={statusTone(inv.fiscalStatus)} />
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
            <EmptyState title="No invoices yet" hint="Raise an invoice from a confirmed sales order." />
          </div>
        )}
      </div>
    </>
  );
}
