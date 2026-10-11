---
name: g2-corner-integration
description: G2 曲率连续圆角（squircle / superellipse / corner-shape）的工程接入流程。从零新项目接入（能力探测 → 令牌 CSS → 几何模块 → 档位闸门 → CSS 三层策略 → SVG/Canvas/clip-path → 数值验收）与现有项目迁移（静态盘点脚本 → 旧写法映射表 → 逐层替换 → 五个回归项 → 单点回退）两套完整步骤；全部代码内联在本文档内，无附属文件。当要做 squircle/corner-shape 圆角落地、评审"这个角是不是 G2"、修圆角接缝不顺/阴影起节/嵌套内角错位/`clip-path` 裁掉阴影、或要把曲率判据接进 CI 时使用。
---

# G2 圆角工程接入

## 这个技能给什么

两套可照着走完的流程，代码全部内联，零外部依赖（Node 与浏览器同码）：

- **A 段：新项目接入** — 8 步，从零到"组件带上曲率连续角并有验收"。
- **B 段：现有项目切换** — 6 步，从"满仓库 `border-radius`"迁到 G2，含只读盘点脚本与逐层替换清单。

先读 **§0 的三条硬事实**，它们决定了步骤为什么是这个顺序；不遵守就白做。

| # | 硬事实 | 后果 |
| --- | --- | --- |
| 1 | `border-radius` 默认形状是**圆弧**（`corner-shape: round`，内部指数 p=2），直边接缝处 κ 从 0 跳到 1/r ⇒ 永远只是 **G1** | 把半径调大只稀释跳变（Δκ=1/r），**不会**变成 G2。要 G2 必须换族：`p>2` 或平端贝塞尔 |
| 2 | `corner-shape` 的 `superellipse(v)` 里 **p = 2^v**，不是 p=v | `round`=`superellipse(1)`=p2；`squircle`=`superellipse(2)`=p4；**v<0 ⇒ p<1 ⇒ 内凹**，不是"更方" |
| 3 | `getComputedStyle().borderRadius` 回的是**声明值**，f 缩放（用过值）读不回来；`clip-path` 会**连自身 `box-shadow` 一起裁**；祖先 `overflow:hidden + border-radius` 会把后代 G2 **削平** | 判据必须自己算用过半径；`clip-path` 只能当兜底层；裁剪与阴影不要放同一元素 |

---

## 0. 一次性前置：能力探测（新项目 A1 / 迁移 B0 都要跑）

把下面这段丢进浏览器控制台，或存成 `g2-support.mjs` 在页面加载时调用。**结果写进项目 README**，后续所有分支决策以它为准。

```js
// g2-support.mjs — 能力探测（UA 一变就重测，别引用别人的数字）
export function detectG2() {
  const s = (prop, value) => { try { return CSS.supports(prop, value); } catch { return false; } };
  return {
    ua: navigator.userAgent,
    cornerShape: s('corner-shape', 'squircle'),
    superellipseAny: s('corner-shape', 'superellipse(2.322)'),
    superellipseNegative: s('corner-shape', 'superellipse(-0.2)'), // true 只代表语法合法
    borderShape: s('border-shape', 'squircle'),                    // 草案，勿依赖
    clipShape: s('clip-path', 'shape(rect(0px 10px 10px 0px))'),   // 常为 false
    clipPath: s('clip-path', 'path("M 0 0 L 10 0 L 10 10 Z")'),    // 兜底路线，坐标须无单位
    ctxRoundRect: typeof CanvasRenderingContext2D !== 'undefined'
      && 'roundRect' in CanvasRenderingContext2D.prototype,
    animateCornerShape: s('transition-property', 'corner-shape'),
  };
}
// console.table(detectG2())
```

本机参考值（Electron 43.1.1 / Chrome 150.0.7871.114，2026-10-11 实测）：`cornerShape=true`、`superellipseNegative=true`、`borderShape=false`、`clipShape=false`、`clipPath=true`、`ctxRoundRect=true`、`animateCornerShape=true`。

**分支规则**：`cornerShape=true` → 走 A4/B3 的**增强层**（真属性，`border`/`box-shadow`/`backdrop-filter`/`overflow` 全部自动跟随）。`false` → 只能走 A6/B3 的 **`clip-path` 兜底层**，并接受"不带动 border 与 box-shadow"这条代价。

---

## A. 新项目接入（8 步）

### A1 建形状令牌

形状与半径是**两套令牌**，但互不独立（A3 的闸门会把描边上限绑死在半径上）。

```css
/* tokens/g2-corner.css — 三层结构：保底圆弧 / 增强层 / 兜底层由 JS 决定是否注入 */
:root {
  /* 角形档：superellipse(v)，p = 2^v。round = p2（圆弧），squircle = p4 */
  --g2-shape: squircle;
  --g2-shape-soft: superellipse(1.4);   /* p≈2.64，比 squircle 温和 */

  /* 半径档（px）*/
  --r-xs: 4px;  --r-sm: 8px;  --r-md: 12px;  --r-lg: 16px;  --r-xl: 24px;
  --r-pill: 9999px;

  /* 组件级引用，别让业务代码直接写 px */
  --radius-card: var(--r-md);
  --radius-btn: var(--r-sm);
  --radius-avatar: 20px;
}

/* ① 保底：任何时候都写，不支持的引擎就是它 */
.g2-card { border-radius: var(--radius-card); }

/* ② 增强：corner-shape 需要**非零** border-radius 才生效 */
@supports (corner-shape: squircle) {
  .g2-card { corner-shape: var(--g2-shape); }
}
```

