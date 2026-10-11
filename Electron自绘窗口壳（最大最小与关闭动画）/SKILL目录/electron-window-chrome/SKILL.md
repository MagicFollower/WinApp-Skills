---
name: electron-window-chrome
description: 给 Electron + React 的 frameless(transparent / frame:false)应用装一套自绘窗口壳:自绘拖拽(位移由主进程现读光标算)、拖拽带上右键浮出「最小化/最大化/关闭」三项浅色控制条、自绘最小化退场动画(Win10/Win11 表现一致)。当任务是去掉 -webkit-app-region 让右键归 DOM 管、弹出了不该有的深色系统菜单、窗口拖起来跟手差/瞬移、双击最大化后单击就恢复、frameless 最小化没有动画、退场动画"帧率太低"、或要把这套壳迁到另一个 Electron 工程时使用。附可直接拷入目标工程的示例源文件(assets/)、会自己失败的接线审计脚本(scripts/)、以及真窗口三腿验收手册(references/)。
---

# Electron 自绘窗口壳

## Overview

三件事一起才能成立,所以它们必须作为一个包落地:

| 能力 | 做法 | 为什么不能用 OS 的 |
| --- | --- | --- |
| 拖拽 | `pointerdown` 起手 → IPC 报"动了一下" → 主进程 `screen.getCursorScreenPoint()` 现读光标搬窗 | `-webkit-app-region: drag` 会把右键一并交给 OS |
| 右键三项控制条 | `document` 上吃 `contextmenu`,`createPortal` 到 body 画浅色控制条 | OS 那个深色菜单只能置灰条目,改不了条目集合,也关不掉 |
| 最小化退场 | 给窗口根元素挂一条 `transition: transform/opacity` | OS 的"缩进任务栏"要 `WS_SYSMENU`,Chromium 不给 frameless 窗口挂 |

**共同契约**(整套资产靠这三条对齐,任一条破了就表现为"点了没反应"而不是报错):

1. CSS 里**不存在** `-webkit-app-region`(连 `no-drag` 也删,类只留给 JS 读)。
2. 可拖的元素标 `.drag-region`,其内的按钮/输入框标 `.no-drag`。
3. `window.api` 暴露 7 个方法:`minimize / maximize / close / dragStart / dragMove / dragEnd / onRestore`,前 6 个零参数。

## 何时使用

- 新 frameless 应用要自绘标题带/窗口壳。
- 现有工程用了 `-webkit-app-region: drag`,右键弹系统深色菜单,与界面不协调且关不掉。
- 拖拽手感有问题:窗口追光标、拖到一半停在半路、贴边行为不对。
- 双击最大化之后**单击**就恢复(真 bug,见 pitfalls ③)。
- frameless 窗口 `win.minimize()` 直接消失,想要"缩下去"那段动画,且要求 Win10/Win11 一致。
- 退场动画被反馈"帧率太低"。

不适用:需要 OS Aero Snap 左右分屏吸附(自绘拖拽拿不到,只能自己补顶边最大化那一条);macOS/Linux 的绿钮语义(资产按 Windows 语义写:双击切换最大化、贴顶卡住松手最大化)。

## 资源结构

```text
electron-window-chrome/
├── SKILL.md
├── assets/                            可直接拷进目标工程的示例源文件
│   ├── WindowChrome.tsx               渲染层组件(零第三方依赖,图标内联)
│   ├── window-chrome.css              拖拽带标记 + 退场动画 + 控制条(令牌全带默认值)
│   ├── window-chrome-main.ts          主进程模块:attachWindowChrome(win) + 三条控制 IPC + 拖拽三件套
│   ├── preload-window-chrome.ts       preload 要暴露的 7 个方法
│   └── window-api.d.ts                渲染层的 window.api 声明
├── references/
│   ├── pitfalls.md                    17 条实测坑:症状 → 真因 → 处置(含实测数字与被证伪的假设)
│   └── verification.md                真窗口三组探针(A 右键归属 / B 拖拽与位移 / C 退场三腿)
└── scripts/
    ├── check-wiring.mjs               接线静态审计,13 组判定,有 FAIL 就 exit 1
    └── prove-audit.mjs                用 assets/ 现搭最小工程 + 注入 12 种缺陷,证明审计"确实会红"
```

**动手前先读 `references/pitfalls.md` 的 ①③⑦⑧** —— 这四条决定设计取舍,不知道就会走回头路(我在这四条上各烧了一轮实测)。

## Step 0 — 先让用户接受三条代价

这三条不是实现细节,是方案本身的边界。说清再动手:

