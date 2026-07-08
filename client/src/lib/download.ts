import { api } from './api';

/**
 * Downloads an authenticated file endpoint. A plain <a href> can't attach the bearer
 * token, so fetch it as a blob through the API client and trigger the save.
 */
export async function downloadFile(url: string, fallbackName: string) {
  const response = await api.get<Blob>(url, { responseType: 'blob' });

  // Prefer the server's filename from Content-Disposition.
  const disposition = response.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const filename = match?.[1] ?? fallbackName;

  const objectUrl = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(objectUrl);
}