**写令牌的三条约束**（违反必出事）：

1. 每个形状档写注释标出 p 值：`superellipse(1.4) /* p=2^1.4≈2.64 */`。
2. `--r-*` 与 `--border-width` 不是独立自由度：`n=5` 时 border 上限只有 r 的 **30.8%**。
3. 兜底层（`clip-path`）用 `@supports not (corner-shape: squircle)` 隔离，**不能与增强层同时生效**。

### A2 建几何模块（形状生成 + 闸门 + 验收，全部代码在此）

```js
// g2-corners.mjs — 零依赖；Node 与浏览器同码。
// 关键设计：κ 用**解析导数**而不是差分。差分步长 h 必须远小于该点的局部特征尺度，
// 而在近接缝处（s=0.0156px 对应 θ≈4e-7）任何可用的 h 都太大 —— 会把 J1 量成 1.27 而不是 257。
export function kappaNum(f, t, h = 1e-4) {          // 只给"第三方任意曲线"兜底用
  const a = f(t - h), b = f(t), c = f(t + h);
  const d1x = (c[0] - a[0]) / (2 * h), d1y = (c[1] - a[1]) / (2 * h);
  const d2x = (c[0] - 2 * b[0] + a[0]) / (h * h), d2y = (c[1] - 2 * b[1] + a[1]) / (h * h);
  return Math.abs(d1x * d2y - d1y * d2x) / Math.pow(d1x * d1x + d1y * d1y, 1.5);
}
export const kappa = (c, t) => {                    // c = 下面的角部曲线对象
  const [x1, y1] = c.d1(t), [x2, y2] = c.d2(t);
  return Math.abs(x1 * y2 - y1 * x2) / Math.pow(x1 * x1 + y1 * y1, 1.5);
};

// 三种角部构造。局部坐标：补丁占 [0,r]×[0,r]，起点在竖直边接缝、终点在水平边接缝。
// 分数次幂的底数一律 max(0,·) 钳位：θ 稍微越界就会得到 NaN，一整张表跟着废掉。
export const arcCorner = (r) => ({
  tMax: Math.PI / 2,
  P: (t) => [r - r * Math.cos(t), r - r * Math.sin(t)],
  d1: (t) => [r * Math.sin(t), -r * Math.cos(t)],
  d2: (t) => [r * Math.cos(t), r * Math.sin(t)],
});
export const superCorner = (r, p) => {                 // p=2 即圆弧；p>2 接缝 κ→0 ⇒ G2
  const m = 2 / p, c = (v, e) => Math.pow(Math.max(0, v), e);
  return {
    tMax: Math.PI / 2,
    P: (t) => [r - r * c(Math.cos(t), m), r - r * c(Math.sin(t), m)],
    d1: (t) => [r * m * c(Math.cos(t), m - 1) * Math.sin(t), -r * m * c(Math.sin(t), m - 1) * Math.cos(t)],
    d2: (t) => [r * m * (c(Math.cos(t), m) - (m - 1) * c(Math.cos(t), m - 2) * Math.sin(t) ** 2),
                -r * m * ((m - 1) * c(Math.sin(t), m - 2) * Math.cos(t) ** 2 - c(Math.sin(t), m))],
  };
};
// 平端三次贝塞尔：P1=P2=盒角 ⇒ 两端 κ=0 ⇒ G2；几何等价 p≈5.19
export const flatCubicCorner = (r) => ({
  tMax: 1,
  P: (u) => [r * u ** 3, r * (1 - u) ** 3],
  d1: (u) => [3 * r * u * u, -3 * r * (1 - u) * (1 - u)],
  d2: (u) => [6 * r * u, 6 * r * (1 - u)],
});

export function peakKappa(c, n = 360) {                // 峰值在角的中缝，均匀网格够用
  let k = 0;
  for (let i = 1; i < n; i++) k = Math.max(k, kappa(c, c.tMax * i / n));
  return k;
}
export function arcLength(c, to = c.tMax, n = 600) {
  let L = 0, prev = c.P(0);
  for (let i = 1; i <= n; i++) { const q = c.P(to * i / n); L += Math.hypot(q[0] - prev[0], q[1] - prev[1]); prev = q; }
  return L;
}
// 折线容差律：最大弦长 s_max = sqrt(8ε/κ峰值) ⇒ 每角段数 = ceil(弧长/s_max)，段数 ∝ √尺寸
export const maxChord = (eps, kPeak) => Math.sqrt(8 * eps / kPeak);
export const segPerCorner = (c, eps) => Math.ceil(arcLength(c) / maxChord(eps, peakKappa(c)));

// CSS 的 f 缩放：getComputedStyle 读不回用过值，判据必须自己算（相邻半径和超边长 ⇒ 全盒压小）
export function usedRadius(w, h, r) {
  const f = Math.min(1, w / (2 * r), h / (2 * r));
  return { f, used: r * f, bitten: f < 1, kSeam: 1 / (r * f) };
}
// 档位闸门：1/κ峰值 = 最大可用内偏移（border-width / inset spread / outline-offset），超过就自交
export function cornerGate(p, r, w) {
  const k = peakKappa(superCorner(r, p));
  return { p, r, kPeak: k, safeInner: 1 / k, want: w, ok: 1 / k >= 1.2 * w };
}

// SVG path：整块圆角矩形，四角全用平端 C 段（两个控制点写成同一个盒角）
export function rectPath(w, h, r) {
  const R = Math.min(r, w / 2, h / 2);
  return `M ${R},0 H ${w - R} C ${w},0 ${w},0 ${w},${R} V ${h - R} C ${w},${h} ${w},${h} ${w - R},${h}`
       + ` H ${R} C 0,${h} 0,${h} 0,${h - R} V ${R} C 0,0 0,0 ${R},0 Z`;
}

// 超椭圆整块矩形点集（clip-path / Canvas 用）；一份左上角模板，其余三角靠镜像
export function rectPoints(w, h, r, p, eps = 0.5) {
  const R = Math.min(r, w / 2, h / 2);
  const c = superCorner(R, p), seg = segPerCorner(c, eps);
  const A = []; for (let i = 0; i <= seg; i++) A.push(c.P((Math.PI / 2) * i / seg));   // (0,R)→(R,0)
  const mx = ([x, y]) => [w - x, y], my = ([x, y]) => [x, h - y], mxy = ([x, y]) => [w - x, h - y];
  const pts = [];
  const push = (q) => { const l = pts[pts.length - 1];
    if (!l || Math.abs(l[0] - q[0]) > 1e-9 || Math.abs(l[1] - q[1]) > 1e-9) pts.push([+q[0].toFixed(3), +q[1].toFixed(3)]); };
  push([R, 0]); push([w - R, 0]);
  A.slice().reverse().forEach((q) => push(mx(q)));    // 右上 (w-R,0)→(w,R)
  push([w, h - R]);
  A.forEach((q) => push(mxy(q)));                     // 右下 (w,h-R)→(w-R,h)
  push([R, h]);
  A.slice().reverse().forEach((q) => push(my(q)));    // 左下 (R,h)→(0,h-R)
  push([0, R]);
  A.forEach((q) => push(q));                          // 左上 (0,R)→(R,0)
  return pts;
}
// clip-path 的 path() 数字**必须无单位**（写 10px 被拒），且是绝对坐标 ⇒ 尺寸变了要重算
export const clipPathOf = (pts) =>
  `path("M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z")`;
export const svgPolyOf = (pts) =>
  `M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z`;

// ---- 验收判据 ----
// θ 网格取 i^4（近接缝密集）且**从 i=1 起**（t=0 处解析导数发散，且均匀网格会让两个取样点落同一处、J1 假报 1.0000）
export function sTable(c, N = 6000) {
  const out = []; let prev = c.P(0), s = 0;
  for (let i = 1; i <= N; i++) {
    const t = c.tMax * Math.pow(i / N, 4), q = c.P(t);
    s += Math.hypot(q[0] - prev[0], q[1] - prev[1]); prev = q;
    out.push({ s, k: kappa(c, t) });
  }
  return out;
}
const kAtS = (g, s) => g.reduce((b, p) => Math.abs(p.s - s) < Math.abs(b.s - s) ? p : b, g[0]);
// J1：κ(s=0.25)/κ(s=0.0156)，比值 ≥2 ⇒ G2；圆弧恒 1.00×
export function j1(c, sA = 0.25, sB = 0.0156) {
  const g = sTable(c), a = kAtS(g, sA), b = kAtS(g, sB);
  return { ratio: a.k / Math.max(b.k, 1e-30), sA, sB };
}
// J2：log-log 斜率（窗口 s∈[0.25,2]）≈ p−2；≥1.2 ⇒ 够 G3，≈1 ⇒ 止于 G2
export function j2(c) {
  const g = sTable(c).filter((p) => p.s >= 0.25 && p.s <= 2 && p.k > 1e-9);
  let sx = 0, sy = 0, sxx = 0, sxy = 0;
  for (const p of g) { const x = Math.log(p.s), y = Math.log(p.k); sx += x; sy += y; sxx += x * x; sxy += x * y; }
  return (g.length * sxy - sx * sy) / (g.length * sxx - sx * sx);
}
// J3：折线逼近偏离。方向必须是"逐参照点取**最近**线段再取最大"；反过来量的是"最远线段"（0.088px 会报成 28.9px）
export function j3Corner(c, seg, N = 1200) {
  const poly = []; for (let i = 0; i <= seg; i++) poly.push(c.P(c.tMax * i / seg));
  let worst = 0;
  for (let i = 0; i <= N; i++) {
    const p = c.P(c.tMax * i / N);
    let d = Infinity;
    for (let k = 0; k < poly.length - 1; k++) {
      const a = poly[k], b = poly[k + 1];
      const abx = b[0] - a[0], aby = b[1] - a[1], L2 = abx * abx + aby * aby;
      const t = Math.max(0, Math.min(1, L2 ? ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / L2 : 0));
      d = Math.min(d, Math.hypot(p[0] - (a[0] + t * abx), p[1] - (a[1] + t * aby)));
    }
    worst = Math.max(worst, d);
  }
  return worst;
}
// 一档形状的综合判定。expect = 解析值 (sA/sB)^(p−2)，用它反证 J1 是否量对了
export function g2report(p, r) {
  const c = p === 2 ? arcCorner(r) : superCorner(r, p);
  const { ratio, sA, sB } = j1(c), slope = j2(c), kPeak = peakKappa(c);
  return { p, r, j1: +ratio.toFixed(4), expect: +Math.pow(sA / sB, p - 2).toFixed(4), slope: +slope.toFixed(4),
           kPeak: +kPeak.toFixed(6), safeInner: +(1 / kPeak).toFixed(3),
           grade: ratio < 2 ? 'G1' : (slope >= 1.2 ? 'G2+够G3' : 'G2') };
}
```

