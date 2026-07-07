import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import type { Category, UnitOfMeasure, Warehouse } from '../../lib/types';
import { PageHeader } from '../../components/ui';
import { ReferenceManager } from './ReferenceManager';
import { TaxesManager } from './TaxesManager';

interface Branch {
  id: string;
  code: string;
  name: string;
  city?: string | null;
  country?: string | null;
  taxIdentificationNumber?: string | null;
  isActive: boolean;
}

const TABS = ['Taxes', 'Warehouses', 'Branches', 'Categories', 'Units'] as const;
type Tab = (typeof TABS)[number];

export function SettingsPage() {
  const [tab, setTab] = useState<Tab>('Taxes');

  // Branches drive the warehouse picker.
  const branches = useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await api.get<Branch[]>('/api/branches')).data,
  });

  return (
    <>
      <PageHeader title="Settings" subtitle="Configure the shared vocabulary your business runs on." />

      <div className="mb-5 flex flex-wrap gap-1 border-b border-line">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
              tab === t ? 'border-lagoon-500 text-ink' : 'border-transparent text-ink-muted hover:text-ink-soft'
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === 'Taxes' && <TaxesManager />}

      {tab === 'Warehouses' && (
        <ReferenceManager<Warehouse>
          title="Warehouse"
          queryKey="warehouses-list"
          listUrl="/api/warehouses"
          createUrl="/api/warehouses"
          fields={[
            { name: 'code', label: 'Code' },
            { name: 'name', label: 'Name' },
            {
              name: 'branchId',
              label: 'Branch',
              options: (branches.data ?? []).map((b) => ({ value: b.id, label: b.name })),
            },
          ]}
          columns={[
            { label: 'Code', render: (w) => w.code },
            { label: 'Name', render: (w) => w.name },
            { label: 'Branch', render: (w) => w.branchName },
          ]}
        />
      )}

      {tab === 'Branches' && (
        <ReferenceManager<Branch>
          title="Branch"
          queryKey="branches"
          listUrl="/api/branches"
          createUrl="/api/branches"
          fields={[
            { name: 'code', label: 'Code' },
            { name: 'name', label: 'Name' },
            { name: 'city', label: 'City', required: false },
            { name: 'country', label: 'Country', required: false },
            { name: 'taxIdentificationNumber', label: 'TIN', required: false },
          ]}
          columns={[
            { label: 'Code', render: (b) => b.code },
            { label: 'Name', render: (b) => b.name },
            { label: 'City', render: (b) => b.city ?? '—' },
            { label: 'TIN', render: (b) => b.taxIdentificationNumber ?? '—' },
          ]}
        />
      )}

      {tab === 'Categories' && (
        <ReferenceManager<Category>
          title="Category"
          queryKey="categories"
          listUrl="/api/categories"
          createUrl="/api/categories"
          fields={[
            { name: 'code', label: 'Code' },
            { name: 'name', label: 'Name' },
            { name: 'description', label: 'Description', required: false },
          ]}
          columns={[
            { label: 'Code', render: (c) => c.code },
            { label: 'Name', render: (c) => c.name },
            { label: 'Description', render: (c) => c.description ?? '—' },
          ]}
        />
      )}

      {tab === 'Units' && (
        <ReferenceManager<UnitOfMeasure>
          title="Unit"
          queryKey="units"
          listUrl="/api/units-of-measure"
          createUrl="/api/units-of-measure"
          fields={[
            { name: 'code', label: 'Code' },
            { name: 'name', label: 'Name' },
          ]}
          columns={[
            { label: 'Code', render: (u) => u.code },
            { label: 'Name', render: (u) => u.name },
          ]}
        />
      )}
    </>
  );
}
