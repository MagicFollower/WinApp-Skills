---
name: g2-corner-integration
description: G2 曲率连续圆角（squircle / superellipse / corner-shape）的工程接入流程。从零新项目接入（能力探测 → 令牌 CSS → 几何模块 → 档位闸门 → CSS 三层策略 → SVG/Canvas/clip-path → 数值验收）与现有项目迁移（静态盘点脚本 → 旧写法映射表 → 逐层替换 → 五个回归项 → 单点回退）两套完整步骤，另附 C 段"引擎支持 corner-shape 时的单声明最短路径"真实工程样本；全部代码内联在本文档内，无附属文件。当要做 squircle/corner-shape 圆角落地、评审"这个角是不是 G2"、修圆角接缝不顺/阴影起节/嵌套内角错位/磨砂方角漏在弧外/`clip-path` 裁掉阴影或裁掉热区、或要把曲率判据接进 CI 时使用。
---

# G2 圆角工程接入

## 这个技能给什么

三套可照着走完的流程，代码全部内联，零外部依赖（Node 与浏览器同码）：

- **A 段：新项目接入** — 8 步，从零到"组件带上曲率连续角并有验收"。
- **B 段：现有项目切换** — 6 步，从"满仓库 `border-radius`"迁到 G2，含只读盘点脚本与逐层替换清单。
- **C 段：单引擎最短路径** — 目标引擎支持 `corner-shape` 且不要求跨引擎时，一条 CSS 声明的落地形态与五步渲染层验收（真实工程样本，含实测数字）。

先读 **§0 的八条硬事实**，它们决定了步骤为什么是这个顺序；不遵守就白做。

| # | 硬事实 | 后果 |
| --- | --- | --- |
| 1 | `border-radius` 默认形状是**圆弧**（`corner-shape: round`，内部指数 p=2），直边接缝处 κ 从 0 跳到 1/r ⇒ 永远只是 **G1** | 把半径调大只稀释跳变（Δκ=1/r），**不会**变成 G2。要 G2 必须换族：`p>2` 或平端贝塞尔 |
| 2 | `corner-shape` 的 `superellipse(v)` 里 **p = 2^v**，不是 p=v | `round`=`superellipse(1)`=p2；`squircle`=`superellipse(2)`=p4；**v<0 ⇒ p<1 ⇒ 内凹**，不是"更方" |
| 3 | `getComputedStyle().borderRadius` 回的是**声明值**，f 缩放（用过值）读不回来 | 判据必须自己算用过半径（A2 `usedRadii()`）；本机两引擎读回 `9999px` 原样、`50%` 仍是 `50%` |
| 4 | 同元素 `clip-path` 会**连自己的 `box-shadow` 一起裁**，且**不带动** `border`/`overflow` 裁切 | `clip-path` 只能当兜底层；裁剪与阴影不要放同一元素 |
| 5 | 祖先 `overflow:hidden + border-radius` 会把后代 G2 **削平**（祖先按圆弧裁，超椭圆角外鼓被切掉） | 祖先同形状裁切或不裁；阴影留在不裁的宿主上 |
| 6 | `corner-shape` 带动 `background`/`border`/`outline`/`box-shadow`/`backdrop-filter`/`overflow` 裁切与**命中区**，但**不带动 `clip-path` 的角形** | 自带 `clip-path: inset(0 round …)` 画形的组件（液面/进度填充）无法被 `corner-shape` 迁移，只能整件留 G1（实测见 §0 命中表） |
| 7 | `CSS.supports` **不能当支持性判据**：含 `var()` 的声明一律 true，`shape(rect(...))` 回 false 而 `shape(… var())` 回 true | 判支持只能落到 `getComputedStyle()` 读回；且读回字符串随引擎版本变（150 给 `squircle`/`round`，152 给 `superellipse(2)`/`superellipse(1)`）⇒ 判据只判"两态不同 + 非默认" |
| 8 | `border-radius`、`corner-shape`、`clip-path` **三者都参与命中测试**；`backdrop-filter` 按自己的矩形 border-box 合成，祖先圆角与 `clip-path` 都拦不住它 | 每角热区代价：圆弧 `0.21460r²`、p=4 超椭圆 `0.07296r²` ⇒ 换 G2 **多回**约 2/3 的可点角区；满宽的渐隐/磨砂带会把方角露在祖先弧外（症状见 A8 末行） |

---

## 0. 一次性前置：能力探测（新项目 A1 / 迁移 B0 都要跑）

`detectG2()` 的 `CSS.supports` 只当**提示**（硬事实 7），最终结论以 `probeComputed()` 的读回为准。两段都要跑，把结果写进项目 README，后续分支决策以它为准。