### A3 选档位（先过闸门，再谈手感）

`1/κ峰值` 就是这一档**最大可用描边宽**。r=24 时的实测闸门值（`cornerGate(p,24,w)` 应逐个复现）：

| 形状 | p | `κ峰值` | `1/κ峰值` = 最大内偏移 | `c(n)=1/κ峰值÷r` |
| --- | --- | --- | --- | --- |
| 圆弧 `round` | 2 | 0.041667 | 24.000 | 1.0000 |
| `superellipse(1.4)`≈ | 3 | 0.074242 | 13.470 | 0.5612 |
| `squircle` | 4 | 0.105112 | 9.514 | 0.3964 |
| ≈ | 5 | 0.135376 | 7.387 | 0.3078 |
| 平端贝塞尔 | ≈5.19 | 0.157135 | 6.364 | — |

判定顺序（任一不过就降档，别硬上）：

```js
import { cornerGate, g2report, usedRadius } from './g2-corners.mjs';

const shape = 4;                              // squircle
const box = usedRadius(240, 96, 24);           // → { f: 1, used: 24, bitten: false, kSeam: 0.041667 }
const gate = cornerGate(shape, box.used, 3);   // 描边 3px → safeInner=9.514 ⇒ ok:true
const grade = g2report(shape, box.used);       // → j1=254.1365 slope=2 grade:'G2+够G3'
console.log({ box, gate, grade });
// 短边变 40 就被咬：usedRadius(240, 40, 24) → { f: 0.8333, used: 20 } ⇒ 闸门要按 used 重算
```

