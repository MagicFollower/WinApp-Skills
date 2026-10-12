# ChatAPP · React Bits 动效层升级方案

## Context

当前渲染层是自绘的三栏 IM 复刻（`src/components/` 4 个文件 + `src/styles/` 3 个文件，35 个 CSS 类），除 `react`/`zustand` 外**零运行时依赖**，产物 JS 157.42 kB（gzip 51.77）、CSS 13.09 kB（gzip 3.51）。上一轮刚把视觉对齐原型（悬浮卡片 + 全局光影流动背景 + 窄窗浮层），并留下一张实测基线表（1280/1121/1120/960 四挡的 `chatW`/`sendClear`/`pillActionsGap`）。

你要的是「组件全部用 reactbits 升级」。已定三条口径：**允许 `motion`+`gsap`+`three`**、**动效层叠加**（DOM 结构/尺寸/令牌不动）、**背景换 SoftAurora**。

### 先摊开一个前提（分析结论，不是拒做）

字面意义上的「全部组件换成 reactbits」做不到：reactbits 的 Components 类目是展示型件（卡片/轮播/画廊/坞），**没有**聊天气泡、消息列表行、输入栏、表情网格这类 IM 专用件。所以「动效层叠加」是唯一同时满足「每个区域都过一遍 reactbits」和「不破坏原型还原度」的口径——能换的换件，没有对等件的由 reactbits 动效原语（AnimatedContent / ClickSpark / CountUp…）包住，几何与令牌不变。

## 调研实测（全部现取，不凭记忆）

组件目录来自 GitHub tree API（`DavidHDev/react-bits@main`，`truncated:false`，7267 条目）：**Animations 41 / Backgrounds 59 / Components 48 / Micro 34 / TextAnimations 33 = 215**。

依赖来自 registry 实取（54 个候选，`GET https://reactbits.dev/r/<Name>-TS-CSS`）：

| 依赖 | 命中组件 |
|---|---|
| **无额外依赖** | **45/54** —— SoftAurora、Aurora、Threads、GlassSurface、AnimatedContent、ClickSpark、Magnet、GlareHover、CountUp、ShinyText、GradientText、BorderGlow、StarBorder、ScrollReveal、Counter、Dock |
| `motion@^12.23` | BlurText、DecryptedText、SquishSwitch、SpringCheck、WarmTooltip |
| `gsap@^3.13` + `@gsap/react` | PillNav、Masonry、SplitText、FadeContent |
| `three@^0.180` + `@react-three/fiber` / `ogl` | Silk、GhostCursor / GradientWaves |
| `@hugeicons/*` | AnimatedList、VoicePill、SpringCheck |

**两颗雷直接排除**：`WarmTooltip` 声明 `react-dom@^19.0.0`（本工程 React 18，装了撕裂渲染树）；`PillNav` 声明 `react-router-dom`（本工程无路由）。

**纪律**：`允许` ≠ `全装`。P0 取源后逐个核对 import，只装真正被 import 的包——按本表预期只需 `motion`，`gsap`/`three` 应为 0，不装。

## 区域映射表

| 区域 / 现有实现 | reactbits 件 | 依赖 | 结论 |
|---|---|---|---|
| `.bg-flow` 全局流动背景 | **SoftAurora** | 无 | 替换；`--flow` 降级为 reduced-motion 静态底 |
| 头部群名 `.header-pill .title` | **BlurText** | motion | 接入，仅切会话时触发一次 |
| 消息入场 `.msg-row` 的 `@keyframes msg-in` | **AnimatedContent** | 无 | 接入，保持 180ms/`--ease` 量级 |
| 大表情「9 1 7 8」`.bigemoji` | **CountUp** | 无 | 纯数字分支才走 CountUp |
| 发送钮 `.send-btn` | **ClickSpark** + **Magnet** | 无 | Spark 的 canvas 层须 `pointer-events:none` 且不落拖拽区 |
| 输入框聚焦环 `:focus-within` | **BorderGlow** | 无 | 仅 focus 时挂载 |
| 右栏浮层开合（上轮 deferred 的 `--t-panel` 动画） | **AnimatedContent** | 无 | 补滑入淡出；`data-panel` 卸载式开关不变 |
| 表情格 `.emoji-cell` hover | GlareHover | 无 | **默认不接**（30px 小格上做 glare 是噪声），留可选 |
| 左右悬浮卡片表面 | GlassSurface | 无 | **默认不接**（改边框/折射观感，偏离原型），列二期 |
| 系统光标 | SplashCursor / GhostCursor / CursorGrid | three | **拒绝**：IM 不该换系统光标 |
| 会话列表行 | AnimatedList | @hugeicons | **拒绝**：自带行结构与图标集，属结构性替换，越出本轮口径 |

