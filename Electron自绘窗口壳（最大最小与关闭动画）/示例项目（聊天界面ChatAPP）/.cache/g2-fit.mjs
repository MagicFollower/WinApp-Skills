// 逐行把真实轮廓从 PNG 里取出来,和两条真值曲线比。
// 角弧(半径 r,角点在 (0,0),弧从 (r,0) 到 (0,r)):
//   第 row 行(0<=row<=r)的边界 x:
//     G1 圆弧   : r - sqrt(r^2 - (r-row)^2)
//     G2 超椭圆 : r - r*(1 - ((r-row)/r)^n)^(1/n)
// 取边:从盒子外面朝内走。窗口看 alpha 越过 128(外面只有阴影,alpha 实测 7~34);
// 卡片看亮度第一次突变 >=25(不依赖绝对颜色:左卡深、右卡浅、aurora 彩色)。
// 若 clip 根本没生效,测到的会贴住 G1 那一列,偏差立刻暴露。
import { readFileSync } from 'node:fs';
import { inflateSync } from 'node:zlib';

const [, , file, target] = process.argv;
const buf = readFileSync(file);
let pos = 8, w = 0, h = 0, ctype = 0;
const idat = [];
while (pos < buf.length) {
  const len = buf.readUInt32BE(pos);
  const type = buf.slice(pos + 4, pos + 8).toString('ascii');
  if (type === 'IHDR') { w = buf.readUInt32BE(pos + 8); h = buf.readUInt32BE(pos + 12); ctype = buf[pos + 17]; }
  else if (type === 'IDAT') idat.push(buf.slice(pos + 8, pos + 8 + len));
  else if (type === 'IEND') break;
  pos += 12 + len;
}
const bpp = ctype === 6 ? 4 : 3;
const raw = inflateSync(Buffer.concat(idat));
const stride = w * bpp;
const px = Buffer.alloc(h * stride);
const paeth = (a, b, c) => { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; };
for (let y = 0; y < h; y++) {
  const ft = raw[y * (stride + 1)], src = y * (stride + 1) + 1;
  for (let x = 0; x < stride; x++) {
    const v = raw[src + x];
    const a = x >= bpp ? px[y * stride + x - bpp] : 0;
    const b = y > 0 ? px[(y - 1) * stride + x] : 0;
    const c = x >= bpp && y > 0 ? px[(y - 1) * stride + x - bpp] : 0;
    px[y * stride + x] = (ft === 0 ? v : ft === 1 ? v + a : ft === 2 ? v + b : ft === 3 ? v + ((a + b) >> 1) : v + paeth(a, b, c)) & 0xff;
  }
}
const chan = (x, y, k) => px[y * stride + x * bpp + k];
const lum = (x, y) => 0.299 * chan(x, y, 0) + 0.587 * chan(x, y, 1) + 0.114 * chan(x, y, 2);
const alpha = (x, y) => (bpp === 4 ? chan(x, y, 3) : 255);

const N = 4, R = 32, PAD = 10;
const inG2 = (row) => R - R * Math.pow(1 - Math.pow((R - row) / R, N), 1 / N);
const inG1 = (row) => R - Math.sqrt(R * R - (R - row) * (R - row));
// 像素覆盖按"行中心"算:角弧在切点附近一个行内能走掉 16px,按行上沿比会把 row0 判成偏差 16px。
const C = (row) => row + 0.5;

/** 从 startX 朝 dir 走,返回第一个"进入形状"的 x */
const THRESH = Number(process.env.THRESH) || 25;
function enter(x0, y, dir, mode) {
  for (let i = 0; i < R + 24; i++) {
    const x = x0 + dir * i;
    if (x < 0 || x >= w) return null;
    if (mode === 'alpha') { if (alpha(x, y) >= 128) return x; }
    else {
      const xp = x - dir;
      if (xp < 0 || xp >= w) continue;
      if (Math.abs(lum(x, y) - lum(xp, y)) >= THRESH) return x;
    }
  }
  return null;
}

