/**
 * A small, dependency-free natural-language toolkit that powers the offline
 * assistant. No network, no LLM — just classic information-retrieval techniques:
 * normalisation, tokenisation, a light stemmer, domain synonym expansion, and
 * fuzzy string similarity for typo tolerance.
 *
 * Everything here is deterministic and runs entirely in the browser.
 */

/** Words that carry little retrieval signal and are dropped before scoring. */
export const STOP_WORDS = new Set([
  'a', 'an', 'the', 'and', 'or', 'but', 'if', 'then', 'else', 'of', 'to', 'in',
  'on', 'at', 'for', 'with', 'as', 'by', 'is', 'are', 'was', 'were', 'be', 'been',
  'being', 'do', 'does', 'did', 'doing', 'have', 'has', 'had', 'i', 'you', 'we',
  'they', 'it', 'this', 'that', 'these', 'those', 'my', 'our', 'your', 'me', 'us',
  'so', 'up', 'out', 'about', 'into', 'over', 'again', 'here', 'there', 'all',
  'any', 'both', 'each', 'more', 'most', 'some', 'such', 'no', 'nor', 'not', 'only',
  'own', 'same', 'than', 'too', 'very', 'can', 'will', 'just', 'from', 'am', 'please',
]);

/**
 * Contractions expanded before tokenising so "how's" / "won't" don't fragment.
 */
const CONTRACTIONS: Record<string, string> = {
  "how's": 'how is', "what's": 'what is', "where's": 'where is', "who's": 'who is',
  "it's": 'it is', "that's": 'that is', "there's": 'there is', "i'm": 'i am',
  "i've": 'i have', "i'd": 'i would', "i'll": 'i will', "can't": 'can not',
  "won't": 'will not', "don't": 'do not', "doesn't": 'does not', "didn't": 'did not',
  "isn't": 'is not', "aren't": 'are not', "wasn't": 'was not', "shouldn't": 'should not',
  "couldn't": 'could not', "wouldn't": 'would not', "you're": 'you are', "we're": 'we are',
  "let's": 'let us',
};

/**
 * Domain synonyms and abbreviations. Each key is normalised to the canonical
 * term(s) on the right so "po", "vendor bill" and "purchase order" all collapse
 * to the same signal. Multi-word keys are matched before single tokens.
 */
export const SYNONYMS: Record<string, string> = {
  po: 'purchase order', pos: 'purchase orders',
  so: 'sales order', sos: 'sales orders',
  inv: 'invoice', invs: 'invoices',
  qty: 'quantity', amt: 'amount', num: 'number', no: 'number',
  ar: 'accounts receivable', ap: 'accounts payable',
  mfa: 'multi factor authentication', '2fa': 'multi factor authentication',
  otp: 'verification code', pwd: 'password',
  client: 'customer', clients: 'customers', buyer: 'customer',
  vendor: 'supplier', vendors: 'suppliers', seller: 'supplier',
  bill: 'invoice', bills: 'invoices',
  stock: 'inventory', 'stock level': 'inventory quantity',
  item: 'product', items: 'products', sku: 'product',
  reorder: 're order restock', restock: 'reorder restock',
  fulfil: 'fulfill', fulfilment: 'fulfillment',
  sign: 'log', login: 'log in sign in', logout: 'log out sign out', signin: 'sign in',
  signout: 'sign out', signup: 'sign up',
  create: 'create add new make', add: 'add create new', make: 'make create',
  remove: 'delete remove', edit: 'edit change update modify',
  pay: 'payment pay', paid: 'payment paid',
  report: 'report insights', reports: 'reports insights',
  admin: 'administrator admin', user: 'user account', users: 'users accounts',
  role: 'role permission', roles: 'roles permissions',
  chart: 'chart graph trend', graph: 'chart graph',
  find: 'find search look', search: 'search find lookup',
  aging: 'aging ageing overdue', ageing: 'aging ageing overdue',
};

/** Lowercase, expand contractions, and strip accents/punctuation to spaces. */
export function normalize(text: string): string {
  let t = text.toLowerCase().trim();
  for (const [k, v] of Object.entries(CONTRACTIONS)) {
    t = t.split(k).join(v);
  }
  // Strip diacritics, then replace anything that isn't a letter/number with a space.
  t = t.normalize('NFD').replace(/[̀-ͯ]/g, '');
  t = t.replace(/[^a-z0-9\s]/g, ' ');
  return t.replace(/\s+/g, ' ').trim();
}