## 实施步骤

**P0 取源与依赖收敛**
1. 逐个 `GET https://reactbits.dev/r/<Name>-TS-CSS`（通路已验证），`files[]` 落到 **`src/components/bits/<Name>/`**（vendored 源码入库，不装 npm 组件库）。shadcn CLI 要 `components.json`+Tailwind，不走它。
2. 核对每个文件的 `import` 行，汇总真实依赖，只 `npm i` 命中的。
3. **令牌化改写**：bits 自带 CSS 的字面量色值/时长逐个改引 `tokens.css`（`--t-*`/`--accent*`/`--r-*`）——工程红线（手册第四章「组件禁字面量色值」），不改就是两套设计系统。
4. 新建 `doc/第三方组件许可.md`：来源 URL、组件名、变体、取用日期。reactbits 是 **MIT + Commons Clause**，署名算交付项。

**P1 背景替换** — `App.tsx` 的 `.bg-flow` div → `<SoftAurora/>`，仍 `position:absolute; inset:0; z-index:0; pointer-events:none`。

**P2 动效层接入** — 按映射表**一次一件、一件一测**：BlurText → AnimatedContent(消息) → CountUp → ClickSpark+Magnet → BorderGlow → AnimatedContent(浮层)。

**P3 统一降级闸门** — 新增 `src/lib/motionPref.ts`（`matchMedia('(prefers-reduced-motion: reduce)')` 读一次导出常量），所有 bits 包装层据此关闭动画。reactbits 组件多数不自带这个判断，必须在 wrapper 兜。

**P4 验收（数字必须实测）**
1. `npm run typecheck` 双侧 0；`npm run build` 通过。
2. **体积**：`dist/assets/*.js|css` 前后字节与 gzip 增量；`npm ls --omit=dev --depth=0` 仍为空（渲染层包全留 devDependencies）。
3. **帧间隔**：SoftAurora 运行时 rAF 采样 3s，报 p50/p95 帧间隔；建议闸门 p95 ≤ 20ms，**阈值由你定**。
4. **布局零回归**：复跑上轮强制容器宽量表（1280/1121/1120/960），`chatW`/`sendClear`/`pillActionsGap` 三列与基线**逐格相同**。
5. **对比度**：白气泡压 SoftAurora，量气泡内文字与底色亮度差，先量现值再定线。
6. **Electron 真窗口自证**：给 `electron/main.ts` 加 `--shot <path>` 分支用 `webContents.capturePage()` 落 PNG（绕开 GPU 合成客户区抓不到屏的老问题），与 `doc/crops/bottom-window.png` 并排核对，并确认透明窗口下 canvas 背景不出现黑底方块。

## 关键文件

| 文件 | 动作 |
|---|---|
| `src/components/bits/*` | 新增（vendored + 令牌化） |
| `src/App.tsx` | 背景层换 SoftAurora |
| `src/components/ChatArea.tsx` | BlurText / AnimatedContent / CountUp / ClickSpark / Magnet / BorderGlow |
| `src/components/RightPanel.tsx` | AnimatedContent 浮层开合 |
| `src/styles/tokens.css`、`src/styles/app.css` | `--flow` 降级；被替换 keyframes 收口 |
| `src/lib/motionPref.ts` | 新增 reduced-motion 闸门 |
| `electron/main.ts` | `--shot` 截图分支 |
| `package.json` | 只加实际 import 的包 |
| `doc/第三方组件许可.md` | 新增 |
| `doc/复刻分析报告与实施手册.md`、`doc/运行与构建（T0Level）.md`、`README.md` | 第五章补 bits 动效层小节 + M6；T0Level 补取源命令；README 技术栈表加 motion |

## 回退

每件动效是独立 wrapper，按件摘除即可；背景层单独一次提交，`revert` 该提交即回到 `--flow`。