// 曲率实测台：报告中所有曲率/半径数字都出自本文件的输出
// 运行：node "demo/曲率校验.mjs"
//
// 两个参照系，本文件严格分开：
//  [整块] 中心在原点的超椭圆 |x/a|^n + |y/a|^n = a^n，第一象限弧从 (a,0) 到 (0,a)。
//         θ=0 端是"边的中点"，θ=45° 是"角"。
//  [角部] 盒角在原点、圆心在 (r,r) 的圆角补丁，|x-r|^n + |y-r|^n = r^n，
//         弧从 (r,0)（与底边相切）到 (0,r)（与左边相切）。
//  两者靠平移+镜像全等，κ 剖面完全一样；本文件把 [整块] 的第一象限弧直接当作
//  [角部] 的镜像来量，所以 θ=0 端一律解释为"与直边的接缝"。

const f6 = (v) => v.toFixed(6);
const fx = (v, d = 3) => v.toFixed(d);

function kappa(curve, t, h) {
  const a = curve(t - h), b = curve(t), c = curve(t + h);
  const d1x = (c[0] - a[0]) / (2 * h), d1y = (c[1] - a[1]) / (2 * h);
  const d2x = (c[0] - 2 * b[0] + a[0]) / (h * h), d2y = (c[1] - 2 * b[1] + a[1]) / (h * h);
  return Math.abs(d1x * d2y - d1y * d2x) / Math.pow(d1x * d1x + d1y * d1y, 1.5);
}
function arcLenFrom0(curve, t, n = 400000) {
  const dt = t / n;
  let L = 0, prev = curve(0);
  for (let i = 1; i <= n; i++) {
    const p = curve(i * dt);
    L += Math.hypot(p[0] - prev[0], p[1] - prev[1]);
    prev = p;
  }
  return L;
}
// 已知弧长反解参数：对单调的 s(t) 二分
function tAtArcLen(curve, target, lo, hi, n = 60) {
  for (let i = 0; i < n; i++) {
    const mid = (lo + hi) / 2;
    if (arcLenFrom0(curve, mid, 4000) < target) lo = mid; else hi = mid;
  }
  return (lo + hi) / 2;
}
const lineCurve = (t) => [t, 0];
const circleCurve = (r) => (t) => [r * Math.cos(t), r * Math.sin(t)];
const sec = (t) => console.log('\n===== ' + t + ' =====');
const R = 24;

// 角部超椭圆补丁：圆心 (R,R)，半径 R，接缝 A=(R,0) 与底边相切
// 用 θ 参数化：P(θ) = (R - R·cos^{2/n}θ, R - R·sin^{2/n}θ)，θ=0 → (0,R) 是竖直边接缝
// 这里取镜像写法，让 θ=0 落在水平边接缝上：
const patchSuper = (n, r) => (th) => [r - r * Math.pow(Math.cos(th), 2 / n), r * Math.pow(Math.sin(th), 2 / n)];
// 平端三次贝塞尔角部：A=(r,0) 切线水平, D=(0,r) 切线竖直, P1=P2=盒角 (0,0)
const flatCubic = (r) => (u) => {
  const p0 = [r, 0], p1 = [0, 0], p2 = [0, 0], p3 = [0, r], m = 1 - u;
  return [
    m * m * m * p0[0] + 3 * m * m * u * p1[0] + 3 * m * u * u * p2[0] + u * u * u * p3[0],
    m * m * m * p0[1] + 3 * m * m * u * p1[1] + 3 * m * u * u * p2[1] + u * u * u * p3[1],
  ];
};
// 正圆弧角部的 κ 恒为 1/r，本文件直接用 circleCurve(r) 量（圆心位置不影响曲率与弧长）

sec('0 台架自校验：圆 r=24 真值 kappa=1/24=' + f6(1 / 24));
for (const h of [1e-2, 1e-3, 1e-4, 1e-5, 1e-6]) {
  console.log('  h=' + h.toExponential(0) + '  圆κ=' + f6(kappa(circleCurve(24), 0.7, h)) + '  直线κ=' + kappa(lineCurve, 3, h).toExponential(2));
}

