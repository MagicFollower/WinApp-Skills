# ChatAPP · 圆角加大 + 全局 G2（曲率连续）圆角方案

> **落地结果（2026-10-11）**：两条诉求都已完成，但**实现形态与本方案 S1–S5 不同**。S0 探针否掉了 `clip-path: shape()`（Chromium 150 读回全 `none`）；S2–S5 的 JS 生成 `path()` 路线先落地、像素验证通过，随后被**原生 `corner-shape: superellipse(2)`** 取代——一条声明、零 DOM/零 JS/零新增伪元素层，且 `box-shadow`、`backdrop-filter`、`overflow` 裁切跟着变形、命中区不收缩。最终形态 = `--r-window` 20→32（窗口与左右卡片共用）+ 12 个圆角矩形挂 `corner-shape`（pill/圆形按口径排除）；被否决路线的实测数字与踩坑清单归档在 手册 4.1.1，复现命令在 T0Level 第 11 节。下面的 S/ V 表保留作决策过程记录。

## Context

你提的两条：**1、增大背景+左右侧卡片圆角并保持一致；2、全局圆角算法变更为 G2**。口径已定：G2 覆盖**所有圆角矩形**（pill/圆形本身已是全圆，无直线-圆弧接缝可谈曲率连续，按你的选择排除），圆角**32px**。

CSS `border-radius` 的圆弧在直线与圆弧的接缝处曲率从 0 跳到 1/r，只有切线连续（G1），所以大圆角会看出"突然圆起来"的折角感。G2 要在接缝处曲率为 0 并平滑上升——即超椭圆 `|x/a|^n+|y/b|^n=1`（squircle 家族）。这是视觉诉求，不是重构。

## 现状实测（本轮从 app.css 逐行取的，行号即依据）

**半径分布**

| 位置 | 行号 | 现值 | 归类 |
|---|---|---|---|
| `.window` / `.sidebar` / `.right-panel` | 11 / 113 / 548 | 同一令牌 `--r-window: 20px` | G2 目标，**已经一致** |
| `.bubble` | 370–371 | `--r-card` 16 + 左上 `--r-bubble-tail` 6 | 目标，**四角不等径** |
| `.msg-row.own .bubble` | 381–382 | 小角换到右上 | 目标，镜像 |
| `.bubble .thumb` | 397 | `--r-thumb` 14 | 目标 |
| `.chat-item` / `.quote-card` | 212 / 438 | `--r-chip` 12 | 目标 |
| `.menu-btn` / `.app-badge` / `.filter-tab` / `.emoji-cell` / `.chip` | 129/138/189/627/666 | 字面量 8/6/10/8/10 | 目标（字面量要收进令牌） |
| `.searchbox` `.header-pill` `.action-pill` `.composer` `.composer-input` `.bigemoji` `.panel-tab` | 150/277/318/475/499/412/597 | `--r-pill` | **排除** |
| `.bg-flow::before/::after` `.avatar` `.icon-btn` `.round-btn` `.send-btn` | 51/242/483/513/523 | `50%` | **排除** |

**关键冲突（决定了方案形状，不是待办）**

1. `clip-path` 会把元素**自身的 `box-shadow` 一起裁掉**。同时带半径+阴影的 G2 目标有 6 个：`.window`(15 `--shadow-window`)、`.sidebar`(114 `--shadow-float`，浮层态 567 `--shadow-pop`)、`.right-panel`(549/567)、`.bubble`(373)、`.quote-card`(440)、`.chip`(669)。
2. `filter` 在祖先链上会造 **backdrop root**，可能废掉页内 `backdrop-filter` 磨砂。用 `backdrop-filter` 的有 14(`.window`)、279(`.header-pill`)、320(`.action-pill`)、477(`.composer`)、547(`.right-panel`)——后 4 个真正在采样页内 SoftAurora，是可见玻璃效果。**所以本方案全程不引入 `filter`。**
3. `clip-path` 同时收缩命中区（border-radius 不收缩）——`.chat-item`/`.filter-tab`/`.emoji-cell`/`.chip`/`.menu-btn` 都点了才生效，不能随便裁。
4. `.window` 一身多职：radius+overflow:hidden(12)+backdrop-filter(14)+box-shadow(15)+`container-type:inline-size`(17，容器查询 560 依赖它)。不能把它当成一个"简单圆角盒子"改。

**可用的几何事实（省掉 wrapper 的依据）**

n>2 的超椭圆比同半径圆弧**外鼓**：45° 方向外扩 = `0.1892r`（r=32→6.05px，16→3.03，12→2.27，10→1.89，8→1.51）。因此 **G1 圆弧区域 ⊂ G2 形状区域**，"外层的 G1 背景/裁剪永远不会戳出内层的 G2 轮廓"。这条包含关系是本方案不用加 wrapper、不改 DOM 的支点，V2 会实测复核。

