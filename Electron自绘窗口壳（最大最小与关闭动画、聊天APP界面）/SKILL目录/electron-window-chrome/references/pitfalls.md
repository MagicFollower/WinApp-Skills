# 坑位清单(全部实测得到,不是推测)

按"什么时候会撞上"排序。每条给:症状 → 真因 → 处置。数字都是真窗口里量出来的。

---

## ① `-webkit-app-region: drag` 会把右键交给操作系统

**症状**:拖拽带的右键弹出深色系统菜单(还原/移动/大小/最大化/最小化/关闭 六项),与应用主题完全不搭,应用侧关不掉、条目也改不了。

**真因**:Chromium 对该属性返回 `HTCAPTION`,右键落进 `DefWindowProc`。三条实测支撑:

- `win.setMenuBarVisibility(false)`、`autoHideMenuBar` 全无效 —— 那个菜单不是菜单。
- `Menu.setApplicationMenu(null)` 无效。
- 判别实验:页面里给 `contextmenu` 加 `preventDefault()` 后,`webContents.on('context-menu')` 的触发次数从 **0** 变/不变 —— 这个事件在 OS 接管时一次都不发,可以当探针用。
- `frame: false` 上的 `minimizable / maximizable / resizable` 只能把系统菜单里的条目**置灰**,改不了条目集合。

**处置**:整条撤掉 `-webkit-app-region`(连 `no-drag` 一起,免得日后误读),改自绘拖拽 + 自绘控制条。**代价要当面说清**:放弃 OS 拖拽 = 放弃 Aero Snap(拖到屏幕左右边缘的分屏吸附)、拖到顶边的最大化要靠我们自己补(已补)、OS 的"最大化窗口拖下来自动还原"也要自己补。

---

## ② 位移必须主进程现读光标,不能渲染层传坐标

渲染层每次 `pointermove` 把 `clientX/clientY` 发过去,主进程按增量搬窗 ⇒ **IPC 往返延迟被算进位移**,手感是"窗口追光标",而且丢一帧就永久错位。

正确做法:`pointerdown` 报一次"起手"(主进程记下 `窗口原点 + 光标原点`),之后每帧只报"动了一下",主进程用 `screen.getCursorScreenPoint()` 现读实时光标算 `目标 = 窗口原点 + (实时光标 - 光标原点)`。这样不累计误差,丢帧也只是下一帧补上。

**顺带的硬约束**:`drag*` 三条都走 `ipcRenderer.send`(不是 `invoke`)—— `pointermove` 是几十 Hz 的高频源,不需要每帧一个回执 promise。preload 里 `dragStart/dragMove/dragEnd` **必须是零参数**;一旦带参数,就等于把坐标重新引回来了。

---

## ③ 单击不能把最大化的窗口拽下来(本轮真 bug)

**症状**:双击标题带最大化后,**再点一下**(不是双击)就恢复了。

**真因**:`drag-start` 一到就 `unmaximize()`。Windows 的语义是"位移超过起拖阈值才算拖拽",`SM_CXDRAG` 在 100% 缩放下约 **4px**。

**处置**:`drag-start` 只记 `wasMax` 不动窗口;`drag-move` 里 `Math.abs(cx-a.cx) >= 4 || Math.abs(cy-a.cy) >= 4` 成立才还原,还原后按新 bounds 重新锚定。

**元教训**:我上一轮的断言 `restoredOnDragStart: true` 把缺陷本身写成了"期望值"。**判据里出现"符合 bug 的行为"就一定是错的**。新的三条腿要同时成立:`stillMaxOnPointerdown`、`stillMaxAfterClick` 为真,而 `clickLog = {start:1, move:0, end:1}` 证明这一枪真的开过(不是空跑)。

---

## ④ `setPointerCapture` 在合成事件上会抛

窗口跟着光标跑,光标很容易甩出窗口 ⇒ 不捕获指针就收不到后续 `pointermove`,窗口停在半路。但验收探针用的是**合成 `PointerEvent`**,它的 `pointerId` 不是真实的,`setPointerCapture` 会抛 `InvalidPointerId`。

**处置**:`try { el.setPointerCapture?.(e.pointerId) } catch {}`。真实指针照样捕获,合成事件继续能驱动逻辑 —— 两者都要,不能只留一边。

---

## ⑤ 恢复时不要反向播退场动画

**想法**:"从任务栏点回来"应该反向播一遍。
**实测**:restore 之后头 **~160ms 渲染器不出帧**(rAF 采样只有 4 个点,中间一个 164ms 空档),这段"入场"到底有没有被呈现**证明不了**;而按计算值看它的起始是 `scale .64 / opacity .09`,真播出来很可能是一下近乎透明的窗口。