sec('1 圆弧角部（border-radius 默认形状）：接缝两侧 κ 与跳变');
console.log('  r(px)   直线侧κ    圆弧侧κ     Δκ        1/Δκ');
for (const r of [8, 12, 16, 24, 32, 48]) {
  const kA = kappa(circleCurve(r), 0.6, 1e-4), kL = kappa(lineCurve, 0.6, 1e-4);
  console.log('  ' + fx(r, 0) + '      ' + f6(kL) + '   ' + f6(kA) + '   ' + f6(kA - kL) + '   ' + fx(1 / (kA - kL), 1));
}

sec('2 超椭圆角部 κ 剖面（补丁 r=24；θ=0 是直边接缝，θ=45° 是角的正中）');
const ths = [1e-3, 1e-2, 0.05, 0.15, Math.PI / 4, 1.4208, 1.5708 - 1e-3];
console.log('  n     ' + ths.map((t) => 'θ=' + fx(t, 3)).join('  '));
for (const n of [2, 2.5, 3, 4, 5, 6, 8, 12]) {
  const cv = patchSuper(n, R);
  console.log('  ' + fx(n, 1) + '    ' + ths.map((t) => f6(kappa(cv, t, 1e-5))).join('  '));
}

sec('2b 端点归零趋势与峰值位置');
for (const n of [2, 2.5, 3, 4, 5, 8]) {
  const cv = patchSuper(n, R);
  const edge = [1e-2, 3e-3, 1e-3, 3e-4].map((t) => f6(kappa(cv, t, t / 20)));
  let peak = 0, at = 0;
  for (let t = 1e-3; t <= Math.PI / 2 - 1e-3; t += 2e-4) {
    const k = kappa(cv, t, 1e-5);
    if (k > peak) { peak = k; at = t; }
  }
  const k1 = kappa(cv, 1e-2, 5e-4);
  console.log('  n=' + fx(n, 1) + '  接缝侧 ' + edge.join(' → ') + ' | 峰值κ=' + f6(peak) + ' @θ=' + fx(at * 180 / Math.PI, 2) + '° | 峰/端=' + (peak / Math.max(k1, 1e-15)).toFixed(1) + '× | 峰值处等效半径=' + fx(1 / peak, 2) + 'px');
}

sec('2c κ 随弧长 s 的衰减斜率（解析预测：超椭圆 κ∝s^(n-2)，平端贝塞尔 ∝s^1，圆弧 ∝s^0）');
console.log('  曲线        s=0.25      0.5       1.0       2.0      拟合斜率(0.25~2)  理论');
for (const tag of ['sup3', 'sup4', 'sup5', 'sup8', 'cubic', 'arc']) {
  const cv = tag === 'cubic' ? flatCubic(R) : tag === 'arc' ? circleCurve(R) : patchSuper(Number(tag.slice(3)), R);
  const tmax = tag === 'cubic' ? 1 : Math.PI / 2;
  const row = [];
  const pts = [];
  for (const s of [0.25, 0.5, 1, 2]) {
    const t = tAtArcLen(cv, s, 1e-6, tmax);
    const k = kappa(cv, Math.max(t, 1e-6), Math.max(t / 20, 1e-6));
    row.push(k.toExponential(3)); pts.push([Math.log(s), Math.log(Math.max(k, 1e-15))]);
  }
  const n = pts.length;
  const mx = pts.reduce((a, p) => a + p[0], 0) / n, my = pts.reduce((a, p) => a + p[1], 0) / n;
  let num = 0, den = 0;
  for (const p of pts) { num += (p[0] - mx) * (p[1] - my); den += (p[0] - mx) ** 2; }
  const slope = num / den;
  const theory = tag === 'cubic' ? 1 : tag === 'arc' ? 0 : Number(tag.slice(3)) - 2;
  const gnote = tag.startsWith('sup')
    ? (Number(tag.slice(3)) > 3 ? '   (斜率>1 → dκ/ds→0，G3 也成立)' : '   (斜率=1 → dκ/ds 为有限非零，止于 G2)')
    : tag === 'cubic' ? '   (斜率≈1 → 止于 G2，不是 G3)' : '   (斜率=0 → 接缝两侧 κ 不等，止于 G1)';
  console.log('  ' + tag.padEnd(10) + row.map((v) => v.padEnd(11)).join('') + '  ' + fx(slope, 3) + '          ' + fx(theory, 1) + gnote);
}

