/**
 * The offline assistant engine — a compact but genuinely capable information-
 * retrieval stack, with no language model and no network.
 *
 * Pipeline, per query:
 *   1. Intent detection      — greetings / thanks / capability questions.
 *   2. Sentiment & urgency    — an empathetic lead-in when a user sounds stuck.
 *   3. Entity extraction      — document references (SO-1001, PO 42, INV 7) → actions.
 *   4. Spell correction       — unknown terms snapped to the indexed vocabulary.
 *   5. Ranking (BM25)         — Okapi BM25 over a weighted field index, with
 *                               bigram phrase boosts, fuzzy (typo) bonuses and a
 *                               per-user feedback prior learned from thumbs up/down.
 *   6. Confidence gating      — answer directly, offer clarifications, or fall back.
 *
 * Multi-part questions ("do X and then Y") are split and answered in turn.
 */

import { analyze, bigrams, correctToken, fuzzyRatio, normalize } from './nlp';
import { KNOWLEDGE, KB_BY_ID, type KbEntry, type PageContext } from './knowledge';
import { loadFeedback, logUnanswered, recordFeedback, type FeedbackMap } from './store';

// ── Field weights ─────────────────────────────────────────────────────────
// The same term counts for more when it appears in a curated utterance, title
// or keyword than deep in prose.
const UTTERANCE_WEIGHT = 3;
const TITLE_WEIGHT = 3;
const KEYWORD_WEIGHT = 2;
const ANSWER_WEIGHT = 1;

// BM25 parameters (Okapi defaults).
const K1 = 1.5;
const B = 0.75;

interface IndexedDoc {
  entry: KbEntry;
  /** Term → weighted term frequency within this document. */
  tf: Map<string, number>;
  /** Weighted document length (∑ tf), for BM25 length normalisation. */
  length: number;
  /** Surface unigram vocabulary, for fuzzy fallback matching. */
  vocab: Set<string>;
  /** Bigram phrases present in the document. */
  phrases: Set<string>;
}

export interface Answer {
  entry: KbEntry;
  /** Confidence in [0,1]. */
  score: number;
}

/** An actionable reference the assistant spotted in the message. */
export interface EntityAction {
  label: string;
  to: string;
}

export interface EngineResult {
  kind: 'answer' | 'suggest' | 'intent' | 'fallback';
  answer?: Answer;
  suggestions: Answer[];
  intentReply?: string;
  /** Set when the query was auto-corrected, e.g. "invoce" → "invoice". */
  correctedQuery?: string;
  /** Query terms that drove the match, for highlighting in the UI. */
  matchedTerms?: string[];
  /** Document references detected in the message (deep-link actions). */
  entities?: EntityAction[];
  /** An empathetic lead-in when the user sounds stuck or frustrated. */
  empathyPrefix?: string;
}

/** Terms we treat as a request to open/navigate rather than to explain. */
const NAV_VERBS = ['open', 'go', 'goto', 'take', 'show', 'navigate', 'jump', 'bring'];

/** Connectors that separate a compound question into parts. */
const SPLIT_RE = /\s+(?:and then|then|after that|also|as well as|and also)\s+/gi;

