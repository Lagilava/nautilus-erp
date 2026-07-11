import { useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Paperclip, Download, Trash2, Upload } from 'lucide-react';
import { api, apiErrorMessage } from '../lib/api';
import { downloadFile } from '../lib/download';
import type { Attachment } from '../lib/types';
import { fmtDate } from '../lib/format';
import { Loading, Spinner } from './ui';
import { useToast } from './Toast';

// Mirrors UploadAttachmentCommandValidator.MaxSizeBytes on the API — checked client-side too
// so an oversized file is rejected instantly rather than after a slow upload.
const MAX_UPLOAD_BYTES = 10 * 1024 * 1024;

function fmtSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * Files attached to a record — a scanned invoice, a delivery photo — via the generic
 * (entityType, entityId) attachments API. Drop this into any detail page's card stack.
 */
export function AttachmentsPanel({ entityType, entityId }: { entityType: string; entityId: string }) {
  const qc = useQueryClient();
  const toast = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  const queryKey = ['attachments', entityType, entityId];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: async () =>
      (await api.get<Attachment[]>('/api/attachments', { params: { entityType, entityId } })).data,
  });

  const upload = useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append('entityType', entityType);
      form.append('entityId', entityId);
      form.append('file', file);
      return api.post('/api/attachments', form);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey });
      toast('File uploaded.');
    },
    onError: (e) => toast(apiErrorMessage(e, 'Upload failed.'), 'error'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/api/attachments/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey });
      toast('File removed.');
    },
    onError: (e) => toast(apiErrorMessage(e, 'Could not remove the file.'), 'error'),
  });

  const onFileChosen = (file: File | undefined) => {
    if (!file) return;
    if (file.size > MAX_UPLOAD_BYTES) {
      toast('That file is larger than the 10 MB limit.', 'error');
      return;
    }
    upload.mutate(file);
  };

  return (
    <div className="card overflow-hidden">
      <div className="flex items-center justify-between border-b border-line px-4 py-3">
        <h2 className="text-sm font-semibold text-ink">Attachments</h2>
        <button
          type="button"
          className="btn-ghost px-2 py-1 text-xs"
          disabled={upload.isPending}
          onClick={() => fileInput.current?.click()}
        >
          {upload.isPending ? <Spinner className="h-3.5 w-3.5" /> : <Upload className="h-3.5 w-3.5" />}
          Upload
        </button>
        <input
          ref={fileInput}
          type="file"
          className="hidden"
          aria-label="Choose a file to attach"
          onChange={(e) => {
            onFileChosen(e.target.files?.[0]);
            e.target.value = '';
          }}
        />
      </div>
      {isLoading ? (
        <Loading />
      ) : data && data.length > 0 ? (
        <ul className="divide-y divide-line">
          {data.map((a) => (
            <li key={a.id} className="flex items-center justify-between gap-3 px-4 py-3">
              <div className="flex min-w-0 items-center gap-2">
                <Paperclip className="h-4 w-4 shrink-0 text-ink-muted" />
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-ink">{a.fileName}</p>
                  <p className="text-xs text-ink-muted">
                    {fmtSize(a.sizeBytes)} · {fmtDate(a.createdAt)}
                  </p>
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                <button
                  type="button"
                  className="btn-ghost p-1.5"
                  aria-label={`Download ${a.fileName}`}
                  onClick={() => downloadFile(`/api/attachments/${a.id}/download`, a.fileName)}
                >
                  <Download className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  className="btn-ghost p-1.5 text-danger"
                  aria-label={`Remove ${a.fileName}`}
                  disabled={remove.isPending}
                  onClick={() => remove.mutate(a.id)}
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p className="px-4 py-6 text-center text-sm text-ink-muted">No attachments yet.</p>
      )}
    </div>
  );
}