- **不要用大半径冒充 G2**：圆弧 `Δκ=1/r`，r=8→0.125000、r=48→0.020833，但 J1 永远是 `1.00×`。
- **不要盲目追大 p**：p=8 时接缝好四个数量级，但 `1/κ峰值` 只剩 ~4.45px（r=24），3px 描边就没余量了。
- `p<2`（bevel/scoop 方向）与 `p<1`（内凹）是两种风险，需单独审批。

### A4 CSS 三层接入（组件层写法）

```css
/* 增强层：卡片/按钮/头像 —— border、box-shadow、backdrop-filter 会跟着一起变角形 */
.avatar {
  border-radius: var(--radius-avatar);            /* ① 保底，永远写 */
  border: 2px solid var(--line);                  /* 2px < 7.39px 上限，p=5 才安全 */
  box-shadow: 0 2px 12px rgba(0,0,0,.18);
}
@supports (corner-shape: squircle) {
  .avatar { corner-shape: var(--g2-shape); }      /* ② 增强 */
}

/* 兜底层（不支持 corner-shape 时）：由 JS 注入 --g2-clip，注意两条代价 */
@supports not (corner-shape: squircle) {
  .avatar { clip-path: var(--g2-clip); }          /* ③ 会连自己的 box-shadow 一起裁掉 */
}
```

```js
// 兜底层的 --g2-clip 生成 + 尺寸变化重算
import { rectPoints, clipPathOf } from './g2-corners.mjs';

function applyG2Clip(el, p = 4, eps = 0.5) {
  const { width: w, height: h } = el.getBoundingClientRect();
  const r = Math.min(parseFloat(getComputedStyle(el).borderRadius) || 0, w / 2, h / 2);
  if (!r) return;                                     // 无半径就别裁
  el.style.setProperty('--g2-clip', clipPathOf(rectPoints(w, h, r, p, eps)));
  if (!applyG2Clip.obs) {                              // 尺寸变了必须重算（path() 是绝对坐标）
    applyG2Clip.obs = new ResizeObserver((es) => es.forEach((e) => applyG2Clip(e.target, p, eps)));
  }
  applyG2Clip.obs.observe(el);
}
```

本机渲染实测：上面生成的字符串被引擎**接受**（`getComputedStyle().clipPath` 读回 `path("M 24 0 L 72 0 …")` 而非 `none`），命中测试同步收缩（中心与边中点命中、左上角 `(1,1)` 不命中）；`96×96 / r=24 / p=5 / ε=0.5` 时字符串长 515 字符。

