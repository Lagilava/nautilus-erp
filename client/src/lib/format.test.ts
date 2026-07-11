import { describe, it, expect } from 'vitest';
import { fmtMoney, fmtNumber, fmtDate } from './format';

describe('fmtMoney', () => {
  it('formats a whole number with two decimal places and the FJD symbol', () => {
    expect(fmtMoney(100)).toBe('$100.00');
  });

  it('rounds to two decimal places', () => {
    expect(fmtMoney(19.999)).toBe('$20.00');
  });

  it('formats zero', () => {
    expect(fmtMoney(0)).toBe('$0.00');
  });

  it('formats negative amounts (e.g. a credit/refund)', () => {
    expect(fmtMoney(-45.5)).toBe('-$45.50');
  });

  it('groups thousands', () => {
    expect(fmtMoney(1234567.89)).toBe('$1,234,567.89');
  });
});

describe('fmtNumber', () => {
  it('groups thousands with no decimal places for a whole number', () => {
    expect(fmtNumber(1234567)).toBe('1,234,567');
  });

  it('formats a small integer', () => {
    expect(fmtNumber(7)).toBe('7');
  });
});

describe('fmtDate', () => {
  it('formats an ISO date string as day month year', () => {
    expect(fmtDate('2026-07-02T00:00:00Z')).toMatch(/2 Jul|Jul 2/);
  });

  it('formats a date-only ISO string', () => {
    // en-FJ renders as "2 Jul 2026" — assert the year and month name land somewhere,
    // rather than pinning the exact token order across Intl implementations.
    const formatted = fmtDate('2026-01-15');
    expect(formatted).toContain('2026');
    expect(formatted).toMatch(/Jan/);
  });
});