## 方案：两种机制，零 DOM 改动

**S0 语法探针（先量再写，别假设）**
`shape()` 的 `curve-to` 实参形式、控制点能否用 `calc()`/百分比/`var()`，MDN 这轮抓不下来，所以先在真窗口里探针：`--selftest` 打开一个 `.cache/g2-probe.html`（`.window` 同级、`.cache/` 内，不进 src），放 6 个固定尺寸测试盒（r=32/16/12/10/8/6）与若干候选 `clip-path` 写法，读回 `getComputedStyle(el).clipPath`——**只有回读值不是 `none` 才算这条语法成立**（自定义属性代入后解析失败会 IACVT 成 `none`，静默失效，必须这样判）。
- 通过 → 走 S1（纯 CSS，推荐）。
- 不通过 → **Plan B**：`src/lib/g2.ts` 生成 `clip-path: path(...)`（超椭圆采样 ~16 点/角）+ 一个共享 `ResizeObserver` + callback ref 挂到 12 个选择器；代价：消息行新增/尺寸变化都要重算，且 `path()` 是绝对坐标，`.cache` 探针同样要先过。

**S1 令牌层（`src/styles/tokens.css`）**
- `--r-window: 20px → 32px`（`.window`/`.bg-layer`/`.sidebar`/`.right-panel` 共用，"增大并一致"一步到位）。
- 新增 G2 令牌：`--g2-n: 4`（指数，注释里给 n=3/4/5/6 四套贝塞尔常量，由 `node -e` 推导脚本产出）；`--g2-clip`（四角等径宏）与 `--g2-clip-tl` / `--g2-clip-tr`（气泡尾巴宏，尾巴角单独用 `--g2-tail`）；`--g2-r`（每元素别名，`border-radius` 与 clip 共读一个数，杜绝两套数字漂移）。
- 小半径收进令牌：`--r-btn: 8px`、`--r-badge: 6px`、`--r-tab: 10px`（替掉 129/138/189/627/666 的字面量）。
- 兜底与开关：`@supports not (clip-path: shape(from 0 0, line-to 10px 10px)) { :root { --g2-clip: none; ... } }`——不支持就退回今天的 G1；想把整套关掉也只改这三个 token 为 `none`（单点回退）。

**S2 机制 M1「直接裁」**：无阴影、无命中顾虑的**非交互**图形——`.bubble .thumb`(img)、`.app-badge`。`clip-path: var(--g2-clip)` + `--g2-r`。

**S3 机制 M2「附加轮廓」**：其余 11 个目标（`.sidebar`、`.right-panel`、`.bubble`、`.msg-row.own .bubble`、`.quote-card`、`.chip`、`.chat-item`、`.filter-tab`、`.emoji-cell`、`.menu-btn` + `.window` 走 S4 变体）。宿主保持**不裁**（阴影、命中、backdrop-filter、overflow 全部原样），只加一层外鼓的 G2 轮廓：
```
x { position: relative; isolation: isolate; --g2-r: var(--r-chip); border-radius: var(--g2-r); }
x::before { content: ''; position: absolute; inset: 0; z-index: -1;
            background: inherit; clip-path: var(--g2-clip); }
```
`background: inherit` 让伪元素重画宿主当前背景（含 hover/active 换色、渐变）并裁成 G2；宿主自己的 G1 背景被它完全盖住（上面的包含关系）。`isolation:isolate` 只造堆叠上下文、**不造 backdrop root**，所以 `--shadow-*`、玻璃采样、`z-index` 子层都不受影响——这一条 V5 实测。目标元素现况都没占用 `::before`（只有 `.bg-flow`、`.border-glow-card` 用了伪元素，二者不在目标集）。

**S4 `.window` 特例（不动 DOM，只搬两条声明）**：把 `background: var(--window-bg)` + `backdrop-filter: blur(28px) saturate(1.4)` 从 `.window` 移到**已存在的** `.bg-layer`（app.css:27，`absolute inset:0 z0 overflow:hidden pointer-events:none`，它本来就在 `.window` 背景之下、内容之下，层序不变），并在 `.bg-layer` 上挂 `--g2-r: var(--r-window)` + `clip-path`。这样：窗口内所有子层（含 SoftAurora 的 WebGL canvas）由 `.bg-layer` 裁出 G2 轮廓；`.window` 不再需要裁自己 → **`--shadow-window` 完整保留**；`overflow:hidden` 留着当 G1 兜底（裁得更紧，只可能内缩不会戳出）；`container-type` 不碰。`.chat-area` 上下出血区水平位置在 x≈324 之外，与 32px 角弧不相交，无需处理。

