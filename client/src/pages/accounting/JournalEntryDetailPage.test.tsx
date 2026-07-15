import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { JournalEntryDetailPage } from './JournalEntryDetailPage';
import { ToastProvider } from '../../components/Toast';
import { api } from '../../lib/api';
import { useAuth } from '../../auth/AuthContext';
import type { JournalEntryDetail } from '../../lib/types';

vi.mock('../../lib/api', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  apiErrorMessage: (_e: unknown, fallback = 'Something went wrong.') => fallback,
}));

vi.mock('../../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}));

const entry: JournalEntryDetail = {
  id: 'je1',
  branchId: null,
  entryDate: '2026-07-01',
  reference: 'INV-2026-0001',
  description: 'Invoice issued',
  status: 'Draft',
  source: 'Manual',
  sourceDocumentId: null,
  preparedBy: 'staff@erp.local',
  postedBy: null,
  totalDebits: 500,
  totalCredits: 500,
  lines: [
    { id: 'l1', accountId: 'a1', accountCode: '1100', accountName: 'Accounts Receivable', debit: 500, credit: 0, memo: null },
    { id: 'l2', accountId: 'a2', accountCode: '4000', accountName: 'Sales Revenue', debit: 0, credit: 500, memo: null },
  ],
};

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <MemoryRouter initialEntries={['/accounting/journal-entries/je1']}>
          <Routes>
            <Route path="/accounting/journal-entries/:id" element={<JournalEntryDetailPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('JournalEntryDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuth).mockReturnValue({
      user: null,
      ready: true,
      isAuthenticated: true,
      hasRole: () => false,
      login: vi.fn(),
      verifyMfa: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });
  });

  it('fetches and shows the journal entry with its lines', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: entry });
    renderPage();

    expect(await screen.findByText('Accounts Receivable', { exact: false })).toBeInTheDocument();
    expect(screen.getByText('Sales Revenue', { exact: false })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/api/journal-entries/je1');
  });

  it('hides Post/Void actions for a read-only user', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: entry });
    renderPage();

    await screen.findByText('Accounts Receivable', { exact: false });
    expect(screen.queryByRole('button', { name: 'Post' })).not.toBeInTheDocument();
  });
});