**处置**:挂 `.window--noanim`(`transition: none`)→ 摘 `.window--minimizing` → 强制回流 → 摘 `.window--noanim`。恢复即时到位,和系统原本行为一致。

**强制回流那一步不能省**:`void root.offsetWidth`。不挡这一下,加类和摘类会被合并进同一帧,`transition` 照样播 —— 这正好是"看着代码对但效果没有"的经典形态。

---

## ⑥ `transitionend` 是主路,兜底计时器不是可选

退场动画播完再真的 `minimize()`。但两种情况下事件永远不来:

1. 系统开了"减少动态效果" ⇒ CSS 是 `transition: none`;
2. 事件被吞(切窗、层被移除)。

后果不是"少一段动画",而是**那颗按钮变成点了没反应**。所以 `setTimeout(go, 400)` 这类兜底必须留,并用 `done` 标志保证只走一次。

同时要筛 `e.target === root && e.propertyName === 'opacity'` —— 子树里任何一条 transition 结束都会冒泡上来,不筛就会在动画播到一半时提前最小化。

---

## ⑦ OS 的最小化动画拿不到:缺的是 `WS_SYSMENU`

**症状**:`win.minimize()` 直接消失,没有"缩进任务栏"那段。

**三臂实测**(同一台机、同一登录会话):

| 窗口 | frame | transparent | shell minimize 动画 |
| --- | --- | --- | --- |
| 本应用 | false | true | 无 |
| 对照 A | false | false | 无 |
| 对照 B | true | false | **有** |

⇒ 决定因素是 `frame`,更准确地说是 Chromium 不给 frameless 窗口挂 `WS_SYSMENU`。注册表动画开关(`MinAnimate`/`EnableAnimations`)都是开的,前台窗口的 exstyle 也一样,所以**不是系统关了动画**。

**已被证伪的假设**:`WS_EX_LAYERED`(实测所有窗口都没有,包括有动画的那颗)。**未分清的混淆项**:`WS_CLIPCHILDREN`(它与 frame 同时出现,没能单独拆开)。

**结论**:frameless 应用只能自绘退场。好处是自绘的那条 `transition` 不读任何系统渲染路径,**Win10 与 Win11 表现一致**;代价是收拢点只能瞄窗口自己的底边中心,不瞄任务栏按钮(按钮位置要按系统版本/DPI/任务栏停靠边推,那正是不一致的来源)。`Win+D`、任务栏点击触发的最小化走 OS 路径,**应用拦不到,所以确实没有动画** —— 这是已声明的未覆盖范围,不是 bug。

---

## ⑧ "帧率太低"多半是曲线用错了,不是掉帧

**症状**:退场动画看着一格一格。

**instrument 走过的弯路**:rAF 间隔量的是主线程 BeginFrame 节奏,**不是呈现帧**;`getComputedStyle().transform` 读的是时钟插值,不是实际呈现。所以先别拿它下"没掉帧"的结论。

**能下限约束呈现帧的做法**:动画期间循环 `webContents.capturePage({x,y,w:160,h:160})`,对 `img.toBitmap()` 做哈希去重,得到"不同内容帧"的个数(每条腿采 240ms)。**两轮独立实测**,`off` = 撤掉下面两条优化(用回强减速入场曲线 + `will-change:auto`),`on` = 出厂态:

| 腿 | 第一轮 `presented` | 第二轮 `presented` | `raf` p50/p95/max(两轮) |
| --- | --- | --- | --- |
| `off` | 11/16 | 13/16 | 5-13-20 / 7-**13**-**20** ms |
| `on` | **15/18** | **17/19** | 5-7-11 / 7-**9**-**12** ms |

⇒ 主因是**把强减速的入场曲线用在退场上**:`off` 腿的 scale 在 79ms 就走完全程(`1 → 0.6`)的 **85.6%**(采样值 0.6577)、100ms 到 92.9%,后面全是肉眼分不出的尾;`on` 腿是 26→184ms 均匀铺开(0.9955 / 0.9428 / 0.8694 / 0.7572 / 0.6229)。换成强加速的 `cubic-bezier(.4,0,1,1)` 并加 `will-change: transform, opacity` 之后,呈现帧与帧距同时改善。

**这条仪表的绝对值不能当阈值**:`presented` 会被 `capturePage` 自己的吞吐带走 ±2 帧(同一腿两轮差 2 帧实测存在,`calls` 也在 16–22 之间抖)。要拿的是**同一轮内各腿的相对次序**与**曲线形状** —— 曲线形状两轮完全一致,那才是稳的依据。

