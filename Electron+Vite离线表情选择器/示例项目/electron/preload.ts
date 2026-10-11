import { contextBridge, ipcRenderer } from 'electron';

// sandbox: true 下 preload 只能用 contextBridge 与 ipcRenderer，不要 require 其他模块。
contextBridge.exposeInMainWorld('api', {
  ping: (): Promise<string> => ipcRenderer.invoke('app:ping'),
  env: (): Promise<{ themeSource: string; shouldUseDarkColors: boolean }> =>
    ipcRenderer.invoke('app:env')
});
