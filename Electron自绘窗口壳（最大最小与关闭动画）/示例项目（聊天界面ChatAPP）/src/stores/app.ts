import { create } from 'zustand';
import type { ChatFilter, EmojiTab, Message } from '../types';
import { QUICK_PHRASES, SEED_MESSAGES } from '../data/seed';

const BIGEMOJI_RE = /^[\p{Extended_Pictographic}\d\s🀄️⃣*#]{1,8}$/u;

function classify(text: string): Message['kind'] {
  const t = text.trim();
  if (t.length > 0 && t.length <= 8 && BIGEMOJI_RE.test(t) && !/[a-zA-Z\u4e00-\u9fff]/.test(t)) {
    return 'bigemoji';
  }
  return 'text';
}

interface AppState {
  filter: ChatFilter;
  search: string;
  activeChatId: string;
  messages: Message[];
  panelOpen: boolean;
  emojiTab: EmojiTab;
  phrases: string[];
  phrasesOpen: boolean;
  draft: string;
  setFilter: (f: ChatFilter) => void;
  setSearch: (s: string) => void;
  selectChat: (id: string) => void;
  togglePanel: () => void;
  setEmojiTab: (t: EmojiTab) => void;
  togglePhrases: () => void;
  addPhrase: (p: string) => void;
  updatePhrase: (old: string, next: string) => void;
  setDraft: (d: string) => void;
  appendDraft: (t: string) => void;
  send: (text: string) => void;
}

let seq = 100;

export const useAppStore = create<AppState>((set) => ({
  filter: '全部',
  search: '',
  activeChatId: 'c0',
  messages: SEED_MESSAGES,
  panelOpen: true,
  emojiTab: '定制',
  phrases: [...QUICK_PHRASES],
  phrasesOpen: true,
  draft: '',
  setFilter: (filter) => set({ filter }),
  setSearch: (search) => set({ search }),
  selectChat: (activeChatId) => set({ activeChatId }),
  togglePanel: () => set((s) => ({ panelOpen: !s.panelOpen })),
  setEmojiTab: (emojiTab) => set({ emojiTab }),
  togglePhrases: () => set((s) => ({ phrasesOpen: !s.phrasesOpen })),
  addPhrase: (p) => set((s) => ({ phrases: [...s.phrases, p] })),
  /* 改名撞车或改成空串时,把原来那条丢掉:空短语点不出东西,重复两条会撞 key。 */
  updatePhrase: (old, next) =>
    set((s) => {
      const value = next.trim();
      const rest = s.phrases.filter((p) => p !== old);
      return { phrases: value && !rest.includes(value) ? [...rest, value] : rest };
    }),
  setDraft: (draft) => set({ draft }),
  appendDraft: (t) => set((s) => ({ draft: s.draft + t })),
  send: (text) =>
    set((s) => ({
      messages: [
        ...s.messages,
        {
          id: `u${seq++}`,
          chatId: s.activeChatId,
          sender: '我',
          own: true,
          kind: classify(text),
          text: text.trim(),
          createdAt: Date.now(),
        },
      ],
    })),
}));
