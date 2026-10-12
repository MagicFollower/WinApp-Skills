// 中间卡那只"角"到底看不看得见 = 沿着弧线两侧的亮度差。
// 为什么不用阈值找边:4 级噪声会让游走乱停(实测过);也不用"角方格内外均值":
// 弧外只有 ~86px 薄薄一条,均值会被影的梯度吃掉,给出与肉眼相反的数字。
// 这里按已知的超椭圆(角点 CL,CB、圆心 CX,CY、n=4、R=32)逐行解析出弧的 x,
// 取弧内 2px 与弧外 2px 两点之差 → 每行一个"弧对比度",报 mean/min/p10。
// 用法: node .cache/cor-arc.mjs <a.png> [b.png ...]
import { readFileSync } from 'node:fs';
import { inflateSync } from 'node:zlib';

function decode(file) {
  const buf = readFileSync(file);
  let p = 8, w = 0, h = 0, ctype = 0;
  const idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p);
    const type = buf.toString('ascii', p + 4, p + 8);
    const data = buf.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); ctype = data[9]; }
    else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    p += 12 + len;
  }
  const bpp = ctype === 6 ? 4 : 3;
  const raw = inflateSync(Buffer.concat(idat));
  const stride = w * bpp;
  const px = Buffer.alloc(h * stride);
  const paeth = (a, b, c) => { const q = a + b - c, pa = Math.abs(q - a), pb = Math.abs(q - b), pc = Math.abs(q - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; };
  for (let y = 0; y < h; y++) {
    const ft = raw[y * (stride + 1)], src = y * (stride + 1) + 1;
    const row = px.subarray(y * stride, (y + 1) * stride);
    const prev = y ? px.subarray((y - 1) * stride, y * stride) : Buffer.alloc(stride);
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? row[x - bpp] : 0, b = prev[x], c = x >= bpp ? prev[x - bpp] : 0;
      let v = raw[src + x];
      if (ft === 1) v += a; else if (ft === 2) v += b;
      else if (ft === 3) v += (a + b) >> 1; else if (ft === 4) v += paeth(a, b, c);
      row[x] = v & 255;
    }
  }
  return { w, h, bpp, stride, px };
}

const R = 32;
// 角点列表由环境变量给(默认中间卡那只):"CL:CB,CL:CB,…"
const CORNERS = (process.env.CORNERS || '324:790').split(',').map((s) => s.split(':').map(Number));
const arcX = (CX, CY) => (y) => CX - R * Math.pow(Math.max(0, 1 - Math.pow((y - CY) / R, 4)), 0.25);

for (const file of process.argv.slice(2)) {
  const im = decode(file);
  const lum = (x, y) => { const i = y * im.stride + Math.round(x) * im.bpp; return 0.299 * im.px[i + 2] + 0.587 * im.px[i + 1] + 0.114 * im.px[i]; };
  for (const [CL, CB] of CORNERS) {
    const CX = CL + R, CY = CB - R;
    const f = arcX(CX, CY);
    const d = [], win = [];
    for (let y = CY + 2; y < CB - 1; y++) {
      const xa = f(y);
      if (xa < CL || xa > CX - 1) continue;
      // step = 两侧各 2px 之差(色差型边界靠它);spread = 弧上 ±3px 窗口内的极差
      // —— 描边/内环类方案把边界画在弧"上",两侧采样都落在环里,step 会假小,只有 spread 看得见它。
      d.push(Math.abs(lum(xa - 2, y) - lum(xa + 2, y)));
      let mn = 999, mx = -999;
      for (let k = -3; k <= 3; k++) { const v = lum(xa + k, y); mn = Math.min(mn, v); mx = Math.max(mx, v); }
      win.push(mx - mn);
    }
    const stat = (arr) => {
      const s = arr.slice().sort((a, b) => a - b);
      const q = (p) => +s[Math.min(s.length - 1, Math.floor(s.length * p))].toFixed(1);
      return { mean: +(arr.reduce((a, b) => a + b, 0) / arr.length).toFixed(1), p10: q(0.1), median: q(0.5), min: +s[0].toFixed(1) };
    };
    console.log(JSON.stringify({ file, corner: `${CL},${CB}`, n: d.length, step: stat(d), spread: stat(win) }));
  }
}
