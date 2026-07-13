import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  ArrowRight,
  CornerDownLeft,
  ExternalLink,
  MapPin,
  MessageCircleQuestion,
  Send,
  Sparkles,
  ThumbsDown,
  ThumbsUp,
  Trash2,
  X,
} from 'lucide-react';
import { BrandMark } from '../components/Brand';
import { useAuth } from '../auth/AuthContext';
import { assistantEngine, type Answer, type EngineResult, type EntityAction } from './engine';
import { getPageContext, KB_BY_ID, STARTER_PROMPTS, type KbEntry } from './knowledge';
import { clearConversation, loadConversation, saveConversation } from './store';
import { analyze } from './nlp';

interface Chip {
  label: string;
  entryId?: string;
  text?: string;
}

interface BotContent {
  text: string;
  entry?: KbEntry;
  steps?: string[];
  links?: KbEntry['links'];
  chips: Chip[];
  clarifying?: boolean;
  empathyPrefix?: string;
  correctedQuery?: string;
  entities?: EntityAction[];
  matchedTerms?: string[];
  /** Topic id, present on confident answers so feedback can be attributed. */
  topicId?: string;
}

interface Message {
  id: number;
  role: 'user' | 'bot';
  text?: string;
  bot?: BotContent;
  /** Feedback the user gave on a bot answer. */
  feedback?: 'up' | 'down';
}

let msgId = 0;

/**
 * The floating, fully-offline help assistant. It answers questions about
 * Nautilus using the local BM25 retrieval engine, renders step-by-step guides,
 * highlights the matched terms, learns from thumbs up/down, and can take the
 * user straight to the relevant screen — no LLM, no network.
 */
