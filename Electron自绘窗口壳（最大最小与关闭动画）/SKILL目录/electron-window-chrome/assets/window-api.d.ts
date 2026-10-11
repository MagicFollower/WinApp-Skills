/* 渲染层的 window.api 声明。拷进目标工程(例:src/window-chrome.d.ts)即可,
   文件名以 .d.ts 结尾、被 tsconfig 的 include 覆盖到就生效,不需要 import。
   工程里已有 WindowApi 接口的话,把这 7 个成员并进那个接口,别留两份 Window 声明。 */

interface WindowChromeApi {
  minimize: () => Promise<void>;
  maximize: () => Promise<void>;
  close: () => Promise<void>;
  /** 起手拖窗。同步 send,不等回执。 */
  dragStart: () => void;
  /** 拖拽中每帧一次。不传坐标:主进程现读光标。 */
  dragMove: () => void;
  /** 松手/取消/窗口失焦。 */
  dragEnd: () => void;
  /** 主进程 win.on('restore') 的推送。恢复入场靠它。 */
  onRestore: (cb: () => void) => void;
}

interface Window {
  /** preload 没注入时是 undefined(纯浏览器跑 dev server 就是这种),窗口壳据此整块不渲染。 */
  api?: WindowChromeApi;
}
