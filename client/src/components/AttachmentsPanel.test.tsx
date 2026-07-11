import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AttachmentsPanel } from './AttachmentsPanel';
import { ToastProvider } from './Toast';
import { api } from '../lib/api';
import type { Attachment } from '../lib/types';

vi.mock('../lib/api', () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
  apiErrorMessage: (_e: unknown, fallback = 'Something went wrong.') => fallback,
}));

vi.mock('../lib/download', () => ({
  downloadFile: vi.fn(),
}));

const attachment: Attachment = {
  id: 'a1',
  entityType: 'Invoice',
  entityId: 'inv-1',
  fileName: 'delivery-note.pdf',
  contentType: 'application/pdf',
  sizeBytes: 204_800,
  createdAt: '2026-07-01T00:00:00Z',
};

function renderPanel() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <AttachmentsPanel entityType="Invoice" entityId="inv-1" />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe('AttachmentsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lists attachments returned for the given entity', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [attachment] });
    renderPanel();

    expect(await screen.findByText('delivery-note.pdf')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/api/attachments', {
      params: { entityType: 'Invoice', entityId: 'inv-1' },
    });
    // Size is formatted in KB for a 200KB file.
    expect(screen.getByText(/200\.0 KB/)).toBeInTheDocument();
  });

  it('shows an empty state when there are no attachments', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [] });
    renderPanel();

    expect(await screen.findByText('No attachments yet.')).toBeInTheDocument();
  });

  it('uploads the chosen file and refreshes the list', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [] });
    vi.mocked(api.post).mockResolvedValueOnce({ data: attachment });
    vi.mocked(api.get).mockResolvedValueOnce({ data: [attachment] });

    const user = userEvent.setup();
    renderPanel();
    await screen.findByText('No attachments yet.');

    const file = new File(['contents'], 'delivery-note.pdf', { type: 'application/pdf' });
    const input = screen.getByLabelText('Choose a file to attach') as HTMLInputElement;
    await user.upload(input, file);

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [url, body] = vi.mocked(api.post).mock.calls[0];
    expect(url).toBe('/api/attachments');
    expect(body).toBeInstanceOf(FormData);
    const form = body as FormData;
    expect(form.get('entityType')).toBe('Invoice');
    expect(form.get('entityId')).toBe('inv-1');
    expect((form.get('file') as File).name).toBe('delivery-note.pdf');

    expect(await screen.findByText('File uploaded.')).toBeInTheDocument();
  });

  it('rejects a file over the 10 MB limit without calling the API', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [] });
    const user = userEvent.setup();
    renderPanel();
    await screen.findByText('No attachments yet.');

    const tooBig = new File([new Uint8Array(11 * 1024 * 1024)], 'huge.zip');
    const input = screen.getByLabelText('Choose a file to attach') as HTMLInputElement;
    await user.upload(input, tooBig);

    expect(await screen.findByText(/larger than the 10 MB limit/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('deletes an attachment and refreshes the list', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [attachment] });
    vi.mocked(api.delete).mockResolvedValueOnce({ data: undefined });
    vi.mocked(api.get).mockResolvedValueOnce({ data: [] });

    const user = userEvent.setup();
    renderPanel();
    await screen.findByText('delivery-note.pdf');

    await user.click(screen.getByRole('button', { name: 'Remove delivery-note.pdf' }));

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/api/attachments/a1'));
    expect(await screen.findByText('No attachments yet.')).toBeInTheDocument();
  });

  it('downloads an attachment through the authenticated API client', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [attachment] });
    const { downloadFile } = await import('../lib/download');
    const user = userEvent.setup();
    renderPanel();
    await screen.findByText('delivery-note.pdf');

    await user.click(screen.getByRole('button', { name: 'Download delivery-note.pdf' }));

    expect(downloadFile).toHaveBeenCalledWith('/api/attachments/a1/download', 'delivery-note.pdf');
  });
});