`clip-path` 兜底的**两条必写注释**：① 描边要抬到父层（同元素 border 不会被裁）；② 祖先若带圆弧 + `overflow:hidden`，后代的 G2 会被削平，父层也得同形状或去掉裁剪。

### A5 SVG / Canvas：用平端 C 段，一行拿到 G2

```js
import { rectPath, rectPoints } from './g2-corners.mjs';

// SVG：直接塞给 <path>，无采样、无段数问题（两端 κ=0 ⇒ G2）
document.querySelector('#icon').setAttribute('d', rectPath(200, 120, 24));
// 实测生成的 d：M 24,0 H 176 C 200,0 200,0 200,24 V 96 C 200,120 200,120 176,120 H 24 C 0,120 0,120 0,96 V 24 C 0,0 0,0 24,0 Z
```

```js
// Canvas：整块 squircle 图标。点数复用 rectPoints（已按顺时针给出闭合点列），别自己拼四角的镜像顺序
import { rectPoints } from './g2-corners.mjs';

function squirclePath(ctx, S, p, eps = 0.5) {
  ctx.beginPath();
  for (const [x, y] of rectPoints(S, S, S / 2, p, eps)) ctx.lineTo(x, y);
  ctx.closePath();
}

const dpr = Math.max(1, Math.floor(window.devicePixelRatio));  // 先设尺寸，再设 transform
canvas.width = size * dpr; canvas.height = size * dpr;
canvas.style.width = size + 'px'; canvas.style.height = size + 'px';
ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
squirclePath(ctx, S, 4, 0.5);
ctx.fillStyle = '#2b6'; ctx.fill();
```

**为什么不让它自己拼四角**：用 `fx/fy` 符号镜像去展开角部模板是错的——超椭圆补丁模板的方向不对称（θ=0 落在竖直边、θ=π/2 落在水平边），有的角需要**反序**遍历才能保持顺时针；顺序错了路径自交，`fill` 出来的面积只剩约一半。
// 本机渲染实测（dpr=1，S=96、p=4、ε=0.5）：错误版覆盖率 0.5036 ⇒ 自交；改成 rectPoints 后 0.93186，解析值 0.92705（差 0.5%，属边缘像素计数）。`rectPoints()` 内部已处理正反序，并用面积校验过（鞋带面积 vs `Γ` 函数解析面积）。

`ctx.roundRect()` 只是便利方法，画的是**圆弧**（G1）；要 G2 就走上面的自绘或改用 `corner-shape`。

### A6 clip-path 兜底的段数从哪来

段数 **∝ √边长**（`κ峰值 ∝ 1/S`、弧长 `∝ S`）。n=5、ε=0.5px 的实测需求（每角）：

| S | 32 | 48 | 96 | 128 | 256 | 512 |
| --- | --- | --- | --- | --- | --- | --- |
| 每角段数 | 7 | 8 | 12 | 13 | 19 | 26 |
| `κ峰值` | 0.203063 | 0.135376 | 0.067688 | 0.050766 | 0.025383 | 0.012691 |

别"看着差不多"手填：S 从 32→512（16 倍）段数只需约 4 倍（7→26）。A2 的 `segPerCorner()` 就是这张表的算法来源。

### A7 验收：四道判据 + PASS 条件

| 判据 | 定义 | 阈值 | 圆弧该给 | p=4/r=24 该给 |
| --- | --- | --- | --- | --- |
| **J1** | `κ(s=0.25) ÷ κ(s=0.0156)` | ≥2 ⇒ G2 | `1.00×` | `254.14×`（解析 `(0.25/0.0156)²=256.82`，差 −1%） |
| **J2** | log-log 斜率，窗 s∈[0.25,2] | ≈p−2；≥1.2 ⇒ 够 G3 | `0.000` | `2.000` |
| **J3** | 折线逐点取**最近线段**再取最大 | ≤0.5px（对齐用 0.05px） | — | 12 段/角 → 0.088px（6 段 0.356、3 段 1.467） |
| **J4** | 构造间最大射线偏差 | ≤0.2px | — | 平端贝塞尔 vs n=5 → 0.151px |

```js
import { g2report, superCorner, segPerCorner, j3Corner, usedRadius, cornerGate } from './g2-corners.mjs';

// ① 对照组：圆弧必须是 G1 / j1≈1.0000 / slope≈0.0000，否则判据代码在撒谎
console.table([g2report(2, 24), g2report(3, 24), g2report(4, 24), g2report(5, 24)]);
// Node v24 实测输出：
//   p=2  j1=1.0000   expect=1.0000   slope=0.0000  kPeak=0.041667  safeInner=24.000  grade=G1
//   p=3  j1=16.0299  expect=16.0256  slope=1.0003  kPeak=0.074242  safeInner=13.470  grade=G2
//   p=4  j1=254.1365 expect=256.8212 slope=2.0000  kPeak=0.105112  safeInner=9.514   grade=G2+够G3
//   p=5  j1=3963.326 expect=4115.724 slope=3.0000  kPeak=0.135375  safeInner=7.387   grade=G2+够G3
//   （j1 与解析值差 −1% 与 −3.7% 量级，来自近接缝取样网格，不影响档位判定）

// ② J3：段数由 segPerCorner 给，别手填
const c = superCorner(48, 5);              // S=96 的整块 squircle，每角半径 = S/2
const seg = segPerCorner(c, 0.5);          // → 12（A6 表）
const dev = j3Corner(c, seg);              // → 0.088px
console.log(`J3 每角 ${seg} 段，最大偏离 ${dev.toFixed(3)}px`, dev <= 0.5 ? 'PASS' : 'FAIL');
console.log(`段数砍半 → ${j3Corner(c, 6).toFixed(3)}px，再砍 → ${j3Corner(c, 3).toFixed(3)}px`);  // 0.356 / 1.467 ⇒ 已越阈值

// ③ 用过半径 + 闸门：短边不够就先按用过值算，再谈描边放不放得下
const box = usedRadius(100, 40, 30);       // → { f: 0.6667, used: 20, bitten: true }
console.log(box, cornerGate(4, box.used, 2));   // gate.safeInner=9.514÷缩放后 → ok 与否看 .ok
```

