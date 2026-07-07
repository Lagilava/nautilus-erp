import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Tooltip,
  CartesianGrid,
  Cell,
} from 'recharts';
import { Boxes, ShoppingCart, ClipboardList, TrendingUp, ArrowRight } from 'lucide-react';
import { api } from '../lib/api';
import type { Dashboard } from '../lib/types';
import { fmtMoney, fmtNumber } from '../lib/format';
import { PageHeader, Loading, ErrorNote } from '../components/ui';
import { apiErrorMessage } from '../lib/api';

function StatCard({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="card p-5">
      <p className="text-xs font-medium uppercase tracking-wider text-ink-muted">{label}</p>
      <p className="mt-2 font-display text-3xl font-semibold tabular text-ink">{value}</p>
      {hint && <p className="mt-1 text-xs text-ink-muted">{hint}</p>}
    </div>
  );
}

export function DashboardPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => (await api.get<Dashboard>('/api/dashboard')).data,
  });

  if (isLoading) return <Loading />;
  if (error) return <ErrorNote message={apiErrorMessage(error)} />;
  if (!data) return null;

  const chart = [
    { name: 'Sales (mo)', value: data.salesThisMonth, fill: '#0E7367' },
    { name: 'Receivable', value: data.accountsReceivable, fill: '#3F9385' },
    { name: 'Payable', value: data.accountsPayable, fill: '#B98B3E' },
  ];

  return (
    <>
      <PageHeader title="Dashboard" subtitle="A snapshot of your business today." />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Sales this month" value={fmtMoney(data.salesThisMonth)} />
        <StatCard label="Accounts receivable" value={fmtMoney(data.accountsReceivable)} />
        <StatCard label="Accounts payable" value={fmtMoney(data.accountsPayable)} />
        <StatCard label="Inventory value" value={fmtMoney(data.inventoryValue)} />
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="card p-5 lg:col-span-2">
          <div className="mb-4 flex items-center gap-2">
            <TrendingUp className="h-4 w-4 text-lagoon-500" />
            <h2 className="text-base font-semibold text-ink">Financial snapshot</h2>
          </div>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chart} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
                <CartesianGrid vertical={false} stroke="#E7E3DA" />
                <XAxis dataKey="name" tickLine={false} axisLine={false} tick={{ fill: '#697974', fontSize: 12 }} />
                <YAxis
                  tickLine={false}
                  axisLine={false}
                  tick={{ fill: '#697974', fontSize: 12 }}
                  width={70}
                  tickFormatter={(v) => fmtMoney(v).replace('FJ$', '$')}
                />
                <Tooltip
                  cursor={{ fill: '#E9F3F1' }}
                  formatter={(value) => fmtMoney(Number(value))}
                  contentStyle={{ borderRadius: 8, border: '1px solid #E7E3DA', fontSize: 13 }}
                />
                <Bar dataKey="value" radius={[4, 4, 0, 0]} maxBarSize={72}>
                  {chart.map((c) => (
                    <Cell key={c.name} fill={c.fill} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="card divide-y divide-line">
          <div className="px-5 py-4">
            <h2 className="text-base font-semibold text-ink">Needs attention</h2>
          </div>
          <AttentionRow
            to="/inventory"
            icon={<Boxes className="h-4 w-4" />}
            label="Low-stock items"
            value={fmtNumber(data.lowStockCount)}
            tone={data.lowStockCount > 0 ? 'warn' : 'ok'}
          />
          <AttentionRow
            to="/sales-orders"
            icon={<ShoppingCart className="h-4 w-4" />}
            label="Open sales orders"
            value={fmtNumber(data.openSalesOrders)}
          />
          <AttentionRow
            to="/purchase-orders"
            icon={<ClipboardList className="h-4 w-4" />}
            label="Open purchase orders"
            value={fmtNumber(data.openPurchaseOrders)}
          />
        </div>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard label="Products" value={fmtNumber(data.productCount)} />
        <StatCard label="Customers" value={fmtNumber(data.customerCount)} />
        <StatCard label="Suppliers" value={fmtNumber(data.supplierCount)} />
      </div>
    </>
  );
}

function AttentionRow({
  to,
  icon,
  label,
  value,
  tone = 'ok',
}: {
  to: string;
  icon: ReactNode;
  label: string;
  value: string;
  tone?: 'ok' | 'warn';
}) {
  return (
    <Link to={to} className="flex items-center justify-between px-5 py-4 transition-colors hover:bg-lagoon-50/50">
      <div className="flex items-center gap-3">
        <span className={tone === 'warn' ? 'text-warning' : 'text-lagoon-500'}>{icon}</span>
        <span className="text-sm text-ink-soft">{label}</span>
      </div>
      <div className="flex items-center gap-2">
        <span className="tabular font-semibold text-ink">{value}</span>
        <ArrowRight className="h-4 w-4 text-ink-muted" />
      </div>
    </Link>
  );
}
