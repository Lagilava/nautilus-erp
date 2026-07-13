import { useState } from 'react';
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
}: {
  title: string;
  description: string;
  path: string;
  slug: string;
  previewPath?: string;
}) {
  const toast = useToast();
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showPreview, setShowPreview] = useState(false);

  const preview = useQuery({
    queryKey: ['report-preview', previewPath],
    queryFn: async () => (await api.get<ReportTable>(previewPath!)).data,
    enabled: showPreview && !!previewPath,
  });

  async function download(format: number) {
    setBusy(format);
    setError(null);
    try {
      // Authenticated download: fetch as a blob (so the bearer header is attached), then save.
      const response = await api.get(path, { params: { format }, responseType: 'blob' });
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
      </div>
    </>
  );
}