**S5 气泡不等径**：`.bubble` 用 `--g2-clip-tl`，`.msg-row.own .bubble` 用 `--g2-clip-tr`，尾巴角走 `--g2-tail: var(--r-bubble-tail)`。四角半径不同不破坏 G2——接缝处曲率仍为 0。

**S6 派生脚本与常量**：`node -e` 内联（不落文件）解一个常数 `h`（贝塞尔把手长/半径），使四分之一角在 `n=4` 下与精确超椭圆的**最大径向偏差最小**，并断言拟合曲线全程**不内切到圆弧以内**（min gap ≥ 0），报出该最大偏差（目标 ≤0.5px @ r=32）。常量写进 `--g2-clip` 的 `calc(var(--g2-r) * <h>)` 并在注释留 n=3/4/5/6 对照。

## 验收（数字必须实测，判据打在缺陷可见的那一层）

| # | 判据 | 方法 |
|---|---|---|
| V1 | 语法真的生效 | 真窗口 `getComputedStyle().clipPath` 回读非 `none`；探针页 6 个半径逐个报 |
| V2 | 轮廓**确实是 G2**（防假绿：只测延迟不算过） | `--shot` 落 PNG，沿窗口角/两卡片角 45° 采样边缘交点，与「精确超椭圆 n=4」和「圆弧」两条真值并列报偏差；期望外扩 ≈0.1892r（r=32→6.05px），若测得 0 就说明 clip 静默失效 |
| V3 | 阴影没被裁掉 | 同两张 PNG 在轮廓外 1px/4px 采样 `--shadow-window`、`--shadow-float` 像素，改前/改后并列 |
| V4 | 布局零回归 | 复跑四挡表 1280/1121/1120/960，`chatW`/`sendClear`/`pillActionsGap`/`inputW` 逐格与基线相同；半径不改 padding，理应全等，不等就查明原因 |
| V5 | 玻璃与交互没坏 | grep 确认没新增 `filter:`；`html.browser` 下读 `.header-pill`/`.composer` 的 `backdropFilter` 计算值仍在；`--shot` 确认磨砂在；点击 chip/emoji-cell/tab 各一次仍有响应（M2 不改命中区） |
| V6 | 内容没戳出轮廓 | `.chat-item.active`、`.emoji-cell:hover`、滚动到底的列表，在 `--shot` 裁图里逐个看角部 |
| V7 | 常规闸门 | `npm run typecheck` 双侧 0；`npm run build` 通过；体积增量报字节数（预期纯 CSS，≈0） |
| V8 | 帧间隔不受影响 | 复采一次 rAF p50/p95（clip-path 是静态几何，无新动画） |
| V9 | 降级 | `--force-prefers-reduced-motion` 与无 `shape()` 环境（`@supports` 分支）各出一张，确认退回 G1 不破相 |

## 关键文件

| 文件 | 动作 |
|---|---|
| `src/styles/tokens.css` | `--r-window` 20→32；新增 `--g2-n`/`--g2-clip`/`--g2-clip-tl`/`--g2-clip-tr`/`--g2-r`/`--g2-tail`、`--r-btn`/`--r-badge`/`--r-tab`；`@supports` 兜底 |
| `src/styles/app.css` | `.bg-layer` 承接 window 填充+磨砂+clip（27）；`.window`(4–18) 搬走两条声明；11 处 M2 规则；`.bubble`(367/379)、`.thumb`(392) |
| `.cache/g2-probe.html` | 临时探针，验完删除 |
| `doc/复刻分析报告与实施手册.md` | 2.2 令牌表更新；新增 **2.9 G2（曲率连续）圆角**（含 G1⊂G2 包含关系、clip 与 box-shadow/backdrop-filter 的取舍、n 取值理由）；4.1 图层结构补 clip 说明 |
| `README.md` | 样式规范章节补 G2；8.6 附近补"几何降级=退回 border-radius" |
| `doc/运行与构建（T0Level）.md` | §11：探针与 `--shot` 角部测量命令、判据自查表加 V2/V3 两行 |

## 回退

单提交回退。运行期开关：三个 `--g2-clip*` 令牌改 `none` 即整体退回 G1（几何回到今天的样子）；`--r-window` 独立可回 20px。

## 需要你留意的两点（不拦路，实现时按默认走）

1. `n=4` 的角比圆弧"方"一些，r=32 时 45° 方向外扩 6px 是**肉眼可见**的变化——这是 G2 的代价也是它的效果；不满意可改 `--g2-n` 常量组（一处）。
2. 上一轮遗留未决：SoftAurora 在真窗口里的橙色云比原型更饱和；`release/` 仍被外部进程锁着。本轮不动这两件。