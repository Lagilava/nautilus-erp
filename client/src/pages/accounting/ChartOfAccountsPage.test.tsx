import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ChartOfAccountsPage } from './ChartOfAccountsPage';
import { ToastProvider } from '../../components/Toast';
import { api } from '../../lib/api';
import { useAuth } from '../../auth/AuthContext';
import type { Account } from '../../lib/types';

vi.mock('../../lib/api', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  apiErrorMessage: (_e: unknown, fallback = 'Something went wrong.') => fallback,
}));

vi.mock('../../auth/AuthContext', () => ({
  useAuth: vi.fn(),
}));

const accounts: Account[] = [
  { id: 'a1', code: '1100', name: 'Accounts Receivable', type: 'Asset', isSystem: true, isActive: true },
  { id: 'a2', code: '4000', name: 'Sales Revenue', type: 'Revenue', isSystem: true, isActive: true },
];

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <ChartOfAccountsPage />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('ChartOfAccountsPage', () => {
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

  it('fetches and lists accounts', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: accounts });
    renderPage();

    expect(await screen.findByText('Accounts Receivable')).toBeInTheDocument();
    expect(screen.getByText('Sales Revenue')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/api/chart-of-accounts', { params: { activeOnly: undefined } });
  });

  it('does not show the New Account button for a non-administrator', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: accounts });
    renderPage();

    await screen.findByText('Accounts Receivable');
    expect(screen.queryByRole('button', { name: /New Account/ })).not.toBeInTheDocument();
  });
});