/** Document-reference patterns → the list screen that surfaces them. */
const ENTITY_PATTERNS: { re: RegExp; label: (n: string) => string; to: string }[] = [
  { re: /\b(?:so|sales\s*order)[-\s#]*(\d{1,6})\b/i, label: (n) => `Find sales order #${n}`, to: '/sales-orders' },
  { re: /\b(?:po|purchase\s*order)[-\s#]*(\d{1,6})\b/i, label: (n) => `Find purchase order #${n}`, to: '/purchase-orders' },
  { re: /\b(?:inv|invoice)[-\s#]*(\d{1,6})\b/i, label: (n) => `Find invoice #${n}`, to: '/invoices' },
];

/** Words that signal the user is stuck or frustrated. */
const FRUSTRATION = [
  'not working', 'doesnt work', 'does not work', 'wont work', 'broken', 'stuck',
  'frustrated', 'confused', 'confusing', 'cant figure', 'can not figure', 'help me',
  'no idea', 'lost', 'annoying', 'error', 'keeps failing',
];
const URGENCY = ['urgent', 'asap', 'immediately', 'right now', 'quickly', 'emergency'];

export class AssistantEngine {
  private docs: IndexedDoc[] = [];
  private idf = new Map<string, number>();
  private avgdl = 1;
  private vocabulary = new Set<string>();
  private lastTopicId: string | null = null;
  private feedback: FeedbackMap;
  private knowledge: KbEntry[];

  constructor(knowledge: KbEntry[] = KNOWLEDGE) {
    this.knowledge = knowledge;
    this.feedback = loadFeedback();
    this.build();
  }

  /** Build the BM25 index once at construction. */
  private build() {
    const docFreq = new Map<string, number>();

    for (const entry of this.knowledge) {
      const tf = new Map<string, number>();
      const phrases = new Set<string>();
      const add = (text: string, weight: number) => {
        const terms = analyze(text);
        for (const term of terms) tf.set(term, (tf.get(term) ?? 0) + weight);
        for (const bg of bigrams(terms)) phrases.add(bg);
      };
      add(entry.title, TITLE_WEIGHT);
      for (const u of entry.utterances) add(u, UTTERANCE_WEIGHT);
      for (const k of entry.keywords) add(k, KEYWORD_WEIGHT);
      add(entry.answer, ANSWER_WEIGHT);
      for (const s of entry.steps ?? []) add(s, ANSWER_WEIGHT);

      const vocab = new Set(tf.keys());
      for (const term of vocab) {
        docFreq.set(term, (docFreq.get(term) ?? 0) + 1);
        this.vocabulary.add(term);
      }
      let length = 0;
      for (const w of tf.values()) length += w;
      this.docs.push({ entry, tf, length, vocab, phrases });
    }

    const N = this.docs.length;
    for (const [term, df] of docFreq) {
      // BM25 idf with the usual +0.5 smoothing.
      this.idf.set(term, Math.log(1 + (N - df + 0.5) / (df + 0.5)));
    }
    this.avgdl = this.docs.reduce((s, d) => s + d.length, 0) / (N || 1);
  }

  /** Reset the follow-up context (e.g. when the panel is cleared). */
  resetContext() {
    this.lastTopicId = null;
  }

  /** Record a thumbs up/down on a topic and update the in-memory prior. */
  recordFeedback(topicId: string, helpful: boolean) {
    this.feedback = recordFeedback(topicId, helpful);
  }

  /**
   * Answer a possibly-compound message: split on connectors and resolve each
   * part, so "how do I add a product and then set its price" yields two answers.
   */
  askAll(message: string, allowedRoles?: string[], pageContext?: PageContext | null): EngineResult[] {
    const parts = this.splitParts(message);
    if (parts.length <= 1) return [this.ask(message, allowedRoles, pageContext)];
    return parts.map((p) => this.ask(p, allowedRoles, pageContext));
  }

  private splitParts(message: string): string[] {
    const raw = message.split(SPLIT_RE).map((p) => p.trim()).filter(Boolean);
    // Only treat as multi-part when each side is a substantial clause.
    if (raw.length >= 2 && raw.every((p) => p.split(/\s+/).length >= 3)) {
      return raw.slice(0, 2);
    }
    return [message];
  }

  /** Main entry point: interpret a single message and return the best response. */
  ask(message: string, allowedRoles?: string[], pageContext?: PageContext | null): EngineResult {
    const normalized = normalize(message);

    // 1) Non-informational intents.
    const intent = this.detectIntent(normalized);
    if (intent) return { kind: 'intent', suggestions: [], intentReply: intent };

    // 2) Entities and empathy travel alongside whatever answer we produce.
    const entities = this.extractEntities(message);
    const empathyPrefix = this.empathy(normalized);

    // 3) Follow-up: a terse continuation re-opens the previous topic.
    if (this.isFollowUp(normalized) && this.lastTopicId) {
      const entry = KB_BY_ID[this.lastTopicId];
      if (entry) {
        return this.decorate(
          { kind: 'answer', answer: { entry, score: 1 }, suggestions: this.relatedOf(entry, allowedRoles) },
          entities,
          empathyPrefix,
        );
      }
    }

    // 4) Analyse + spell-correct the query against the indexed vocabulary.
    const rawTerms = analyze(message);
    const corrected = rawTerms.map((t) => correctToken(t, this.vocabulary));
    const didCorrect = corrected.some((t, i) => t !== rawTerms[i]);
    const queryTerms = corrected;

    if (queryTerms.length === 0) {
      // Nothing to rank — but if we found an entity, that alone is useful.
      const base: EngineResult = { kind: 'fallback', suggestions: this.topPicks(allowedRoles, pageContext) };
      return this.decorate(base, entities, empathyPrefix);
    }

    // 5) Rank with BM25 (+ phrase, fuzzy, feedback and page-context signals).
    const scored = this.rank(queryTerms, allowedRoles, pageContext);
    const isNav = NAV_VERBS.some((v) => normalized.split(' ').includes(v));

    const best = scored[0];
    if (!best || best.raw < 0.6) {
      logUnanswered(message);
      const base: EngineResult = { kind: 'fallback', suggestions: this.topPicks(allowedRoles, pageContext) };
      return this.decorate(base, entities, empathyPrefix);
    }

    const secondRaw = scored[1]?.raw ?? 0;
    const conf = best.raw / (best.raw + 4); // saturating → 0..1
    const separated = best.raw - secondRaw >= 1.2;
    const confident = conf >= 0.45 || separated || isNav;

    const matchedTerms = this.matchedTerms(queryTerms, best);
    const correctedQuery = didCorrect ? corrected.join(' ') : undefined;

    if (confident) {
      this.lastTopicId = best.entry.id;
      return this.decorate(
        {
          kind: 'answer',
          answer: this.display(best),
          suggestions: this.relatedOf(best.entry, allowedRoles),
          correctedQuery,
          matchedTerms,
        },
        entities,
        empathyPrefix,
      );
    }

    const runnersUp = scored.slice(0, 4).filter((s) => s.raw > 0.4).map((s) => this.display(s));
    return this.decorate(
      { kind: 'suggest', answer: this.display(best), suggestions: runnersUp, correctedQuery, matchedTerms },
      entities,
      empathyPrefix,
    );
  }

  private decorate(result: EngineResult, entities: EntityAction[], empathyPrefix: string | null): EngineResult {
    if (entities.length) result.entities = entities;
    if (empathyPrefix) result.empathyPrefix = empathyPrefix;
    return result;
  }

  // ── Ranking ────────────────────────────────────────────────────────────────

  private rank(
    queryTerms: string[],
    allowedRoles?: string[],
    pageContext?: PageContext | null,
  ): { entry: KbEntry; raw: number }[] {
    const qtf = new Map<string, number>();
    for (const t of queryTerms) qtf.set(t, (qtf.get(t) ?? 0) + 1);
    const queryPhrases = new Set(bigrams(queryTerms));

    const results: { entry: KbEntry; raw: number }[] = [];
    for (const doc of this.docs) {
      if (!this.isAllowed(doc.entry, allowedRoles)) continue;

      // Okapi BM25 over shared terms.
      let score = 0;
      for (const [term] of qtf) {
        const f = doc.tf.get(term);
        if (!f) continue;
        const idf = this.idf.get(term) ?? 0;
        const denom = f + K1 * (1 - B + (B * doc.length) / this.avgdl);
        score += idf * ((f * (K1 + 1)) / denom);
      }

      // Phrase boost: reward exact adjacent-term matches ("purchase order").
      for (const p of queryPhrases) if (doc.phrases.has(p)) score += 1.4;

      // Fuzzy bonus for near-miss terms the exact match missed.
      score += this.fuzzyBonus(qtf, doc);

      // Learned prior from this user's thumbs up/down.
      const prior = this.feedback[doc.entry.id];
      if (prior) score += 0.25 * prior;

      // The user's current screen is a strong relevance signal: a topic
      // curated for this page ranks higher, and same-module topics get a
      // smaller nudge, only once the term match already shows some relevance.
      if (pageContext && score > 0) {
        if (pageContext.topicIds.includes(doc.entry.id)) score += 1.1;
        else if (doc.entry.module === pageContext.module) score += 0.4;
      }

      if (score > 0) results.push({ entry: doc.entry, raw: score });
    }

    results.sort((a, b) => b.raw - a.raw);
    return results;
  }

  /** Map an internal raw score to a friendly 0–1 confidence for display. */
  private display(r: { entry: KbEntry; raw: number }): Answer {
    return { entry: r.entry, score: Math.min(1, r.raw / (r.raw + 4) + 0.15) };
  }

  private fuzzyBonus(qtf: Map<string, number>, doc: IndexedDoc): number {
    let bonus = 0;
    const maxTf = Math.max(1, ...doc.tf.values());
    for (const qterm of qtf.keys()) {
      if (doc.vocab.has(qterm)) continue;
      let bestRatio = 0;
      let bestTerm = '';
      for (const dterm of doc.vocab) {
        if (Math.abs(dterm.length - qterm.length) > 2) continue;
        const r = fuzzyRatio(qterm, dterm);
        if (r > bestRatio) {
          bestRatio = r;
          bestTerm = dterm;
        }
        if (bestRatio === 1) break;
      }
      if (bestRatio >= 0.8 && bestTerm) {
        const idf = this.idf.get(bestTerm) ?? 1;
        const salience = (doc.tf.get(bestTerm) ?? 1) / maxTf;
        bonus += 0.6 * bestRatio * idf * (0.5 + 0.5 * salience);
      }
    }
    return bonus;
  }

  /** Query terms that actually appear (exactly) in the winning document. */
  private matchedTerms(queryTerms: string[], best: { entry: KbEntry; raw: number }): string[] {
    const doc = this.docs.find((d) => d.entry.id === best.entry.id);
    if (!doc) return [];
    return [...new Set(queryTerms.filter((t) => doc.vocab.has(t)))];
  }

  // ── Entities, sentiment, intents ─────────────────────────────────────────────

  private extractEntities(message: string): EntityAction[] {
    const found: EntityAction[] = [];
    const seen = new Set<string>();
    for (const { re, label, to } of ENTITY_PATTERNS) {
      const m = message.match(re);
      if (m && m[1]) {
        const key = `${to}:${m[1]}`;
        if (!seen.has(key)) {
          seen.add(key);
          found.push({ label: label(m[1]), to });
        }
      }
    }
    return found;
  }

  private empathy(normalized: string): string | null {
    const urgent = URGENCY.some((w) => normalized.includes(w));
    const stuck = FRUSTRATION.some((w) => normalized.includes(w));
    if (stuck) return 'No worries — let’s sort this out together. Here’s exactly what to do:';
    if (urgent) return 'On it — here’s the quickest path:';
    return null;
  }

  private isAllowed(entry: KbEntry, allowedRoles?: string[]): boolean {
    if (!entry.roles) return true;
    if (!allowedRoles) return false;
    return entry.roles.some((r) => allowedRoles.includes(r));
  }

  private relatedOf(entry: KbEntry, allowedRoles?: string[]): Answer[] {
    return (entry.related ?? [])
      .map((id) => KB_BY_ID[id])
      .filter((e): e is KbEntry => Boolean(e) && this.isAllowed(e, allowedRoles))
      .map((e) => ({ entry: e, score: 1 }));
  }

  private topPicks(allowedRoles?: string[], pageContext?: PageContext | null): Answer[] {
    const pageIds = pageContext?.topicIds ?? [];
    const fallbackIds = ['orientation', 'create-sales-order', 'create-invoice', 'low-stock', 'search'];
    const ids = [...pageIds, ...fallbackIds.filter((id) => !pageIds.includes(id))];
    return ids
      .map((id) => KB_BY_ID[id])
      .filter((e): e is KbEntry => Boolean(e) && this.isAllowed(e, allowedRoles))
      .slice(0, 5)
      .map((e) => ({ entry: e, score: 1 }));
  }

  /** Topics curated for a given screen, filtered to what this user's role can see. */
  topicsForPage(pageContext: PageContext, allowedRoles?: string[]): KbEntry[] {
    return pageContext.topicIds
      .map((id) => KB_BY_ID[id])
      .filter((e): e is KbEntry => Boolean(e) && this.isAllowed(e, allowedRoles));
  }

  private detectIntent(normalized: string): string | null {
    const words = new Set(normalized.split(' '));
    const has = (...w: string[]) => w.some((x) => words.has(x));

    if (normalized.length <= 24 && has('hi', 'hello', 'hey', 'hiya', 'greetings', 'bula')) {
      return 'Bula! I’m the Nautilus assistant. Ask me how to do anything in the system — creating orders and invoices, managing stock, running reports, and more. What would you like to do?';
    }
    if (has('thanks', 'thank', 'thankyou', 'cheers', 'vinaka') && normalized.length <= 30) {
      return 'You’re welcome! Ask me anything else whenever you need a hand. 🙂';
    }
    if (
      (has('what', 'who') && has('you', 'this')) &&
      has('do', 'help', 'are', 'is') &&
      normalized.length <= 60
    ) {
      return 'I’m an offline help assistant built into Nautilus — no internet needed. I can walk you through tasks step by step and take you straight to the right screen. Try: “How do I create a sales order?”, “What’s low on stock?”, or “How do I record a payment?”';
    }
    return null;
  }

  private isFollowUp(normalized: string): boolean {
    const followUps = [
      'how', 'how do i', 'why', 'tell me more', 'more', 'go on', 'continue',
      'next', 'and then', 'then what', 'steps', 'show me', 'ok', 'okay',
      'explain', 'details', 'more info',
    ];
    return followUps.includes(normalized);
  }
}

/** Shared singleton — the index is built once for the whole app. */
export const assistantEngine = new AssistantEngine();
