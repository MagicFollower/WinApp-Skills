import { BrowserWindow, ipcMain, screen } from 'electron';

/* ============================================================================
   主进程侧:三项窗口控制 + 自绘拖拽。拷进目标工程(例:electron/window-chrome.ts),
   然后在每个窗口的创建处调用一次 attachWindowChrome(win)。

   导出面:
     attachWindowChrome(win)  注册 IPC(全局一次)+ 接 restore 推送。多窗口各调一次。
     minimizeWindow / maximizeWindow / closeWindow  可选,给快捷键、托盘菜单等主进程调用方复用。

   前置条件:BrowserWindow 要 frame:false + transparent:true(退场动画靠透明露出桌面),
   且渲染层里不存在任何 -webkit-app-region 声明。
   ========================================================================== */

type DragAnchor = {
  wx: number; wy: number;   // 起手时的窗口原点
  cx: number; cy: number;   // 起手时的光标原点
  moved: boolean;           // 真的位移过(纯点击不算)
  hitTop: boolean;          // 顶边贴过工作区顶
  /** 起手时窗口是最大化的:要等位移超过起拖阈值才还原 —— 否则"点一下标题带"就等于
      "拽下来",用户看到的就是双击最大化之后单击即恢复。 */
  wasMax: boolean;
};

/* Windows 的 SM_CXDRAG 档(100% 缩放下 4px):小于它算点击,不算拖拽。
   阈值按 DIP 比较(screen.getCursorScreenPoint 返回的就是 DIP),不要乘 devicePixelRatio,
   否则 125%/150% 缩放下起拖手感会变。 */
const DRAG_SLOP = 4;
/** 从最大化拖下来之后,光标要落在标题带的哪条横线上(取标题带视觉中心附近)。改标题带高度要同步改。 */
const DRAG_GRAB_Y = 24;

/* 单槽锚点:同一时刻只跟一次拖拽。多窗口同时拖会互相踩 —— 单窗口应用无所谓,
   真要多窗口并发就把 dragAnchor 改成 Map<webContents.id, DragAnchor>。 */
let dragAnchor: DragAnchor | null = null;
let ipcInstalled = false;

/** 位移公式单独抽出来,是为了让验收探针能拿合成数字验它本身 —— 真实光标不能替用户搬窗口。 */
export function dragTarget(a: DragAnchor, cx: number, cy: number, workTop: number) {
  let y = a.wy + (cy - a.cy);
  const hitTop = y < workTop;
  if (hitTop) y = workTop;
  return { x: Math.round(a.wx + (cx - a.cx)), y: Math.round(y), hitTop };
}

function pastDragSlop(a: DragAnchor, cx: number, cy: number) {
  return Math.abs(cx - a.cx) >= DRAG_SLOP || Math.abs(cy - a.cy) >= DRAG_SLOP;
}

export function minimizeWindow(win: BrowserWindow) {
  win.minimize();
}

/** 最大化的对立面是"还原到之前的 normal bounds",不是"设回一个写死的尺寸"。 */
export function maximizeWindow(win: BrowserWindow) {
  if (win.isMaximized()) win.unmaximize();
  else win.maximize();
}

export function closeWindow(win: BrowserWindow) {
  win.close();
}

function registerIpcOnce() {
  if (ipcInstalled) return;
  ipcInstalled = true;

  /* 控制条的三颗按钮。都用 handle:点击是低频源,要一个回执才好确认"没点到但也没报错"。 */
  ipcMain.handle('window:minimize', (e) => {
    const win = BrowserWindow.fromWebContents(e.sender);
    if (win) minimizeWindow(win);
  });
  ipcMain.handle('window:maximize', (e) => {
    const win = BrowserWindow.fromWebContents(e.sender);
    if (win) maximizeWindow(win);
  });
  ipcMain.handle('window:close', (e) => {
    BrowserWindow.fromWebContents(e.sender)?.close();
  });

  /* ===== 自绘拖拽 =====
     位移算法:起手时记住"窗口原点 + 光标原点",每帧用**主进程现读的实时光标**算增量
     ⇒ 渲染层只报"动了一下",不传坐标,所以既不累计误差,也不受某一帧丢事件影响。
     (渲染层传坐标的写法会把 IPC 延迟算进位移,手感是"窗口追光标"。)
     顶边贴住工作区顶 = Aero 那套"卡住再松手就最大化";松手时没动过(纯点击)不算。 */
  ipcMain.on('window:drag-start', (e) => {
    const win = BrowserWindow.fromWebContents(e.sender);
    if (!win) return;
    const b = win.getBounds();
    const c = screen.getCursorScreenPoint();
    /* 这里**不能**还原最大化窗口 —— 起手就还原等于"单击即恢复"。见 DragAnchor.wasMax。 */
    dragAnchor = { wx: b.x, wy: b.y, cx: c.x, cy: c.y, moved: false, hitTop: false, wasMax: win.isMaximized() };
  });

  ipcMain.on('window:drag-move', (e) => {
    const win = BrowserWindow.fromWebContents(e.sender);
    if (!win || !dragAnchor) return;
    const c = screen.getCursorScreenPoint();
    const a = dragAnchor;
    if (a.wasMax) {
      // 阈值之前什么都不做:最大化状态下"动一点点"仍然是点击(双击恢复靠的就是它)
      if (!pastDragSlop(a, c.x, c.y)) return;
      // 真拖起来了 ⇒ 还原,并让光标落在标题带同一相对位置,然后按新状态重新锚定
      const m = win.getBounds();
      const fx = m.width > 0 ? (c.x - m.x) / m.width : 0.5;
      win.unmaximize();
      const n = win.getBounds();
      win.setPosition(Math.round(c.x - n.width * fx), Math.round(c.y - DRAG_GRAB_Y));
      const c2 = screen.getCursorScreenPoint();
      const b2 = win.getBounds();
      a.wx = b2.x; a.wy = b2.y; a.cx = c2.x; a.cy = c2.y; a.wasMax = false; a.moved = true;
      return;
    }
    const wa = screen.getDisplayMatching(win.getBounds()).workArea;
    const t = dragTarget(a, c.x, c.y, wa.y);
    if (t.x !== a.wx || t.y !== a.wy) a.moved = true;
    if (t.hitTop) a.hitTop = true;
    win.setPosition(t.x, t.y);
  });

  ipcMain.on('window:drag-end', (e) => {
    const win = BrowserWindow.fromWebContents(e.sender);
    const a = dragAnchor;
    dragAnchor = null;
    if (win && a && a.moved && a.hitTop && !win.isMaximized()) win.maximize();
  });
}

/**
 * 在一个窗口上装好窗口壳。返回同一个 win,方便 `const win = attachWindowChrome(new BrowserWindow(...))`。
 * @param restoreChannel 渲染层 onRestore 监听的频道名,默认 'window:restored'
 */
export function attachWindowChrome(win: BrowserWindow, restoreChannel = 'window:restored') {
  registerIpcOnce();
  /* 退场动画留下的类要在恢复之后才摘(渲染层收到这条就摘,且不反向播)。
     minimized → restore 是 OS 路径,应用拦不到"从任务栏点回来"这一下,所以只能听 restore 事件。 */
  win.on('restore', () => {
    if (!win.isDestroyed()) win.webContents.send(restoreChannel);
  });
  return win;
}
