import { useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileSpreadsheet, FileText, FileDown, Eye, EyeOff, BarChart3 } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import { PageHeader, ErrorNote, Spinner, Loading } from '../components/ui';
import { useToast } from '../components/Toast';

// ReportFormat on the API: Csv=1, Excel=2, Pdf=3.
const FORMATS = [
  { key: 1, label: 'CSV', icon: FileDown },
  { key: 2, label: 'Excel', icon: FileSpreadsheet },
  { key: 3, label: 'PDF', icon: FileText },
] as const;

interface ReportTable {
  title: string;
  headers: string[];
  rows: string[][];
}

/** Right-align money/quantity columns; the first column is always the label. */
function cellAlign(col: number) {
  return col === 0 ? 'text-left' : 'text-right tabular';
}

function ReportCard({
  title,
  description,
  path,
  slug,
  previewPath,
  params,
  filters,
}: {
  title: string;
  description: string;
  path: string;
  slug: string;
  previewPath?: string;
  /** Extra query params (e.g. date filters) sent with both the preview and the export request. */
  params?: Record<string, string | undefined>;
  /** Optional filter controls rendered above the action buttons. */
  filters?: ReactNode;
}) {
  const toast = useToast();
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showPreview, setShowPreview] = useState(false);

  const preview = useQuery({
    queryKey: ['report-preview', previewPath, params],
    queryFn: async () => (await api.get<ReportTable>(previewPath!, { params })).data,
    enabled: showPreview && !!previewPath,
  });

  async function download(format: number) {
    setBusy(format);
    setError(null);
    try {
      // Authenticated download: fetch as a blob (so the bearer header is attached), then save.
      const response = await api.get(path, { params: { ...params, format }, responseType: 'blob' });
      const disposition = response.headers['content-disposition'] as string | undefined;
      const match = disposition?.match(/filename="?([^"]+)"?/);
      const filename = match?.[1] ?? `${slug}.${format === 2 ? 'xlsx' : format === 3 ? 'pdf' : 'csv'}`;

      const url = URL.createObjectURL(response.data as Blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      link.click();
      URL.revokeObjectURL(url);
      toast('Report downloaded.');
    } catch (e) {
      setError(apiErrorMessage(e, 'Could not generate the report.'));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="card p-6">
      <h2 className="text-base font-semibold text-ink">{title}</h2>
      <p className="mt-1 text-sm text-ink-muted">{description}</p>
      {error && (
        <div className="mt-3">
          <ErrorNote message={error} />
        </div>
      )}
      {filters && <div className="mt-4">{filters}</div>}
      <div className="mt-5 flex flex-wrap gap-3">
        {FORMATS.map((f) => (
          <button key={f.key} className="btn-secondary" onClick={() => download(f.key)} disabled={busy !== null}>
            {busy === f.key ? <Spinner className="h-4 w-4" /> : <f.icon className="h-4 w-4" />}
            {f.label}
          </button>
        ))}
        {previewPath && (
          <button className="btn-secondary" onClick={() => setShowPreview((s) => !s)}>
            {showPreview ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            {showPreview ? 'Hide' : 'View'}
          </button>
        )}
      </div>
      {showPreview &&
        (preview.isLoading ? (
          <Loading />
        ) : preview.error ? (
          <div className="mt-4">
            <ErrorNote message={apiErrorMessage(preview.error)} />
          </div>
        ) : preview.data ? (
          <div className="mt-5 overflow-x-auto rounded-md border border-line">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  {preview.data.headers.map((h, i) => (
                    <th key={h} className={`table-head px-4 py-2.5 ${cellAlign(i)}`}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {preview.data.rows.map((row, ri) => {
                  const isTotal = row[0] === 'TOTAL';
                  return (
                    <tr key={ri} className={isTotal ? 'bg-lagoon-50/40 font-semibold' : undefined}>
                      {row.map((cell, ci) => (
                        <td key={ci} className={`px-4 py-2 ${cellAlign(ci)} ${ci === 0 ? 'text-ink' : 'text-ink-soft'}`}>
                          {cell}
                        </td>
                      ))}
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {preview.data.rows.length <= 1 && (
              <p className="px-4 py-3 text-sm text-ink-muted">Nothing outstanding — all settled.</p>
            )}
          </div>
        ) : null)}
    </div>
  );
}

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

/** Trial balance: a single as-of date filter. */
function TrialBalanceCard() {
  const [asOfDate, setAsOfDate] = useState(todayIso());
  return (
    <ReportCard
      title="Trial Balance"
      description="Every posted account with its net debit or credit balance as of a given date."
      path="/api/reports/trial-balance"
      previewPath="/api/reports/trial-balance/data"
      slug="trial-balance"
      params={{ asOfDate }}
      filters={
        <div>
          <label className="field-label" htmlFor="tb-as-of">
            As of
          </label>
          <input
            id="tb-as-of"
            type="date"
            className="input w-auto"
            value={asOfDate}
            onChange={(e) => setAsOfDate(e.target.value)}
          />
        </div>
      }
    />
  );
}

/** Profit & loss: a from/to date range. */
function ProfitAndLossCard() {
  const [fromDate, setFromDate] = useState(() => todayIso().slice(0, 8) + '01');
  const [toDate, setToDate] = useState(todayIso());
  return (
    <ReportCard
      title="Profit & Loss"
      description="Revenue and expense accounts summarized over a date range, with net income."
      path="/api/reports/profit-and-loss"
      previewPath="/api/reports/profit-and-loss/data"
      slug="profit-and-loss"
      params={{ fromDate, toDate }}
      filters={
        <div className="flex flex-wrap gap-4">
          <div>
            <label className="field-label" htmlFor="pl-from">
              From
            </label>
            <input
              id="pl-from"
              type="date"
              className="input w-auto"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>
          <div>
            <label className="field-label" htmlFor="pl-to">
              To
            </label>
            <input
              id="pl-to"
              type="date"
              className="input w-auto"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>
        </div>
      }
    />
  );
}

/** Balance sheet: a single as-of date filter. */
function BalanceSheetCard() {
  const [asOfDate, setAsOfDate] = useState(todayIso());
  return (
    <ReportCard
      title="Balance Sheet"
      description="Assets, liabilities and equity as of a given date."
      path="/api/reports/balance-sheet"
      previewPath="/api/reports/balance-sheet/data"
      slug="balance-sheet"
      params={{ asOfDate }}
      filters={
        <div>
          <label className="field-label" htmlFor="bs-as-of">
            As of
          </label>
          <input
            id="bs-as-of"
            type="date"
            className="input w-auto"
            value={asOfDate}
            onChange={(e) => setAsOfDate(e.target.value)}
          />
        </div>
      }
    />
  );
}

export function ReportsPage() {
  return (
    <>
      <PageHeader
        icon={BarChart3}
        eyebrow="Insights"
        title="Reports"
        subtitle="Export operational reports for analysis or filing."
      />
      <div className="grid max-w-4xl gap-6">
        <ReportCard
          title="Receivables Aging"
          description="Who owes you and how overdue it is — outstanding customer invoice balances bucketed by days past due."
          path="/api/reports/receivables-aging"
          previewPath="/api/reports/receivables-aging/data"
          slug="receivables-aging"
        />
        <ReportCard
          title="Payables Aging"
          description="What you owe suppliers — outstanding supplier invoice balances bucketed by days past due."
          path="/api/reports/payables-aging"
          previewPath="/api/reports/payables-aging/data"
          slug="payables-aging"
        />
        <ReportCard
          title="Inventory Valuation"
          description="Quantity on hand and FIFO value per product and warehouse, with a total."
          path="/api/reports/inventory-valuation"
          slug="inventory-valuation"
        />
        <TrialBalanceCard />
        <ProfitAndLossCard />
        <BalanceSheetCard />
      </div>
    </>
  );
}
