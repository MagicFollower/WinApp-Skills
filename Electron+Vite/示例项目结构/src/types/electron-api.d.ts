interface DocReadResult {
  text: string | null
  savedAt: number | null
}

interface DocSaveResult {
  savedAt: number
}

interface DocRevertResult {
  removed: boolean
}

interface AppInfoResult {
  version: string
  userData: string
  editsDir: string
}

interface ManualApi {
  readDoc(id: string): Promise<DocReadResult>
  saveDoc(id: string, text: string): Promise<DocSaveResult>
  revertDoc(id: string): Promise<DocRevertResult>
  appInfo(): Promise<AppInfoResult>
}

declare global {
  interface Window {
    api: ManualApi
  }
}

export {}
