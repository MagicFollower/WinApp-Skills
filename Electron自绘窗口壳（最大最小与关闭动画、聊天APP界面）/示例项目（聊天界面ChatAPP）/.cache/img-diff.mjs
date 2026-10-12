// 两张 PNG 在给定矩形内的逐像素差:用于"这一帧到底有没有在动"的判定。
// 数颜色命中(如 glow 命中 0)只能说明"没看到那个色",要坐实"确实有东西在动",
// 得拿同一区域的 on/off 两帧做差——否则 0 命中也可能只是根本没动画。
// 用法: node .cache/img-diff.mjs <a.png> <b.png> x0 x1 y0 y1
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

const [fa, fb, sx0, sx1, sy0, sy1] = process.argv.slice(2);
const A = decode(fa), B = decode(fb);
const X0 = +sx0, X1 = +sx1, Y0 = +sy0, Y1 = +sy1;
let diff = 0, total = 0, sum = 0, max = 0;
// 变化像素在 b 帧里的颜色:饱和(通道差 > 40)说明是彩色的,近零说明是白/灰
let sat = 0, sr = 0, sg = 0, sb = 0;
const cols = new Array(X1 - X0).fill(0);
for (let y = Y0; y < Y1; y++) {
  for (let x = X0; x < X1; x++) {
    const ia = y * A.stride + x * A.ch, ib = y * B.stride + x * B.ch;
    let d = 0;
    for (let k = 0; k < 3; k++) d += Math.abs(A.px[ia + k] - B.px[ib + k]);
    d /= 3;
    sum += d;
    if (d > 2) {
      diff += 1;
      cols[x - X0] += 1;
      const r = B.px[ib], g = B.px[ib + 1], b = B.px[ib + 2];
      sr += r; sg += g; sb += b;
      if (Math.max(r, g, b) - Math.min(r, g, b) > 40) sat += 1;
    }
    if (d > max) max = d;
    total += 1;
  }
}
console.log(JSON.stringify({
  a: fa, b: fb, rect: [X0, X1, Y0, Y1], size: [A.w, A.h, B.w, B.h],
  total, diffPx: diff, diffPct: +(100 * diff / total).toFixed(2),
  meanAbs: +(sum / total).toFixed(2), maxAbs: +max.toFixed(1),
  diffCols: cols.filter((n) => n > 0).length,
  diffMeanRGB: diff ? [Math.round(sr / diff), Math.round(sg / diff), Math.round(sb / diff)] : null,
  diffSaturatedPx: sat, diffSaturatedPct: diff ? +(100 * sat / diff).toFixed(2) : 0
}, null, 1));
