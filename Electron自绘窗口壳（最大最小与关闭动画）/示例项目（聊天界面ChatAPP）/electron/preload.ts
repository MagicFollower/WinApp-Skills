import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('api', {
  ping: () => ipcRenderer.invoke('app:ping'),
  minimize: () => ipcRenderer.invoke('window:minimize'),
  maximize: () => ipcRenderer.invoke('window:maximize'),
  close: () => ipcRenderer.invoke('window:close'),
  // 拖拽走 send 而不是 invoke:pointermove 是几十 Hz 的高频源,不需要每帧一个回执 promise。
  // 不带坐标 —— 位移由主进程现读真实光标算,见 main.ts 的 window:drag-*。
  dragStart: () => ipcRenderer.send('window:drag-start'),
  dragMove: () => ipcRenderer.send('window:drag-move'),
  dragEnd: () => ipcRenderer.send('window:drag-end'),
  // 恢复入场要等主进程的 restore 事件(窗口已经可见),所以是单向推送
  onRestore: (cb: () => void) => ipcRenderer.on('window:restored', () => cb()),
});