sec('3 平端三次贝塞尔角部 κ 剖面（P1=P2=盒角，r=24）');
const cbz = flatCubic(R);
console.log('  u        κ            坐标');
for (const u of [0.001, 0.01, 0.05, 0.15, 0.3, 0.5, 0.7, 0.85, 0.99, 0.999]) {
  const p = cbz(u);
  console.log('  ' + fx(u, 3) + '    ' + f6(kappa(cbz, u, 1e-5)) + '   (' + fx(p[0], 3) + ', ' + fx(p[1], 3) + ')');
}
console.log('  弧长=' + fx(arcLenFrom0(cbz, 1, 200000), 3) + 'px，端点 κ(u=0.001)=' + f6(kappa(cbz, 1e-3, 1e-4)));

sec('3b 平端贝塞尔 与 超椭圆补丁 的几何差（沿圆心 (24,24) 射线比距离，正=贝塞尔更靠外/更方）');
const center = [R, R];
function cubicRadial(phi) {
  let lo = 0, hi = 1;
  for (let i = 0; i < 80; i++) {
    const mid = (lo + hi) / 2;
    const p = cbz(mid);
    const ang = Math.atan2(Math.abs(center[1] - p[1]), Math.abs(center[0] - p[0]));
    if (ang > phi) lo = mid; else hi = mid;
  }
  const p = cbz((lo + hi) / 2);
  return Math.hypot(center[0] - p[0], center[1] - p[1]);
}
const patchRadial = (n, phi) => R / Math.pow(Math.pow(Math.cos(phi), n) + Math.pow(Math.sin(phi), n), 1 / n);
const degs = [5, 15, 25, 35, 45, 55, 65, 75, 85];
console.log('  n     ' + degs.map((d) => String(d) + '°').join('   ') + '  最大|偏差|px   RMS');
for (const n of [2, 3, 4, 5, 6, 8]) {
  let worst = 0, sum = 0; const cells = [];
  for (const d of degs) {
    const g = cubicRadial(d * Math.PI / 180) - patchRadial(n, d * Math.PI / 180);
    worst = Math.max(worst, Math.abs(g)); sum += g * g;
    cells.push((g >= 0 ? '+' : '') + fx(g, 3));
  }
  console.log('  ' + fx(n, 1) + '    ' + cells.join('  ') + '   ' + fx(worst, 3) + '        ' + fx(Math.sqrt(sum / degs.length), 3));
}
sec('3c 平端贝塞尔在中缝(45°)处等效的超椭圆指数 n（由 x/r = 1-2^(-1/n) 反解）');
{
  const p = cbz(0.5);
  const ratio = p[0] / R;
  const nEq = -Math.log(2) / Math.log(1 - ratio);
  console.log('  u=0.5 点=(' + fx(p[0], 4) + ', ' + fx(p[1], 4) + ')，x/r=' + fx(ratio, 5) + ' → 等效 n=' + fx(nEq, 3));
  console.log('  对照（同 x/r 口径）：正圆弧 n=2 → ' + fx(1 - Math.pow(2, -0.5), 5) + '，n=3 → ' + fx(1 - Math.pow(2, -1 / 3), 5) + '，n=4 → ' + fx(1 - Math.pow(2, -0.25), 5) + '，n=5 → ' + fx(1 - Math.pow(2, -0.2), 5) + '，n=6 → ' + fx(1 - Math.pow(2, -1 / 6), 5) + '，n=8 → ' + fx(1 - Math.pow(2, -0.125), 5));
  console.log('  → 与 3b 的射线偏差表交叉验证：3b 中偏差最小(最大|偏差|=' + fx(0.151, 3) + 'px)的 n=5 是否与上面的等效 n 一致');
}

sec('4 折线逼近角部（r=24）：由 κmax 反解最大弦长与样本数（弦高 ε=κs²/8）');
console.log('  曲线        κ峰值        弧长px    弦长@ε=0.10  弦长@ε=0.05   每角样本@0.05  四角合计');
for (const tag of ['arc', 'sup4', 'sup5', 'sup6', 'sup8', 'cubic']) {
  const cv = tag === 'cubic' ? flatCubic(R) : tag === 'arc' ? circleCurve(R) : patchSuper(Number(tag.slice(3)), R);
  const tmax = tag === 'cubic' ? 1 : Math.PI / 2;
  let kmax = 0;
  for (let t = 1e-4; t <= tmax - 1e-4; t += tmax / 4000) kmax = Math.max(kmax, kappa(cv, t, 1e-5));
  const s10 = Math.sqrt(8 * 0.10 / kmax), s05 = Math.sqrt(8 * 0.05 / kmax);
  const L = arcLenFrom0(cv, tmax, 200000);
  console.log('  ' + tag.padEnd(10) + f6(kmax) + '    ' + fx(L, 3) + '      ' + fx(s10, 3) + '         ' + fx(s05, 3) + '       ' + Math.ceil(L / s05) + '               ' + Math.ceil(L / s05) * 4);
}

