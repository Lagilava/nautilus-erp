import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import { Loading, ErrorNote, EmptyState, Spinner } from '../../components/ui';

export interface RefField {
  name: string;
  label: string;
  type?: 'text' | 'number';
  options?: { value: string; label: string }[];
  required?: boolean;
}

export interface RefColumn<T> {
  label: string;
  render: (item: T) => React.ReactNode;
  align?: 'left' | 'right';
}

/**
 * Generic list + inline-create for a simple reference resource. Keeps the five settings
 * tabs consistent without repeating table/form boilerplate.
 */
export function ReferenceManager<T extends { id: string }>({
  title,
  queryKey,
  listUrl,
  createUrl,
  columns,
  fields,
}: {
  title: string;
  queryKey: string;
  listUrl: string;
  createUrl: string;
  columns: RefColumn<T>[];
  fields: RefField[];
}) {
  const qc = useQueryClient();
  const [values, setValues] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: [queryKey],
    queryFn: async () => (await api.get<T[]>(listUrl)).data,
  });

  const create = useMutation({
    mutationFn: () => {
      const payload: Record<string, unknown> = {};
      for (const f of fields) {
        const raw = values[f.name] ?? '';
        payload[f.name] = f.type === 'number' ? Number(raw || 0) : raw || null;
      }
      return api.post(createUrl, payload);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [queryKey] });
      qc.invalidateQueries({ queryKey: ['picker'] });
      setValues({});
      setError(null);
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  const valid = fields.filter((f) => f.required !== false).every((f) => (values[f.name] ?? '').trim() !== '');

  return (
    <div className="space-y-4">
      <div className="card p-4">
        <h3 className="mb-3 text-sm font-semibold text-ink">Add {title.toLowerCase()}</h3>
        {error && (
          <div className="mb-3">
            <ErrorNote message={error} />
          </div>
        )}
        <div className="flex flex-wrap items-end gap-3">
          {fields.map((f) => (
            <div key={f.name} className="min-w-[8rem] flex-1">
              <label className="field-label">{f.label}</label>
              {f.options ? (
                <select
                  className="input"
                  value={values[f.name] ?? ''}
                  onChange={(e) => setValues((v) => ({ ...v, [f.name]: e.target.value }))}
                >
                  <option value="">Select…</option>
                  {f.options.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  className="input"
                  type={f.type === 'number' ? 'number' : 'text'}
                  value={values[f.name] ?? ''}
                  onChange={(e) => setValues((v) => ({ ...v, [f.name]: e.target.value }))}
                />
              )}
            </div>
          ))}
          <button className="btn-primary" disabled={!valid || create.isPending} onClick={() => create.mutate()}>
            {create.isPending ? <Spinner className="h-4 w-4 text-white" /> : <Plus className="h-4 w-4" />}
            Add
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        {isLoading ? (
          <Loading />
        ) : data && data.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-line">
                  {columns.map((c) => (
                    <th key={c.label} className={`table-head px-4 py-3 ${c.align === 'right' ? 'text-right' : ''}`}>
                      {c.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data.map((item) => (
                  <tr key={item.id}>
                    {columns.map((c) => (
                      <td key={c.label} className={`px-4 py-3 ${c.align === 'right' ? 'text-right tabular' : 'text-ink-soft'}`}>
                        {c.render(item)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-4">
            <EmptyState title={`No ${title.toLowerCase()} yet`} />
          </div>
        )}
      </div>
    </div>
  );
}