**判据三条纪律**（都是踩出来的）：① **绝对值判据会被"调大半径"骗过去**，必须用比值；② 任何"偏差为 0"类判据，**先跑圆弧对照组**，n=2 那行不为 `0.000` 就先修测量代码；③ θ 网格不能用均匀的（`i^4` 才在近接缝有分辨率）。

### A8 硬性禁令

| 禁止 | 原因 | 正确做法 |
| --- | --- | --- |
| 同元素 `clip-path` + `box-shadow` | 阴影连元素一起被裁，直接消失 | 阴影放未裁剪的父层，或走 `corner-shape` |
| 祖先 `overflow:hidden + border-radius` 裁 G2 后代 | 祖先按圆弧裁，把外鼓的超椭圆角削平 | 祖先同形状裁切或不裁；阴影留在不裁的宿主上 |
| 用 `border-radius:50%` 做扁条药丸 | 按各自轴 ⇒ 1/4 椭圆，两轴 κ 不对称 `(a/b)³`（200×80 → 15.63×，320×40 → 512×） | `border-radius: 9999px` |
| 用声明值算曲率 | f 缩放读不回：100×40 写 24/30/60px 实际全用 20px | 一律先 `usedRadius()` |
| `border-radius:0` + `corner-shape` | `corner-shape` 依赖非零半径 | 保底半径必须写 |
| `corner-shape: superellipse(-0.2)` 当"更方" | p=2^−0.2≈0.87<1 ⇒ **内凹** | 要更方走 `p∈(2,4)` 即 v∈(1,2)，或 `bevel` |
| 内角写成"外角 − border-width"（G2 形状下） | 真内边界是法向等距线，偏差 ≈ 0.23·w（p=5）；圆弧才精确 | 生成等距线，或 `p=2` 才用这公式 |
| 药丸宽度动画无条件安全 | 咬合点在 `w=h`，窄于此 κ 被钉成 `2/w`（w=20 → +60%） | 保证最窄档 `w ≥ h`，或宽度动画走自绘 |

---

## B. 现有项目切换到 G2（6 步）

### B0 先跑能力探测（§0），再跑静态盘点

盘点脚本**只读**，不改任何文件；输出"必须人工确认的位置清单"。

```js
// g2-audit.mjs — 用法：node g2-audit.mjs src/ [更多路径…]
import fs from 'node:fs';
import path from 'node:path';

const EXT = ['.css', '.scss', '.less', '.html', '.vue', '.svelte', '.js', '.jsx', '.ts', '.tsx'];
const SKIP = /^(node_modules|\.git|dist|build|coverage)$/;
const roots = process.argv.slice(2).length ? process.argv.slice(2) : ['.'];
const files = [];
const walk = (p) => {
  const s = fs.statSync(p);
  if (s.isDirectory()) fs.readdirSync(p).forEach((f) => { if (!SKIP.test(f)) walk(path.join(p, f)); });
  else if (EXT.includes(path.extname(p))) files.push(p);
};
roots.forEach(walk);

const RULES = [
  ['R1 形状族没换', /border-radius\s*:/, /corner-shape/, '只有圆弧：补 @supports (corner-shape: …) 增强层'],
  ['R2 形状失效', /corner-shape\s*:/, /border-radius\s*:\s*0(\s|px|;|}|,|"|')/, 'corner-shape 需要非零半径，检查同规则块'],
  ['R3 按轴解析', /border-radius\s*:[^;\n]*%/, null, '百分比按各自轴 ⇒ 长方形成椭圆角，两轴 κ 不对称 (a/b)³'],
  ['R4 阴影被裁', /clip-path\s*:/, /box-shadow\s*:/, '同元素 clip-path 连自身 box-shadow 一起裁'],
  ['R5 祖先削平', /overflow(-x|-y)?\s*:[^;\n]*(hidden|clip|auto|scroll)/, null, '祖先圆弧裁剪会把后代 G2 削平；确认这个元素有没有圆角后代'],
  ['R6 阴影不等价', /filter\s*:\s*drop-shadow/, null, 'drop-shadow 跟 alpha 轮廓，与 box-shadow 跟盒子角形不同，别当等价'],
  ['R7 动画首帧', /(transition|animation)[^;\n]*corner-shape/, null, '角形动画第一帧是圆弧端点（G1），G2 只在 v>1 之后成立'],
  ['R8 内角公式', /border-radius\s*:\s*calc\([^;\n]*-[^;\n]*\)/, null, '内角=外角−边框宽只在 p=2 精确；G2 下偏差≈0.23·w'],
];

let total = 0;
const count = {};
for (const file of files) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  lines.forEach((line, i) => {
    for (const [name, need, forbid, action] of RULES) {
      if (!need.test(line)) continue;
      if (forbid && forbid.test(line)) continue;      // 已经有对应写法，不算命中
      count[name] = (count[name] || 0) + 1; total++;
      console.log(`${file}:${i + 1} [${name}] ${line.trim().slice(0, 90)}\n    → ${action}`);
    }
  });
}
console.log(`\n扫描 ${files.length} 个文件，命中 ${total} 处：`);
Object.entries(count).forEach(([k, v]) => console.log(`  ${k}: ${v}`));
console.log('下一步：B1 归类 → B2 建令牌 → B3 逐层替换 → B4 回归项 → B5 回退开关 → B6 验收');
```

