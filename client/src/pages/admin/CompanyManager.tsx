import { useEffect, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../../lib/api';
import { Loading, ErrorNote, Spinner } from '../../components/ui';
import { useToast } from '../../components/Toast';

interface Company {
  legalName: string;
  tradingName?: string | null;
  tin?: string | null;
  addressLine1?: string | null;
  city?: string | null;
  country?: string | null;
  phone?: string | null;
  email?: string | null;
  baseCurrency: string;
  rowVersion?: string | null;
}

// The business's own identity — the seller name + FRCS TIN printed on every tax invoice.
export function CompanyManager() {
  const qc = useQueryClient();
  const toast = useToast();
  const [form, setForm] = useState<Company | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['company'],
    queryFn: async () => (await api.get<Company>('/api/company')).data,
  });

  useEffect(() => {
    if (data) setForm(data);
  }, [data]);

  // Another administrator saved a change while this form was open — refetch instead of
  // sitting on a stale copy.
  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ entityType: string }>).detail;
      if (detail?.entityType === 'CompanyProfile') qc.invalidateQueries({ queryKey: ['company'] });
    };
    window.addEventListener('erp:entity-updated', handler);
    return () => window.removeEventListener('erp:entity-updated', handler);
  }, [qc]);

  const save = useMutation({
    mutationFn: () => api.put('/api/company', form),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['company'] });
      toast('Company profile saved.');
      setConflict(false);
    },
    onError: (e) => {
      if (apiErrorMessage(e).toLowerCase().includes('changed by someone else')) setConflict(true);
      else setError(apiErrorMessage(e));
    },
  });

  if (isLoading || !form) return <Loading />;

  const field = (key: keyof Company, label: string, type = 'text') => (
    <div>
      <label className="field-label" htmlFor="label">{label}</label>
      <input id="label"
        className="input"
        type={type}
        value={(form[key] as string) ?? ''}
        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
      />
    </div>
  );

  return (
    <div className="max-w-2xl space-y-4">
      <p className="text-sm text-ink-muted">
        These details appear on tax invoices. A compliant Fiji tax invoice must show your legal name and FRCS TIN.
      </p>
      {conflict ? (
        <ErrorNote message="The company profile was changed by someone else while this form was open. Reload the page to see their changes before editing." />
      ) : (
        error && <ErrorNote message={error} />
      )}
      <div className="card space-y-4 p-5">
        <div className="grid grid-cols-2 gap-4">
          {field('legalName', 'Legal name')}
          {field('tradingName', 'Trading name')}
        </div>
        <div className="grid grid-cols-2 gap-4">
          {field('tin', 'FRCS TIN')}
          <div>
            <label className="field-label" htmlFor="base-currency">Base currency</label>
            <input id="base-currency" className="input bg-canvas" value={form.baseCurrency} disabled />
          </div>
        </div>
        {field('addressLine1', 'Address')}
        <div className="grid grid-cols-2 gap-4">
          {field('city', 'City')}
          {field('country', 'Country')}
        </div>
        <div className="grid grid-cols-2 gap-4">
          {field('phone', 'Phone')}
          {field('email', 'Email', 'email')}
        </div>
        <div className="flex justify-end">
          <button
            className="btn-primary"
            disabled={save.isPending || !form.legalName || conflict}
            onClick={() => save.mutate()}
          >
            {save.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Save changes'}
          </button>
        </div>
      </div>
    </div>
  );
}
