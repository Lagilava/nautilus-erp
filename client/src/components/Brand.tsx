// A restrained wordmark with a wave/horizon glyph — the "open, natural" Fiji note,
// executed with a precise geometric line rather than anything ornamental.
export function BrandMark({ className = 'h-8 w-8' }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" className={className} role="img" aria-label="Nautilus ERP">
      <rect width="32" height="32" rx="7" className="fill-lagoon-500" />
      <path
        d="M6 20c2.4 0 2.4-3 4.8-3s2.4 3 4.8 3 2.4-3 4.8-3 2.4 3 4.8 3"
        fill="none"
        stroke="white"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
      <circle cx="16" cy="11.5" r="2.2" className="fill-sand-300" />
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
