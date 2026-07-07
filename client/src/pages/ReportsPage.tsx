import { useState } from 'react';
import { FileSpreadsheet, FileText, FileDown } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import { PageHeader, ErrorNote, Spinner } from '../components/ui';
import { useToast } from '../components/Toast';

// ReportFormat on the API: Csv=1, Excel=2, Pdf=3.
const FORMATS = [
  { key: 1, label: 'CSV', icon: FileDown },
  { key: 2, label: 'Excel', icon: FileSpreadsheet },
  { key: 3, label: 'PDF', icon: FileText },
] as const;

export function ReportsPage() {
  const toast = useToast();
  const [busy, setBusy] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function download(format: number) {
    setBusy(format);
    setError(null);
    try {
      // Authenticated download: fetch as a blob (so the bearer header is attached), then save.
      const response = await api.get('/api/reports/inventory-valuation', {
        params: { format },
        responseType: 'blob',
      });
      const disposition = response.headers['content-disposition'] as string | undefined;
      const match = disposition?.match(/filename="?([^"]+)"?/);
      const filename = match?.[1] ?? `inventory-valuation.${format === 2 ? 'xlsx' : format === 3 ? 'pdf' : 'csv'}`;

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
    <>
      <PageHeader title="Reports" subtitle="Export operational reports for analysis or filing." />
      {error && (
        <div className="mb-4">
          <ErrorNote message={error} />
        </div>
      )}
      <div className="card max-w-xl p-6">
        <h2 className="text-base font-semibold text-ink">Inventory Valuation</h2>
        <p className="mt-1 text-sm text-ink-muted">
          Quantity on hand and FIFO value per product and warehouse, with a total.
        </p>
        <div className="mt-5 flex flex-wrap gap-3">
          {FORMATS.map((f) => (
            <button key={f.key} className="btn-secondary" onClick={() => download(f.key)} disabled={busy !== null}>
              {busy === f.key ? <Spinner className="h-4 w-4" /> : <f.icon className="h-4 w-4" />}
              {f.label}
            </button>
          ))}
        </div>
      </div>
    </>
  );
}
