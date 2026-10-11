// 从位图里量白色 pill 的包围盒 —— 水珠是 canvas 画的,DOM 读不到,只能打像素这一层。
// 用法: node .cache/tabs-pill.mjs <crop.png> <cropX> <cropY> [期望x0 期望x1]
// 期望区间传被选中那一格的 item rect(页面 CSS px);给了就直接判"pill 有没有盖住这一格"。
import fs from 'node:fs';
import zlib from 'node:zlib';

function decode(file) {
  const buf = fs.readFileSync(file);
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
  return { w, h, ch, stride, px: out };
}

const [file, cx, cy] = process.argv.slice(2);
const want = process.argv.slice(5).map(Number);
const img = decode(file);
const nearWhite = (i) => {
  const r = img.px[i], g = img.px[i + 1], b = img.px[i + 2];
  return Math.min(r, g, b) >= 225 && Math.max(r, g, b) - Math.min(r, g, b) <= 18;
};

let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9, n = 0;
for (let y = 0; y < img.h; y++) {
  for (let x = 0; x < img.w; x++) {
    if (!nearWhite(y * img.stride + x * img.ch)) continue;
    n++;
    if (x < x0) x0 = x;
    if (x > x1) x1 = x;
    if (y < y0) y0 = y;
    if (y > y1) y1 = y;
  }
}
const page = { x0: +(+cx + x0).toFixed(1), x1: +(+cx + x1 + 1).toFixed(1), y0: +(+cy + y0).toFixed(1), y1: +(+cy + y1 + 1).toFixed(1) };
console.log(JSON.stringify({
  file, imgSize: `${img.w}x${img.h}`, ch: img.ch, whitePx: n,
  pill: { ...page, w: +(page.x1 - page.x0).toFixed(1), h: +(page.y1 - page.y0).toFixed(1) },
  want: want.length === 2 ? {
    x0: want[0], x1: want[1],
    dLeft: +(page.x0 - want[0]).toFixed(1), dRight: +(page.x1 - want[1]).toFixed(1)
  } : null
}, null, 1));