if (target === 'profile') {
  // 诊断:若干行从头开始的 alpha / 亮度,用来解释拟合里的异常点
  for (const yy of [0, 2, 4, 6, 10, 16]) {
    const a = [], l = [];
    for (let x = 0; x <= 56; x += 4) { a.push(alpha(x, yy)); l.push(Math.round(lum(x, yy))); }
    console.log(`y=${yy}\n  alpha: ${a.join(' ')}\n  lum  : ${l.join(' ')}`);
  }
  process.exit(0);
}

function edge(name, startX, dir, cornerX, rowToY) {
  let maxG2 = 0, maxG1 = 0, n = 0;
  const lines = [];
  for (let row = 0; row <= R; row += 2) {
    const x = enter(startX, rowToY(row), dir, name.includes('alpha') ? 'alpha' : 'lum');
    if (x === null) { lines.push(`  row${String(row).padStart(2)}: 没找到边`); continue; }
    const inset = Math.abs(cornerX - x);
    const dG2 = Math.abs(inset - inG2(C(row))), dG1 = Math.abs(inset - inG1(C(row)));
    maxG2 = Math.max(maxG2, dG2); maxG1 = Math.max(maxG1, dG1); n++;
    if (row % 6 === 0 || row === R)
      lines.push(`  row${String(row).padStart(2)}: 内缩 ${inset.toFixed(1)}  G2 ${inG2(C(row)).toFixed(1)}(差${dG2.toFixed(1)})  G1 ${inG1(C(row)).toFixed(1)}(差${dG1.toFixed(1)})`);
  }
  console.log(`\n[${name}] ${n} 行 -> 对 G2 最大偏差 ${maxG2.toFixed(1)}px,对 G1 最大偏差 ${maxG1.toFixed(1)}px`);
  lines.forEach((l) => console.log(l));
  return { maxG2, maxG1 };
}

const H = h - 1;
const SB_L = PAD, SB_R = PAD + 300, PN_L = w - PAD - 300, PN_R = w - 1 - PAD;
if (target === 'chat') {
  // 中间卡 rect 由 `npx electron . --rows` 给出:chatArea x=324 y=10 w=632 h=780
  const CL = 324, CT = 10, CR = 324 + 632, CB = 10 + 780;
  if (process.env.PROFILE) {
    for (const row of [0, 2, 6, 12, 20, 30, 34]) {
      const y = CB - 1 - row; const l = [];
      for (let x = CL - 14; x <= CL + 40; x += 2) l.push(Math.round(lum(x, y)));
      console.log(`row${String(row).padStart(2)} y=${y}\n  lum: ${l.join(' ')}`);
    }
  }
  edge('中卡 TL', CL - 12, +1, CL, (row) => CT + row);
  edge('中卡 BL', CL - 12, +1, CL, (row) => CB - 1 - row);
  edge('中卡 BR', CR + 20, -1, CR, (row) => CB - 1 - row);
  // 左卡片同一条(对照:用户认为它的角是好的)
  edge('左卡 BL(对照)', 6, +1, SB_L, (row) => H - PAD - row);
} else if (target === 'window') {
  edge('窗口 TL alpha', 0, +1, 0, (row) => row);
  edge('窗口 TR alpha', w - 1, -1, w - 1, (row) => row);
  edge('窗口 BL alpha', 0, +1, 0, (row) => H - row);
  edge('窗口 BR alpha', w - 1, -1, w - 1, (row) => H - row);
} else if (target === 'sidebar') {
  edge('左卡片 TL', 6, +1, SB_L, (row) => PAD + row);
  edge('左卡片 BL', 6, +1, SB_L, (row) => H - PAD - row);
  edge('左卡片 TR', SB_R + 20, -1, SB_R, (row) => PAD + row);
  edge('左卡片 BR', SB_R + 20, -1, SB_R, (row) => H - PAD - row);
} else {
  edge('右卡片 TL', PN_L - 20, +1, PN_L, (row) => PAD + row);
  edge('右卡片 BL', PN_L - 20, +1, PN_L, (row) => H - PAD - row);
  // 右卡片的 TR/BR 不测:它离窗口外形只有 10px 净空,两条弧在同一段行里前后脚出现,
  // 1px 分辨率下分不出"哪一次突变是卡片的边"。同一个 path 串覆盖四角,左右各测一角已足够。
  console.log('\n[右卡片 TR/BR] 主动不测:与窗口外形净空仅 10px,两条弧重叠,判据会假绿');
}
