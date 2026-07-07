import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, CalendarPlus } from 'lucide-react';
import { api, apiErrorMessage } from '../../lib/api';
import type { Tax } from '../../lib/types';
import { Loading, ErrorNote, EmptyState, Spinner } from '../../components/ui';
import { Modal } from '../../components/Modal';
import { useToast } from '../../components/Toast';

// Effective-dated VAT is the Fiji-critical feature; this surfaces the rate history and
// lets an admin schedule a new rate from a future date without touching code.
export function TaxesManager() {
  const toast = useToast();
  const [creating, setCreating] = useState(false);
  const [addRateTo, setAddRateTo] = useState<Tax | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['taxes'],
    queryFn: async () => (await api.get<Tax[]>('/api/taxes')).data,
  });

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button className="btn-primary" onClick={() => setCreating(true)}>
          <Plus className="h-4 w-4" /> New tax
        </button>
      </div>

      {isLoading ? (
        <Loading />
      ) : error ? (
        <ErrorNote message={apiErrorMessage(error)} />
      ) : data && data.length > 0 ? (
        <div className="space-y-3">
          {data.map((tax) => (
            <div key={tax.id} className="card p-4">
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-medium text-ink">
                    {tax.name} <span className="text-ink-muted">({tax.code})</span>
                  </p>
                  <p className="mt-0.5 text-sm text-ink-muted">
                    {tax.treatment} · current rate <span className="tabular font-medium text-ink-soft">{tax.currentRate}%</span>
                  </p>
                </div>
                {tax.treatment === 'Standard' && (
                  <button className="btn-secondary px-3 py-1.5 text-xs" onClick={() => setAddRateTo(tax)}>
                    <CalendarPlus className="h-3.5 w-3.5" /> Add rate
                  </button>
                )}
              </div>
              {tax.rates.length > 0 && (
                <div className="mt-3 flex flex-wrap gap-2 border-t border-line pt-3">
                  {tax.rates.map((r) => (
                    <span key={r.id} className="pill bg-lagoon-50 text-lagoon-700 tabular">
                      {r.percentage}% from {r.effectiveFrom}
                      {r.effectiveTo ? ` to ${r.effectiveTo}` : ''}
                    </span>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      ) : (
        <EmptyState title="No taxes yet" hint="Create a VAT definition to begin." />
      )}

      {creating && <NewTaxModal onClose={() => setCreating(false)} onDone={() => toast('Tax created.')} />}
      {addRateTo && (
        <AddRateModal tax={addRateTo} onClose={() => setAddRateTo(null)} onDone={() => toast('Rate scheduled.')} />
      )}
    </div>
  );
}

function NewTaxModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const qc = useQueryClient();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [treatment, setTreatment] = useState<'Standard' | 'ZeroRated' | 'Exempt'>('Standard');
  const [percentage, setPercentage] = useState('15');
  const [effectiveFrom, setEffectiveFrom] = useState('2020-01-01');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      api.post('/api/taxes', {
        code,
        name,
        treatment,
        initialPercentage: treatment === 'Standard' ? Number(percentage) : null,
        effectiveFrom: treatment === 'Standard' ? effectiveFrom : null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['taxes'] });
      onDone();
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title="New tax"
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={!code || !name || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Create tax'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">Code</label>
            <input className="input" value={code} onChange={(e) => setCode(e.target.value)} />
          </div>
          <div>
            <label className="field-label">Name</label>
            <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
        </div>
        <div>
          <label className="field-label">Treatment</label>
          <select className="input" value={treatment} onChange={(e) => setTreatment(e.target.value as typeof treatment)}>
            <option value="Standard">Standard (rated)</option>
            <option value="ZeroRated">Zero-rated</option>
            <option value="Exempt">Exempt</option>
          </select>
        </div>
        {treatment === 'Standard' && (
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="field-label">Initial rate (%)</label>
              <input className="input" type="number" step="0.01" value={percentage} onChange={(e) => setPercentage(e.target.value)} />
            </div>
            <div>
              <label className="field-label">Effective from</label>
              <input className="input" type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}

function AddRateModal({ tax, onClose, onDone }: { tax: Tax; onClose: () => void; onDone: () => void }) {
  const qc = useQueryClient();
  const [percentage, setPercentage] = useState('');
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/taxes/${tax.id}/rates`, { taxId: tax.id, percentage: Number(percentage), effectiveFrom }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['taxes'] });
      onDone();
      onClose();
    },
    onError: (e) => setError(apiErrorMessage(e)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={`Schedule new rate — ${tax.name}`}
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-primary" disabled={!percentage || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Schedule rate'}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <ErrorNote message={error} />}
        <p className="text-sm text-ink-muted">
          The current open rate is closed the day before the new one begins. Invoices always use the rate in force on
          their issue date.
        </p>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="field-label">New rate (%)</label>
            <input className="input" type="number" step="0.01" value={percentage} onChange={(e) => setPercentage(e.target.value)} />
          </div>
          <div>
            <label className="field-label">Effective from</label>
            <input className="input" type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />
          </div>
        </div>
      </div>
    </Modal>
  );
}
