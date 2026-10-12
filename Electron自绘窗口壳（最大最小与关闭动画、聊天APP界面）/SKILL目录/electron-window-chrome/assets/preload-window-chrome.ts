import { contextBridge, ipcRenderer } from 'electron';

/* ============================================================================
   preload 片段:窗口壳要暴露的那 7 个方法。
   两种接法,选一种:
     A. 工程还没有 contextBridge 暴露点 ⇒ 直接保留本文件的 exposeInMainWorld('api', chromeApi)。
     B. 工程已经有一个 exposeInMainWorld('api', {...}) ⇒ 把 chromeApi 的字段并进那个对象,
        不要新开第二个名字(两个键名会让渲染层不知道该用哪个,而且 d.ts 会打架)。
   ========================================================================== */

const chromeApi = {
  minimize: () => ipcRenderer.invoke('window:minimize'),
  maximize: () => ipcRenderer.invoke('window:maximize'),
  close: () => ipcRenderer.invoke('window:close'),
  // 拖拽走 send 而不是 invoke:pointermove 是几十 Hz 的高频源,不需要每帧一个回执 promise。
  // 不带坐标 —— 位移由主进程现读真实光标算,见 window-chrome.ts 的 window:drag-*。
  // 一旦这里改成传坐标,就等于把 IPC 延迟算进位移,手感变成"窗口追光标"。
  dragStart: () => ipcRenderer.send('window:drag-start'),
  dragMove: () => ipcRenderer.send('window:drag-move'),
  dragEnd: () => ipcRenderer.send('window:drag-end'),
  // 恢复入场要等主进程的 restore 事件(窗口已经可见),所以是单向推送
  onRestore: (cb: () => void) => ipcRenderer.on('window:restored', () => cb()),
};

contextBridge.exposeInMainWorld('api', chromeApi);

/* sandbox:true 的两条约束(工程用 sandbox 时都会踩到):
   1. preload 必须是 CJS(本文件的 import 由构建工具转 CJS;别直接用 ESM 产物 + .mjs)。
   2. 只能 require electron,拿不到 fs / path / node:*,所以这里不掺业务 IPC。 */