1. **放弃 OS 拖拽 = 放弃 Aero Snap**。左右边缘分屏吸附拿不到;顶边"卡住松手即最大化"可以自己实现(资产已实现)。
2. **OS 的最小化动画对 frameless 窗口不可得**。实测三臂:`frame:false` 透明/不透明都没有动画,只有 `frame:true` 有 ⇒ 决定因素是 `WS_SYSMENU`。所以只能自绘,`Win+D` 和从任务栏点回来的那条路**仍然没有动画**。
3. **自绘退场的收拢点瞄的是窗口自己的底边中心**,不是任务栏那颗按钮 —— 按钮位置要按系统版本/DPI/停靠边推,那正是 Win10 与 Win11 会不一致的地方。换来的是两端跑同一条 `transition`。

## Step 1 — 落文件

```bash
SKILL="$HOME/.qoder-cn/skills/electron-window-chrome"      # 或项目级 .qoder/skills/...
DST="."                                                     # 目标工程根
cp "$SKILL/assets/WindowChrome.tsx"        "$DST/src/components/WindowChrome.tsx"
cp "$SKILL/assets/window-chrome.css"       "$DST/src/styles/window-chrome.css"
cp "$SKILL/assets/window-chrome-main.ts"   "$DST/electron/window-chrome.ts"
cp "$SKILL/assets/preload-window-chrome.ts" "$DST/electron/preload-chrome.ts"
cp "$SKILL/assets/window-api.d.ts"         "$DST/src/window-api.d.ts"
```

目录名按目标工程的约定改(`src/renderer/`、`electron/main/` 都行),**文件名和内容不用改**。工程已有 `WindowChrome`/同名样式时把资产改名再落,别覆盖宿主的东西。

## Step 2 — 主进程接线

在窗口创建处调一次(多窗口各调一次;IPC handler 用 `BrowserWindow.fromWebContents(e.sender)` 定位,本来就是按发送者解析的):

```ts
import { attachWindowChrome } from './window-chrome';

const win = new BrowserWindow({
  width: 1100, height: 720,
  frame: false,            // 必须
  transparent: true,       // 必须:退场过程要露出真桌面,观感才与系统动画同向
  backgroundColor: '#00000000',
  webPreferences: { preload: preloadPath, sandbox: true, contextIsolation: true },
});
attachWindowChrome(win);
```

两个常量按宿主改:

- `DRAG_GRAB_Y`(默认 24)= 从最大化拖下来之后光标落在标题带的哪条横线 ⇒ 取标题带高度的视觉中心附近。
- `DRAG_SLOP`(默认 4)= 起拖阈值,对齐 Windows 的 `SM_CXDRAG`。**单位是 DIP,不要乘 `devicePixelRatio`**。

## Step 3 — preload 与类型

已有 `exposeInMainWorld('api', {...})` 的工程:把 `preload-chrome.ts` 里那 7 个字段**并进现有对象**,不要新开第二个全局名(两个键名会让渲染层不知该用哪个,`.d.ts` 也会打架)。没有的,直接保留该文件的 `exposeInMainWorld('api', chromeApi)`。

`sandbox: true` 时 preload 必须是 CJS 且只能拿 `electron` —— 所以这份片段里不掺业务 IPC。

`window-api.d.ts` 落到被 `tsconfig.include` 覆盖的位置即可,不需要 import。工程已有 `WindowApi` 就把 7 个成员并进那个接口。

## Step 4 — 渲染层:挂组件 + 标带

```tsx
import WindowChrome from './components/WindowChrome';

<div className="window">                        {/* 视觉上就是那张贴桌面的卡片;类名不同就传 rootSelector */}
  <header className="drag-region">              {/* 可起手拖 */}
    <input className="no-drag" />               {/* 带内控件必须反向标,否则点输入框会拖窗 */}
  </header>
  <WindowChrome />                              {/* 挂在根元素子树末尾即可,内部 portal 到 body */}
</div>
```

样式入口 `@import` 一次 `window-chrome.css`。它自带 `--chrome-*` 令牌默认值(含浅色/深色两套),宿主有主题变量就在 `:root` 覆盖那 9 个键,**不要改资产里的选择器名**(`.window` / `.window--minimizing` / `.win-menu*`)—— 审计脚本按这些名字判。

退场两段的正确形状(改坏了 `check-wiring.mjs` 会报 `exit-origin` / `exit-layer`):

```css
.window { transition: transform var(--chrome-t) var(--chrome-ease-exit), opacity ... }
.window--minimizing {
  transform: translateY(28px) scale(0.6);
  transform-origin: 50% 100%;          /* 底边中心,不是默认 50% 50% */
  opacity: 0;
  will-change: transform, opacity;     /* 只在退场期间挂;常驻会一直占一份层纹理 */
}
```

曲线必须是**强加速** `cubic-bezier(.4,0,1,1)`。把入场常用的强减速曲线拿去做退场,85% 的行程会压进头 80ms(实测该腿 79ms 就到 scale 0.6577 = 全程的 85.6%),观感就是"帧率太低"(呈现的不同帧 11-13/16 → 15-17/19,主线程帧距 p95 13 → 9ms、max 20 → 12ms;`presented` 的绝对值随取图吞吐抖 ±2 帧,只当同一轮内的相对次序用)。

