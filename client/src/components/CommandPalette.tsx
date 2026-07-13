import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ClipboardList,
  CornerDownLeft,
  FileText,
  Package,
  ReceiptText,
  Search,
  ShoppingCart,
  Truck,
  Users,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { api } from '../lib/api';
import { useAuth } from '../auth/AuthContext';
import { NAV } from '../app/nav';

interface SearchHit {
  type:
    | 'product'
    | 'customer'
    | 'supplier'
    | 'salesOrder'
    | 'invoice'
    | 'purchaseOrder'
    | 'supplierInvoice';
  id: string;
  title: string;
  subtitle: string | null;
}

const HIT_META: Record<SearchHit['type'], { label: string; icon: LucideIcon; to: (h: SearchHit) => string }> = {
  product: { label: 'Product', icon: Package, to: () => '/products' },
  customer: { label: 'Customer', icon: Users, to: () => '/customers' },
  supplier: { label: 'Supplier', icon: Truck, to: () => '/suppliers' },
  salesOrder: { label: 'Sales Order', icon: ShoppingCart, to: (h) => `/sales-orders/${h.id}` },
  invoice: { label: 'Invoice', icon: FileText, to: (h) => `/invoices/${h.id}` },
  purchaseOrder: { label: 'Purchase Order', icon: ClipboardList, to: (h) => `/purchase-orders/${h.id}` },
  supplierInvoice: { label: 'Supplier Invoice', icon: ReceiptText, to: (h) => `/supplier-invoices/${h.id}` },
};

interface Item {
  key: string;
  icon: LucideIcon;
  title: string;
  subtitle?: string | null;
  kind: string;
  to: string;
}

function useDebounced(value: string, ms: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return debounced;
}

/**
 * Ctrl/Cmd+K command palette: jump to any page, or search products, customers, suppliers
 * and documents server-side and go straight to the matching record.
 */
export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const [term, setTerm] = useState('');
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  const debounced = useDebounced(term, 200);

  const { data: hits, isFetching } = useQuery({
    queryKey: ['global-search', debounced],
    queryFn: async () =>
      (await api.get<SearchHit[]>('/api/search', { params: { q: debounced } })).data,
    enabled: open && debounced.trim().length >= 2,
    staleTime: 30_000,
  });

  const items = useMemo<Item[]>(() => {
    const q = term.trim().toLowerCase();
    const pages: Item[] = NAV.flatMap((s) => s.items)
      .filter((i) => !i.roles || i.roles.some((r) => hasRole(r)))
      .filter((i) => q.length === 0 || i.label.toLowerCase().includes(q))
      .map((i) => ({ key: `page:${i.to}`, icon: i.icon, title: i.label, kind: 'Page', to: i.to }));

    const records: Item[] = (q.length >= 2 ? (hits ?? []) : []).map((h) => {
      const meta = HIT_META[h.type];
      return {
        key: `${h.type}:${h.id}`,
        icon: meta.icon,
        title: h.title,
        subtitle: h.subtitle,
        kind: meta.label,
        to: meta.to(h),
      };
    });

    return [...records, ...pages];
  }, [term, hits, hasRole]);

  // Reset state each time the palette opens, and focus the input.
  useEffect(() => {
    if (open) {
      setTerm('');
      setActive(0);
      // Focus after the element renders.
      setTimeout(() => inputRef.current?.focus(), 0);
    }
  }, [open]);

  useEffect(() => setActive(0), [items.length, debounced]);

  // Keep the active row visible while arrowing through the list.
  useEffect(() => {
    listRef.current
      ?.querySelector(`[data-index="${active}"]`)
      ?.scrollIntoView({ block: 'nearest' });
  }, [active]);

  if (!open) return null;

  function go(item: Item) {
    onClose();
    navigate(item.to);
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => Math.min(a + 1, items.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => Math.max(a - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const item = items[active];
      if (item) go(item);
    } else if (e.key === 'Escape') {
      onClose();
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-ink/30 p-4 pt-[12vh]">
      <button type="button" aria-label="Close search" className="absolute inset-0 cursor-default" onClick={onClose} />
      <div
        // oxlint-disable-next-line jsx-a11y/prefer-tag-over-role
        role="dialog"
        aria-modal="true"
        aria-label="Search"
        className="relative w-full max-w-xl overflow-hidden rounded-lg border border-line bg-surface shadow-raised"
      >
        <div className="flex items-center gap-3 border-b border-line px-4">
          <Search className="h-4 w-4 shrink-0 text-ink-muted" />
          <input
            ref={inputRef}
            value={term}
            onChange={(e) => setTerm(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder="Search products, customers, orders, invoices… or jump to a page"
            aria-label="Search"
            className="w-full bg-transparent py-3.5 text-sm text-ink outline-none placeholder:text-ink-muted"
          />
          {isFetching && (
            <span className="text-xs text-ink-muted" aria-live="polite">
              Searching…
            </span>
          )}
        </div>
        <ul ref={listRef} className="max-h-[50vh] overflow-y-auto p-2">
          {items.length === 0 ? (
            <li className="px-3 py-6 text-center text-sm text-ink-muted">
              {term.trim().length >= 2 ? 'No matches.' : 'Type to search.'}
            </li>
          ) : (
            items.map((item, i) => (
              <li key={item.key}>
                <button
                  type="button"
                  data-index={i}
                  onClick={() => go(item)}
                  onMouseEnter={() => setActive(i)}
                  className={`flex w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm ${
                    i === active ? 'bg-lagoon-50 text-lagoon-700' : 'text-ink-soft'
                  }`}
                >
                  <item.icon className="h-4 w-4 shrink-0" />
                  <span className="min-w-0 flex-1 truncate font-medium">{item.title}</span>
                  {item.subtitle && <span className="truncate text-xs text-ink-muted">{item.subtitle}</span>}
                  <span className="shrink-0 rounded border border-line px-1.5 py-0.5 text-[10px] uppercase tracking-wide text-ink-muted">
                    {item.kind}
                  </span>
                  {i === active && <CornerDownLeft className="h-3.5 w-3.5 shrink-0 text-ink-muted" />}
                </button>
              </li>
            ))
          )}
        </ul>
      </div>
    </div>
  );
}