sec('5 椭圆角部（水平半径 a ≠ 垂直半径 b）接缝 κ：与水平边相切处 κ=a/b²，与竖直边相切处 κ=b/a²');
console.log('  写法                          a      b      水平边接缝κ  等效半径  竖直边接缝κ  等效半径  κ不对称比');
for (const [tag, a, b] of [['border-radius:50% @200x80', 100, 40], ['border-radius:50% @80x80', 40, 40], ['border-radius:50% @320x40', 160, 20], ['border-radius:24px 8px', 24, 8]]) {
  const kH = a / (b * b), kV = b / (a * a);
  console.log('  ' + tag.padEnd(28) + fx(a, 1) + '   ' + fx(b, 1) + '     ' + f6(kH) + '    ' + fx(1 / kH, 2) + '     ' + f6(kV) + '   ' + fx(1 / kV, 1) + '     ' + fx(kH / kV, 2) + '×');
}

sec('6 f 缩放（盒 100x40，四角同值 r；纵向两侧半径和 2r 与高 40 比）');
console.log('  指定r  2r    f=min(1,40/2r)  用过值  请求κ=1/r  实际κ     κ偏差      结论');
for (const r of [10, 15, 20, 24, 25, 30, 40, 60]) {
  const f = Math.min(1, 40 / (2 * r)), used = r * f;
  console.log('  ' + fx(r, 0) + '     ' + fx(2 * r, 0) + '     ' + fx(f, 4) + '         ' + fx(used, 2) + '    ' + f6(1 / r) + '   ' + f6(1 / used) + '  ' + f6(1 / used - 1 / r) + '   ' + (used < r ? '写过值被压小' : '按字面生效'));
}

sec('7 外角 24 内角 r_in=r_out-p 的 κ 对照（几何差规则）');
console.log('  p    r_in   κ内        κ外        Δκ        κ内/κ外');
for (const p of [2, 4, 6, 8, 12, 16, 20]) {
  const ri = 24 - p;
  console.log('  ' + fx(p, 0) + '    ' + fx(ri, 0) + '   ' + f6(1 / ri) + '  ' + f6(1 / 24) + '  ' + f6(1 / ri - 1 / 24) + '    ' + fx((1 / ri) / (1 / 24), 3));
}

sec('8 iOS 风格整块 squircle：边长 1 时 n 值对应的"角部切入量"与最大 κ');
console.log('  n     边中点κ(→0?)   45°角κ    角处等效半径/边长   对角线上点到盒角距离/边长');
for (const n of [2, 4, 5, 6, 8]) {
  const a = 0.5, cv = patchSuper(n, a);
  const kSeam = kappa(cv, 1e-3, 1e-5), kPeak = kappa(cv, Math.PI / 4, 1e-5);
  console.log('  ' + fx(n, 1) + '    ' + f6(kSeam) + '    ' + f6(kPeak) + '      ' + fx(1 / kPeak, 4) + '              ' + fx(a * (1 - Math.pow(2, -1 / n)) * Math.SQRT2, 5));
}

// ---------- 9. 整块 squircle 裁切：点数随边长怎么长（场景 4） ----------
sec('9 整块 squircle（边长 S，半轴 a=S/2）：折线样本数需求');
console.log('  n  边长S   κ峰值       每角弧长px   @ε=0.50px 弦长/样本  四角   @ε=0.25px 弦长/样本  四角');
for (const n of [4, 5]) {
  for (const S of [32, 48, 96, 128, 256, 512]) {
    const a = S / 2, cv = patchSuper(n, a);
    let kmax = 0;
    for (let t = 1e-3; t <= Math.PI / 2 - 1e-3; t += 1e-4) kmax = Math.max(kmax, kappa(cv, t, 1e-5));
    const L = arcLenFrom0(cv, Math.PI / 2, 60000);
    const s50 = Math.sqrt(8 * 0.50 / kmax), s25 = Math.sqrt(8 * 0.25 / kmax);
    const q50 = Math.ceil(L / s50), q25 = Math.ceil(L / s25);
    console.log('  ' + fx(n, 1) + ' ' + fx(S, 0) + '    ' + f6(kmax) + '   ' + fx(L, 3) + '       ' + fx(s50, 3) + '/' + q50 + '     ' + q50 * 4 + '       ' + fx(s25, 3) + '/' + q25 + '     ' + q25 * 4);
  }
}
console.log('  读法：κ峰值 ∝ 1/S，弧长 ∝ S ⇒ 样本数 ∝ √S。S 从 32 到 512（16×）样本数应约涨 4×。');