/** Split normalised text into raw tokens (no stemming/stopword removal). */
export function tokenizeRaw(text: string): string[] {
  const n = normalize(text);
  return n.length === 0 ? [] : n.split(' ');
}

/**
 * A compact Porter-style stemmer: strips the handful of English suffixes that
 * cause the most vocabulary fragmentation (plurals, -ing, -ed, -ment …). It is
 * intentionally conservative — over-stemming hurts precision.
 */
export function stem(word: string): string {
  let w = word;
  if (w.length <= 3) return w;
  // Plurals and third-person singular.
  if (w.endsWith('ies') && w.length > 4) w = w.slice(0, -3) + 'y';
  else if (w.endsWith('sses')) w = w.slice(0, -2);
  else if (w.endsWith('ss')) { /* keep */ }
  else if (w.endsWith('s') && !w.endsWith('us') && !w.endsWith('ss')) w = w.slice(0, -1);
  // Common verb/noun suffixes.
  const suffixes = ['ingly', 'edly', 'ing', 'edly', 'ed', 'ly', 'ment', 'ness', 'tion', 'sion', 'ation'];
  for (const suf of suffixes) {
    if (w.length > suf.length + 2 && w.endsWith(suf)) {
      w = w.slice(0, -suf.length);
      break;
    }
  }
  // Restore a trailing 'e' that many stems read better with is skipped for simplicity.
  return w;
}

/**
 * Full pipeline: normalise → expand synonyms → tokenise → drop stopwords → stem.
 * Returns the list of processed terms used for indexing and querying.
 */
export function analyze(text: string): string[] {
  let n = normalize(text);
  // Expand multi-word synonym keys first, then single tokens.
  for (const [k, v] of Object.entries(SYNONYMS)) {
    if (k.includes(' ') && n.includes(k)) n = n.split(k).join(v);
  }
  const out: string[] = [];
  for (const tok of n.split(' ')) {
    if (!tok) continue;
    const expanded = SYNONYMS[tok] ?? tok;
    for (const part of expanded.split(' ')) {
      if (STOP_WORDS.has(part) || part.length < 2) continue;
      out.push(stem(part));
    }
  }
  return out;
}

/** Adjacent-token bigrams, e.g. ["purchase","order","stock"] → ["purchase order","order stock"]. */
export function bigrams(terms: string[]): string[] {
  const out: string[] = [];
  for (let i = 0; i < terms.length - 1; i++) out.push(`${terms[i]} ${terms[i + 1]}`);
  return out;
}

/**
 * Correct a query token to the closest term in a known vocabulary when it is an
 * obvious near-miss. Returns the original token when nothing is close enough.
 * The edit-distance budget scales with word length so short words aren't
 * over-corrected.
 */
export function correctToken(token: string, vocab: Set<string>): string {
  if (token.length < 4 || vocab.has(token)) return token;
  const budget = token.length >= 8 ? 2 : 1;
  let best = token;
  let bestDist = budget + 1;
  for (const candidate of vocab) {
    if (Math.abs(candidate.length - token.length) > budget) continue;
    const d = levenshtein(token, candidate);
    if (d < bestDist) {
      bestDist = d;
      best = candidate;
      if (d === 1) break;
    }
  }
  return bestDist <= budget ? best : token;
}

/** Levenshtein edit distance (iterative, single-row). */
export function levenshtein(a: string, b: string): number {
  if (a === b) return 0;
  if (a.length === 0) return b.length;
  if (b.length === 0) return a.length;
  let prev = Array.from({ length: b.length + 1 }, (_, i) => i);
  for (let i = 1; i <= a.length; i++) {
    let cur = [i, ...new Array(b.length).fill(0)];
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      cur[j] = Math.min(cur[j - 1] + 1, prev[j] + 1, prev[j - 1] + cost);
    }
    prev = cur;
  }
  return prev[b.length];
}

/**
 * Similarity in [0,1] derived from edit distance, normalised by the longer word.
 * Used to forgive typos ("invoce" ≈ "invoice") when matching query terms to the
 * indexed vocabulary.
 */
export function fuzzyRatio(a: string, b: string): number {
  if (!a.length && !b.length) return 1;
  const dist = levenshtein(a, b);
  return 1 - dist / Math.max(a.length, b.length);
}
