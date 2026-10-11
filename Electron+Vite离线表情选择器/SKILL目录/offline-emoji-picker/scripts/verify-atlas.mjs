// atlas 像素级对齐判据（需要 sharp + emoji-datasource-apple，属"重拼/换版本"后才要跑的那条）。
//
//   node <skill>/scripts/verify-atlas.mjs [--root <工程>] [--prefix src/emoji]
//                                         [--upstream <img/apple/64 目录>] [--glyphs 1f600,1f44d]
//
// 度量口径三条，都是踩过才定的：
//   · 两边都先 flatten 到白底再只比 RGB —— 直接比 RGBA 均值没意义：64×64 里大部分是
//     全透明像素，alpha=0 底下存的 RGB 是任意的，WebP 往返一次就变。
//   · 主闸门是"与源图的绝对差 < 6/255"；对照组用同页远处的格子，不用相邻格 ——
//     版式是"基形紧跟它的肤色变体"，相邻两格常常是几乎同款的脸（实测 1f600 旁边就是 1f603），
//     用邻居当对照会把对齐正确的 atlas 也判成可疑。
//   · SHIFT_CELLS=1 是反向对照：让每次取值都读右边一格，此时必须大面积 SUSPECT 且退出码 1，
//     否则说明这条判据根本没在区分东西。
//
// 退出码：0 全绿；1 有可疑格；3 前置条件不满足（这轮**没有**跑成，别当通过）。

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

const notRun = (msg, hint) => {
  console.log(`NOT RUN ${msg}`);
  console.log(`        ${hint}`);
  process.exit(3);
};

// 脚本住在技能目录里，裸 import('sharp') 会沿着技能目录往上找 node_modules 而落空。
// 必须从目标工程一侧解析依赖。
let sharp;
try {
  const require = createRequire(path.join(ROOT, 'noop.js'));
  sharp = require('sharp');
} catch {
  notRun('目标工程里 require 不到 sharp', '在工程里 npm i -D sharp（这条只在重拼 atlas 后用得上；日常接入用 check-data.mjs）');
}

const indexPath = path.join(ROOT, PREFIX, 'atlas/index.json');
if (!fs.existsSync(indexPath)) notRun('找不到 atlas/index.json', `先跑 install.mjs --root "${ROOT}"`);

const upstreamOpt = opt('upstream', '');
const SRC =
  upstreamOpt ||
  path.join(ROOT, 'node_modules/emoji-datasource-apple/img/apple/64');
if (!fs.existsSync(SRC))
  notRun(`没有上游位图目录：${SRC}`, 'npm i -D emoji-datasource-apple@16，或用 --upstream <img/apple/64> 指过去');

const atlas = JSON.parse(fs.readFileSync(indexPath, 'utf8'));
const CELL = atlas.cell;
const BG = { r: 255, g: 255, b: 255 };
const SHIFT = Number(process.env.SHIFT_CELLS || 0);
const samples = (opt('glyphs', '') ||
  '1f600,1f604,1f970,1f44d,1f3c1,2764-fe0f,1f98a,1f40e,1f354,1f697,1f3c6,1f4a1,1f5ff').split(',');

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

function farCell(a, g) {
  const fx = (Math.floor(g.x / CELL) + Math.ceil(a.cols / 2)) % a.cols;
  const fy = (Math.floor(g.y / CELL) + Math.ceil(a.rows / 2)) % a.rows;
  return { left: fx * CELL, top: fy * CELL };
}

let worstGood = 0;
let worstRatio = Infinity;
let failures = 0;
let checked = 0;

if (SHIFT) console.log(`（反向对照模式 SHIFT_CELLS=${SHIFT}：这一轮**期望**看到 SUSPECT）`);
console.log('glyph       correct  far-cell  ratio   verdict');
for (const u of samples) {
  const g = atlas.glyphs[u];
  if (!g) {
    console.log(`${u.padEnd(11)} SKIPPED - 不在 atlas 里`);
    continue;
  }
  const srcFile = path.join(SRC, `${u}.png`);
  if (!fs.existsSync(srcFile)) {
    console.log(`${u.padEnd(11)} SKIPPED - 上游没有这张源图（就是那 3 个 has_img_apple=false 之类）`);
    continue;
  }
  checked++;
  const a = atlas.atlases[g.a];
  const page = path.join(ROOT, PREFIX, 'atlas', a.file);
  const src = await flattenedRGB(srcFile, { left: 0, top: 0 });
  const sx = ((((Math.floor(g.x / CELL) + SHIFT) % a.cols) + a.cols) % a.cols) * CELL;
  const good = await flattenedRGB(page, { left: sx, top: g.y });
  const bad = await flattenedRGB(page, farCell(a, g));
  const dGood = meanAbsDiff(src, good);
  const dBad = meanAbsDiff(src, bad);
  const ratio = dBad / Math.max(dGood, 0.001);
  const pass = dGood < 6 && ratio > 3;
  if (!pass) failures++;
  worstGood = Math.max(worstGood, dGood);
  worstRatio = Math.min(worstRatio, ratio);
  console.log(
    `${u.padEnd(11)} ${dGood.toFixed(2).padStart(7)} ${dBad.toFixed(2).padStart(10)} ${ratio.toFixed(1).padStart(7)}x   ${pass ? 'ok' : 'SUSPECT'}`
  );
}

console.log('');
console.log(`glyphs checked             : ${checked}`);
console.log(`worst correct-offset diff  : ${worstGood.toFixed(2)} / 255  (闸门 6)`);
console.log(`smallest correct/far ratio : ${worstRatio.toFixed(1)}x  (闸门 3)`);
console.log(`suspect                    : ${failures}`);
if (!checked) notRun('一个样本都没测到', '样本码位都不在这份 atlas 里，用 --glyphs 传几个真实存在的');
if (failures) {
  console.log(SHIFT ? '\n反向对照如期失败 —— 判据有效。' : '\nFAIL 有格子取错位置：拼版顺序与 index.json 的 x/y 不一致。');
  process.exit(1);
}
console.log('\nALL PASS 0 fail');