export function AssistantWidget() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const roles = user?.roles;

  // What screen is the user actually looking at right now? Recomputed on
  // every navigation so the assistant's ranking and suggestions track it.
  const pageContext = useMemo(() => getPageContext(location.pathname), [location.pathname]);
  const pageTopics = useMemo(
    () => (pageContext ? assistantEngine.topicsForPage(pageContext, roles) : []),
    [pageContext, roles],
  );

  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [thinking, setThinking] = useState(false);
  const [messages, setMessages] = useState<Message[]>(() => {
    const saved = loadConversation<Message[]>() ?? [];
    // Keep the id counter ahead of any restored ids so new keys never collide.
    msgId = saved.reduce((max, m) => Math.max(max, m.id + 1), 0);
    return saved;
  });
  const scrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Persist the thread so a reload doesn't lose the exchange.
  useEffect(() => {
    saveConversation(messages);
  }, [messages]);

  const greeting = useMemo<Message>(
    () => ({
      id: -1,
      role: 'bot',
      bot: {
        text: pageContext
          ? `Bula! I’m your Nautilus assistant — built right into the system and fully offline. I can see you’re on the ${pageContext.label} page, so I’ve pulled up a few things people usually ask here. You can also ask me anything else.`
          : 'Bula! I’m your Nautilus assistant — built right into the system and fully offline. Ask me how to do anything, and I’ll walk you through it and take you to the right screen.',
        chips: [],
      },
    }),
    [pageContext],
  );

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, thinking, open]);

  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 50);
  }, [open]);

  const chipsFor = useCallback(
    (entry: KbEntry): Chip[] =>
      (entry.related ?? [])
        .map((id) => KB_BY_ID[id])
        .filter((e): e is KbEntry => Boolean(e) && (!e.roles || e.roles.some((r) => roles?.includes(r))))
        .map((e) => ({ label: e.title, entryId: e.id })),
    [roles],
  );

  const contentForEntry = useCallback(
    (entry: KbEntry): BotContent => ({
      text: entry.answer,
      entry,
      steps: entry.steps,
      links: entry.links,
      chips: chipsFor(entry),
      topicId: entry.id,
    }),
    [chipsFor],
  );

  const buildBotContent = useCallback((result: EngineResult): BotContent => {
    const common = {
      empathyPrefix: result.empathyPrefix,
      correctedQuery: result.correctedQuery,
      entities: result.entities,
      matchedTerms: result.matchedTerms,
    };
    if (result.kind === 'intent') {
      return { text: result.intentReply ?? '', chips: [], ...common };
    }
    if (result.kind === 'answer' && result.answer) {
      const { entry } = result.answer;
      return {
        text: entry.answer,
        entry,
        steps: entry.steps,
        links: entry.links,
        chips: result.suggestions.map((s) => ({ label: s.entry.title, entryId: s.entry.id })),
        topicId: entry.id,
        ...common,
      };
    }
    if (result.kind === 'suggest') {
      return {
        text: 'I found a few things that might help — which one did you mean?',
        clarifying: true,
        chips: result.suggestions.map((s: Answer) => ({ label: s.entry.title, entryId: s.entry.id })),
        ...common,
      };
    }
    return {
      text:
        'I’m not quite sure about that one. I can help with products, inventory, customers, sales, purchasing, reports and your account. Try one of these:',
      clarifying: true,
      chips: result.suggestions.map((s) => ({ label: s.entry.title, entryId: s.entry.id })),
      ...common,
    };
  }, []);

  const respondTo = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed) return;

      setMessages((m) => [...m, { id: msgId++, role: 'user', text: trimmed }]);
      setInput('');
      setThinking(true);

      window.setTimeout(() => {
        // Compound questions ("do X and then Y") come back as several answers.
        // The current screen (pageContext) nudges ranking toward what's relevant here.
        const results = assistantEngine.askAll(trimmed, roles, pageContext);
        const bots: Message[] = results.map((r) => ({ id: msgId++, role: 'bot', bot: buildBotContent(r) }));
        setMessages((m) => [...m, ...bots]);
        setThinking(false);
      }, 260);
    },
    [roles, buildBotContent, pageContext],
  );

  const onChip = useCallback(
    (chip: Chip) => {
      const entry = chip.entryId ? KB_BY_ID[chip.entryId] : undefined;
      if (entry) {
        setMessages((m) => [...m, { id: msgId++, role: 'user', text: chip.label }]);
        setThinking(true);
        window.setTimeout(() => {
          setMessages((m) => [...m, { id: msgId++, role: 'bot', bot: contentForEntry(entry) }]);
          setThinking(false);
        }, 200);
        return;
      }
      respondTo(chip.text ?? chip.label);
    },
    [respondTo, contentForEntry],
  );

  const helpForPage = useCallback(() => {
    if (!pageContext) return;
    const topics = assistantEngine.topicsForPage(pageContext, roles);
    if (topics.length === 0) return;
    setMessages((m) => [...m, { id: msgId++, role: 'user', text: `Help with the ${pageContext.label} page` }]);
    setThinking(true);
    window.setTimeout(() => {
      const [first, ...rest] = topics;
      setMessages((m) => [
        ...m,
        {
          id: msgId++,
          role: 'bot',
          bot: {
            ...contentForEntry(first),
            chips: rest.map((e) => ({ label: e.title, entryId: e.id })),
          },
        },
      ]);
      setThinking(false);
    }, 200);
  }, [pageContext, roles, contentForEntry]);

  const onFeedback = useCallback((messageId: number, topicId: string, helpful: boolean) => {
    assistantEngine.recordFeedback(topicId, helpful);
    setMessages((m) =>
      m.map((msg) => (msg.id === messageId ? { ...msg, feedback: helpful ? 'up' : 'down' } : msg)),
    );
  }, []);

  const goTo = useCallback(
    (to: string) => {
      setOpen(false);
      navigate(to);
    },
    [navigate],
  );

  const clearThread = useCallback(() => {
    assistantEngine.resetContext();
    clearConversation();
    setMessages([]);
  }, []);

  const thread = messages.length === 0 ? [greeting] : messages;

  return (
    <>
      {/* Launcher */}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-label={open ? 'Close assistant' : 'Open help assistant'}
        aria-expanded={open}
        className={`fixed bottom-5 right-5 z-40 flex h-14 w-14 items-center justify-center rounded-full text-white shadow-raised transition-all duration-200 hover:scale-105 focus:outline-none focus-visible:ring-2 focus-visible:ring-lagoon-400 focus-visible:ring-offset-2 focus-visible:ring-offset-canvas ${
          open ? 'bg-lagoon-700' : 'bg-brand-gradient-soft'
        }`}
      >
        {!open && (
          <span className="absolute inset-0 animate-ping rounded-full bg-lagoon-400/40" style={{ animationDuration: '2.6s' }} />
        )}
        {open ? <X className="relative h-6 w-6" /> : <MessageCircleQuestion className="relative h-6 w-6" />}
      </button>

      {/* Panel */}
      {open && (
        <div
          // oxlint-disable-next-line jsx-a11y/prefer-tag-over-role
          role="dialog"
          aria-label="Nautilus help assistant"
          className="fixed bottom-24 right-5 z-40 flex h-[min(34rem,calc(100vh-8rem))] w-[min(24rem,calc(100vw-2.5rem))] flex-col overflow-hidden rounded-2xl border border-line bg-surface shadow-lift animate-scale-in"
        >
          {/* Header */}
          <div className="relative overflow-hidden bg-brand-gradient px-4 py-3.5">
            <svg className="pointer-events-none absolute -bottom-2 left-0 w-full text-[#2C63AB]/30" viewBox="0 0 400 60" preserveAspectRatio="none">
              <path d="M0 30 C 70 5, 130 55, 210 30 S 350 10, 400 34 V60 H0 Z" fill="currentColor" />
            </svg>
            <div className="relative flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/95 shadow-sm">
                <BrandMark className="h-6 w-6" />
              </div>
              <div className="min-w-0 flex-1 leading-tight">
                <div className="flex items-center gap-1.5 font-display text-sm font-semibold text-white">
                  Nautilus Assistant
                  <Sparkles className="h-3.5 w-3.5 text-sand-300" />
                </div>
                <div className="flex items-center gap-1.5 text-[11px] text-lagoon-100">
                  <span className="inline-block h-1.5 w-1.5 rounded-full bg-emerald-300" />
                  Offline · always available
                </div>
              </div>
              {messages.length > 0 && (
                <button
                  type="button"
                  onClick={clearThread}
                  aria-label="Clear conversation"
                  title="Clear conversation"
                  className="rounded-md p-1.5 text-white/80 transition-colors hover:bg-white/10 hover:text-white"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              )}
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Close"
                className="rounded-md p-1.5 text-white/80 transition-colors hover:bg-white/10 hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>

          {/* Page-context strip: what the assistant can see you're looking at */}
          {pageContext && (
            <button
              type="button"
              onClick={helpForPage}
              className="flex items-center gap-1.5 border-b border-line bg-lagoon-50/70 px-4 py-1.5 text-left text-xs text-lagoon-700 transition-colors hover:bg-lagoon-50"
            >
              <MapPin className="h-3.5 w-3.5 shrink-0" />
              <span className="min-w-0 flex-1 truncate">
                Viewing <span className="font-medium">{pageContext.label}</span> — tap for help with this page
              </span>
            </button>
          )}

          {/* Messages */}
          <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto bg-canvas px-3.5 py-4">
            {thread.map((m) =>
              m.role === 'user' ? (
                <div key={m.id} className="flex justify-end">
                  <div className="max-w-[85%] rounded-2xl rounded-br-sm bg-lagoon-500 px-3.5 py-2 text-sm text-white shadow-sm">
                    {m.text}
                  </div>
                </div>
              ) : (
                <BotBubble
                  key={m.id}
                  message={m}
                  onChip={onChip}
                  onGo={goTo}
                  onFeedback={onFeedback}
                />
              ),
            )}

            {thinking && (
              <div className="flex items-center gap-2 pl-1 text-ink-muted" aria-live="polite">
                <span className="flex gap-1">
                  <Dot delay="0ms" /> <Dot delay="150ms" /> <Dot delay="300ms" />
                </span>
              </div>
            )}

            {messages.length === 0 && !thinking && (
              <div className="space-y-3 pt-1">
                {pageTopics.length > 0 && (
                  <div>
                    <p className="mb-2 px-1 text-[11px] font-semibold uppercase tracking-wider text-ink-muted">
                      On this page
                    </p>
                    <div className="flex flex-col gap-1.5">
                      {pageTopics.map((e) => (
                        <button
                          key={e.id}
                          type="button"
                          onClick={() => onChip({ label: e.title, entryId: e.id })}
                          className="group flex items-center justify-between gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-left text-xs font-medium text-ink-soft transition-colors hover:border-lagoon-300 hover:bg-lagoon-50 hover:text-lagoon-700"
                        >
                          {e.title}
                          <CornerDownLeft className="h-3.5 w-3.5 text-ink-muted transition-colors group-hover:text-lagoon-500" />
                        </button>
                      ))}
                    </div>
                  </div>
                )}
                <div>
                <p className="mb-2 px-1 text-[11px] font-semibold uppercase tracking-wider text-ink-muted">
                  Try asking
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {STARTER_PROMPTS.map((p) => (
                    <button
                      key={p}
                      type="button"
                      onClick={() => respondTo(p)}
                      className="rounded-full border border-line bg-surface px-3 py-1.5 text-left text-xs text-ink-soft transition-colors hover:border-lagoon-300 hover:bg-lagoon-50 hover:text-lagoon-700"
                    >
                      {p}
                    </button>
                  ))}
                </div>
                </div>
              </div>
            )}
          </div>

          {/* Composer */}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              respondTo(input);
            }}
            className="flex items-center gap-2 border-t border-line bg-surface px-3 py-2.5"
          >
            <input
              ref={inputRef}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask how to do something…"
              aria-label="Ask the assistant"
              className="min-w-0 flex-1 rounded-full border border-line bg-canvas px-3.5 py-2 text-sm text-ink outline-none transition-colors placeholder:text-ink-muted focus:border-lagoon-400 focus:bg-surface"
            />
            <button
              type="submit"
              disabled={!input.trim()}
              aria-label="Send"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-lagoon-500 text-white transition-colors hover:bg-lagoon-600 disabled:opacity-40"
            >
              <Send className="h-4 w-4" />
            </button>
          </form>
        </div>
      )}
    </>
  );
}

