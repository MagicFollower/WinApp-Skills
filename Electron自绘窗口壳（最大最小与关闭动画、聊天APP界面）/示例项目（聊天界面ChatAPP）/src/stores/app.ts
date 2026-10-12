import { create } from 'zustand';
import type { ChatFilter, EmojiTab, GlyphSize, Message, MessagePart } from '../types';
import { QUICK_PHRASES, SEED_MESSAGES } from '../data/seed';

/** 一条"把字符落进输入框"的命令:输入框是 contenteditable,DOM 才是草稿的真源,
    store 里再存一份 draft 就会有两个真源(光标在中间时字符串拼接还会写错位置)。
    存队列而不是单条:同一拍里连点两格,React 只会把最后一条送进渲染,前一条会丢(实测)。 */
interface DraftInsert {
  text: string;
  /** 这一格从哪个字号档来的,气泡照同一档渲染(框内另有自己的两档) */
  size: GlyphSize;
  /** 单调递增:同一段文字连点两次也要当成新命令 */
  id: number;
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
  draftQueue: DraftInsert[];
  setFilter: (f: ChatFilter) => void;
  setSearch: (s: string) => void;
  selectChat: (id: string) => void;
  togglePanel: () => void;
  setEmojiTab: (t: EmojiTab) => void;
  togglePhrases: () => void;
  addPhrase: (p: string) => void;
  updatePhrase: (old: string, next: string) => void;
  appendDraft: (t: string, size?: GlyphSize) => void;
  ackDraft: (id: number) => void;
  send: (parts: MessagePart[]) => void;
}

let seq = 100;
let insertSeq = 0;

export const useAppStore = create<AppState>((set) => ({
  filter: '全部',
  search: '',
  activeChatId: 'c0',
  messages: SEED_MESSAGES,
  panelOpen: true,
  emojiTab: '大表情',
  phrases: [...QUICK_PHRASES],
  phrasesOpen: true,
  draftQueue: [],
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
  appendDraft: (text, size = 'text') =>
    set((s) => ({ draftQueue: [...s.draftQueue, { text, size, id: ++insertSeq }] })),
  /* 输入框应用到这里就可以把旧的丢掉;按 id 过滤而不是清空,免得跨拍的插入被误删。 */
  ackDraft: (id) => set((s) => ({ draftQueue: s.draftQueue.filter((d) => d.id > id) })),
  /* 发出的消息一律是文本气泡:大小由分段自己带,不再用"整段长得像 emoji"去猜
     (那条规则把 666 这类纯数字也判成了大表情)。 */
  send: (parts) =>
    set((s) => ({
      messages: [
        ...s.messages,
        {
          id: `u${seq++}`,
          chatId: s.activeChatId,
          sender: '我',
          own: true,
          kind: 'text',
          text: parts.map((p) => p.text).join(''),
          parts,
          createdAt: Date.now(),
        },
      ],
    })),
}));