R1–R8 是**行级启发式**，跨行/跨规则块的关系（比如祖先与后代、R2 的"同规则块"）它判断不了，必须按输出人工过一遍——这正是它只读的原因。

### B1 归类现有半径 → 令牌档

把盘点出的散值按"就近档"收进 `--r-*`，**一次只收一档**，每档改完就跑 B6 验收：

```bash
# 先看有哪些值在仓库里出现过（示例：只统计 CSS）
grep -rEho 'border-radius:[^;}]+' src/ | tr -d ' ' | sort | uniq -c | sort -rn
```

归类原则：同值不同语义（卡片 12px / 按钮 12px）要**分开两个令牌**，别合并成 `--r-md` 后一起动。浮点/百分比一律换算成 px 再入档。

### B2 引入 A1 的令牌文件 + A2 的几何模块

顺序固定：**令牌文件先落地且内容等价于现状**（`--g2-shape` 先设 `round`，即 p=2），这样这一步是**纯重构、零视觉变化**，可以先合一次 MR 让回归基线干净。

```css
/* 迁移中间态：形状显式写成 round，等价于现状，视觉零差异 */
:root { --g2-shape: round; }   /* p=2，就是圆弧；下一步只改这一个变量 */
```

### B3 逐层替换（四类各一份 diff，别一次全改）

| 层 | 旧写法 | 新写法 | 该层的坑 |
| --- | --- | --- | --- |
| CSS 声明式 | `border-radius:12px` | 保留 + `@supports` 里加 `corner-shape: var(--g2-shape)` | 保底层必须留着，不支持的引擎靠它 |
| SVG | `A r,r 0 0 1 x,y` | `rectPath()` 生成的 `C w,0 w,0 w,r` 平端段 | `A` 弧半径会被自动放大，且永远只是 G1 |
| Canvas | `ctx.arc()` 拼四角 | `squirclePath()`（A5）或 `rectPoints()` 折线 | 段数用 `segPerCorner()`，先设 `canvas.width` 再 `setTransform` |
| `clip-path` | `circle()`/`inset(round)` | `clipPathOf(rectPoints(...))` | 数字**无单位**；不带动 border/shadow；尺寸变了要重算 |

单点翻形状：`--g2-shape: round` → `squircle`。**一次只动一个形状档**，动完跑 B4 的五个回归项。

### B4 五个必检回归项（每项：症状 / 判据 / 修法）

1. **描边与 focus ring**：症状=内角出现两道缝或 ring 起节。判据=`cornerGate(p, usedR, borderWidth).ok`，且 `w ≤ 1/κ峰值`（p=4/r=24 是 9.514px，p=5 是 7.387px）。修法=减描边/升半径/生成真等距线；`p=2` 才可用"外角−w"。
2. **阴影**：症状=大模糊下接缝仍"起节"。判据=外偏移 d 后 `Δκ=1/(R+d)`（R=24：d=6→0.033333、d=24→0.020833、d=48→0.013889，**恒不为 0**）。修法=换形状族；**别拿模糊图做判据**，先把轮廓外偏移成线再量。
3. **嵌套**：症状=内层角与外层角不同心、看到两条线。判据=几何差（p=2 对照组必须 `0.000`）+ `κ内/κ外` 比（`R−p` 口径：p=8 → 1.500×）。修法=内层用等距线生成，或 `max(R−padding, 6)` 并显式接受。
4. **动画**：症状=滑动指示器/tab 宽度变小时角"跳"。判据=两处咬合线 `2r ≤ w`（定值 r）与 `w ≥ h`（药丸）。修法=满足咬合条件，或宽度动画走 `rectPath()` 逐帧重算；`corner-shape` 动画首帧是 G1。
5. **写过值**：症状=改半径没反应。判据=`usedRadius(w,h,r).bitten`。修法=按短边重排档位（100×40 上 24/30/60px 全等于 20px）。

### B5 回退开关（必须留，且是单变量）

```css
/* 一行回退到迁移前形态：p=2 圆弧。形状与半径两个轴不互相补偿 */
:root[data-g2='off'] { --g2-shape: round; }
```

