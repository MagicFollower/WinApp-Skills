// Independent cross-check of atlas alignment: pull each glyph's region out of
// the composed WebP and compare it to the source PNG it was cut from.
//
// The metric has to be "what the user sees": both images are flattened onto a
// solid background and compared on RGB only. Comparing raw RGBA averages is
// meaningless for emoji - most of a 64x64 cell is fully transparent, and the
// RGB stored under alpha=0 is arbitrary and changes across a WebP round trip.
//
// Lossy WebP makes exact equality impossible, so alongside the absolute diff
// the metric is checked against a deliberately WRONG offset to prove it can
// discriminate at all. A pipeline that renders "an emoji" but the wrong one
// passes every size/count check and fails this.

import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const ROOT = path.resolve(import.meta.dirname, '..');
const atlas = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/emoji/atlas/index.json'), 'utf8'));
const SRC = path.join(ROOT, 'node_modules/emoji-datasource-apple/img/apple/64');
const CELL = atlas.cell;
const BG = { r: 255, g: 255, b: 255 };

// Negative control: SHIFT_CELLS=1 makes every lookup read the cell to the
// right. A check that still reports 0 suspect under it proves nothing, so run
// it once whenever this file changes.
const SHIFT = Number(process.env.SHIFT_CELLS || 0);

const samples = [
  '1f600', '1f604', '1f970', '1f44d', '1f3c1', '2764-fe0f',
  '1f98a', '1f40e', '1f354', '1f697', '1f3c6', '1f4a1', '1f5ff'
];

async function flattenedRGB(file, box) {
  return (
    await sharp(file, { animated: false })
      .extract({ left: box.left, top: box.top, width: CELL, height: CELL })
      .flatten({ background: BG })
      .removeAlpha()
      .raw()
      .toBuffer()
  );
}

function meanAbsDiff(a, b) {
  let sum = 0;
  const n = Math.min(a.length, b.length);
  for (let i = 0; i < n; i++) sum += Math.abs(a[i] - b[i]);
  return sum / n;
}

// Control = a cell far away in the same page (half a page over, wrapping).
// The immediate neighbour is a BAD control: the layout stores each base glyph
// followed by its skin-tone variations, and even across bases adjacent faces
// are near-duplicates (measured: 1f600 sits next to 1f603, 1f604 next to
// 1f601), so a neighbour control reports a low ratio for a correctly aligned
// atlas. The primary gate is therefore the absolute diff against the source;
// the far control only proves the metric can tell the two apart at all.
function farCell(atlasInfo, g) {
  const cols = atlasInfo.cols;
  const rows = atlasInfo.rows;
  const cx = Math.floor(g.x / CELL);
  const cy = Math.floor(g.y / CELL);
  const fx = (cx + Math.ceil(cols / 2)) % cols;
  const fy = (cy + Math.ceil(rows / 2)) % rows;
  return { left: fx * CELL, top: fy * CELL };
}

let worstGood = 0;
let worstRatio = Infinity;
let failures = 0;
let checked = 0;

console.log('glyph       correct  far-cell  ratio   verdict');
for (const u of samples) {
  const g = atlas.glyphs[u];
  if (!g) {
    console.log(`${u.padEnd(11)} SKIPPED - not atlased`);
    continue;
  }
  const srcFile = path.join(SRC, `${u}.png`);
  if (!fs.existsSync(srcFile)) {
    console.log(`${u.padEnd(11)} SKIPPED - no source png`);
    continue;
  }
  checked++;
  const a = atlas.atlases[g.a];
  const page = path.join(ROOT, 'src/emoji/atlas', a.file);
  const src = await flattenedRGB(srcFile, { left: 0, top: 0 });
  const shiftedCol = ((((Math.floor(g.x / CELL) + SHIFT) % a.cols) + a.cols) % a.cols) * CELL;
  const good = await flattenedRGB(page, { left: shiftedCol, top: g.y });
  const bad = await flattenedRGB(page, farCell(a, g));

  const dGood = meanAbsDiff(src, good);
  const dBad = meanAbsDiff(src, bad);
  const ratio = dBad / Math.max(dGood, 0.001);
  const ok = dGood < 6 && ratio > 3;
  if (!ok) failures++;
  worstGood = Math.max(worstGood, dGood);
  worstRatio = Math.min(worstRatio, ratio);
  console.log(
    `${u.padEnd(11)} ${dGood.toFixed(2).padStart(7)} ${dBad.toFixed(2).padStart(10)} ${ratio.toFixed(1).padStart(7)}x   ${ok ? 'ok' : 'SUSPECT'}`
  );
}

console.log('');
console.log(`glyphs checked                : ${checked}`);
console.log(`worst correct-offset diff     : ${worstGood.toFixed(2)} / 255  (gate 6)`);
console.log(`smallest correct/far ratio    : ${worstRatio.toFixed(1)}x  (gate 3)`);
console.log(`suspect                       : ${failures}`);
if (failures) process.exit(1);
