import type { CSSProperties } from 'react';
import atlasIndex from './atlas/index.json';

// Vite rewrites each match into a hashed asset URL and copies the file into
// dist/ — that is what keeps the picker working with no network at runtime.
const urls = import.meta.glob('./atlas/*.webp', {
  eager: true,
  query: '?url',
  import: 'default'
}) as Record<string, string>;

function urlFor(file: string): string {
  const found = urls[`./atlas/${file}`];
  if (!found) throw new Error(`atlas "${file}" was not bundled - rerun npm run gen:atlas`);
  return found;
}

const SRC_CELL = atlasIndex.cell;

/**
 * 三档显示尺寸：面板格子是"挑一个"，框内与气泡必须同档同尺寸。
 * 唯一真源在这里，CSS 只写回落（无贴图）时的字号。
 */
export const EMOJI_PX = { cell: 30, sm: 28, lg: 48 } as const;
export type EmojiTier = keyof typeof EMOJI_PX;

/** 表情符号 → atlas 的 unified 键（按码位切，ZWJ/变体选择器各自成段）。 */
export const unifiedOf = (ch: string): string =>
  Array.from(ch)
    .map((c) => (c.codePointAt(0) ?? 0).toString(16))
    .join('-');

export function spriteStyle(unified: string, px: number): CSSProperties | null {
  const glyph = atlasIndex.glyphs[unified as keyof typeof atlasIndex.glyphs];
  if (!glyph) return null;
  const atlas = atlasIndex.atlases[glyph.a];
  const scale = px / SRC_CELL;
  /* 尺寸给成带单位的字符串而不是数字：消费方有一半是命令式 DOM
     （输入框的 token 要 Object.assign 到 span.style），数字在那边不会补 px。 */
  return {
    width: `${px}px`,
    height: `${px}px`,
    backgroundImage: `url(${urlFor(atlas.file)})`,
    backgroundSize: `${atlas.width * scale}px ${atlas.height * scale}px`,
    backgroundPosition: `${-glyph.x * scale}px ${-glyph.y * scale}px`,
    backgroundRepeat: 'no-repeat'
  };
}

/** 一代表情的外观：有贴图就贴图，没贴图（数字、裸 U+2764）才回落系统字形。
 *  两条路径共用 `.glyph-<档>` 的字号，贴图那条再叠 `.emoji-img`。 */
export function emojiGlyph(text: string, tier: Exclude<EmojiTier, 'cell'>) {
  const style = spriteStyle(unifiedOf(text), EMOJI_PX[tier]);
  return style ? { className: `emoji-img glyph-${tier}`, style } : { className: `glyph-${tier}`, style: undefined };
}

export const atlasStats = {
  pages: atlasIndex.atlases.length,
  glyphs: Object.keys(atlasIndex.glyphs).length,
  bytes: atlasIndex.atlases.reduce((s, a) => s + a.bytes, 0)
};
