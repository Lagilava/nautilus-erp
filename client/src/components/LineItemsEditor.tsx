import { Plus, Trash2 } from 'lucide-react';
import type { Product } from '../lib/types';
import { fmtMoney } from '../lib/format';

export interface LineDraft {
  productId: string;
  quantity: number;
  price: number; // unit price (sales) or unit cost (purchase)
}

export function LineItemsEditor({
  products,
  priceLabel,
  lines,
  onChange,
}: {
  products: Product[];
  priceLabel: string;
  lines: LineDraft[];
  onChange: (lines: LineDraft[]) => void;
}) {
  const update = (i: number, patch: Partial<LineDraft>) =>
    onChange(lines.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  const add = () => onChange([...lines, { productId: '', quantity: 1, price: 0 }]);
  const remove = (i: number) => onChange(lines.filter((_, idx) => idx !== i));

  const total = lines.reduce((sum, l) => sum + l.quantity * l.price, 0);

  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <label className="field-label mb-0">Line items</label>
        <button type="button" className="btn-ghost px-2 py-1 text-xs" onClick={add}>
          <Plus className="h-3.5 w-3.5" /> Add line
        </button>
      </div>

      <div className="space-y-2">
        {lines.length === 0 && (
          <p className="rounded-md border border-dashed border-line px-3 py-4 text-center text-sm text-ink-muted">
            No lines yet — add one to begin.
          </p>
        )}
        {lines.map((line, i) => (
          <div key={i} className="flex items-start gap-2">
            <select
              className="input flex-1"
              value={line.productId}
              onChange={(e) => {
                const product = products.find((p) => p.id === e.target.value);
                update(i, {
                  productId: e.target.value,
                  // Prefill the price from the product when first chosen.
                  price: product ? (priceLabel.includes('cost') ? product.costPrice : product.sellingPrice) : line.price,
                });
              }}
            >
              <option value="">Select product…</option>
              {products.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.sku} — {p.name}
                </option>
              ))}
            </select>
            <input
              type="number"
              step="1"
              min="0"
              className="input w-20"
              aria-label="Quantity"
              value={line.quantity}
              onChange={(e) => update(i, { quantity: Number(e.target.value) })}
            />
            <input
              type="number"
              step="0.01"
              min="0"
              className="input w-28"
              aria-label={priceLabel}
              value={line.price}
              onChange={(e) => update(i, { price: Number(e.target.value) })}
            />
            <button type="button" className="btn-ghost p-2 text-danger" onClick={() => remove(i)} aria-label="Remove line">
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>

      <div className="mt-3 flex items-center justify-between border-t border-line pt-3">
        <span className="text-sm text-ink-muted">Subtotal ({priceLabel})</span>
        <span className="tabular font-semibold text-ink">{fmtMoney(total)}</span>
      </div>
    </div>
  );
}
