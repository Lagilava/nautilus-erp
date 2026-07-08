export function BrandMark({ className = 'h-8 w-8' }: { className?: string }) {
  return (
    <img src="/nautilus-logo.png" alt="Nautilus ERP" className={className} />
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
