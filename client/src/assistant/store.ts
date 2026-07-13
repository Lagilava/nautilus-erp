/**
 * Local persistence for the offline assistant. Everything lives in
 * localStorage — no server, no account state — so the assistant "learns" a
 * little from each user's own feedback and remembers the conversation across
 * reloads. All access is guarded so it degrades silently where storage is
 * unavailable (private mode, tests without a DOM).
 */

const FEEDBACK_KEY = 'nautilus.assistant.feedback.v1';
const UNANSWERED_KEY = 'nautilus.assistant.unanswered.v1';
const CONVO_KEY = 'nautilus.assistant.conversation.v1';

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function write(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable — feature simply doesn't persist */
  }
}

// ── Feedback priors ──────────────────────────────────────────────────────────
// A per-topic running score nudged by thumbs up/down. It feeds a small ranking
// prior so topics this user finds helpful surface a touch more readily.

export type FeedbackMap = Record<string, number>;

export function loadFeedback(): FeedbackMap {
  return read<FeedbackMap>(FEEDBACK_KEY, {});
}

export function recordFeedback(topicId: string, helpful: boolean): FeedbackMap {
  const map = loadFeedback();
  const delta = helpful ? 1 : -1;
  // Clamp so a single topic can never dominate ranking.
  map[topicId] = Math.max(-3, Math.min(3, (map[topicId] ?? 0) + delta));
  write(FEEDBACK_KEY, map);
  return map;
}

// ── Unanswered questions ─────────────────────────────────────────────────────
// Questions that fell through to a fallback are logged so gaps in the knowledge
// base can be reviewed and filled later.

export interface UnansweredEntry {
  q: string;
  at: number;
}

export function logUnanswered(query: string): void {
  const log = read<UnansweredEntry[]>(UNANSWERED_KEY, []);
  log.push({ q: query, at: Date.now() });
  // Keep only the most recent 100 to bound storage.
  write(UNANSWERED_KEY, log.slice(-100));
}

export function loadUnanswered(): UnansweredEntry[] {
  return read<UnansweredEntry[]>(UNANSWERED_KEY, []);
}

// ── Conversation persistence ─────────────────────────────────────────────────
// The widget serialises its message thread here so a page reload doesn't lose
// the exchange. Shape is owned by the widget; stored opaquely.

export function loadConversation<T>(): T | null {
  return read<T | null>(CONVO_KEY, null);
}

export function saveConversation(value: unknown): void {
  write(CONVO_KEY, value);
}

export function clearConversation(): void {
  try {
    localStorage.removeItem(CONVO_KEY);
  } catch {
    /* ignore */
  }
}
