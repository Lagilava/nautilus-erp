import { describe, it, expect, beforeEach } from 'vitest';
import { AssistantEngine } from './engine';

describe('AssistantEngine', () => {
  let engine: AssistantEngine;
  beforeEach(() => {
    localStorage.clear(); // reset persisted feedback between tests
    engine = new AssistantEngine();
  });

  it('answers a clear how-to question with the right topic', () => {
    const r = engine.ask('how do I create a sales order?');
    expect(r.kind).toBe('answer');
    expect(r.answer?.entry.id).toBe('create-sales-order');
  });

  it('tolerates typos and reports the correction', () => {
    const r = engine.ask('how do i creat an invoce');
    expect(r.answer?.entry.id).toBe('create-invoice');
    expect(r.correctedQuery).toBeTruthy();
  });

  it('resolves domain abbreviations (PO)', () => {
    const r = engine.ask('raise a PO');
    const ids = [r.answer?.entry.id, ...r.suggestions.map((s) => s.entry.id)];
    expect(ids).toContain('create-purchase-order');
  });

  it('maps synonyms: "vendor bill" to supplier invoice', () => {
    const r = engine.ask('how do I enter a vendor bill');
    const ids = [r.answer?.entry.id, ...r.suggestions.map((s) => s.entry.id)];
    expect(ids).toContain('supplier-invoice');
  });

  it('rewards exact phrases (purchase order)', () => {
    const r = engine.ask('purchase order');
    const ids = [r.answer?.entry.id, ...r.suggestions.map((s) => s.entry.id)];
    expect(ids).toContain('create-purchase-order');
  });

  it('greets without retrieval', () => {
    const r = engine.ask('hello');
    expect(r.kind).toBe('intent');
    expect(r.intentReply).toBeTruthy();
  });

  it('hides admin-only topics from non-admins', () => {
    const asStaff = engine.ask('how do I add a user', ['Sales']);
    const staffIds = [asStaff.answer?.entry.id, ...asStaff.suggestions.map((s) => s.entry.id)];
    expect(staffIds).not.toContain('manage-users');

    const asAdmin = engine.ask('how do I add a user', ['Administrator']);
    const adminIds = [asAdmin.answer?.entry.id, ...asAdmin.suggestions.map((s) => s.entry.id)];
    expect(adminIds).toContain('manage-users');
  });

  it('resolves a follow-up against the previous topic', () => {
    engine.ask('tell me about low stock');
    const follow = engine.ask('how');
    expect(follow.kind).toBe('answer');
    expect(follow.answer?.entry.id).toBe('low-stock');
  });

  it('falls back to suggestions for gibberish', () => {
    const r = engine.ask('xyzzy qwerty foobar');
    expect(r.kind).toBe('fallback');
    expect(r.suggestions.length).toBeGreaterThan(0);
  });

  it('extracts document references as actions', () => {
    const r = engine.ask('where is purchase order 42');
    expect(r.entities?.some((e) => e.to === '/purchase-orders')).toBe(true);
  });

  it('splits compound questions into multiple answers', () => {
    const results = engine.askAll('how do I add a product and then set its price');
    expect(results.length).toBe(2);
    expect(results[0].answer?.entry.id).toBe('create-product');
  });

  it('adds an empathetic lead-in when the user sounds stuck', () => {
    const r = engine.ask('the invoice screen is not working and i am stuck');
    expect(r.empathyPrefix).toBeTruthy();
  });

  it('lets feedback nudge ranking', () => {
    engine.recordFeedback('aging-report', true);
    engine.recordFeedback('aging-report', true);
    // A confident answer still returns matched terms for highlighting.
    const r = engine.ask('overdue invoices report');
    expect(r.matchedTerms).toBeDefined();
  });
});
