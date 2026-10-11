// 「糊直角」的数值判据:角点方格里、弧外那一块(楔形)本来应该和卡片外的窗口底完全同色
// —— 因为没有任何东西该画在那儿。它一旦和外面不同,就是有图层漏到了圆角裁切之外。
// 用法: node .cache/lk-pts.mjs <lk-00.png> ...   (裁图原点固定 302,690)
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

const OX = 302;
// 每只角:裁图原点 OY + 楔形(弧外、方格内)/ 卡外同排参照 / 弧内洗色 三个页面坐标
const CORNER = process.argv[2];
const SPEC = {
  BL: { OY: 690, wedge: [328, 786], outside: [314, 786], inside: [346, 776] },
  TL: { OY: 4, wedge: [328, 14], outside: [314, 14], inside: [346, 24] }
}[CORNER];
if (!SPEC) { console.error('用法: node .cache/lk-pts.mjs <BL|TL> <png...>'); process.exit(2); }
const { OY } = SPEC;
const PTS = SPEC;
const mean = (im, x, y) => {
  // 3×3 均值,避开抗锯齿单像素
  let s = 0, n = 0;
  for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
    const i = (y + dy - OY) * im.stride + (x - OX + dx) * im.bpp;
    s += 0.299 * im.px[i + 2] + 0.587 * im.px[i + 1] + 0.114 * im.px[i]; n++;
  }
  return s / n;
};
const rgb = (im, x, y) => { const i = (y - OY) * im.stride + (x - OX) * im.bpp; return [im.px[i], im.px[i + 1], im.px[i + 2]]; };

for (const file of process.argv.slice(3)) {
  const im = decode(file);
  const v = { wedge: mean(im, ...PTS.wedge), outside: mean(im, ...PTS.outside), inside: mean(im, ...PTS.inside) };
  const leak = Math.abs(v.wedge - v.outside);
  console.log(JSON.stringify({
    file, corner: CORNER,
    wedge: +v.wedge.toFixed(1), outside: +v.outside.toFixed(1), inside: +v.inside.toFixed(1),
    wedgeRGB: rgb(im, ...PTS.wedge),
    leak: +leak.toFixed(1),
    verdict: Number.isNaN(leak) ? '读数失败' : leak < 2 ? '无漏(楔形=窗口底)' : '有漏(楔形≠窗口底)'
  }));
}