/** Wrap occurrences of matched (stemmed) query terms in <mark> for emphasis. */
function highlight(text: string, matched?: string[]) {
  if (!matched || matched.length === 0) return text;
  const set = new Set(matched);
  return text.split(/(\s+)/).map((token, i) => {
    const stems = analyze(token);
    if (stems.length === 1 && set.has(stems[0])) {
      return (
        <mark key={i} className="rounded bg-sand-100 px-0.5 text-ink">
          {token}
        </mark>
      );
    }
    return token;
  });
}

function BotBubble({
  message,
  onChip,
  onGo,
  onFeedback,
}: {
  message: Message;
  onChip: (chip: Chip) => void;
  onGo: (to: string) => void;
  onFeedback: (messageId: number, topicId: string, helpful: boolean) => void;
}) {
  const bot = message.bot!;
  return (
    <div className="flex justify-start">
      <div className="max-w-[92%] space-y-2.5">
        {bot.entry && <p className="px-1 font-display text-sm font-semibold text-ink">{bot.entry.title}</p>}

        <div className="rounded-2xl rounded-bl-sm border border-line bg-surface px-3.5 py-2.5 text-sm leading-relaxed text-ink-soft shadow-sm">
          {bot.empathyPrefix && <p className="mb-1.5 font-medium text-lagoon-700">{bot.empathyPrefix}</p>}

          {bot.correctedQuery && (
            <p className="mb-1.5 text-xs text-ink-muted">
              Showing results for <span className="font-medium text-ink-soft">{bot.correctedQuery}</span>
            </p>
          )}

          <p>{highlight(bot.text, bot.matchedTerms)}</p>

          {bot.steps && bot.steps.length > 0 && (
            <ol className="mt-2.5 space-y-1.5">
              {bot.steps.map((s, i) => (
                <li key={i} className="flex gap-2.5">
                  <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-lagoon-100 text-[11px] font-semibold text-lagoon-700">
                    {i + 1}
                  </span>
                  <span className="text-ink-soft">{highlight(s, bot.matchedTerms)}</span>
                </li>
              ))}
            </ol>
          )}

          {bot.links && bot.links.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-1.5">
              {bot.links.map((l) => (
                <button
                  key={l.to}
                  type="button"
                  onClick={() => onGo(l.to)}
                  className="inline-flex items-center gap-1.5 rounded-md bg-lagoon-500 px-2.5 py-1.5 text-xs font-medium text-white transition-colors hover:bg-lagoon-600"
                >
                  {l.label}
                  <ArrowRight className="h-3.5 w-3.5" />
                </button>
              ))}
            </div>
          )}

          {bot.entities && bot.entities.length > 0 && (
            <div className="mt-3 border-t border-line pt-2.5">
              <p className="mb-1.5 text-[10px] font-semibold uppercase tracking-wider text-ink-muted">
                Spotted a reference
              </p>
              <div className="flex flex-wrap gap-1.5">
                {bot.entities.map((e) => (
                  <button
                    key={e.to + e.label}
                    type="button"
                    onClick={() => onGo(e.to)}
                    className="inline-flex items-center gap-1.5 rounded-md border border-lagoon-200 bg-lagoon-50 px-2.5 py-1.5 text-xs font-medium text-lagoon-700 transition-colors hover:bg-lagoon-100"
                  >
                    {e.label}
                    <ExternalLink className="h-3.5 w-3.5" />
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Was this helpful? — feeds the learning-to-rank prior. */}
        {bot.topicId && !bot.clarifying && (
          <div className="flex items-center gap-2 px-1">
            {message.feedback ? (
              <span className="text-[11px] text-ink-muted">
                {message.feedback === 'up' ? 'Thanks for the feedback! 🙌' : 'Thanks — I’ll keep tuning.'}
              </span>
            ) : (
              <>
                <span className="text-[11px] text-ink-muted">Was this helpful?</span>
                <button
                  type="button"
                  aria-label="Helpful"
                  onClick={() => onFeedback(message.id, bot.topicId!, true)}
                  className="rounded p-1 text-ink-muted transition-colors hover:bg-success/10 hover:text-success"
                >
                  <ThumbsUp className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  aria-label="Not helpful"
                  onClick={() => onFeedback(message.id, bot.topicId!, false)}
                  className="rounded p-1 text-ink-muted transition-colors hover:bg-danger/10 hover:text-danger"
                >
                  <ThumbsDown className="h-3.5 w-3.5" />
                </button>
              </>
            )}
          </div>
        )}

        {bot.chips.length > 0 && (
          <div className="space-y-1.5">
            {bot.clarifying ? (
              <div className="flex flex-col gap-1.5">
                {bot.chips.map((c) => (
                  <button
                    key={c.entryId ?? c.label}
                    type="button"
                    onClick={() => onChip(c)}
                    className="group flex items-center justify-between gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-left text-xs font-medium text-ink-soft transition-colors hover:border-lagoon-300 hover:bg-lagoon-50 hover:text-lagoon-700"
                  >
                    {c.label}
                    <CornerDownLeft className="h-3.5 w-3.5 text-ink-muted transition-colors group-hover:text-lagoon-500" />
                  </button>
                ))}
              </div>
            ) : (
              <div className="flex flex-wrap gap-1.5 px-1">
                <span className="w-full pb-0.5 text-[10px] font-semibold uppercase tracking-wider text-ink-muted">
                  Related
                </span>
                {bot.chips.map((c) => (
                  <button
                    key={c.entryId ?? c.label}
                    type="button"
                    onClick={() => onChip(c)}
                    className="rounded-full border border-line bg-surface px-2.5 py-1 text-xs text-ink-soft transition-colors hover:border-lagoon-300 hover:bg-lagoon-50 hover:text-lagoon-700"
                  >
                    {c.label}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function Dot({ delay }: { delay: string }) {
  return (
    <span
      className="inline-block h-1.5 w-1.5 animate-bounce rounded-full bg-lagoon-400"
      style={{ animationDelay: delay }}
    />
  );
}
