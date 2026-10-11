// Composes one WebP sprite atlas per emoji category from local sources.
//
//   in : node_modules/emoji-picker-react/dist/data/emojis-zh.json  (which glyphs, which category)
//        node_modules/emoji-datasource-apple/img/apple/64/*.png    (the Apple glyph bitmaps)
//   out: src/emoji/atlas/<category>.webp  +  src/emoji/atlas/index.json
//
// Nothing here touches the network, and no CDN URL is ever emitted.

import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const ROOT = path.resolve(import.meta.dirname, '..');
const DATA = path.join(ROOT, 'node_modules/emoji-picker-react/dist/data/emojis-zh.json');
const SRC_DIR = path.join(ROOT, 'node_modules/emoji-datasource-apple/img/apple/64');
const OUT_DIR = path.join(ROOT, 'src/emoji/atlas');

const CELL = 64;
const MAX_EDGE = 2048; // stay well under the 4096 texture ceiling so a page decodes lazily
const PER_AXIS = Math.floor(MAX_EDGE / CELL); // 32 -> a full page is 2048x2048 on BOTH axes
const PER_PAGE = PER_AXIS * PER_AXIS;

const raw = JSON.parse(fs.readFileSync(DATA, 'utf8'));
const available = new Set(
  fs.readdirSync(SRC_DIR).filter((f) => f.endsWith('.png')).map((f) => f.slice(0, -4))
);

const missing = { base: [], variation: [] };
const records = [];

for (const cat of Object.keys(raw.emojis)) {
  for (const rec of raw.emojis[cat] || []) {
    if (!available.has(rec.u)) {
      missing.base.push(rec.u);
      continue;
    }
    const variations = (rec.v || []).filter((v) => {
      if (available.has(v)) return true;
      missing.variation.push(v);
      return false;
    });
    records.push({ ...rec, cat, variations });
  }
}

const byCategory = new Map();
for (const r of records) {
  if (!byCategory.has(r.cat)) byCategory.set(r.cat, []);
  const list = byCategory.get(r.cat);
  list.push(r.u);
  for (const v of r.variations) list.push(v);
}

fs.rmSync(OUT_DIR, { recursive: true, force: true });
fs.mkdirSync(OUT_DIR, { recursive: true });

/** @type {Record<string, {a: number, x: number, y: number}>} */
const glyphs = {};
const atlases = [];

for (const cat of [...byCategory.keys()]) {
  const list = byCategory.get(cat);
  for (let page = 0; page * PER_PAGE < list.length; page++) {
    const slice = list.slice(page * PER_PAGE, (page + 1) * PER_PAGE);
    const cols = Math.max(1, Math.min(PER_AXIS, Math.ceil(Math.sqrt(slice.length))));
    const rows = Math.ceil(slice.length / cols);
    const width = cols * CELL;
    const height = rows * CELL;

    const composite = [];
    slice.forEach((unified, i) => {
      const x = (i % cols) * CELL;
      const y = Math.floor(i / cols) * CELL;
      glyphs[unified] = { a: atlases.length, x, y };
      composite.push({
        input: fs.readFileSync(path.join(SRC_DIR, `${unified}.png`)),
        left: x,
        top: y
      });
    });

    const file = `${cat}${page ? `-p${page + 1}` : ''}.webp`;
    await sharp({ create: { width, height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite(composite)
      .webp({ quality: 92, alphaQuality: 100 })
      .toFile(path.join(OUT_DIR, file));

    const bytes = fs.statSync(path.join(OUT_DIR, file)).size;
    atlases.push({ cat, page, file, cols, rows, width, height, glyphs: slice.length, bytes });
    console.log(
      `  ${file.padEnd(24)} ${String(slice.length).padStart(4)} glyphs  ${String(width).padStart(4)}x${String(height).padStart(4)}  ${(bytes / 1024).toFixed(0).padStart(6)} KB`
    );
  }
}

const manifest = {
  generatedAt: new Date().toISOString().slice(0, 10),
  cell: CELL,
  source: 'emoji-datasource-apple@16.0.0 img/apple/64',
  atlases,
  glyphs
};
fs.writeFileSync(path.join(OUT_DIR, 'index.json'), JSON.stringify(manifest));

const totalGlyphs = Object.keys(glyphs).length;
const totalBytes = atlases.reduce((s, a) => s + a.bytes, 0);
const over = atlases.filter((a) => a.width > MAX_EDGE || a.height > MAX_EDGE);
console.log('');
console.log(`records with a sprite : ${records.length} / ${Object.keys(raw.emojis).reduce((s, c) => s + raw.emojis[c].length, 0)}`);
console.log(`glyphs atlased        : ${totalGlyphs} (${records.length} base + ${totalGlyphs - records.length} variations)`);
console.log(`atlas pages           : ${atlases.length}, ${totalBytes.toLocaleString()} bytes total`);
console.log(`largest edge (both)   : ${Math.max(...atlases.map((a) => Math.max(a.width, a.height)))} px  (limit ${MAX_EDGE})`);
console.log(`over-limit pages      : ${over.length} ${over.length ? 'FAIL' : 'ok'}`);
console.log(`index.json            : ${(fs.statSync(path.join(OUT_DIR, 'index.json')).size / 1024).toFixed(0)} KB`);
console.log(`unresolved base       : ${missing.base.length} ${JSON.stringify(missing.base)}`);
console.log(`unresolved variations : ${missing.variation.length} (Apple ships no bitmap for these skin combinations)`);
if (over.length) process.exit(1);
