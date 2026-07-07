import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { Paged } from '../lib/types';

export function Pagination<T>({
  page,
  onPage,
  data,
}: {
  page: number;
  onPage: (p: number) => void;
  data: Paged<T> | undefined;
}) {
  if (!data || data.totalCount === 0) return null;
  const from = (data.page - 1) * data.pageSize + 1;
  const to = Math.min(data.page * data.pageSize, data.totalCount);

  return (
    <div className="flex items-center justify-between border-t border-line px-4 py-3 text-sm text-ink-muted">
      <span className="tabular">
        {from}–{to} of {data.totalCount}
      </span>
      <div className="flex items-center gap-1">
        <button
          className="btn-secondary px-2 py-1.5"
          disabled={!data.hasPreviousPage}
          onClick={() => onPage(page - 1)}
          aria-label="Previous page"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <button
          className="btn-secondary px-2 py-1.5"
          disabled={!data.hasNextPage}
          onClick={() => onPage(page + 1)}
          aria-label="Next page"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
