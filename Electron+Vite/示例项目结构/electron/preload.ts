import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('api', {
  readDoc: (id: string) => ipcRenderer.invoke('doc:read', id),
  saveDoc: (id: string, text: string) => ipcRenderer.invoke('doc:save', id, text),
  revertDoc: (id: string) => ipcRenderer.invoke('doc:revert', id),
  appInfo: () => ipcRenderer.invoke('app:info')
})
