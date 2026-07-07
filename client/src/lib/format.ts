// FJD is the base currency (see Fiji localization). Formatting stays locale-stable.
const money = new Intl.NumberFormat('en-FJ', {
  style: 'currency',
  currency: 'FJD',
  minimumFractionDigits: 2,
});

const number = new Intl.NumberFormat('en-FJ');

export const fmtMoney = (value: number) => money.format(value);
export const fmtNumber = (value: number) => number.format(value);
export const fmtDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en-FJ', { year: 'numeric', month: 'short', day: 'numeric' });