## Step 5 — 跑审计(必须 exit 0)

```bash
node "$SKILL/scripts/check-wiring.mjs" --root "$DST"          # 只看 FAIL/WARN
node "$SKILL/scripts/check-wiring.mjs" --root "$DST" --verbose
```

13 组判定全绿才算接完。它专门设计成**会自己失败**,每条 FAIL 对应 pitfalls 里一个实测过的静默失效。几条最容易被忽略的:

- `app-region`:只看声明位(CSS 的 `属性: drag`、JS 里设 `webkitAppRegion='drag'`),注释与 `getComputedStyle` 读取不算。
- `slop-gate`:`window:drag-start` 的 handler 里出现 `unmaximize` 就是 FAIL —— 那正是"双击最大化之后单击即恢复"。
- `cursor-in-main`:光标必须在 `drag-start`/`drag-move` **那一团括号里**读。文件别处调过 `getCursorScreenPoint()` 不算。
- `max-toggle`:handler 里既没 `isMaximized` 也没 `unmaximize` 时,只跟一跳去看它调用的本地函数(同文件别处的判断不能替它作证)。
- `menu-box`:把 `clamp` 用的常量与 CSS 的 `padding/gap/btnW/btnH` 重算比对。改按钮尺寸不更常量 ⇒ 贴屏幕边缘右键会画到屏幕外。
- `component-mounted`:组件文件在但渲染树里没挂它 ⇒ 整套逻辑一行都不会跑。

改过 `check-wiring.mjs` 或换了目标工程的目录约定后,跑一次自证:

```bash
node "$SKILL/scripts/prove-audit.mjs"      # 基线必须 rc=0,且 12 条变异全部按预期翻转
```

它用 `assets/` 现搭一个最小工程当基线,所以资产一改、这里的锚点也会跟着暴露(锚点没命中会明确报"这条变异等于没跑",不会假绿)。

## Step 6 — 真窗口三腿验收

静态审计通过 ≠ 行为正确。三组探针的完整做法在 `references/verification.md`,要点:

| 组 | 判据 | 最容易犯的假绿 |
| --- | --- | --- |
| A 右键归属 | 整棵树 `-webkit-app-region: drag` 命中 **0**;两条带各浮出 3 项;`.no-drag` 上**不**浮出;`webContents.on('context-menu')` **0 次** | 在入场动画进行中量菜单几何(115×42 而不是 120×44);写死坐标落到 `.no-drag` 上 |
| B 拖拽与位移 | 合成 pointer 三连 → `dragLog {start:1,move:n,end:1}` **且窗口位置一格不差**(真实光标没动);位移公式用合成数字单测(静止/右下/贴顶夹住/左上越界);**最大化时单击不恢复**,过阈值才真还原 | 想把拖拽端到端验完 —— 合成坐标是 view 相对的,`screenX = winX + viewX`,自指反馈量不出跟手 |
| C 退场三腿 | off / on / 去 `will-change` 三条腿各自跑:`capturePage` 小方块 + 位图哈希去重得到呈现帧下限,`getComputedStyle` 在 20/60/100/140/180ms 采曲线形状 | 拿 rAF 间隔当"呈现帧率";只看落定帧分不清"播过"与"根本没动画" |

主进程里 `console.log` 撞关闭的管道会弹模态错误框(实测事故)。跑探针一律 `> out.txt 2>&1`,并在入口挂:

```ts
process.stdout.on('error', () => {});
process.stderr.on('error', () => {});
```

## 交付前自检清单

- [ ] `check-wiring.mjs` exit 0(不是"跑过了",是**没有 FAIL**)。
- [ ] `prove-audit.mjs` 输出 `12/12 条变异全部按预期翻转`。
- [ ] 三腿验收里每条判据都带对照腿,且对照腿是"不该触发的场景"。
- [ ] 顶边拖到工作区顶、松手 → 最大化;拖最大化的带 → 单击不动、真拖才还原。
- [ ] 系统开"减少动态效果"后:立即最小化、不播动画、**也不卡住**(兜底计时器那条路)。
- [ ] 从任务栏点回来:立即恢复,没有"先透明再长回来"那一闪(`window--noanim` + 强制回流)。
- [ ] 纯浏览器打开 Vite 页面不白屏(`window.api` 为 undefined 时组件整块不渲染)。

## Resources

- `assets/` — 5 个直接拷入目标工程的示例源文件;通用化的关键是那三条契约,不含任何具体应用的业务。
- `references/pitfalls.md` — 17 条实测坑与数字(含被证伪的假设,如 `WS_EX_LAYERED`)。改行为前先查这里。
- `references/verification.md` — 三组探针的注入代码、腿次表、假绿清单。
- `scripts/check-wiring.mjs` — 接线审计(`--root`、`--verbose`)。
- `scripts/prove-audit.mjs` — 审计脚本的自证(`--keep` 保留临时工程)。
