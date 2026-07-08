import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Tooltip,
  CartesianGrid,
} from 'recharts';
import {
  Boxes,
  ShoppingCart,
  ClipboardList,
  TrendingUp,
  ArrowRight,
  Activity,
  FileText,
  Plus,
  UserPlus,
} from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Dashboard, Paged, InvoiceSummary } from '../lib/types';
import { fmtMoney, fmtNumber } from '../lib/format';
import { statusTone, humanize } from '../lib/status';
import { PageHeader, Loading, ErrorNote, StatusPill } from '../components/ui';
import { useAuth } from '../auth/AuthContext';

interface AuditEntry {
  id: string;
  entityName: string;
  action: string;
  userId?: string | null;
  timestamp: string;
}

interface TrendPoint {
  month: string;
  label: string;
  total: number;
}

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
  const { user, hasRole } = useAuth();
  const isAdmin = hasRole('Administrator');

  const { data, isLoading, error } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => (await api.get<Dashboard>('/api/dashboard')).data,
  });

  // Admin-only feeds. Audit is Administrator-only on the API; invoices any authenticated user.
  const audit = useQuery({
    queryKey: ['dashboard-audit'],
    queryFn: async () =>
      (await api.get<Paged<AuditEntry>>('/api/audit-logs', { params: { page: 1, pageSize: 7 } })).data,
    enabled: isAdmin,
  });
  const recentInvoices = useQuery({
    queryKey: ['dashboard-invoices'],
    queryFn: async () =>
      (await api.get<Paged<InvoiceSummary>>('/api/invoices', { params: { page: 1, pageSize: 5 } })).data,
    enabled: isAdmin,
  });
  const trend = useQuery({
    queryKey: ['dashboard-trend'],
    queryFn: async () => (await api.get<TrendPoint[]>('/api/dashboard/sales-trend')).data,
  });

  if (isLoading) return <Loading />;
  if (error) return <ErrorNote message={apiErrorMessage(error)} />;
  if (!data) return null;

  return (
    <>
      <PageHeader
        title={`Welcome back, ${user?.firstName ?? ''}`.trim()}
        subtitle={
          isAdmin
            ? 'You have full administrative access. Here is where the business stands today.'
            : 'A snapshot of your business today.'
        }
        actions={
          isAdmin && (
            <div className="hidden items-center gap-2 sm:flex">
              <Link to="/sales-orders" className="btn-secondary">
                <Plus className="h-4 w-4" /> Sales order
              </Link>
              <Link to="/admin/users" className="btn-secondary">
                <UserPlus className="h-4 w-4" /> Add user
              </Link>
            </div>
          )
        }
      />

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
            <h2 className="text-base font-semibold text-ink">Invoiced sales — last 6 months</h2>
          </div>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={trend.data ?? []} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
                <defs>
                  <linearGradient id="salesFill" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#0E7367" stopOpacity={0.25} />
                    <stop offset="100%" stopColor="#0E7367" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid vertical={false} stroke="#E7E3DA" />
                <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: '#697974', fontSize: 12 }} />
                <YAxis
                  tickLine={false}
                  axisLine={false}
                  tick={{ fill: '#697974', fontSize: 12 }}
                  width={70}
                  tickFormatter={(v) => fmtMoney(v).replace('FJ$', '$')}
                />
                <Tooltip
                  formatter={(value) => fmtMoney(Number(value))}
                  contentStyle={{ borderRadius: 8, border: '1px solid #E7E3DA', fontSize: 13 }}
                />
                <Area
                  type="monotone"
                  dataKey="total"
                  stroke="#0E7367"
                  strokeWidth={2}
                  fill="url(#salesFill)"
                  dot={{ r: 3, fill: '#0E7367' }}
                />
              </AreaChart>
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

      {/* Admin-only operational feeds */}
      {isAdmin && (
        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="card">
            <div className="flex items-center justify-between border-b border-line px-5 py-4">
              <div className="flex items-center gap-2">
                <Activity className="h-4 w-4 text-lagoon-500" />
                <h2 className="text-base font-semibold text-ink">Recent activity</h2>
              </div>
              <Link to="/audit" className="text-xs font-medium text-lagoon-600 hover:text-lagoon-700">
                View all
              </Link>
            </div>
            <div className="divide-y divide-line">
              {audit.isLoading ? (
                <div className="px-5 py-8 text-center text-sm text-ink-muted">Loading…</div>
              ) : audit.data && audit.data.items.length > 0 ? (
                audit.data.items.map((a) => (
                  <div key={a.id} className="flex items-center justify-between px-5 py-3">
                    <div className="flex items-center gap-3">
                      <StatusPill
                        label={a.action}
                        tone={a.action === 'Created' ? 'success' : a.action === 'Deleted' ? 'danger' : 'neutral'}
                      />
                      <span className="text-sm text-ink-soft">{humanize(a.entityName)}</span>
                    </div>
                    <span className="tabular text-xs text-ink-muted">
                      {new Date(a.timestamp).toLocaleString('en-FJ', {
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </span>
                  </div>
                ))
              ) : (
                <div className="px-5 py-8 text-center text-sm text-ink-muted">No recorded activity yet.</div>
              )}
            </div>
          </div>

          <div className="card">
            <div className="flex items-center justify-between border-b border-line px-5 py-4">
              <div className="flex items-center gap-2">
                <FileText className="h-4 w-4 text-lagoon-500" />
                <h2 className="text-base font-semibold text-ink">Recent invoices</h2>
              </div>
              <Link to="/invoices" className="text-xs font-medium text-lagoon-600 hover:text-lagoon-700">
                View all
              </Link>
            </div>
            <div className="divide-y divide-line">
              {recentInvoices.isLoading ? (
                <div className="px-5 py-8 text-center text-sm text-ink-muted">Loading…</div>
              ) : recentInvoices.data && recentInvoices.data.items.length > 0 ? (
                recentInvoices.data.items.map((inv) => (
                  <Link
                    key={inv.id}
                    to={`/invoices/${inv.id}`}
                    className="flex items-center justify-between px-5 py-3 transition-colors hover:bg-lagoon-50/50"
                  >
                    <div className="flex items-center gap-3">
                      <span className="tabular text-sm font-medium text-ink">{inv.number}</span>
                      <StatusPill label={humanize(inv.status)} tone={statusTone(inv.status)} />
                    </div>
                    <span className="tabular text-sm text-ink-soft">{fmtMoney(inv.total)}</span>
                  </Link>
                ))
              ) : (
                <div className="px-5 py-8 text-center text-sm text-ink-muted">No invoices yet.</div>
              )}
            </div>
          </div>
        </div>
      )}

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