// ---------- 10. 法向偏移 vs "内角 = 外角 − 边框宽"（场景 5/7 的几何核心） ----------
// 参照系：角部补丁圆心在 (R,R)，半径 rad；rad=R 时 θ=0 → (R,0) 与水平边相切
function patchAt(n, cx, cy, rad) {
  const m = 2 / n;
  return function (t) {
    const c = Math.max(0, Math.cos(t)), s = Math.max(0, Math.sin(t));
    return [cx - rad * Math.pow(c, m), cy - rad * Math.pow(s, m)];
  };
}
// 真法向偏移：sign=+1 走外法向（变大），sign=-1 走内法向（描边内边界 / 内容盒）
function normalOffset(base, center, d, sign) {
  return function (t) {
    const h = 1e-6, p = base(t), a = base(t - h), b = base(t + h);
    let tx = (b[0] - a[0]) / (2 * h), ty = (b[1] - a[1]) / (2 * h);
    const L = Math.hypot(tx, ty) || 1; tx /= L; ty /= L;
    let nx = -ty, ny = tx;
    if ((p[0] - center[0]) * nx + (p[1] - center[1]) * ny < 0) { nx = -nx; ny = -ny; }
    return [p[0] + sign * d * nx, p[1] + sign * d * ny];
  };
}
function radialAt(f, phi, tmax) {   // 沿圆心射线取距离（两条曲线都对该圆心星形）
  let lo = 0, hi = tmax;
  for (let i = 0; i < 60; i++) {
    const mid = (lo + hi) / 2, p = f(mid);
    if (Math.atan2(-(p[1]), -(p[0])) < phi) lo = mid; else hi = mid;   // 以圆心为原点看第二象限方向
  }
  const p = f((lo + hi) / 2);
  return Math.hypot(p[0], p[1]);
}
sec('10 外角 R=24、圆心 (24,24)：真法向内偏移 w 后的内边界，与"天真写法(同 n，半径 R−w)"的射线偏差 px');
console.log('  n     w=1       w=2       w=3       w=4       w=8      w=12     最大|偏差|处的 w');
for (const n of [2, 3, 4, 5, 6, 8]) {
  const base = patchAt(n, R, R, R);
  const cells = [];
  let worstAt = 1, worst = 0;
  for (const w of [1, 2, 3, 4, 8, 12]) {
    const tru = normalOffset(base, [R, R], w, -1);
    const naiv = patchAt(n, R, R, R - w);
    let dev = 0;
    for (let t = 1e-3; t <= Math.PI / 2 - 1e-3; t += 2e-3) {
      const p = tru(t);
      const phi = Math.atan2(-(p[1] - R), -(p[0] - R));
      const dt = Math.hypot(p[0] - R, p[1] - R);
      const dn = radialAt(function (tt) { const q = naiv(tt); return [q[0] - R, q[1] - R]; }, phi, Math.PI / 2);
      dev = Math.max(dev, Math.abs(dt - dn));
    }
    cells.push((dev >= 0 ? ' ' : '') + fx(dev, 3));
    if (dev > worst) { worst = dev; worstAt = w; }
  }
  console.log('  ' + fx(n, 1) + '    ' + cells.join('  ') + '     峰值 ' + fx(worst, 3) + 'px @w=' + worstAt + (n === 2 ? '   ← 圆弧对照：应≈0（等距线仍是同心圆弧）' : ''));
}

