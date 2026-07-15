import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { JournalEntriesPage } from './JournalEntriesPage';
import { ToastProvider } from '../../components/Toast';
import { api } from '../../lib/api';
import { useAuth } from '../../auth/AuthContext';
import type { JournalEntrySummary, Paged } from '../../lib/types';

vi.mock('../../lib/api', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  apiErrorMessage: (_e: unknown, fallback = 'Something went wrong.') => fallback,
}));

vi.mock('../../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}));

const paged: Paged<JournalEntrySummary> = {
  items: [
    {
      id: 'je1',
      entryDate: '2026-07-01',
      reference: 'INV-2026-0001',
      status: 'Posted',
      source: 'SalesInvoice',
      totalDebits: 500,
      totalCredits: 500,
    },
  ],
  page: 1,
  pageSize: 15,
  totalCount: 1,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <MemoryRouter>
          <JournalEntriesPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('JournalEntriesPage', () => {
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

  it('fetches and lists journal entries', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: paged });
    renderPage();

    expect(await screen.findByText('INV-2026-0001')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/api/journal-entries', {
      params: {
        page: 1,
        pageSize: 15,
        status: undefined,
        source: undefined,
        fromDate: undefined,
        toDate: undefined,
      },
    });
  });

  it('hides the New Journal Entry button for read-only users', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: paged });
    renderPage();

    await screen.findByText('INV-2026-0001');
    expect(screen.queryByRole('button', { name: /New Journal Entry/ })).not.toBeInTheDocument();
  });
});