```js
// g2-support.mjs — 能力探测（UA 一变就重测，别引用别人的数字）
export function detectG2() {
  const s = (prop, value) => { try { return CSS.supports(prop, value); } catch { return false; } };
  return {
    ua: navigator.userAgent,
    // ↓ 以下全是"提示"：含 var() 一律 true，shape() 的两种写法自相矛盾，别拿来定分支
    cornerShape: s('corner-shape', 'squircle'),
    superellipseAny: s('corner-shape', 'superellipse(2.322)'),
    superellipseNegative: s('corner-shape', 'superellipse(-0.2)'), // true 只代表语法合法
    cornerShapeLonghand: s('corner-top-left-shape', 'squircle'),   // 四角分写(还有 corner-start-*/end-* 逻辑轴)
    borderShape: s('border-shape', 'squircle'),                    // 草案，勿依赖
    clipShape: s('clip-path', 'shape(rect(0px 10px 10px 0px))'),   // 常为 false，但同族 var() 写法会回 true
    clipPath: s('clip-path', 'path("M 0 0 L 10 0 L 10 10 Z")'),    // 兜底路线，坐标须无单位
    ctxRoundRect: typeof CanvasRenderingContext2D !== 'undefined'
      && 'roundRect' in CanvasRenderingContext2D.prototype,
    animateCornerShape: s('transition-property', 'corner-shape'),
  };
}
// 真正的判据：写进 DOM 再读回来。readback 非默认值才算这个内核接受 corner-shape。
export function probeComputed() {
  const d = document.createElement('div');
  d.style.cssText = 'position:fixed;left:-9999px;width:80px;height:80px;border-radius:20px;corner-shape:superellipse(2);';
  document.body.appendChild(d);
  const cs = getComputedStyle(d);
  const out = { cornerShape: cs.cornerShape, tl: cs.cornerTopLeftShape, borderRadius: cs.borderRadius };
  d.remove();
  return out;   // 150 -> "squircle" / 152 -> "superellipse(2)"；内核不支持则 "round"
}
// 命中区：三点判据。(2,2) 谁都不中、(8,8) 两者都中，只有 (4,4) 能分出圆弧与超椭圆
export function hitGrid() {
  const mk = (x, css) => { const d = document.createElement('div');
    d.style.cssText = `position:fixed;left:${x}px;top:120px;width:80px;height:80px;background:#0f0;${css}`;
    document.body.appendChild(d); return d; };
  const arc = mk(140, 'border-radius:20px;'), squ = mk(260, 'border-radius:20px;corner-shape:superellipse(2);');
  const at = (d, x, dx, dy) => document.elementFromPoint(x + dx, 120 + dy) === d;
  const rows = { arc: [], squ: [] };
  for (const [dx, dy] of [[2, 2], [4, 4], [8, 8]]) {
    rows.arc.push(`${dx},${dy}=${at(arc, 140, dx, dy) ? 'hit' : 'no'}`);
    rows.squ.push(`${dx},${dy}=${at(squ, 260, dx, dy) ? 'hit' : 'no'}`);
  }
  [arc, squ].forEach((d) => d.remove());
  return rows;   // r=20：圆弧边界离角 (√2−1)r=8.284，超椭圆离角 r(√2−2^(1/2−1/4))=4.5 ⇒ (4,4) 只有超椭圆该中
}
// console.table(detectG2()); console.log(probeComputed(), hitGrid());
```

本机实测（2026-10-12，同一套探针在两个内核各跑一次；152 侧跑的是等价页面，探针函数逐字相同）：

| 探针 | Electron 43.1.1 / Chrome 150.0.7871.114 | Electron 44.7.0 / Chrome 152.0.7977.130 |
| --- | --- | --- |
| `probeComputed().cornerShape` | `squircle` | `superellipse(2)` |
| 不设形状时的 `cornerShape` | `round` | `superellipse(1)` |
| `corner-shape: superellipse(3)` 读回 | `superellipse(3)` | 未测 |
| 四角长写 | `corner-top-left-shape` 存在且生效；分写后简写读回 `round squircle squircle` | 未测 |
| `CSS.supports('corner-shape','var(--x)')` | **true** | **true** |
| `CSS.supports('clip-path','shape(rect(0px 10px 10px 0px))')` | **false** | **false** |
| `CSS.supports('clip-path','shape(from 0 0, hline to var(--w))')` | **true**（与上一行矛盾） | **true**（同） |
| `getComputedStyle().borderRadius` | `24px` / `50%` / `9999px` / 四角写 `6px 16px 16px`（三值塌缩） | 未测（工程侧同理：读回声明值） |
| `hitGrid()` 圆弧 | `2,2=no 4,4=no 8,8=hit` | `2,2=no 4,4=no 8,8=hit` |
| `hitGrid()` 超椭圆 | `2,2=no 4,4=hit 8,8=hit` | `2,2=no 4,4=hit 8,8=hit` |
| `clip-path: inset(0 round 20px)` 加不加 `corner-shape` | `4,4` 两态都 **no**，computed 读回一模一样 | `4,4` 两态都 **no** |

⇒ 六条结论：① `corner-shape` 两内核都接受，但**读回字符串不同，判据不许钉死字符串**；② `CSS.supports` 对 `var()` 恒真、对 `shape()` 自相矛盾，只能当提示；③ `border-radius` 与 `corner-shape` **都收缩命中区**，且实测 8/8 与两条解析曲线的预测同侧（离角 8.284 vs 4.5）；④ 换 G2 让角部热区**变大**（0.21460r² → 0.07296r² 的损失）；⑤ `corner-shape` 改不动 `clip-path` 的角形；⑥ computed `borderRadius` 回声明值，`50%` 不会被换算成 px。

**分支规则**：`probeComputed()` 非默认 → 走 A4/B3 的**增强层**或 C 段（真属性，`border`/`box-shadow`/`backdrop-filter`/`overflow`/命中区全部自动跟随）。回默认 → 只能走 A6/B3 的 **`clip-path` 兜底层**，并接受"不带动 border 与 box-shadow、绝对坐标要重算、角形与 corner-shape 互不相干"这三条代价。

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
  --r-pill: 9999px;   /* 药丸：故意的"写过值"，f 一定 < 1，不参与 bitten 判据（见 B6/A8） */

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
2. `--r-*` 与 `--border-width` 不是独立自由度：`p=4` 时 border 上限只有 r 的 **39.64%**（A3 闸门）。
3. 兜底层（`clip-path`）用 `@supports not (corner-shape: squircle)` 隔离，**不能与增强层同时生效**——这是 `@supports` 包裹唯一不可替代的作用；若根本没有兜底层，增强层可以不包 `@supports`（不支持的内核整条忽略声明，见 C 段）。

**参与范围（先定口径，否则验收没有边界）**：G2 只覆盖**圆角矩形**。`--r-pill` 与 `border-radius:50%` 的胶囊/圆形按设计意图排除——它们的端点是半径 = 短边一半的正半圆，"胶囊"这个身份就靠这个正圆端。注意别把排除理由写成"没有接缝"：胶囊的直边↔半圆接缝照样跳变 `Δκ = 2/h`，36px 高的胶囊是 0.055556，比 32px 半径的矩形角（0.031250）**大一倍**（见附一）。

### A2 建几何模块（形状生成 + 闸门 + 验收，全部代码在此）

```js
// g2-corners.mjs — 零依赖；Node 与浏览器同码。
// 关键设计：κ 用**解析导数**而不是差分。差分步长 h 必须远小于该点的局部特征尺度，
// 而在近接缝处（s=0.0156px 对应 θ=4.226e-7）任何可用的 h 都太大 —— 差分把 J1 量成 1.2612，
// 同一条曲线解析值是 254.1365。差分只给"第三方任意曲线"兜底用。
export function kappaNum(f, t, h = 1e-4) {
  const a = f(t - h), b = f(t), c = f(t + h);
  const d1x = (c[0] - a[0]) / (2 * h), d1y = (c[1] - a[1]) / (2 * h);
  const d2x = (c[0] - 2 * b[0] + a[0]) / (h * h), d2y = (c[1] - 2 * b[1] + a[1]) / (h * h);
  return Math.abs(d1x * d2y - d1y * d2x) / Math.pow(d1x * d1x + d1y * d1y, 1.5);
}
export const kappa = (c, t) => {
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

// f 缩放（用过半径）：CSS 真口径是**逐边求和**取最小 f，然后四角一起乘。
// getComputedStyle 读不回用过值，所以判据必须自己算；四角不等径（气泡尾巴）也必须走这条。
export function usedRadii(w, h, rs) {                 // rs = [左上, 右上, 右下, 左下]
  const e = { top: w / (rs[0] + rs[1]), right: h / (rs[1] + rs[2]), bottom: w / (rs[2] + rs[3]), left: h / (rs[3] + rs[0]) };
  const f = Math.min(1, e.top, e.right, e.bottom, e.left);
  const edge = ['top', 'right', 'bottom', 'left'].reduce((a, k) => (e[k] < e[a] ? k : a), 'top');
  return { f, used: rs.map((r) => r * f), bitten: f < 1, edge, tight: e[edge] };
}
export function usedRadius(w, h, r) {
  const u = usedRadii(w, h, [r, r, r, r]);
  return { f: u.f, used: r * u.f, bitten: u.bitten, kSeam: 1 / (r * u.f) };
}
// 档位闸门：1/κ峰值 = 最大可用内偏移（border-width / inset spread / outline-offset），超过就自交
export function cornerGate(p, r, w) {
  const k = peakKappa(superCorner(r, p));
  return { p, r, kPeak: k, safeInner: 1 / k, want: w, ok: 1 / k >= 1.2 * w };
}
// G1 ⊂ G2 的支点：整块参照系里超椭圆边界半径 / 同半径圆弧半径，恒 >=1
export const radialRatio = (theta, p) => 1 / Math.pow(Math.pow(Math.cos(theta), p) + Math.pow(Math.sin(theta), p), 1 / p);
export const diagRadius = (p) => Math.pow(2, 0.5 - 1 / p);                       // 沿对角方向的边界半径 / r(p=2 时 =1)
export const bulge = (p) => diagRadius(p) - 1;                                   // = (√2−1)−(√2−diagRadius),沿对角朝盒角外扩 / r
// 嵌套等距判据：内侧(半径 rin,盒角内缩 d)到外侧(半径 R)轮廓的最近距离
export function offsetGap(p, R, d, rin, N = 2000) {
  const cur = (r, pp) => (pp === 2 ? arcCorner(r) : superCorner(r, pp));
  const o = cur(R, p), i0 = cur(rin, p);
  const OP = [], IP = [];
  for (let k = 0; k <= N; k++) { const t = (Math.PI / 2) * k / N; OP.push(o.P(t)); IP.push([d + i0.P(t)[0], d + i0.P(t)[1]]); }
  let mn = Infinity, mx = 0;
  for (const [x, y] of IP) {
    let g = Infinity;
    for (const [a, b] of OP) g = Math.min(g, Math.hypot(x - a, y - b));
    mn = Math.min(mn, g); mx = Math.max(mx, g);
  }
  return { min: mn, max: mx, spread: mx - mn };
}

// SVG path：整块圆角矩形，四角全用平端 C 段（两个控制点写成同一个盒角）
export function rectPath(w, h, r) {
  const R = Math.min(r, w / 2, h / 2);
  return `M ${R},0 H ${w - R} C ${w},0 ${w},0 ${w},${R} V ${h - R} C ${w},${h} ${w},${h} ${w - R},${h}`
       + ` H ${R} C 0,${h} 0,${h} 0,${h - R} V ${R} C 0,0 0,0 ${R},0 Z`;
}

// 超椭圆整块矩形点集（clip-path / Canvas 用）；一份左上角模板，其余三角靠镜像。
// rs 可以是单个 r（四角等径）或 [左上,右上,右下,左下]；每个角按自己的半径各算自己的段数。
export function rectPoints(w, h, rs, p, eps = 0.5) {
  const arr = Array.isArray(rs) ? rs : [rs, rs, rs, rs];
  const R = arr.map((r) => Math.min(r, w / 2, h / 2));
  const corner = (r) => { const c = superCorner(r, p), seg = segPerCorner(c, eps), A = [];
    for (let i = 0; i <= seg; i++) A.push(c.P((Math.PI / 2) * i / seg));   // (0,r)->(r,0)
    return A; };
  const TL = corner(R[0]), TR = corner(R[1]), BR = corner(R[2]), BL = corner(R[3]);
  const pts = [];
  const push = (q) => { const l = pts[pts.length - 1];
    if (!l || Math.abs(l[0] - q[0]) > 1e-9 || Math.abs(l[1] - q[1]) > 1e-9) pts.push([+q[0].toFixed(3), +q[1].toFixed(3)]); };
  push([R[0], 0]); push([w - R[1], 0]);
  TR.slice().reverse().forEach(([x, y]) => push([w - x, y]));    // 右上 (w-r,0)->(w,r)
  push([w, h - R[2]]);
  BR.forEach(([x, y]) => push([w - x, h - y]));                  // 右下 (w,h-r)->(w-r,h)
  push([R[3], h]);
  BL.slice().reverse().forEach(([x, y]) => push([x, h - y]));    // 左下 (r,h)->(0,h-r)
  push([0, R[0]]);
  TL.forEach(([x, y]) => push([x, y]));                          // 左上 (0,r)->(r,0)
  return pts;
}
// clip-path 的 path() 数字**必须无单位**（写 10px 被拒），且是绝对坐标 ⇒ 尺寸变了要重算
export const clipPathOf = (pts) =>
  `path("M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z")`;
export const svgPolyOf = (pts) =>
  `M ${pts[0][0]},${pts[0][1]} ${pts.slice(1).map((q) => `L ${q[0]},${q[1]}`).join(' ')} Z`;
// 解析 getComputedStyle(el).borderRadius 的真实序列化形态（塌缩、斜杠、百分比）
export function radiiFrom(spec) {
  const parts = String(spec).trim().split('/').map((s) => s.trim());
  const expand = (v) => { const a = v.split(/\s+/).map(parseFloat);
    return a.length === 1 ? [a[0], a[0], a[0], a[0]]
         : a.length === 2 ? [a[0], a[1], a[0], a[1]]
         : a.length === 3 ? [a[0], a[1], a[2], a[1]]
         : a.slice(0, 4); };
  const X = expand(parts[0]), Y = parts[1] ? expand(parts[1]) : X;
  const percent = /%/.test(parts[0]) || (parts[1] ? /%/.test(parts[1]) : false);
  return { radii: X, percent, ellipse: percent || X.some((v, i) => v !== Y[i]) };
}

// 档位量表：一次把所有参与元素过两道闸门（描边上限 + 短边咬合），"该不该降档"变成一张表
export function tierTable(rows, p) {
  return rows.map(([name, r, w, h, bw]) => {
    const g = cornerGate(p, r, bw), u = usedRadius(w, h, r);
    return { name, r, box: `${w}x${h}`, safeInner: +g.safeInner.toFixed(3), want: bw,
             ok: bw === 0 ? 'n/a' : g.ok, marginPx: +(Math.min(w, h) - 2 * r).toFixed(1), bitten: u.bitten };
  });
}

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
// J5 渲染层双真值：第 row 行(0<=row<=R)形状边界离角列的内缩量。像素取边跟这两条比，
// 且**两条必须同时报**——只报"离超椭圆很近"是假绿，形状没换时它正好离圆弧也近。
export const seamArc = (row, R) => R - Math.sqrt(R * R - (R - row) * (R - row));
export const seamSuper = (row, R, p) => R - R * Math.pow(1 - Math.pow((R - row) / R, p), 1 / p);
```

### A3 选档位（先过闸门，再谈手感）

`1/κ峰值` 就是这一档**最大可用内偏移**。r=24 时的实测闸门值（`cornerGate(p,24,w)` 应逐个复现）：

| 形状 | p | `κ峰值` | `1/κ峰值` = 最大内偏移 | `1/κ峰值 ÷ r` |
| --- | --- | --- | --- | --- |
| 圆弧 `round` | 2 | 0.041667 | 24.000 | 1.0000 |
| `superellipse(1.4)`≈ | 3 | 0.074242 | 13.470 | 0.5612 |
| `squircle` | 4 | 0.105112 | 9.514 | 0.3964 |
| ≈ | 5 | **0.135375** | 7.387 | 0.3078 |
| 平端贝塞尔 | ≈5.19 | 0.157135 | 6.364 | — |

判定顺序（任一不过就降档，别硬上）：

```js
import { cornerGate, g2report, usedRadii, tierTable } from './g2-corners.mjs';

const shape = 4;                              // squircle
// 用法一：单档手算（不等径就传四个角）
usedRadii(240, 96, [24, 24, 24, 24]);          // → { f: 1, bitten: false, edge: 'top' }
usedRadii(240, 40, [24, 24, 24, 24]);          // → f=0.8333，四角一起缩到 20 ⇒ 闸门要按 used 重算
usedRadii(200, 42.475, [6, 16, 16, 16]);       // 气泡：左上尾巴 6，其余 16 → f=1，最紧的边是 right
cornerGate(4, 24, 3);                          // 描边 3px → safeInner=9.514 ⇒ ok:true
g2report(4, 24);                               // → j1=254.1365 slope=2 grade:'G2+够G3'

// 用法二：整张量表（参与 G2 的每个元素一行），跑出来就是 C 段那张表
console.table(tierTable([
  ['card', 32, 300, 780, 0], ['chip', 10, 60, 27, 1], ['cell', 8, 17.8, 17.8, 0], ['badge', 6, 20, 20, 0],
], shape));
```

- **不要用大半径冒充 G2**：圆弧 `Δκ=1/r`，r=8→0.125000、r=48→0.020833，但 J1 永远是 `1.00×`。
- **不要盲目追大 p**：p=8 时接缝好四个数量级，但 `1/κ峰值` 只剩 ~4.45px（r=24），3px 描边就没余量了。
- `p<2`（bevel/scoop 方向）与 `p<1`（内凹）是两种风险，需单独审批。
- 外扩常数 `bulge(p) = 2^(1/2−1/p) − 1`：p=2 为 0.000000、p=3 0.122462、**p=4 0.189207**、p=5 0.231144、p=8 0.296840。别把 `2^(1/p)` 当对角半径通式——它只在 p=4 与通式同值（1.18921），p=3 就差 12%。

### A4 CSS 三层接入（组件层写法）

```css
/* 增强层：卡片/按钮/头像 —— border、box-shadow、backdrop-filter、命中区会跟着一起变角形 */
.avatar {
  border-radius: var(--radius-avatar);            /* ① 保底，永远写 */
  border: 2px solid var(--line);                  /* 2px < 9.514px 上限（p=4/r=24），A3 闸门 */
  box-shadow: 0 2px 12px rgba(0,0,0,.18);
}
@supports (corner-shape: squircle) {
  .avatar { corner-shape: var(--g2-shape); }      /* ② 增强 */
}

/* 兜底层（不支持 corner-shape 时）：由 JS 注入 --g2-clip */
@supports not (corner-shape: squircle) {
  .avatar { clip-path: var(--g2-clip); }          /* ③ 三条代价见 §0 分支规则 */
}
```

```js
// 兜底层的 --g2-clip 生成 + 尺寸变化重算
import { rectPoints, clipPathOf, radiiFrom, usedRadii } from './g2-corners.mjs';

function applyG2Clip(el, p = 4, eps = 0.5) {
  const { width: w, height: h } = el.getBoundingClientRect();
  const cs = getComputedStyle(el);
  if (cs.clipPath !== 'none' && !cs.clipPath.startsWith('path(')) return null;
  //   ↑ 组件自己用 clip-path 画形（液面/进度/遮罩）时不要抢：corner-shape 带不动它，
  //     两条 clip 互相覆盖只会把组件画坏。这种元素整件留 G1（见 A8 与 C6）。
  const rs = radiiFrom(cs.borderRadius);
  if (rs.percent || rs.ellipse) return null;
  //   ↑ 百分比/两轴不等的椭圆角在 path() 里没有等价形状，宁可不动（A8 禁第 3 行）
  const used = usedRadii(w, h, rs.radii);
  if (!used.used.some((r) => r > 0)) return null;                 // 无半径就别裁
  el.style.setProperty('--g2-clip', clipPathOf(rectPoints(w, h, used.used, p, eps)));
  if (!applyG2Clip.obs) {                                          // 尺寸变了必须重算（path() 是绝对坐标）
    applyG2Clip.obs = new ResizeObserver((es) => es.forEach((e) => applyG2Clip(e.target, p, eps)));
  }
  applyG2Clip.obs.observe(el);
}
```

**注意上面用的是 `usedRadii(...).used` 而不是声明值**：`getComputedStyle().borderRadius` 回 `9999px` 时盒子实际半径是短边一半（硬事实 3），照声明值生成 path 会把角画到盒外。

兜底层的**两条必写注释**：① 描边要抬到父层（同元素 border 不会被裁）；② 祖先若带圆弧 + `overflow:hidden`，后代的 G2 会被削平，父层也得同形状或去掉裁剪。

本机渲染实测（增强层路径，Chromium 152）：`clipPathOf` 生成的串被引擎**接受**（`getComputedStyle().clipPath` 读回 `path("M 24 0 L 72 0 …")` 而非 `none`），命中区同步收缩。`96×96 / r=24 / p=5 / ε=0.5` 时串长 **515 字符**（每角 8 段，39 个点）；同一尺寸取 `r=48` 是 **737 字符**（每角 12 段，51 个点）。

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
本机渲染实测（dpr=1，S=96、p=4、ε=0.5）：错误版覆盖率 0.5036 ⇒ 自交；改成 `rectPoints` 后 0.93186，解析值 0.92705（差 0.5%，属边缘像素计数）。`rectPoints()` 内部已处理正反序，并用鞋带面积校过：折线覆盖率 p=4 **0.923937**、p=5 **0.948204**，解析 `B(p)=∫₀¹(1−u^p)^(1/p)du` 对应 **0.927037** 与 **0.950150**——折线略小是对的（内接），反过来就要怀疑代码。

`ctx.roundRect()` 只是便利方法，画的是**圆弧**（G1）；要 G2 就走上面的自绘或改用 `corner-shape`。

### A6 clip-path 兜底的段数从哪来

段数 **∝ √边长**（`κ峰值 ∝ 1/S`、弧长 `∝ S`）。n=5、ε=0.5px 的实测需求（每角）：

| S | 32 | 48 | 96 | 128 | 256 | 512 |
| --- | --- | --- | --- | --- | --- | --- |
| 每角段数 | 7 | 8 | 12 | 13 | 19 | 26 |
| `κ峰值` | 0.203063 | 0.135375 | 0.067688 | 0.050766 | 0.025383 | 0.012691 |

（S=整块边长，每角半径 = S/2。别"看着差不多"手填：S 从 32→512（16 倍）段数只需约 4 倍（7→26）。`segPerCorner()` 就是这张表的算法来源；四角不等径时每个角各算各的。）

### A7 验收：六道判据 + PASS 条件

| 判据 | 定义 | 阈值 | 圆弧该给 | p=4/r=24 该给 | 层 |
| --- | --- | --- | --- | --- | --- |
| **J1** | `κ(s=0.25) ÷ κ(s=0.0156)` | ≥2 ⇒ G2 | `1.00×` | `254.1365×`（解析 256.8212，差 −1%） | 解析 |
| **J2** | log-log 斜率，窗 s∈[0.25,2] | ≈p−2；≥1.2 ⇒ 够 G3 | `0.000` | `2.000` | 解析 |
| **J3** | 折线逐点取**最近线段**再取最大 | ≤0.5px（对齐用 0.05px） | — | 12 段/角 → 0.088px（6 段 0.356、3 段 1.467） | 解析 |
| **J4** | 构造间最大射线偏差 | ≤0.2px | — | 平端贝塞尔 vs n=5 → 0.151px | 解析 |
| **J5** | 像素取边对**两条真值**并列（`seamArc`/`seamSuper`） | 离超椭圆 ≪ 离圆弧，且两列同时报 | 离圆弧 ≈ 0 | 0.6~2.1px vs 10.6~12.4px | 渲染 |
| **J6** | 命中三点判据（`hitGrid`） | 与 `bulge` 预测同侧 | `(4,4)=no` | `(4,4)=hit` | 渲染 |

```js
import { g2report, superCorner, segPerCorner, j3Corner, usedRadii, cornerGate } from './g2-corners.mjs';

// ① 对照组：圆弧必须是 G1 / j1≈1.0000 / slope≈0.0000，否则判据代码在撒谎
console.table([g2report(2, 24), g2report(3, 24), g2report(4, 24), g2report(5, 24)]);
// Node v24 实测输出：
//   p=2  j1=1.0000   expect=1.0000   slope=0.0000  kPeak=0.041667  safeInner=24.000  grade=G1
//   p=3  j1=16.0299  expect=16.0256  slope=1.0003  kPeak=0.074242  safeInner=13.470  grade=G2
//   p=4  j1=254.1365 expect=256.8212 slope=2.0000  kPeak=0.105112  safeInner=9.514   grade=G2+够G3
//   p=5  j1=3963.326 expect=4115.7239 slope=3.0000  kPeak=0.135375  safeInner=7.387   grade=G2+够G3
//   （j1 与解析值差 −1% 与 −3.7% 量级，来自近接缝取样网格，不影响档位判定）

// ② J3：段数由 segPerCorner 给，别手填
const c = superCorner(48, 5);              // S=96 的整块 squircle，每角半径 = S/2
const seg = segPerCorner(c, 0.5);          // → 12（A6 表）
const dev = j3Corner(c, seg);              // → 0.088px
console.log(`J3 每角 ${seg} 段，最大偏离 ${dev.toFixed(3)}px`, dev <= 0.5 ? 'PASS' : 'FAIL');
console.log(`段数砍半 → ${j3Corner(c, 6).toFixed(3)}px，再砍 → ${j3Corner(c, 3).toFixed(3)}px`);  // 0.356 / 1.467 ⇒ 已越阈值

// ③ 用过半径 + 闸门：短边不够就先按用过值算，再谈描边放不放得下
console.log(usedRadii(100, 40, [30, 30, 30, 30]), cornerGate(4, 20, 2));   // f=0.6667 → used 全 20
```

**判据五条纪律**（都是踩出来的）：

1. **绝对值判据会被"调大半径"骗过去**，必须用比值。
2. 任何"偏差为 0"类判据，**先跑圆弧对照组**，p=2 那行不为 `0.000` 就先修测量代码。
3. θ 网格不能用均匀的（`i^4` 才在近接缝有分辨率）。
4. **解析层阈值不许套到渲染层**：J3 的 ≤0.5px 是"折线 vs 解析曲线"，真实像素取边正常落在 0.6~2.1px（行量化 + 抗锯齿）；拿 0.5px 去卡像素判据会把合规态判成失败。
5. **渲染层判据要自己带对照**：J5 必须两列并列（离超椭圆 / 离圆弧），形状没换时"离超椭圆"会很大；J6 的 `(2,2)` 与 `(8,8)` 两点是正负对照，只有 `(4,4)` 有区分度——先确认对照点符合预期再读判据点。

### A8 硬性禁令

| 禁止 | 原因 | 正确做法 |
| --- | --- | --- |
| 同元素 `clip-path` + `box-shadow` | 阴影连元素一起被裁，直接消失 | 阴影放未裁剪的父层，或走 `corner-shape` |
| 祖先 `overflow:hidden + border-radius` 裁 G2 后代 | 祖先按圆弧裁，把外鼓的超椭圆角削平 | 祖先同形状裁切或不裁；阴影留在不裁的宿主上 |
| 给自带 `clip-path: inset(… round …)` 画形的组件挂 `corner-shape` | `corner-shape` **改不动 clip-path 的角形**（两引擎实测：加与不加，`(4,4)` 都不命中，computed 读回一字不差）→ 盒体变超椭圆、内部液面还是圆弧，两者错位 | 整件保留 G1，并在清单里写明理由（C6） |
| 用 `CSS.supports` 判支持 | 含 `var()` 一律 true；`shape(rect(…))` false 而同族 `shape(… var())` true | `getComputedStyle()` 读回，且判"两态不同"而非钉死字符串 |
| 用声明值算曲率 / 生成 path | f 缩放读不回：100×40 写 24/30/60px 实际全用 20px | 一律先 `usedRadii()`，clip 路线用 `.used` |
| `border-radius:0` + `corner-shape` | `corner-shape` 依赖非零半径（声明还在，但没有角可变形） | 保底半径必须写 |
| `corner-shape: superellipse(-0.2)` 当"更方" | p=2^−0.2≈0.87<1 ⇒ **内凹** | 要更方走 `p∈(2,4)` 即 v∈(1,2)，或 `bevel` |
| 内角写成"外角 − border-width"（G2 形状下） | 真内边界是法向等距线，偏差 ≈ `bulge(p)·w`（p=4 → 0.189w）；圆弧才精确 | `offsetGap()` 量，或 `p=2` 才用这公式 |
| 药丸宽度动画无条件安全 | 咬合点在 `w=h`，窄于此 κ 被钉成 `2/w`（w=20 → +60%） | 保证最窄档 `w ≥ h`，或宽度动画走自绘 |
| 用 `border-radius:50%` 做扁条药丸 | 按各自轴 ⇒ 1/4 椭圆，两轴 κ 不对称 `(a/b)³`（200×80 → 15.63×，320×40 → 512×） | `border-radius: 9999px` |
| 把 `--r-pill` 的 `bitten` 当缺陷报 | 9999px 在任何盒子上 f<1，这是**有意的超值写法** | 药丸按 A1 口径不参与 G2，量表里排除 |
| 拿模糊图做阴影判据 | 模糊把轮廓糊掉，判不出形状 | 先把轮廓**外偏移成线**再量（B4 第 2 项） |
| 只报"阴影还在"当"阴影形状对" | alpha 有值只证存在，不证轮廓是 G2 | 外偏移取边对 `seamArc/seamSuper` 比，或明确写"只验存在" |
| 把解析阈值套到像素测量（反之亦然） | 两种量的噪声源不同 | 阈值跟层一起标（A7 表的"层"列） |
| 满宽 `backdrop-filter` 带放在圆角容器里 | 它按自己的矩形 border-box 合成，祖先 `border-radius`/`overflow`/`clip-path` **都拦不住**，方角会露在弧外（饱和差实测 27 → 0） | 给这层自己挂 `border-radius:inherit; corner-shape:inherit`，或撤掉它的模糊 |

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
  ['R8 内角公式', /border-radius\s*:\s*calc\([^;\n]*-[^;\n]*\)/, null, '内角=外角−边框宽只在 p=2 精确；G2 下偏差≈bulge(p)·w'],
  ['R9 形状带不动', /clip-path\s*:[^;\n]*round/, null, 'corner-shape 改不了 clip-path 的角形；这个元素的可见角由 clip-path 说了算，考虑整件留 G1'],
  ['R10 磨砂外漏', /backdrop-filter\s*:/, null, '它按自己的矩形 border-box 合成，祖先圆角与 clip-path 都拦不住；确认这层有没有方角露在祖先弧外'],
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

R1–R10 是**行级启发式**，跨行/跨规则块的关系（比如祖先与后代、R2 的"同规则块"）它判断不了，必须按输出人工过一遍——这正是它只读的原因。

本技能定稿时在一个真实工程 `src/` 上实跑（只读，30 个文件）：命中 82 处 = R1 39 / R5 18 / R10 9 / R4 8 / R3 5 / R9 2 / R2 1（R6、R7 各 0）。**R9 的 2 条里有 1 条落在 CSS 注释文本上**（那行注释正好写了 `clip-path: inset(0 round …)`）——命中里混着注释与字符串是这套启发式的固有噪声，人工过一遍不是形式主义：把注释当成待改元素去改，就会凭空动刀。

### B1 归类现有半径 → 令牌档

把盘点出的散值按"就近档"收进 `--r-*`，**一次只收一档**，每档改完就跑 B6 验收：

```bash
# 先看有哪些值在仓库里出现过（示例：只统计 CSS）
grep -rEho 'border-radius:[^;}]+' src/ | tr -d ' ' | sort | uniq -c | sort -rn
```

归类原则：同值不同语义（卡片 12px / 按钮 12px）要**分开两个令牌**，别合并成 `--r-md` 后一起动。浮点/百分比一律换算成 px 再入档。

**允许"G1 孤岛"，但必须写明理由**。三类是正当孤岛：① 组件自己用 `clip-path` 画形（R9）；② 药丸/圆形按 A1 口径本来就不参与；③ 内角小于 ~8px 的角，接缝跳变 `1/r` 大到肉眼看不出差别，改造成本换不来观感。每条孤岛在盘点表里留一行"为什么留"，别让它变成沉默的例外。

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
| `clip-path` | `circle()`/`inset(round)` | `clipPathOf(rectPoints(...))` | 数字**无单位**；不带动 border/shadow；尺寸变了要重算；与 corner-shape 互不相干 |

单点翻形状：`--g2-shape: round` → `squircle`。**一次只动一个形状档**，动完跑 B4 的五个回归项。

### B4 五个必检回归项（每项：症状 / 判据 / 修法）

1. **描边与 focus ring**：症状=内角出现两道缝或 ring 起节。判据=`cornerGate(p, usedR, borderWidth).ok`，且 `w ≤ 1/κ峰值`（p=4/r=24 是 9.514px，p=5 是 7.387px）。修法=减描边/升半径/生成真等距线；`p=2` 才可用"外角−w"。
2. **阴影**：症状=大模糊下接缝仍"起节"。判据=外偏移 d 后 `Δκ=1/(R+d)`（R=24：d=6→0.033333、d=24→0.020833、d=48→0.013889，**恒不为 0**）。修法=换形状族；**别拿模糊图做判据**，先把轮廓外偏移成线再量——"阴影还在"（在轮廓外取到 alpha）只是存在性，不是形状判据。
3. **嵌套**：症状=内层角与外层角不同心、看到两条线。判据=`offsetGap(p, R, padding, rin).spread`（圆弧对照组 `p=2,R=12,d=5,rin=7` 给 **min 5.000 / max 5.000**，即等距）；超椭圆下"外角−净空"不等距：`p=4,R=12,d=5,rin=7` → 5.000~5.853，`rin=10`（真实工程里的写法）→ **5.019~6.417**，浮动 1.399px。修法=内层用等距线生成，或按 `max(R−padding, 6)` 并显式接受（内角比外角更圆时不会戳出外轮廓，属可接受偏离，但要记下来）。
4. **动画**：症状=滑动指示器/tab 宽度变小时角"跳"。判据=两处咬合线 `2r ≤ w`（定值 r）与 `w ≥ h`（药丸）。修法=满足咬合条件，或宽度动画走 `rectPath()` 逐帧重算；`corner-shape` 动画首帧是 G1。
5. **写过值**：症状=改半径没反应。判据=`usedRadii(w,h,radii).bitten`，并读 `.edge` 看是哪条边咬住的。修法=按短边重排档位（100×40 上 24/30/60px 全等于 20px；四角不等径要看逐边和，不是拿单个 r 除）。

### B5 回退开关（必须留，且是单变量）

```css
/* 一行回退到迁移前形态：p=2 圆弧。形状与半径两个轴不互相补偿 */
:root[data-g2='off'] { --g2-shape: round; }
```

```js
// 运行时开关（灰度/线上急救）：形状切换不重建 DOM，只改自定义属性
document.documentElement.dataset.g2 = 'off';   // 'on' 恢复
```

兜底层（`clip-path`）的回退**不能**靠这个开关——它由 `@supports not (…)` 决定，所以增强层与兜底层必须互斥，切换形状档时两层都要同步改。**开关必须自证**：两态各读一次 `getComputedStyle(el).cornerShape`，两个字符串不同才算开关真的换过形状；相同则说明要么声明没生效，要么被更高特异度的规则盖住（`:root[data-*]{…}` 是 0,2,0，会吃掉 `:root{…}` 的 0,1,0）。

### B6 验收与提交前检查表

```js
import { g2report, cornerGate, usedRadii, rectPath, rectPoints, clipPathOf, segPerCorner, superCorner } from './g2-corners.mjs';

console.table([g2report(2, 24), g2report(4, 24)]);        // 必须 G1/1.00× 与 G2/≥2
console.log(cornerGate(4, usedRadii(240, 96, [16, 16, 16, 16]).used[0], 2).ok);  // 描边闸门
console.log(rectPath(200, 120, 24));                     // 与 §A5 的 d 逐字符相同
console.log(segPerCorner(superCorner(48, 5), 0.5), clipPathOf(rectPoints(96, 96, 48, 5, 0.5)).length);  // → 12 737
```

- [ ] `g2report(2, r)` 是 `G1 / j1≈1.0000 / slope≈0.000`（**对照组**，不过就说明判据代码坏了）
- [ ] 目标档 `j1 ≥ 2`，需要 G3 时 `slope ≥ 1.2`
- [ ] 所有描边/inset/outline-offset 过 `cornerGate(...).ok`
- [ ] 每个参与元素过 `usedRadii(...).bitten === false`，或已按用过值重排（药丸/`50%` 按 A1 口径不在这一项里）
- [ ] 没有任何元素同时带 `clip-path` 与 `box-shadow`
- [ ] 祖先链上没有 `overflow:hidden + border-radius` 压在 G2 元素外面
- [ ] 满宽/大尺寸的 `backdrop-filter` 层没有方角露在祖先弧外
- [ ] 自带 `clip-path: inset(… round …)` 的组件没有被单独挂 `corner-shape`
- [ ] 嵌套内角过 `offsetGap().spread`，或已按 `max(R−padding, 6)` 显式接受
- [ ] 动画档：`2r ≤ 最窄宽度`（定值）或 `最窄宽度 ≥ h`（药丸）
- [ ] 渲染层：同一张表里"离超椭圆"与"离圆弧"两列并列，前者明显小（J5）
- [ ] 命中区：`hitGrid()` 三点与预测同侧（J6）
- [ ] `--g2-shape: round` 单行回退实测有效（两态 computed 读数不同）
- [ ] 控制台 0 报错

---

## C. 单引擎最短路径（一条声明 + 渲染层验收）

**适用条件**：§0 的 `probeComputed()` 非默认，且交付面只有这一个内核（Electron/Tauri 桌面端、内部工具），不要求 Safari/Firefox。满足时 **A4/A5/A6 的 JS 生成路线整段跳过**——原生属性会带着 `background`/`border`/`outline`/`box-shadow`/`backdrop-filter`/`overflow` 裁切/命中区一起变形，零 DOM、零 JS、零新增层。真实工程样本：透明无边框 IM 窗口，14 个圆角矩形共用一条规则，Chromium 152。

### C1 令牌（形状与半径两套，形状是单点开关）

```css
:root {
  /* 圆角：border-radius 全部生效，它决定盒子尺寸、阴影基准半径，也是 corner-shape 不支持时的兜底轮廓 */
  --r-window: 32px;   /* 窗口 + 左右两张悬浮卡共用同一令牌，天然一致 */
  --r-card: 16px;  --r-chip: 12px;  --r-md: 10px;  --r-sm: 8px;  --r-xs: 6px;
  --r-bubble-tail: 6px;              /* 四角不等径的那一个角 */
  --r-pill: 999px;                   /* 参与范围之外：胶囊端点保持正圆 */

  /* G2：corner-shape 的指数是 p = 2^v，所以 superellipse(2) = p4 = squircle、(3) = p8 更方、
     round = p2 就是原来的圆弧（等价于不开）。p=4 时角部沿对角朝盒角外扩 0.18921r
     （r=32 → 6.05px，16 → 3.03，8 → 1.51）。引擎门槛 Chromium 139+；不支持时整条声明被忽略，
     画面自动退回圆弧——不需要 JS，也不需要 @supports 分支。 */
  --g2-shape: superellipse(2);
}
```

### C2 一条分组规则（组件层零改动）

```css
/* 覆盖面 = 所有圆角矩形；--r-pill 与 50% 圆形不在此列（没有"直线→正圆端"的设计意图可换）。
   写法要点：var(--g2-shape, round) 让开关缺省时就是圆弧，分组选择器只列一遍。 */
.window, .sidebar, .right-panel, .chat-area, .bubble, .thumb, .quote-card,
.chip, .chat-item, .emoji-cell, .menu-btn, .win-menu, .win-menu-btn, .app-badge {
  corner-shape: var(--g2-shape, round);
}
```

样本里 `.bubble` 同时带 `border-radius: var(--r-card)` 与 `border-top-left-radius: var(--r-bubble-tail)`——**四角不等径不影响 G2 成立**（接缝处曲率仍为 0），但所有判据必须走 `usedRadii()` 的逐边口径。

### C3 量表：一次把 14 档过两道闸门

`tierTable(rows, 4)` 的输出（r=1/κ峰值 一列恒为 **0.3964r**；`bw` 是该元素的 `border-width`）：

| 元素 | r | 最小盒 | 描边上限 `1/κ峰值` | bw | 闸门 | 短边咬合余量 |
| --- | --- | --- | --- | --- | --- | --- |
| `.window` | 32 | 1248×800 | 12.685px | 0 | n/a | 736.0px |
| `.sidebar` | 32 | 300×780 | 12.685px | 0 | n/a | 236.0px |
| `.right-panel` | 32 | 300×780 | 12.685px | 0 | n/a | 236.0px |
| `.chat-area` | 32 | 600×780 | 12.685px | 0 | n/a | 536.0px |
| `.bubble` | 16 | 200×42.5 | 6.342px | 0 | n/a | 10.5px |
| `.bubble .thumb` | 14 | 300×170 | 5.550px | 0 | n/a | 142.0px |
| `.quote-card` | 12 | 200×35.5 | 4.757px | 0 | n/a | 11.5px |
| `.chip.add` | 10 | 60×27 | 3.964px | **1px** | **过** | 7.0px |
| `.chat-item` | 12 | 284×72 | 4.757px | 0 | n/a | 48.0px |
| `.emoji-cell` | 8 | 17.8×17.8 | 3.171px | 0 | n/a | **1.8px** |
| `.menu-btn` | 8 | 32×32 | 3.171px | 0 | n/a | 16.0px |
| `.win-menu` | 12 | 120×44 | 4.757px | 0 | n/a | 20.0px |
| `.win-menu-btn` | 10 | 34×34 | 3.964px | 0 | n/a | 14.0px |
| `.app-badge` | 6 | 20×20 | 2.378px | 0 | n/a | 8.0px |
| `.hold-button`（G1 孤岛） | 12 | 60×28 | 4.757px | 0 | n/a | 4.0px |

三条读法：① 唯一的描边（1px dashed）离上限 3.964px 很远，**形状档不用降**；② 最紧的是格子控件，1.8px 来自侧栏下限那一档（`clamp` 兜底宽 220 → 每格 `(220−28−32)/9 ≈ 17.8`，推导值；真窗最小宽下是 26.7px，余量 10.7px）；③ 余量小于 `2r×10%` 的档要么钉住最窄宽度，要么在量表里标红。

### C4 五步验收（每步都要能自己失败）

| 步 | 判据 | 样本实测（Chromium 152） |
| --- | --- | --- |
| ① 形状换了 | 两态各读 `getComputedStyle(el).cornerShape`，**两个字符串不同**（别钉死内容：150 给 `squircle`/`round`，152 给 `superellipse(2)`/`superellipse(1)`） | `superellipse(2)` vs `superellipse(1)` |
| ② 画出来的轮廓是超椭圆 | 截图逐行取边，对 `seamSuper`/`seamArc` **两列并列**；边界按**行中心**代入（角弧在切点附近一个行内能走掉 16px，按行上沿比会把 row0 误判成 16px 偏差） | 窗口四角 0.6/0.6/2.1/2.1px 对超椭圆，10.6/10.6/12.4/12.4px 对圆弧；左卡片 2.1/2.1/1.1/1.1 对 12.4/12.4/11.4/11.4；reduced-motion 态与常态逐位相同 |
| ③ 阴影与磨砂没丢 | 同一张 PNG 上轮廓外取样有值 + `backdrop-filter` 计算值原样；**这只证存在**，要证形状按 B4 第 2 项外偏移成线 | 窗口轮廓外 1~6px 处 alpha 7~11；卡片左带亮度 253→248 渐变到卡身 41；五处 `backdrop-filter` 原样（`blur(28px) saturate(1.4)` / 其余 `blur(20px)`） |
| ④ 布局不改 | 每一档宽度在**两种形状下各量一次**，要求逐格相同；只在开启态量=抓不到"共享开关规则的特异度压过 `@container`"这一族缺陷 | 样本当时跑的四挡在两态下逐格相同（该工程后来把下限改成 1248、旧四挡作废，这是样本的口径变迁，不影响"两态各量一次"这条做法）；CSS 19.23 kB（gzip 4.71）/ JS 457.26 kB（gzip 157.15），纯 CSS 增量≈0 |
| ⑤ 帧率与热区 | 帧 A/B 带**前置断言**（读到两态 shape 不同才继续）；热区按 J6 三点判据 | 各 432 帧，两侧 p50 6.9 / p95 7.0 / max 7.1ms 完全相同；令牌化后复采各 216 帧，p50 两侧 6.9，p95 7.0 vs 7.1、max 7.1 vs 7.2（0.1ms 落在采样抖动量级） |

**量不到怎么办**：样本里的右卡片玻璃与背景光斑亮度同档，阈值取不到边 ⇒ 这一格如实写"未取到"，改由 ①（computed 读数）+ 与左卡片共用同一条规则来覆盖。**别为了让表好看把阈值调小去凑**——那会把判据调成假绿。

### C5 回退

单变量：`--g2-shape: round`（或删掉那条声明）即整体回到圆弧；半径档独立可回。内核不支持时不需要任何代码分支——声明被忽略，画面本身就是圆弧。

### C6 保留的 G1 孤岛（样本一条）

快捷短语那颗按钮的组件自己用 `clip-path: inset(0 round var(--hb-radius))` 画液面与外框。给它挂 `corner-shape` 只会让**盒体变超椭圆、内部液面仍是圆弧**，按住时两者角部错开。结论：整件保留 G1 并在样式表里写明原因。这是硬事实 6 的直接后果，不是妥协没做够。

### C7 被否决的两条路（样本实测代价，别重走）

| 路 | 能做什么 | 实测代价 |
| --- | --- | --- |
| `clip-path: shape()` | 纯 CSS 一条声明 | Chromium 150 下 `from/close/z/%/calc/curve-to/arc-to/smooth-to/相对 by/填充规则/无逗号` **12 种写法 computed 全读回 `none`**，同页 `circle()/inset()/path()` 都能读回；含 `var()` 的声明 `CSS.supports` 一律 true ⇒ 判支持只能读回 |
| JS 逐元素生成 `clip-path: path()` | 能出形（按**角度**等分采样，SEG=12 最大几何误差 7.16e-3r；像素拟合对超椭圆 0.4~2.1px、对圆弧 10.6~12.4px） | 带阴影的表面每个要加一层 `::before` 重画；伪元素**不能** `background: inherit`（负 z-index 画在宿主背景**之上**，0.92/0.72 这类半透明表面叠两层会变实）；宿主的 `overflow:hidden` 必须放开；共享开关规则（特异度 0,2,1）会盖过 `@container` 里的规则（0,1,0）——**只在 on/off 对照下才看得见**；自定义属性会继承，磨砂挂在宿主上时子层伪元素会多吃一层；运行时 95 个标记元素、内联 path 串 60,464 字节、JS +2.65 kB、改动面 4 组件 + 1 lib + ~40 行 CSS |

⇒ **先查 `corner-shape`，别从 `clip-path` 起手。**

---

## 附一：判据用得到的数字（内联，别跳外链）

| 用途 | 数字 |
| --- | --- |
| p=2^v 映射 | `scoop`0.5 / `bevel`1 / `round`2 / `squircle`4 / `superellipse(3)`8 |
| 圆弧接缝跳变 | `Δκ=1/r`：r=8→0.125000、12→0.083333、16→0.062500、24→0.041667、32→0.031250、48→0.020833 |
| 药丸接缝跳变 | 端点半径 = **短边**一半 ⇒ `Δκ=2/短边`：短边 32→0.062500（=r=32 的 2.00×）、36→0.055556（1.78×）、40→0.050000（1.60×）、48→0.041667（1.33×）、52→0.038462（1.23×） |
| 阴影外偏移 | `Δκ=1/(R+d)`：R=24、d=6/12/24/48 → 0.033333/0.027778/0.020833/0.013889；同偏移下 p=5 恒 ~4.18e-5 |
| 端点曲率（r=24） | θ=0.001 处：p2 0.041667 / p3 0.000833 / p4 0.000125 / p5 0.000042 |
| 峰/端比 | p2 1.0× / p3 19.2× / p4 84.1× / p5 204.1× / p8 771.7× |
| 对角外扩 `bulge(p)=2^(1/2−1/p)−1` | p2 0.000000 / p3 0.122462 / **p4 0.189207** / p5 0.231144 / p8 0.296840；r=32 → 6.05px、16 → 3.03、14 → 2.65、12 → 2.27、10 → 1.89、8 → 1.51、6 → 1.14 |
| 每角热区损失 | 圆弧 `r²(1−π/4)=0.21460r²`；p=4 超椭圆 `r²(1−B(4))=0.07296r²`（`B(p)=∫₀¹(1−u^p)^(1/p)du`，B(4)=0.927037、B(5)=0.950150）⇒ 换 G2 角部热区变大 |
| 折线 vs 解析面积 | 整块 96×96、r=48、ε=0.5：折线覆盖率 p4 0.923937 / p5 0.948204，解析 0.927037 / 0.950150（折线略小 = 内接，反过来先怀疑代码） |
| 天真内角偏差 | ≈ `bulge(p)·w`：p3 0.122 / p4 0.189 / p5 0.231 / p6 0.260 / p8 0.297（每 w）；**p2 恒 0.000** |
| 描边上限 `1/κ峰值 ÷ r` | p2 1.0000 / p3 0.5612 / p4 0.3964 / p5 0.3078（形状越方，能用的描边越细） |
| f 缩放 | `f=min(1, 逐边 边长/该边两角半径和)`，四角一起乘；100×40 上 24/25/30/40/60px → 用过值全 20.00 |
| 百分比不对称 | `κ=a/b²`（水平边接缝）、`κ=b/a²`（竖直边），比 `(a/b)³`：200×80→15.63×、320×40→512.00× |
| 平端贝塞尔 | `B(u)=(u³r,(1−u)³r)`，端点 κ→0、峰值 0.157135(r=24)、斜率 1.072 ⇒ 止于 G2、几何等价 p≈5.19 |
| 采样弦长律 | `s_max=sqrt(8ε/κ峰值)`，段数 ∝ √S；ε=0.5px 为可见阈值、0.05px 为对齐阈值（**都是解析层阈值**） |
| 嵌套等距 `offsetGap(p,R,d,rin)` | p2/12/5/7 → 5.000~5.000（等距，对照组必须如此）；p4/12/5/7 → 5.000~5.853；p4/12/5/10 → 5.019~6.417（浮动 1.399） |
| 引擎地板 | `corner-shape` 自 Chromium 139 起；Safari/Firefox 未支持（列入 Interop 2026）。`clip-path: shape()` 在 Chromium 150 读回 `none` |

---

## 附二：本技能代码的验证状态

| 块 | 怎么验的 | 结果 |
| --- | --- | --- |
| `g2-corners.mjs`（A2 全块） | 抽块拼成模块，Node v24.18.0 跑 **107 项断言 0 失败**；期望值取自闭卷数值台 + 本轮两引擎实测 | 全部 PASS：`κ峰值`/`1κ峰值`(p=2…8)、段数(7/8/12/13/19/26)、`usedRadius`+`usedRadii`（含不等径 `f=0.9375`、逐边最紧边判定）、`radiiFrom`（`24px`/`6px 16px 16px`/`50%`/`9999px`/斜杠两轴）、`rectPath` 逐字符、`rectPoints` 单 r 与四角数组两条入口 + 鞋带面积 vs `B(p)`、`clipPathOf` 长度 515/737、J1(1.0000/16.0299/254.1365/3963.326)、J2(0/1.0003/2/3)、J3(0.088)、`bulge`/`diagRadius`/`radialRatio`、`offsetGap`(含圆弧对照组)、`seamArc`/`seamSuper` 端点与中点、`tierTable` 14 档 |
| A3/A7/B6 的用法块 | 把 A2 块落成 `g2-corners.mjs`，用法块原样 import 后在 Node 跑 | 输出与本档内注释的数值逐条相同（含 `expect=4115.7239`、`kPeak=0.135375`） |
| `g2-audit.mjs` | 抽块后在一个真实工程 `src/` 上原样实跑（只读） | 30 个文件、命中 82 处（R1 39 / R5 18 / R10 9 / R4 8 / R3 5 / R9 2 / R2 1，R6/R7 各 0），rc=0；R9 两条中一条落在 CSS 注释行上 ⇒ 印证"只读 + 人工过"的定位 |
| `g2-support.mjs` / `probeComputed` / `hitGrid` | 抽块后原样内联进 file:// 页面，在 Electron 43.1.1/Chrome 150.0.7871.114 上逐字执行（`detectG2` 10 个键 + `probeComputed` 3 项 + `hitGrid` 6 格 + 两态读回对照）；152 侧用等价网格复跑 | 见 §0 表。两引擎一致的：`CSS.supports` 五项、`clip-path: inset(… round …)` 两态 computed 读回与 `(4,4)`/`(8,8)` 命中、`hitGrid` 的圆弧 `2,2=no 4,4=no 8,8=hit` 与超椭圆 `2,2=no 4,4=hit 8,8=hit`（与 `bulge` 预测同侧）。**不一致的是 `cornerShape` 读回字符串**（150 `squircle`/`round` vs 152 `superellipse(2)`/`superellipse(1)`）⇒ 判据只判两态不同。四角长写生效、`borderRadius` 三值塌缩只在 150 侧读到，152 侧未复测 |
| SVG / `clip-path` / Canvas 块 | 拼进一个 file:// 页面在本机浏览器执行（dpr=1） | `<path>` `getBBox()=[200,120]`；`clip-path` 被接受且命中收缩；Canvas 覆盖率 0.93186 |
| C 段样本数字 | 来自该工程 2026-10-11/12 的真窗取证（`--shot` PNG 逐行取边、`--frames` A/B、四挡布局两态） | 见 C3/C4 表；像素层 0.6~2.1px、布局逐格相同、帧两侧 p50 6.9ms 相同 |

**本轮修掉的五处文档-代码不一致**（都是把代码跑起来才暴露的，留档提醒后来人"文档里的数字也要复算"）：

1. A7 注释里 p=5 的 `expect` 写作 `4115.724`，`g2report()` 的 `toFixed(4)` 实产出 **4115.7239**。
2. A3 与 A6 两张表的 p=5 `κ峰值` 写作 `0.135376`，全精度是 **0.135375399…** ⇒ 应为 `0.135375`（A7 那行原本就写对了，两处互相矛盾）。
3. A2 注释称差分法会把 J1 量成 `1.27 而不是 257`，本轮复现是 **1.2612 而不是 254.1365**（257 是没和自家 A7 对齐的旧值）。
4. 旧版把 `1/κ峰值 ÷ r` 和 `bulge(p)` 都叫 `c(n)`（p=4 分别是 0.3964 与 0.18921），同名两义 ⇒ 现各自命名，并把 `2^(1/p)` 与 `2^(1/2−1/p)` 的区别写进 A3。
5. 旧版 §0 分支规则把"收缩命中区"记作 `clip-path` 独有的代价，实测 `border-radius` 与 `corner-shape` 同样收缩（且 G2 收缩得更少）⇒ 已按硬事实 6/8 重写。

**未验证**：Safari/Firefox 的实际渲染；`corner-shape` 参与动画的中间态；`corner-start-*/end-*` 逻辑轴在 RTL 下的落位；C4 第 ③ 步的阴影**轮廓形状**（样本只证了"还在"）；右卡片像素取边（阈值分不开，改由 computed + 同一规则覆盖）。