```js
// 运行时开关（灰度/线上急救）：形状切换不重建 DOM，只改自定义属性
document.documentElement.dataset.g2 = 'off';   // 'on' 恢复
```

兜底层（`clip-path`）的回退**不能**靠这个开关——它由 `@supports not (…)` 决定，所以增强层与兜底层必须互斥，切换形状档时两层都要同步改。

### B6 验收与提交前检查表

```js
import { g2report, cornerGate, usedRadius, rectPath, rectPoints, clipPathOf } from './g2-corners.mjs';

console.table([g2report(2, 24), g2report(4, 24)]);        // 必须 G1/1.00× 与 G2/≥2
console.log(cornerGate(4, usedRadius(240, 96, 16).used, 2).ok);  // 描边闸门
console.log(rectPath(200, 120, 24));                     // 与 §A5 的 d 逐字符相同
console.log(clipPathOf(rectPoints(96, 96, 48, 5, 0.5)).length);  // 每角段数应为 12
```

- [ ] `g2report(2, r)` 是 `G1 / j1≈1.0000 / slope≈0.000`（**对照组**，不过就说明判据代码坏了）
- [ ] 目标档 `j1 ≥ 2`，需要 G3 时 `slope ≥ 1.2`
- [ ] 所有描边/inset/outline-offset 过 `cornerGate(...).ok`
- [ ] 所有短边过 `usedRadius(...).bitten === false`，或已按用过值重排
- [ ] 没有任何元素同时带 `clip-path` 与 `box-shadow`
- [ ] 祖先链上没有 `overflow:hidden + border-radius` 压在 G2 元素外面
- [ ] 动画档：`2r ≤ 最窄宽度`（定值）或 `最窄宽度 ≥ h`（药丸）
- [ ] 控制台 0 报错；`--g2-shape: round` 单行回退实测有效

---

## 附二：本技能代码的验证状态

| 块 | 怎么验的 | 结果 |
| --- | --- | --- |
| `g2-corners.mjs` | 从本文档抽块（字节相同校验通过），在 Node v24 跑 **50 项断言 0 失败**，期望值取自闭卷报告的数值台 | 全部 PASS：`κ峰值`/`1κ峰值`(p=2…8)、段数(13/22/26/34/28、整块 7/12/26)、`usedRadius`(f=0.6667→20)、`rectPath` 逐字符、`rectPoints` 鞋带面积 vs `Γ` 解析面积、J1(1.0000/16.03/254.14/3963.33)、J2(0/1.0003/2/3)、J3(0.088/0.009) |
| `g2-audit.mjs` | 在本报告 `demo/` 目录实跑 | 输出 153 行、命中 R1=37 R2=20 R3=3 R4=4 R5=5 R6=1 R7=1（R2 多为行级启发式误报，需人工过） |
| `g2-support.mjs` / SVG / `clip-path` / Canvas 块 | 拼进一个 file:// 页面在本机浏览器执行（dpr=1） | `detectG2()` 八项读回与 §0 记录一致；`<path>` `getBBox()=[200,120]`；`clip-path` 被接受且命中收缩；Canvas 覆盖率 0.93186 |

未验证：真实组件树上的视觉验收、Safari/Firefox、`corner-shape` 参与动画的中间态。

---

## 附：判据用得到的数字（内联，别跳外链）

| 用途 | 数字 |
| --- | --- |
| p=2^v 映射 | `scoop`0.5 / `bevel`1 / `round`2 / `squircle`4 / `superellipse(3)`8 |
| 圆弧接缝跳变 | `Δκ=1/r`：r=8→0.125000、12→0.083333、16→0.062500、24→0.041667、32→0.031250、48→0.020833 |
| 阴影外偏移 | `Δκ=1/(R+d)`：R=24、d=6/12/24/48 → 0.033333/0.027778/0.020833/0.013889；同偏移下 p=5 恒 ~4.18e-5 |
| 端点曲率（r=24） | θ=0.001 处：p2 0.041667 / p3 0.000833 / p4 0.000125 / p5 0.000042 |
| 峰/端比 | p2 1.0× / p3 19.2× / p4 84.1× / p5 204.1× / p8 771.7× |
| 天真内角偏差 | ≈`c(n)·w`：p3 0.122 / p4 0.189 / p5 0.231 / p6 0.260 / p8 0.297（每 w）；**p2 恒 0.000** |
| f 缩放 | `f=min(1, 边长/两侧半径和)`；100×40 上 24/25/30/40/60px → 用过值全 20.00 |
| 百分比不对称 | `κ=a/b²`（水平边接缝）、`κ=b/a²`（竖直边），比 `(a/b)³`：200×80→15.63×、320×40→512.00× |
| 平端贝塞尔 | `B(u)=(u³r,(1−u)³r)`，端点 κ→0、峰值 0.157135(r=24)、斜率 1.072 ⇒ 止于 G2、几何等价 p≈5.19 |
| 采样弦长律 | `s_max=sqrt(8ε/κ峰值)`，段数 ∝ √S；ε=0.5px 为可见阈值、0.05px 为对齐阈值 |
