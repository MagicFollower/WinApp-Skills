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

export function spriteStyle(unified: string, px: number): CSSProperties | null {
  const glyph = atlasIndex.glyphs[unified as keyof typeof atlasIndex.glyphs];
  if (!glyph) return null;
  const atlas = atlasIndex.atlases[glyph.a];
  const scale = px / SRC_CELL;
  return {
    width: px,
    height: px,
    backgroundImage: `url(${urlFor(atlas.file)})`,
    backgroundSize: `${atlas.width * scale}px ${atlas.height * scale}px`,
    backgroundPosition: `${-glyph.x * scale}px ${-glyph.y * scale}px`,
    backgroundRepeat: 'no-repeat'
  };
}

export const atlasStats = {
  pages: atlasIndex.atlases.length,
  glyphs: Object.keys(atlasIndex.glyphs).length,
  bytes: atlasIndex.atlases.reduce((s, a) => s + a.bytes, 0)
};
