export function BrandMark({ className = 'h-8 w-8' }: { className?: string }) {
  return (
    <img src="/nautilus-logo.png" alt="Nautilus ERP" className={className} />
  );
}

/**
 * The signature ocean-wave motif from the Nautilus mark, as a decorative SVG
 * overlay for gradient headers and hero panels. Purely presentational.
 */
export function WaveMotif({ className = '' }: { className?: string }) {
  return (
    <svg
      className={`pointer-events-none absolute inset-x-0 bottom-0 w-full ${className}`}
      viewBox="0 0 400 120"
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <path d="M0 60 C 60 20, 120 100, 200 60 S 340 20, 400 60 V120 H0 Z" fill="currentColor" opacity="0.5" />
      <path d="M0 80 C 80 40, 140 110, 220 80 S 360 50, 400 80 V120 H0 Z" fill="currentColor" opacity="0.35" />
    </svg>
  );
}

export function Wordmark() {
  return (
    <div className="flex items-center gap-2.5">
      <BrandMark />
      <div className="leading-none">
        <div className="font-display text-lg font-semibold text-ink">Nautilus</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.2em] text-ink-muted">ERP · Fiji</div>
      </div>
    </div>
  );
}
