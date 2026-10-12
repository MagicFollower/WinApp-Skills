import type { EmojiRenderProps } from 'emoji-picker-react/primitives';
import { EMOJI_PX, emojiGlyph, spriteStyle } from './atlas';

/**
 * `managed` carries the library's own type/class/position style/tabIndex/
 * aria-label/data-epr-* — those are what selection, keyboard navigation,
 * virtualization and accessibility ride on, so they stay on the button.
 */
export function SpriteCell({ emoji, children, ...managed }: EmojiRenderProps) {
  const sprite = spriteStyle(emoji.unified, EMOJI_PX.cell);
  return (
    <button {...managed}>
      {/* 码位文本留在格子里(和气泡的 .emoji-img 同一手法):画出来的是背景那一格,
          但选区/复制拿得到字符。没有它面板格子的 textContent 是空串,拖不出选区也复制不出东西(实测)。 */}
      {sprite ? <span className="ep-glyph" style={sprite}>{emoji.emoji}</span> : children}
    </button>
  );
}

/** 输入框与气泡共用的表情外观：与面板同一张 atlas、同一码位，只有 px 按档走。 */
export function EmojiGlyph({ text, tier }: { text: string; tier: 'sm' | 'lg' }) {
  const g = emojiGlyph(text, tier);
  // 字符本身留在文本里：选中复制才拿得到它，画出来的是背景那一格
  return (
    <span className={g.className} style={g.style} data-size={tier}>
      {text}
    </span>
  );
}
