import type { ReactNode } from 'react';
import { X } from 'lucide-react';

export function Modal({
  open,
  onClose,
  title,
  children,
  footer,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-ink/40 p-4 backdrop-blur-sm sm:p-8">
      {/* Native <dialog> only gets ARIA modality (backdrop, focus trap) via the imperative
          showModal() API; used declaratively via the `open` attribute it behaves as a
          non-modal dialog, which would be a regression from the current div + role="dialog"
          + aria-modal="true", which already communicates modality correctly to assistive tech. */}
      <div
        className="mt-4 w-full max-w-lg animate-scale-in overflow-hidden rounded-2xl border border-line bg-surface shadow-lift sm:mt-12"
        // oxlint-disable-next-line jsx-a11y/prefer-tag-over-role
        role="dialog"
        aria-modal="true"
      >
        <div className="flex items-center justify-between border-b border-line bg-lagoon-50/40 px-5 py-4">
          <h2 className="font-display text-lg font-semibold text-ink">{title}</h2>
          <button onClick={onClose} className="btn-ghost -mr-2 p-1.5" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-line bg-canvas/40 px-5 py-4">{footer}</div>}
      </div>
    </div>
  );
}
