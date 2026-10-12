// 在过滤行带内数"水珠色"像素:#2bb673 / #1fb6a6 / #3b9df8 三档各多少。
// 侧栏底是 --sidebar-bg 的不透明紫色渐变,所以这三档只可能来自 GooeyNav 的水珠。
// 用法: node .cache/nav-tint.mjs <png>
import fs from 'node:fs';
import zlib from 'node:zlib';

const buf = fs.readFileSync(process.argv[2]);
let p = 8, w = 0, h = 0, colorType = 0;
const idat = [];
while (p < buf.length) {
  const len = buf.readUInt32BE(p);
  const type = buf.toString('ascii', p + 4, p + 8);
  const data = buf.subarray(p + 8, p + 8 + len);
  if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); colorType = data[9]; }
  else if (type === 'IDAT') idat.push(data);
  else if (type === 'IEND') break;
  p += 12 + len;
}
const ch = { 0: 1, 2: 3, 4: 2, 6: 4 }[colorType];
const raw = zlib.inflateSync(Buffer.concat(idat));
const stride = w * ch;
const out = Buffer.alloc(h * stride);
for (let y = 0, q = 0; y < h; y++) {
  const f = raw[q++];
  const line = raw.subarray(q, q + stride); q += stride;
  const cur = out.subarray(y * stride, (y + 1) * stride);
  const prev = y ? out.subarray((y - 1) * stride, y * stride) : Buffer.alloc(stride);
  for (let x = 0; x < stride; x++) {
    const a = x >= ch ? cur[x - ch] : 0, b = prev[x], c = x >= ch ? prev[x - ch] : 0;
    let v = line[x];
    if (f === 1) v += a; else if (f === 2) v += b;
    else if (f === 3) v += (a + b) >> 1;
    else if (f === 4) {
      const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c);
      v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
    cur[x] = v & 255;
  }
}
const TINTS = { glow1: [0x2b, 0xb6, 0x73], glow2: [0x1f, 0xb6, 0xa6], glow3: [0x3b, 0x9d, 0xf8] };
const X0 = 40, X1 = 330, Y0 = 60, Y1 = 112, T = Number(process.argv[3] ?? 70);
const hits = {}; const bbox = {};
for (const k of Object.keys(TINTS)) { hits[k] = 0; bbox[k] = null; }
let white = 0, total = 0;
for (let y = Y0; y <= Y1 && y < h; y++) {
  for (let x = X0; x <= X1 && x < w; x++) {
    const i = y * stride + x * ch;
    const r = out[i], g = out[i + 1], b = out[i + 2];
    total++;
    if (r > 235 && g > 235 && b > 235) white++;
    for (const [k, [tr, tg, tb]] of Object.entries(TINTS)) {
      if (Math.abs(r - tr) + Math.abs(g - tg) + Math.abs(b - tb) <= T) {
        hits[k]++;
        const bb = bbox[k] || { x0: x, x1: x, y0: y, y1: y };
        bb.x0 = Math.min(bb.x0, x); bb.x1 = Math.max(bb.x1, x);
        bb.y0 = Math.min(bb.y0, y); bb.y1 = Math.max(bb.y1, y);
        bbox[k] = bb;
      }
    }
  }
}
console.log(JSON.stringify({ file: process.argv[2], band: [X0, Y0, X1, Y1], tol: T, total, whitePx: white, hits, bbox }, null, 1));
