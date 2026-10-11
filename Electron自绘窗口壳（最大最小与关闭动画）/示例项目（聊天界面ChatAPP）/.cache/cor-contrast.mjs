// 中间卡那只角"看不看得见"= 角内(卡片洗色)与角外(窗口底 + 影)的平均亮度差。
// 不用阈值找边:那条弧在 4 级噪声上会乱停(实测过)。改成按已知的超椭圆把角方格里
// 的像素分成"弧内/弧外"两类,分别取均值 —— 角点 (CL,CB)、圆心 (CL+R, CB-R)、n=4。
// 用法: node .cache/cor-contrast.mjs <a.png> [b.png ...]   (每个文件报一行,便于 on/off 比)
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

const R = 32, CL = 324, CB = 790; // 中间卡 rect 左与底(--rows 实测)
const CX = CL + R, CY = CB - R;
const inArc = (x, y) => Math.pow((CX - x) / R, 4) + Math.pow((y - CY) / R, 4) <= 1;

for (const file of process.argv.slice(2)) {
  const img = decode(file);
  const lum = (x, y) => { const i = y * img.stride + x * img.bpp; return 0.299 * img.px[i + 2] + 0.587 * img.px[i + 1] + 0.114 * img.px[i]; };
  let si = 0, ni = 0, so = 0, no = 0, mnI = 999, mxI = -999, mnO = 999, mxO = -999;
  for (let y = CY + 1; y < CB; y++) {
    for (let x = CL; x < CX; x++) {
      const v = lum(x, y);
      if (inArc(x, y)) { si += v; ni++; mnI = Math.min(mnI, v); mxI = Math.max(mxI, v); }
      else { so += v; no++; mnO = Math.min(mnO, v); mxO = Math.max(mxO, v); }
    }
  }
  const mi = si / ni, mo = so / no;
  console.log(JSON.stringify({
    file, arcBox: [CL, CY + 1, CX, CB - 1], nIn: ni, nOut: no,
    meanIn: +mi.toFixed(1), meanOut: +mo.toFixed(1),
    contrast: +Math.abs(mi - mo).toFixed(1),
    rangeIn: [+mnI.toFixed(0), +mxI.toFixed(0)], rangeOut: [+mnO.toFixed(0), +mxO.toFixed(0)]
  }));
}
