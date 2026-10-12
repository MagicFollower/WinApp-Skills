# 真窗口验收:三组探针 + 判据

静态审计(`scripts/check-wiring.mjs`)只能证明**接线在**。这三组要证明**行为对**,只能在真窗口里跑。

通用做法:在主进程加一个旗标分支(`PROBE === 'drag'` 之类),`app.whenReady()` 里跑完探针就 `app.quit()`,结果 `console.log(JSON.stringify(...))` 打到 stdout。

三条纪律,违反任何一条结论就不成立:

1. **输出重定向到文件**,不要接管道 —— 管道读端先死会让主进程 `console.log` 抛 EPIPE,Electron 弹模态错误框糊你一脸(见 pitfalls ⑬)。
2. **计数器自证**:每条腿带 `dragLog` 这类逐事件计数,证明"这一枪真开过"。没有它,"通过"可能只是"什么都没发生"。
3. **判据打在缺陷可见的那一层**,并且要能证伪 —— 每条都要配一条对照腿(不该触发的场景),否则门是虚掩的。

---

## 探针 A:右键归 DOM 管了吗

页面侧先注入一组 helper(用 `executeJavaScript`):

```js
window.__appRegion = () => [...document.querySelectorAll('*')]
  .filter((el) => getComputedStyle(el)['-webkit-app-region'] === 'drag').length;
window.__menu = () => !!document.querySelector('.win-menu');
window.__bandPoint = (sel) => { const r = document.querySelector(sel).getBoundingClientRect();
  return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) }; };
```

腿次(每条都要独立开关):

| 腿 | 动作 | 期望 |
| --- | --- | --- |
| A1 扫描 | `__appRegion()` | **0**(整棵树没有一处 drag) |
| A2 主带 | 在 `.chat-header` 中点右键 | `__menu()` = true,条目数 = 3 |
| A3 次带 | 在另一条 `.drag-region`(如左栏顶带)右键 | 同上 —— 证明不是只接了一处 |
| A4 对照 | 在带内的 `.no-drag` 子元素上右键 | 菜单**不**出现(先关掉再点,别用上一次的残留判) |
| A5 关闭 | `Escape` / 带外 `pointerdown` | 菜单消失 |
| A6 OS 侧 | 全程 `webContents.on('context-menu')` 计数 | **0 次**(右键真的没回到 OS) |

两个坑:

- **别在动画进行中量菜单几何**。入场动效没落定就读,量到的是 115×42 而不是 120×44。右键后 `await sleep(300)` 再读。
- **坐标要页内算**。写死 `(700, 36)` 结果落在 `.no-drag` 胶囊上,整条腿看起来像失败。用 `__bandPoint(sel)`,必要时先 `document.elementFromPoint` 打 hit chain 确认落点。

`sendInputEvent` 可以绕过 OS 的拖拽区拦截把事件送进页面(pitfalls ①的反向用法),所以 A2/A4 能用合成事件跑 —— 但注意它证明的是**DOM 接线**,不是 OS 行为。

---

## 探针 B:拖拽接线与位移公式

**端到端不能用合成指针验证**:合成事件的坐标是 view 相对的,而 `screenX = winX + viewX`,窗口一动 `winX` 就变 ⇒ 自指反馈,量不出真实跟手。所以分两层验:

**B1 接线层** —— 页内派发合成 `pointerdown/move/up` 到 `.drag-region` 中点,读主进程模块变量计数器:

```
dragLog: { start: 1, move: n, end: 1 }        // n>0
boundsBefore === boundsAfter                   // 真实光标没动 ⇒ 窗口必须一格不差
```

第二条是**必要的**:它证明位移公式吃的是"真实光标 - 起手光标",而不是把 view 坐标当成屏幕坐标(后者会瞬移几千 px)。

**B2 公式层** —— 把 `dragTarget(anchor, cx, cy, workTop)` 单独导出,用合成数字验:

| 用例 | 输入 | 期望 |
| --- | --- | --- |
| 静止 | 光标与起手一致 | 位置不变 |
| 右下 | `+50, +30` | 精确 `+50, +30`(不累计、不缩放) |
| 顶边 | `cy` 小于工作区顶 | `y === workTop` 且 `hitTop: true` |
| 左上 | `-5000, -5000` | 夹在 `workTop`,不越界 |

**B3 双击** —— 合成两次 `dblclick` → `isMaximized()` 翻转两次(真最大化、真还原)。

**B4 单击不许恢复**(这条就是修 bug 的那一枪):先 `maximize()`,然后 `pointerdown` + 立刻 `pointerup`(位移 0)。判据是三条**同时**成立:

```
stillMaxOnPointerdown: true      // drag-start 没还原
stillMaxAfterClick:    true      // 整条链走完仍然最大化
clickLog: { start: 1, move: 0, end: 1 }   // 证明这一枪开过,不是空跑
```

再加一条过阈值的对照:构造 `cursor - start >= 4` 的场景,必须**真的**从最大化变成 normal。两条一起才叫限幅生效,只测前者是"阈值写死了"。

**别把"窗口位移 = 光标漂移"当判据**。信息项可以,判据不行:它把 IPC 往返当成了正确性。

---

## 探针 C:退场动画的三腿对照

每条腿都要:注入该腿的 CSS → 开菜单 → 起 rAF 采样器 → 点最小化按钮 → 一边 `capturePage` 小方块一边收样 → 断言真的最小化了 → `win.restore()` 复原再跑下一条。

| 腿 | 注入 | 作用 |
| --- | --- | --- |
| off | 无 transition(`.window{transition:none}`) | 基线:系统自己的呈现节奏 |
| on | 正式退场 CSS | 被测 |
| bd | 去掉 `will-change` | 归因:层级提升到底有没有用 |

采到的两组数:

1. **呈现帧下限**:动画期间循环 `capturePage({x:470,y:300,width:160,height:160})`,`img.toBitmap()` 哈希去重 → `distinct/calls`。两轮实测:`off`(撤优化)11/16 与 13/16,`on`(退场曲线 + `will-change`)15/18 与 17/19。**绝对值随取图吞吐抖 ±2 帧,只能当同一轮内的相对次序用**。
2. **曲线形状**:在 20/60/100/140/180ms 采样 `getComputedStyle(root).transform` 的 scale。强减速曲线在 60ms 就已经走完 ~85%,这是"看着掉帧"的真凶。

两个必须防的假绿:

- **rAF 间隔 ≠ 呈现帧**。它量的是主线程 BeginFrame 节奏;`getComputedStyle` 读的是时钟插值。只有 `capturePage` + 位图哈希能给呈现帧的**下限**。之前我拿 rAF max=8ms 报"帧率没问题",被用户一句"帧率太低"推翻 —— 仪器测错了层。
- **只看落定帧分不清"播过"与"根本没动画"**。必须采中间帧(`.cache/minanim-mid.png` 那种),或者用曲线形状判断。

reduced-motion 腿用 Chromium 旗标跑:`--force-prefers-reduced-motion`,期望是**立即最小化、不播动画、也不卡住**(兜底计时器那条路)。

---

## 取证习惯

- 圆角/位置这类几何取证要 `capturePage` **1:1** 裁图,别缩放截图量像素。
- 隐藏页(`show: false`)的 `elementFromPoint` 用的是上一帧 hit tree,rAF 也会被饿死 —— 要么显式窗口再量,要么同步步进。
- 变异/开关类实验**收尾要恢复**(改注册表、改令牌、改窗口样式都写 revert),并记录恢复后的实测值。
- 杀进程只按自己枚举到的 pid,且先验证 `Path` 属于本工程,不要按名字批量杀。
