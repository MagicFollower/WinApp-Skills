// 重拼 atlas —— 只在"换 emoji 版本 / 改分页参数 / 改分类归属"时跑。
//
//   node <skill>/scripts/gen-atlas.mjs --root <工程> [--prefix src/emoji]
//                                     [--upstream <img/apple 目录>] [--cell 64] [--max-edge 2048]
//
// 前置：目标工程里要能 require 到 sharp，且上游位图目录存在。
//   npm i -D sharp emoji-datasource-apple@16
//
// 分页规则（改动前先读懂再动）：
//   · 每格 CELL 像素，单页边长不超过 MAX_EDGE，PER_AXIS = floor(MAX_EDGE/CELL)，PER_PAGE = PER_AXIS²。
//   · MAX_EDGE 必须同时约束宽和高，并且留出 4096 纹理上限的余量 —— 只卡宽会排出一张
//     2048×4864 的图，直接顶穿上限，表现是部分格子永远不显示（这条踩过）。
//   · 先按分类切、再按 PER_PAGE 切，所以同一分类的格子永远连续排在相邻页。
//   · 结尾的 over-limit 自检会**让脚本自己失败**，不靠人看日志。
//
// 本脚本不联网：所有输入都在 node_modules 与 --upstream 里。

import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const argv = process.argv.slice(2);
const opt = (name, dflt) => {
  const i = argv.indexOf('--' + name);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : dflt;
};
const ROOT = path.resolve(opt('root', process.cwd()));
const PREFIX = opt('prefix', 'src/emoji');
const require = createRequire(path.join(ROOT, 'noop.js')); // 从目标工程一侧解析依赖

const die = (m, h) => {
  console.error(`FAIL ${m}\n     ${h}`);
  process.exit(1);
};

let sharp;
try {
  sharp = require('sharp');
} catch {
  die('目标工程 require 不到 sharp', 'npm i -D sharp');
}

const ZH = path.join(ROOT, 'node_modules/emoji-picker-react/dist/data/emojis-zh.json');
if (!fs.existsSync(ZH)) die('缺上游中文词表', `${ZH}\n     装 emoji-picker-react@5，或先用 install.mjs 落预制数据（那就无需重拼）`);

const SRC = opt('upstream', path.join(ROOT, 'node_modules/emoji-datasource-apple/img/apple/64'));
if (!fs.existsSync(SRC)) die('缺上游位图目录', `${SRC}\n     npm i -D emoji-datasource-apple@16，或 --upstream <img/apple/64> 指过去`);

const OUT = path.join(ROOT, PREFIX, 'atlas');
const CELL = Number(opt('cell', 64));
const MAX_EDGE = Number(opt('max-edge', 2048));
const PER_AXIS = Math.floor(MAX_EDGE / CELL);
const PER_PAGE = PER_AXIS * PER_AXIS;

const raw = JSON.parse(fs.readFileSync(ZH, 'utf8'));
const available = new Set(fs.readdirSync(SRC).filter((f) => f.endsWith('.png')).map((f) => f.slice(0, -4)));

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

fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });

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
      composite.push({ input: fs.readFileSync(path.join(SRC, `${unified}.png`)), left: x, top: y });
    });
    const file = `${cat}${page ? `-p${page + 1}` : ''}.webp`;
    await sharp({ create: { width, height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite(composite)
      .webp({ quality: 92, alphaQuality: 100 })
      .toFile(path.join(OUT, file));
    atlases.push({ cat, page, file, cols, rows, width, height, glyphs: slice.length, bytes: fs.statSync(path.join(OUT, file)).size });
  }
}

// 直接按路径读，不走 require：上游包若有 exports 字段会挡住 package.json 子路径。
const appleVersion = (() => {
  const pkg = path.join(SRC, '..', '..', '..', 'package.json');
  try {
    return JSON.parse(fs.readFileSync(pkg, 'utf8')).version;
  } catch {
    return '未知';
  }
})();

fs.writeFileSync(
  path.join(OUT, 'index.json'),
  JSON.stringify({
    generatedAt: new Date().toISOString().slice(0, 10),
    cell: CELL,
    source: `emoji-datasource-apple@${appleVersion} img/apple/64`,
    atlases,
    glyphs
  })
);

const total = Object.keys(glyphs).length;
const over = atlases.filter((a) => a.width > MAX_EDGE || a.height > MAX_EDGE);
console.log(`records with a sprite : ${records.length} / ${Object.keys(raw.emojis).reduce((s, c) => s + raw.emojis[c].length, 0)}`);
console.log(`glyphs atlased        : ${total} (${records.length} base + ${total - records.length} variations)`);
console.log(`atlas pages           : ${atlases.length}, ${atlases.reduce((s, a) => s + a.bytes, 0).toLocaleString()} bytes`);
console.log(`largest edge (both)   : ${Math.max(...atlases.map((a) => Math.max(a.width, a.height)))} px  (limit ${MAX_EDGE})`);
console.log(`over-limit pages      : ${over.length} ${over.length ? 'FAIL' : 'ok'}`);
console.log(`unresolved base       : ${missing.base.length} ${JSON.stringify(missing.base)}`);
console.log(`unresolved variations : ${missing.variation.length}`);
console.log('');
if (over.length) {
  console.log('FAIL 有页超上限 —— 缩小 --max-edge 或减小 --cell 再来。');
  process.exit(1);
}
console.log('下一步：gen-index.mjs（它拿这份 index.json 筛词表），然后 check-data.mjs 收尾。');