// ---------- 11. 描边/模糊之后：接缝 κ 还剩多少，内偏移什么时候自交（场景 5/6） ----------
sec('11a 外偏移 d 之后的接缝侧 κ（圆弧应恰好等于 1/(R+d)，跳变被稀释但不消除；超椭圆应仍→0）');
console.log('  形状          d    直边侧κ    偏移后接缝κ   预测(圆弧 1/(R+d))   跳变是否还在');
for (const [tag, n] of [['圆弧 n=2', 2], ['超椭圆 n=4', 4], ['超椭圆 n=5', 5]]) {
  for (const d of [6, 12, 24, 48]) {
    const base = patchAt(n, R, R, R);
    const off = normalOffset(base, [R, R], d, +1);
    const k = kappa(off, 1e-3, 1e-5);
    console.log('  ' + tag + '  ' + fx(d, 0) + '     0.000000    ' + k.toExponential(4) + '       ' + f6(1 / (R + d)) + '        ' + (k > 1e-3 ? '在，量级 1/(R+d)=' + fx(1 / k, 2) + 'px' : '基本为 0'));
  }
}
sec('11b 内偏移的安全上限：min 曲率半径 = 1/κ峰值，超过就自交（尖点/evolute）');
console.log('  形状            κ峰值      1/κ峰值(px)=最大安全内偏移   外偏移 24px 后的 κ峰值');
for (const [tag, n] of [['圆弧 R=24', 2], ['超椭圆 n=3', 3], ['n=4', 4], ['n=5', 5], ['n=6', 6], ['平端贝塞尔', 0]]) {
  let f, tmax;
  if (n === 2) { f = patchAt(2, R, R, R); tmax = Math.PI / 2; }
  else if (n === 0) { f = flatCubic(R); tmax = 1; }
  else { f = patchAt(n, R, R, R); tmax = Math.PI / 2; }
  let kmax = 0;
  for (let t = 1e-3; t <= tmax - 1e-3; t += 1e-4) kmax = Math.max(kmax, kappa(f, t, 1e-5));
  console.log('  ' + tag.padEnd(12) + f6(kmax) + '      ' + fx(1 / kmax, 3) + '                    ' + '(外偏移让曲率变小，见 11a)');
}
console.log('  对照含义：圆弧 r=24 允许内偏移 24px 才自交；超椭圆 n=5 只允许 ' + fx(1 / (() => { let k = 0; const f = patchAt(5, R, R, R); for (let t = 1e-3; t <= Math.PI / 2 - 1e-3; t += 1e-4) k = Math.max(k, kappa(f, t, 1e-5)); return k; })(), 3) + 'px ⇒ 粗描边/内阴影更早崩。');

// ---------- 12. 形变与动画：插值过程中 κ 怎么走（场景 8/9） ----------
sec('12a 滑动指示器：高度 32、写死 r=12，宽度变小时 f 缩放什么时候咬住');
console.log('  宽度w   2r 与 w    f=min(1,w/24)   用过r    κ=1/用过r   相对请求κ=0.0833  导数是否连续');
for (const w of [40, 36, 32, 28, 24, 20, 16, 12, 8]) {
  const f = Math.min(1, w / 24), used = 12 * f, k = 1 / used;
  console.log('  ' + fx(w, 0) + '      ' + fx(24, 0) + (w < 24 ? ' > w 咬住' : ' ≤ w 未咬') + '    ' + fx(f, 4) + '      ' + fx(used, 2) + '    ' + f6(k) + '      ' + fx(k / (1 / 12) - 1, 3) + '        ' + (w < 24 ? '不连续（κ 被钉成 2/w）' : '连续'));
}
console.log('  药丸对照（r=9999，h=32）：用过 r = min(w,h)/2 ⇒ 也有咬合点，只是位置在 w=h 而不是 w=2r');
console.log('  宽度w   用过r=min(w,32)/2   κ=1/用过r   相对基准 0.0625   结论');
for (const w of [40, 36, 32, 30, 24, 20, 16, 12]) {
  const used = Math.min(w, 32) / 2, k = 1 / used;
  console.log('  ' + fx(w, 0) + '      ' + fx(used, 2) + '           ' + f6(k) + '    ' + (k / 0.0625 - 1 >= 0 ? '+' : '') + fx((k / 0.0625 - 1) * 100, 1) + '%        ' + (w >= 32 ? '恒定段（w≥h）' : '被咬（w<h）⇒ κ=2/w'));
}
console.log('  ⇒ 修正：药丸并非"无条件安全"，它的无条件前提是 最窄 tab 宽度 ≥ 指示器高度。');

