// Maps domain status strings to the design-system pill tones, in one place.
type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'gold';

const MAP: Record<string, Tone> = {
  // Sales order
  Draft: 'neutral',
  Confirmed: 'gold',
  Fulfilled: 'success',
  Cancelled: 'danger',
  // Invoice
  Issued: 'gold',
  PartiallyPaid: 'warning',
  Paid: 'success',
  Void: 'danger',
  // Purchase order
  PartiallyReceived: 'warning',
  Received: 'success',
  // Supplier invoice
  Approved: 'gold',
  // Fiscal
  NotSubmitted: 'neutral',
  Submitted: 'success',
  Failed: 'danger',
};

export const statusTone = (status: string): Tone => MAP[status] ?? 'neutral';

// Space out PascalCase enum labels, e.g. "PartiallyReceived" -> "Partially Received".
export const humanize = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2');

export const PAYMENT_METHODS = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Card', label: 'Card' },
  { value: 'BankTransfer', label: 'Bank Transfer' },
  { value: 'MobileWallet', label: 'Mobile Wallet' },
  { value: 'Cheque', label: 'Cheque' },
] as const;
