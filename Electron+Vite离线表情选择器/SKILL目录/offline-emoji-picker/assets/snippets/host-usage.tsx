// 宿主接线要点（从跑通的工程里摘出的四段，不是完整组件）。
// 每段都对应一个"照抄才会对"的细节，注释里写的是踩过的原因。
import { useCallback, useEffect, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import type { EmojiClickData } from 'emoji-picker-react/primitives';
import { EmojiPicker } from '../emoji/Picker';
import { spriteStyle } from '../emoji/atlas';

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

export function HostUsage() {
  // ?open 让自证流程能把弹层钉住，file:// 下没有别的入口。
  const [open, setOpen] = useState(() => new URLSearchParams(location.search).has('open'));
  const [text, setText] = useState('');
  const [recent, setRecent] = useState<string[]>(loadRecent);
  const areaRef = useRef<HTMLTextAreaElement>(null);
  const shellRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    localStorage.setItem(RECENT_KEY, JSON.stringify(recent));
  }, [recent]);

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
    // 插到光标处而不是追加：直接 setText(t => t + emoji) 会让用户选完还得手动挪光标。
    const area = areaRef.current;
    if (area) {
      const start = area.selectionStart ?? area.value.length;
      const end = area.selectionEnd ?? start;
      setText(area.value.slice(0, start) + data.emoji + area.value.slice(end));
      // 受控 textarea 的值要先进 DOM 才能设光标，所以放到下一帧。
      requestAnimationFrame(() => {
        area.focus();
        area.setSelectionRange(start + data.emoji.length, start + data.emoji.length);
      });
    } else {
      setText((t) => t + data.emoji);
    }
    setRecent((prev) => [data.unified, ...prev.filter((u) => u !== data.unified)].slice(0, RECENT_MAX));
  }, []);

  // 存下来的码位可能没有格子（换过 emoji 版本、或上游本就没位图）。
  // 不gate 住的话这一条会渲染成一排 0×0 的空栏——只看 DOM 数量看不出来，必须按"能画"过滤。
  const drawable = recent
    .map((u) => ({ u, style: spriteStyle(u, 22) }))
    .filter((x): x is { u: string; style: CSSProperties } => x.style !== null);

  return (
    // popover 是 absolute 定位，所以这层壳必须是 relative 锚点。
    <div ref={shellRef} style={{ position: 'relative' }}>
      <textarea ref={areaRef} value={text} onChange={(e) => setText(e.target.value)} />
      <button onClick={() => setOpen((v) => !v)} aria-expanded={open} aria-haspopup="dialog">
        ☺
      </button>

      {drawable.length > 0 && (
        <div aria-label="最近使用">
          {drawable.map(({ u, style }) => (
            <button
              key={u}
              onClick={() =>
                setText((t) => t + String.fromCodePoint(...u.split('-').map((h) => parseInt(h, 16))))
              }
            >
              {/* 必须是 <span class="ep-glyph">，配合 picker.css 里的 display:block; flex:none */}
              <span className="ep-glyph" style={style} />
            </button>
          ))}
        </div>
      )}

      {open && (
        <div role="dialog" aria-label="表情选择器" className="popover">
          <EmojiPicker onPick={onPick} />
        </div>
      )}
    </div>
  );
}