**被数据否掉的候选**:退场期间移除 `backdrop-filter` 来提帧率 —— 它的 `raf` 甚至略好于出厂态(7-7-11 vs 7-9-12)、`presented` 反而更低(15/22 vs 17/19),即**这个仪表在该档分不出胜负**;保留 `backdrop-filter` 的依据是"收益未证 + 观感损失确定",不是"实测无差异"。

**`will-change` 只在退场期间挂**:常驻会一直占一份层纹理。

---

## ⑨ `position: fixed` 必须挂在包裹层,不能挂在菜单本身

控制条的入场动效是在包裹层自己的元素上做 `translate/scale`。若 `fixed` 留在 `.win-menu` 上,包裹层一带 transform 就成了 fixed 的包含块 ⇒ **菜单位置跟着位移抖 8px**。

同理:控制条要 `createPortal` 到 `document.body`。活在拖拽带的层叠上下文里时,祖先的 `transform` / `backdrop-filter` / `overflow` 会把它裁掉或压住。

---

## ⑩ clamp 用的盒尺寸常量与 CSS 是同一笔账的两半

`WIN_MENU_W/H` 用来把菜单夹回屏幕内。它是 `2*padding + 3*btnW + 2*gap` / `2*padding + btnH` —— 改 CSS 里任何一项(按钮 34、内边距 5、间距 4)都必须同步改常量,否则贴屏幕边缘右键时菜单画到屏幕外。`check-wiring.mjs` 的 `menu-box` 就是算这笔账并比对。

---

## ⑪ 控制条不会因为"刚打开就被自己关掉"而失效 —— 别去"修"它

关闭逻辑监听 `pointerdown`(捕获相)+ `Escape` + `blur`。右键自己那一下发生在监听器挂上**之前**,所以不会立刻关掉刚开的菜单。如果哪天有人把它"优化"成监听 `click`,就会变成"菜单点不开"。

---

## ⑫ 失焦必须结束拖拽

`window.addEventListener('blur', endDrag)`。少了它:拖到一半切到别的程序,回来再动一下鼠标,窗口会跟着光标**隐形地**搬走。`pointercancel` 同理。

---

## ⑬ 主进程里 `console.log` 撞上关闭的管道会弹模态错误框

**真实事故**:一条 shell 命令的管道读端提前退出(EPIPE),主进程的 `console.log` 抛错,Electron 弹出一个错误对话框挡在用户面前。

**处置**:主进程入口尽早挂 `process.stdout.on('error', () => {})` 与 `process.stderr.on('error', () => {})`;跑取证脚本时把 stdout 重定向到文件,而不是接管道 grep。

顺带一条 shell 教训:`grep -oE '...'` 的正则括号不配平会让读端死掉 —— 那才是 EPIPE 的来源。

---

## ⑭ DPI 与阈值单位

`screen.getCursorScreenPoint()` 返回 **DIP**,所以 `DRAG_SLOP = 4` 是 4 DIP,不要再乘 `devicePixelRatio`(125%/150% 缩放下乘了就变成"要拖很久才动")。
`DRAG_GRAB_Y`(从最大化拖下来后光标落点)是物理布局像素,改标题带高度要同步改。

---

## ⑮ 多窗口共享一个拖拽锚点

资产里的 `dragAnchor` 是模块级单槽:同一时刻只跟一次拖拽。单窗口应用无所谓;真要多窗口并发拖,把它改成 `Map<webContents.id, DragAnchor>`,并把 `attachWindowChrome(win)` 的调用保持在每个窗口上各一次(IPC handler 用 `BrowserWindow.fromWebContents(e.sender)` 解析,本来就是按发送者定位窗口的)。

---

## ⑯ sandbox 下 preload 的形态

`sandbox: true` 时 preload 必须是 **CJS**,且只能拿到 `electron`,没有 `fs/path/node:*`。所以 `preload-window-chrome.ts` 里不掺业务 IPC,只暴露这 7 个方法。已有 `exposeInMainWorld('api', …)` 的工程要把字段**并进现有对象**,不要新开第二个全局名。

---

## ⑰ 纯浏览器 dev(Vite 直开)要能整块降级

`window.api` 在浏览器里是 `undefined` ⇒ 组件必须 `if (!api || !menuAt) return null`,各 effect 内再各自 `if (!api) return`。否则纯 Web 预览直接白屏,而且报错点在无关的地方。
