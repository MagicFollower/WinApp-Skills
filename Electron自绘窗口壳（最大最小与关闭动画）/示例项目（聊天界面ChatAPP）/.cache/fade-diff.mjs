// 上下渐变模糊带的 on/off 对照:同一段滚动位置,两张截图逐像素比。
// 判据要分层——带内(14..74)应大面积不同,带下对照区(90..150)应几乎为 0;
// 只报"带内不同"会假绿:那可能只是两张图之间页面自己抖了(动画/字体渲染)。
// 用法: node .cache/fade-diff.mjs <on.png> <off.png> [带区y0 带区y1 对照y0 对照y1]
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

const A = decode(process.argv[2]);
const B = decode(process.argv[3]);
// argv[4..7]: 带区 y0 y1、对照区 y0 y1(默认是顶部带)
const a = process.argv;
const X0 = 355, X1 = 880;
const REGIONS = { band: [+a[4] || 14, +a[5] || 74], control: [+a[6] || 90, +a[7] || 150] };

const stat = ([y0, y1]) => {
  let diff = 0, total = 0, sum = 0, max = 0;
  for (let y = y0; y < y1; y++) {
    for (let x = X0; x < X1; x++) {
      const ia = y * A.stride + x * A.ch, ib = y * B.stride + x * B.ch;
      let d = 0;
      for (let k = 0; k < 3; k++) { const t = Math.abs(A.px[ia + k] - B.px[ib + k]); d += t; sum += t; }
      d /= 3;
      if (d > 2) diff += 1;
      if (d > max) max = d;
      total += 1;
    }
  }
  return { total, diffPx: diff, diffPct: +(100 * diff / total).toFixed(1), meanAbs: +(sum / (total * 3)).toFixed(2), maxAbs: max };
};

console.log(JSON.stringify({
  on: { file: process.argv[2], w: A.w, h: A.h, ch: A.ch },
  off: { file: process.argv[3], w: B.w, h: B.h, ch: B.ch },
  xRange: [X0, X1],
  band: stat(REGIONS.band),
  control: stat(REGIONS.control)
}, null, 1));