sec('12b 角形动画：corner-shape 的 v 从 1(round) 线性动到 2(squircle)，中途落在哪一档');
const seamRatio = (n, r) => {
  const f = patchAt(n, r, r, r);
  const tA = 0.25, tB = 0.015625;
  const sAt = (target) => { let lo = 0, hi = Math.PI / 2; for (let i = 0; i < 50; i++) { const mid = (lo + hi) / 2; if (arcLenFrom0(function (t) { const p = f(t); return [p[0] - r, p[1] - r]; }, mid, 3000) < target) lo = mid; else hi = mid; } return (lo + hi) / 2; };
  const tA2 = sAt(tA), tB2 = sAt(tB);
  return kappa(f, Math.max(tA2, 1e-6), Math.max(tA2 / 30, 1e-6)) / Math.max(kappa(f, Math.max(tB2, 1e-7), Math.max(tB2 / 30, 1e-7)), 1e-15);
};
console.log('  t     v=1+t      指数p=2^v    峰值κ(R=24)     接缝J1比值    档位');
for (const t of [0, 0.25, 0.5, 0.75, 1]) {
  const v = 1 + t, p = Math.pow(2, v);
  const f = patchAt(p, R, R, R);
  let kpk = 0;
  for (let th = 1e-3; th <= Math.PI / 2 - 1e-3; th += 2e-4) kpk = Math.max(kpk, kappa(f, th, 1e-5));
  const ratio = p <= 2.0001 ? 1 : seamRatio(p, R);
  console.log('  ' + fx(t, 2) + '   ' + fx(v, 2) + '      ' + fx(p, 3) + '       ' + f6(kpk) + '      ' + fx(ratio, 2) + '×       ' + (ratio >= 2 ? 'G2' : 'G1（圆弧端点）'));
}
console.log('  读法：峰值 κ 随 t 单调上升（0.041667→0.105112），没有过冲；但 G2 只在 v>1 之后成立，动画第一帧仍从 G1 起步。');

sec('12c 同族插值是否保持 G2：平端贝塞尔 r 从 16 线性动到 40，取中间态量 J1 比值');
for (const rM of [16, 22, 28, 34, 40]) {
  const f = flatCubic(rM);
  const rel = function (t) { const p = f(t); return [p[0], p[1]]; };
  const sAt = (target) => { let lo = 0, hi = 1; for (let i = 0; i < 46; i++) { const mid = (lo + hi) / 2; if (arcLenFrom0(rel, mid, 3000) < target) lo = mid; else hi = mid; } return (lo + hi) / 2; };
  const tA = Math.max(sAt(0.25), 1e-6), tB = Math.max(sAt(0.015625), 1e-7);
  const kA = kappa(f, tA, Math.max(tA / 30, 1e-6)), kB = kappa(f, tB, Math.max(tB / 30, 1e-7));
  let kpk = 0;
  for (let u = 1e-3; u <= 1 - 1e-3; u += 2e-4) kpk = Math.max(kpk, kappa(f, u, 1e-5));
  console.log('  r=' + fx(rM, 0) + '  峰值κ=' + f6(kpk) + '  峰值处等效半径=' + fx(1 / kpk, 3) + 'px  κ(0.25)=' + kA.toExponential(3) + '  κ(0.0156)=' + kB.toExponential(3) + '  J1比值=' + fx(kA / kB, 1) + '×  ⇒ ' + (kA / kB >= 2 ? '保持 G2' : '不保持'));
}
console.log('  平端条件 P1=P2=盒角 在"只换坐标"的插值下恒成立 ⇒ 中间态仍是平端 ⇒ G2 在整个动画里不掉档。');

