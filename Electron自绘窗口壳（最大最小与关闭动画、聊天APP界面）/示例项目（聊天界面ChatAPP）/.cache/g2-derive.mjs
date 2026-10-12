// G2 圆角几何闸门(Plan B:直接采样精确超椭圆,不做贝塞尔拟合)
// 角坐标系:曲率中心 C 在原点,a=r-x, b=r-y >= 0,边界 = (a/r)^n+(b/r)^n=1,归一化 r=1。
// 折线以 SEG 段逼近四分之一角,量三件事:
//   1) 折线对精确曲线的最大径向偏差(越接近真 G2 越好)
//   2) 折线在内部角上是否始终在圆弧之外(包含关系:伪元素 G2 轮廓 ⊇ 宿主 G1 背景)
//   3) 45 度方向角部朝盒角外扩的量(用户能看到的变化幅度)

const N = 4;
const pow = Math.pow, atan2 = Math.atan2, cos = Math.cos, sin = Math.sin, hypot = Math.hypot;

// 精确曲线在角度 theta 处的半径
const rTrue = (theta, n = N) => 1 / pow(pow(cos(theta), n) + pow(sin(theta), n), 1 / n);

// 角点按 theta 等分:0 -> 90 度(等分角度而不是等分 a,否则端点附近弦会大幅戳进圆弧)
function arcPoints(seg) {
  const pts = [];
  for (let i = 0; i <= seg; i++) {
    const th = (Math.PI / 2) * (i / seg);
    const rr = rTrue(th);
    pts.push([rr * cos(th), rr * sin(th)]);
  }
  return pts;
}

// 射线 theta 与弦 P0P1 的交点距原点长度
function rayChord(theta, p0, p1) {
  const dx = p1[0] - p0[0], dy = p1[1] - p0[1];
  const ox = cos(theta), oy = sin(theta);
  const den = ox * dy - oy * dx;
  if (Math.abs(den) < 1e-15) return NaN;
  return (p0[0] * dy - p0[1] * dx) / den;
}

function evalSeg(seg) {
  const pts = arcPoints(seg);
  const ang = pts.map(p => atan2(p[1], p[0]));
  const DENSE = 20000;
  let maxAbs = 0, minGap = Infinity, gapAt = 0, worstTheta = 0;
  for (let i = 0; i <= DENSE; i++) {
    const theta = (Math.PI / 2) * (i / DENSE);
    if (theta < 1e-6 || theta > Math.PI / 2 - 1e-6) continue; // 端点本来就贴在圆弧上
    let k = 0;
    while (k < seg - 1 && ang[k + 1] < theta) k++;
    const dPoly = rayChord(theta, pts[k], pts[k + 1]);
    const err = dPoly - rTrue(theta);
    if (Math.abs(err) > maxAbs) { maxAbs = Math.abs(err); worstTheta = theta; }
    const gap = dPoly - 1; // 与同半径圆弧的余量
    if (gap < minGap) { minGap = gap; gapAt = theta; }
  }
  return { maxAbs, minGap, gapAt, worstTheta };
}

console.log('n = ' + N + '  (超椭圆指数;2=圆弧即今天的 border-radius,4=squircle)');
console.log('SEG | 最大径向偏差/r | 内部最小(折线-圆弧)/r | 包含性');
const rows = [];
for (const seg of [4, 6, 8, 10, 12, 16, 20, 24]) {
  const e = evalSeg(seg);
  rows.push({ seg, ...e });
  console.log(
    String(seg).padStart(3) + ' | ' + e.maxAbs.toExponential(3).padStart(11) + '   | ' +
    e.minGap.toExponential(3).padStart(11) + ' @' + (e.gapAt * 180 / Math.PI).toFixed(1) + 'deg | ' +
    (e.minGap >= -1e-12 ? '始终在圆弧外 ✓' : '会戳进圆弧 ✗')
  );
}

const diagRadius = pow(2, 1 / N);            // 对角线方向超椭圆半径 / r
const bulgeRatio = (Math.SQRT2 - 1) - (Math.SQRT2 - diagRadius); // 朝盒角推进的比例
console.log('\n对角方向:圆弧边界离角 ' + (Math.SQRT2 - 1).toFixed(5) + 'r,超椭圆边界离角 ' +
  (Math.SQRT2 - diagRadius).toFixed(5) + 'r -> 朝角外扩 ' + bulgeRatio.toFixed(5) + 'r');
for (const r of [32, 16, 14, 12, 10, 8, 6]) {
  console.log('  r=' + String(r).padStart(2) + ' -> 外扩 ' + (bulgeRatio * r).toFixed(2) + 'px');
}

console.log('\n各候选 SEG 的最大几何误差(px):');
for (const row of rows.filter(r => [6, 8, 10, 12, 16, 20].includes(r.seg))) {
  console.log('  SEG=' + String(row.seg).padStart(2) + ': ' +
    [32, 16, 12, 10, 8, 6].map(r => 'r' + r + ':' + (row.maxAbs * r).toFixed(3)).join('  '));
}

// ---- clip 模式命中区代价 ----
// clip-path 会按形状收缩命中区(border-radius 不会)。丢掉的面积 = 4 * (r^2 - 四分之一超椭圆面积)
// 四分之一超椭圆面积 = r^2 * B, B = ∫₀¹ (1-u^n)^(1/n) du(数值积分)
function beta(n, steps = 200000) {
  let s = 0;
  const du = 1 / steps;
  for (let i = 0; i < steps; i++) {
    const u = (i + 0.5) * du;
    s += Math.pow(1 - Math.pow(u, n), 1 / n) * du;
  }
  return s;
}
const B = beta(N);
console.log('\n命中区代价(n=' + N + ',B=' + B.toFixed(6) + ',每角损失 = r^2*(1-B)):');
const items = [
  ['.chat-item', 284, 64, 12],
  ['.filter-tab', 66, 40, 10],
  ['.emoji-cell', 26.7, 26.7, 8],
  ['.menu-btn', 32, 32, 8],
  ['.app-badge', 20, 20, 6],
  ['.chip.add', 60, 27, 10],
];
for (const [name, w, h, r] of items) {
  const lost = 4 * r * r * (1 - B);
  console.log('  ' + name.padEnd(12) + ' 盒 ' + w + 'x' + h + ' r=' + r +
    ' -> 丢 ' + lost.toFixed(1) + 'px^2 = ' + ((lost / (w * h)) * 100).toFixed(2) + '%');
}
