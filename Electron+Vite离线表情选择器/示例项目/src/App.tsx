import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import type { EmojiClickData } from 'emoji-picker-react/primitives';
import { EmojiPicker } from './emoji/Picker';
import { spriteStyle } from './emoji/atlas';

const RECENT_KEY = 'ep.recent';
const RECENT_MAX = 12;

function loadRecent(): string[] {
  try {
    const parsed = JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((x) => typeof x === 'string') : [];
  } catch {
    return [];
  }
}

export default function App() {
  const [open, setOpen] = useState(() => new URLSearchParams(location.search).has('open'));
  const [text, setText] = useState('');
  const [recent, setRecent] = useState<string[]>(loadRecent);
  const [messages, setMessages] = useState<string[]>(['表情选择器离线版']);
  const areaRef = useRef<HTMLTextAreaElement>(null);
  const shellRef = useRef<HTMLDivElement>(null);
  const logRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    localStorage.setItem(RECENT_KEY, JSON.stringify(recent));
  }, [recent]);

  useEffect(() => {
    const el = logRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages]);

  const send = () => {
    const body = text.trim();
    if (!body) return;
    setMessages((prev) => [...prev, body]);
    setText('');
  };

  // Proves contextBridge and the IPC round trip work from file:// in the
  // packaged app; the main process logs ipc-roundtrip=ok on the other end.
  useEffect(() => {
    window.api
      .ping()
      .then((v) => console.log(`renderer received ${v}`))
      .catch((err) => console.error('ping failed', err));
  }, []);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => {
      if (!shellRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('pointerdown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const onPick = useCallback((data: EmojiClickData) => {
    const area = areaRef.current;
    if (area) {
      const start = area.selectionStart ?? area.value.length;
      const end = area.selectionEnd ?? start;
      setText(area.value.slice(0, start) + data.emoji + area.value.slice(end));
      requestAnimationFrame(() => {
        area.focus();
        area.setSelectionRange(start + data.emoji.length, start + data.emoji.length);
      });
    } else {
      setText((t) => t + data.emoji);
    }
    setRecent((prev) => [data.unified, ...prev.filter((u) => u !== data.unified)].slice(0, RECENT_MAX));
  }, []);

  // A stored codepoint with no atlas entry cannot be drawn; gating on the
  // drawable count keeps the strip from collapsing into an empty bar.
  const drawable = recent
    .map((u) => ({ u, style: spriteStyle(u, 22) }))
    .filter((x): x is { u: string; style: CSSProperties } => x.style !== null);

  return (
    <div className="chat" ref={shellRef}>
      <div className="chat__log" ref={logRef}>
        {messages.map((m, i) => (
          <p key={i} className="bubble">
            {m}
          </p>
        ))}
      </div>

      {drawable.length > 0 && (
        <div className="recent" aria-label="最近使用">
          {drawable.map(({ u, style }) => (
            <button
              key={u}
              className="recent__item"
              data-unified={u}
              onClick={() => setText((t) => t + String.fromCodePoint(...u.split('-').map((h) => parseInt(h, 16))))}
            >
              <span className="ep-glyph" style={style} />
            </button>
          ))}
        </div>
      )}

      <div className="composer">
        <button
          className={`composer__trigger${open ? ' is-open' : ''}`}
          aria-expanded={open}
          aria-haspopup="dialog"
          aria-label="插入表情"
          onClick={() => setOpen((v) => !v)}
        >
          ☺
        </button>
        <textarea
          ref={areaRef}
          className="composer__input"
          value={text}
          placeholder="输入内容，点左侧笑脸选表情"
          onChange={(e) => setText(e.target.value)}
        />
        <button className="composer__send" onClick={send} disabled={!text.trim()}>
          发送
        </button>
      </div>

      {open && (
        <div className="popover" role="dialog" aria-label="表情选择器">
          <EmojiPicker onPick={onPick} />
        </div>
      )}
    </div>
  );
}