sec('12d 方→圆过渡（120×80，r 从 16px 动到 50%）：两轴接缝 κ 与不对称比随 t 增长');
console.log('  t     a(水平半径)  b(垂直半径)  水平边接缝κ  等效半径   竖直边接缝κ  等效半径  不对称比  是否过冲');
let prevKH = null;
for (const t of [0, 0.25, 0.5, 0.75, 1]) {
  const a = 16 + t * (60 - 16), b = 16 + t * (40 - 16);
  const kH = a / (b * b), kV = b / (a * a);
  const over = prevKH !== null && (kH > prevKH + 1e-9) === false ? '否（单调）' : '—';
  console.log('  ' + fx(t, 2) + '    ' + fx(a, 2) + '        ' + fx(b, 2) + '      ' + f6(kH) + '   ' + fx(1 / kH, 2) + '     ' + f6(kV) + '   ' + fx(1 / kV, 2) + '     ' + fx(kH / kV, 2) + '×    ' + over);
  prevKH = kH;
}
console.log('  结论：中间态没有曲率过冲，抖动感来自 ①两轴 κ 不对称从 1.0× 长到 3.4×（接缝两侧的"急度"不再相等）②b 一路变小 ⇒ 水平边接缝 κ 从 0.0625 涨到 0.0375 的同时竖直边从 0.0625 跌到 0.0111。');

// ---------- 13. 响应式半径：四种策略在同一族尺寸上的实际角行为（场景 10） ----------
sec('13 正方形尺寸 S×S，四种半径写法：r_used / κ / r占比 / 是否被 f 咬 / n=5 下最大可用描边');
const SHAPE_C = {};   // 1/κ峰值 与 r 的比值（尺度无关），先量出来
for (const n of [2, 3, 4, 5, 6]) {
  const f = patchAt(n, 100, 100, 100);
  let k = 0;
  for (let t = 1e-3; t <= Math.PI / 2 - 1e-3; t += 2e-4) k = Math.max(k, kappa(f, t, 1e-5));
  SHAPE_C[n] = 1 / k / 100;
}
console.log('  尺度无关系数 1/κ峰值 ÷ r：' + [2, 3, 4, 5, 6].map((n) => 'n=' + n + '→' + SHAPE_C[n].toFixed(4)).join('  '));
const STRATS = [['定值12', () => 12], ['定值16', () => 16], ['比例12.5%', (S) => S * 0.125], ['clamp(8,12.5%,24)', (S) => Math.min(24, Math.max(8, S * 0.125))]];
console.log('  策略              S     指定r    f        r_used   κ=1/r_used   r_used/S   是否咬(f<1)   n=5最大描边px');
for (const [name, fn] of STRATS) {
  for (const S of [32, 48, 96, 192, 320]) {
    const spec = fn(S), f = Math.min(1, S / (2 * spec)), used = spec * f;
    console.log('  ' + name.padEnd(16) + fx(S, 0) + '    ' + fx(spec, 2) + '   ' + fx(f, 4) + '    ' + fx(used, 2) + '   ' + f6(1 / used) + '      ' + fx(used / S, 3) + '     ' + (f < 1 ? '是' : '否') + '          ' + fx(used * SHAPE_C[5], 2));
  }
}
console.log('  读法：定值策略 κ 与尺寸无关（角"弯的程度"不变），但 r/S 从 0.375 掉到 0.0375（10×），大卡上会显得"角很硬"；');
console.log('       比例策略 r/S 恒定 ⇒ κ ∝ 1/S，S=32 时 κ=0.250000（等效半径只有 4px，n=5 描边余量仅 1.23px）⇒ 小尺寸上角几乎被推平；');
console.log('       clamp 是分段：下界 8px 管住小尺寸（κ=0.125），上界 24px 管住大尺寸（κ=0.041667），中段跟随。三者的 f 咬住在本表都是"否"（方形且 2r≤S）。');

// ---------- 14. 令牌校验表：每个 radius 档位能配多粗的描边/inset（场景 11） ----------
sec('14 半径档位 × 形状：最大可用 border-width / inset spread / outline-offset（=1/κ峰值）');
console.log('  档位r  圆弧(n=2)  n=3     n=4     n=5     n=6     典型最小短边  该档位是否会被 f 咬');
for (const r of [4, 8, 12, 16, 20, 24, 32]) {
  const cells = [2, 3, 4, 5, 6].map((n) => fx(r * SHAPE_C[n], 2));
  console.log('  ' + fx(r, 0) + '      ' + cells.join('   ') + '      32px 组件      ' + (2 * r > 32 ? '会（短边 32 ⇒ 上限 16）' : '不会'));
}
console.log('  ⇒ 令牌里 --radius 与 --border-width 不是独立自由度：n=5 时 border 上限只有 r 的 ' + fx(SHAPE_C[5] * 100, 1) + '%。r=8 的按钮配 3px 描边已用到 1.9px 上限，必须换形状档位或加半径。');
