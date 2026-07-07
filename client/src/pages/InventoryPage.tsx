import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged, StockLevel } from '../lib/types';
import { fmtMoney, fmtNumber } from '../lib/format';
import { PageHeader, Loading, EmptyState, ErrorNote, StatusPill } from '../components/ui';
import { Pagination } from '../components/Pagination';

export function InventoryPage() {
  const [page, setPage] = useState(1);
  const [lowOnly, setLowOnly] = useState(false);

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
        title="Inventory"
        subtitle="Stock on hand and valuation across warehouses (FIFO costed)."
        actions={
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
    </>
  );
}
