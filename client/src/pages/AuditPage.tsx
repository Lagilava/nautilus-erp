import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ScrollText } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import type { Paged } from '../lib/types';
import { PageHeader, Loading, EmptyState, ErrorNote, StatusPill } from '../components/ui';
import { Pagination } from '../components/Pagination';

interface AuditLog {
  id: string;
  entityName: string;
  entityId: string;
  action: 'Created' | 'Modified' | 'Deleted';
  changes?: string | null;
  userId?: string | null;
  timestamp: string;
}

const TONE = { Created: 'success', Modified: 'neutral', Deleted: 'danger' } as const;

export function AuditPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, error } = useQuery({
    queryKey: ['audit', page],
    queryFn: async () =>
      (await api.get<Paged<AuditLog>>('/api/audit-logs', { params: { page, pageSize: 20 } })).data,
  });

  return (
    <>
      <PageHeader
        icon={ScrollText}
        eyebrow="Insights"
        title="Audit Trail"
        subtitle="Every change to business data — who, what, and when."
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
                    <th className="table-head px-4 py-3">When</th>
                    <th className="table-head px-4 py-3">Entity</th>
                    <th className="table-head px-4 py-3">Action</th>
                    <th className="table-head px-4 py-3">User</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data.items.map((a) => (
                    <tr key={a.id} className="align-top transition-colors hover:bg-lagoon-50/40">
                      <td className="whitespace-nowrap px-4 py-3 text-ink-muted tabular">
                        {new Date(a.timestamp).toLocaleString('en-FJ')}
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-ink">{a.entityName}</div>
                        <div className="text-xs text-ink-muted tabular">{a.entityId.slice(0, 8)}</div>
                      </td>
                      <td className="px-4 py-3">
                        <StatusPill label={a.action} tone={TONE[a.action]} />
                      </td>
                      <td className="px-4 py-3 text-ink-muted tabular">{a.userId?.slice(0, 8) ?? 'system'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} onPage={setPage} data={data} />
          </>
        ) : (
          <div className="p-4">
            <EmptyState title="No audit records yet" />
          </div>
        )}
      </div>
    </>
  );
}
