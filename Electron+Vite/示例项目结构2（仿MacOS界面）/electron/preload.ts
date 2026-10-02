import { contextBridge, ipcRenderer } from 'electron'

// sandbox 下的 preload 只能用 contextBridge 与 ipcRenderer：不放业务逻辑、不 require 第三方。
// theme:changed 是主进程 -> 渲染层的单向广播，用 id 管理订阅，避免把函数当返回值传出去。
let nextSubId = 1
const themeSubs = new Map<number, (payload: unknown) => void>()

ipcRenderer.on('theme:changed', (_event, payload: unknown) => {
  for (const cb of themeSubs.values()) {
    try {
      cb(payload)
    } catch {
      // 单个订阅者抛错不该断掉广播
    }
  }
})

contextBridge.exposeInMainWorld('api', {
  getSettings: () => ipcRenderer.invoke('settings:get'),
  setSettings: (patch: Record<string, unknown>) => ipcRenderer.invoke('settings:set', patch),
  notesList: () => ipcRenderer.invoke('notes:list'),
  notesSave: (id: string, text: string) => ipcRenderer.invoke('notes:save', id, text),
  notesRemove: (id: string) => ipcRenderer.invoke('notes:remove', id),
  home: () => ipcRenderer.invoke('fs:home'),
  listDir: (dir: string, showHidden: boolean) => ipcRenderer.invoke('fs:list', dir, showHidden),
  readFile: (file: string) => ipcRenderer.invoke('fs:read', file),
  appInfo: () => ipcRenderer.invoke('app:info'),
  quit: () => ipcRenderer.invoke('app:quit'),
  onThemeChanged: (cb: (payload: unknown) => void) => {
    const id = nextSubId++
    themeSubs.set(id, cb)
    return id
  },
  offThemeChanged: (id: number) => themeSubs.delete(id)
})
