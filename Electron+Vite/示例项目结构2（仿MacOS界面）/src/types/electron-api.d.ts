type Scheme = 'system' | 'light' | 'dark'
type Variant = 'auto' | 'dark' | 'day' | 'oled' | 'high-contrast' | 'day-high-contrast'

interface Settings {
  scheme: Scheme
  palette: string
  variant: Variant
  wallpaper: string
  bg: string
}

interface SettingsResult {
  settings: Settings
  mode: Scheme
}

interface Note {
  id: string
  title: string
  text: string
  updatedAt: number
}

interface FsEntry {
  name: string
  isDir: boolean
  size: number
  mtimeMs: number
  hidden: boolean
}

interface DirListing {
  dir: string
  entries: FsEntry[]
}

interface QuickAccessItem {
  label: string
  path: string
}

interface HomeResult {
  home: string
  quickAccess: QuickAccessItem[]
}

interface TextPreview {
  name: string
  text: string
  truncated: boolean
}

interface AppInfoResult {
  version: string
  name: string
  userData: string
  notesDir: string
  home: string
  settings: Settings
  runtime: {
    electron: string
    chrome: string
    node: string
    platform: string
    osRelease: string
  }
}

interface ThemeChangedPayload {
  settings: Settings
  mode: Scheme
}

interface DesktopApi {
  getSettings(): Promise<SettingsResult>
  setSettings(patch: Partial<Settings>): Promise<SettingsResult>
  notesList(): Promise<Note[]>
  notesSave(id: string, text: string): Promise<Note>
  notesRemove(id: string): Promise<{ removed: boolean }>
  home(): Promise<HomeResult>
  listDir(dir: string, showHidden: boolean): Promise<DirListing>
  readFile(file: string): Promise<TextPreview>
  appInfo(): Promise<AppInfoResult>
  quit(): Promise<void>
  onThemeChanged(cb: (payload: unknown) => void): number
  offThemeChanged(id: number): boolean
}

declare global {
  interface Window {
    api: DesktopApi
  }
}

export type { Scheme, Variant, Settings, SettingsResult, Note, FsEntry, DirListing, QuickAccessItem, HomeResult, TextPreview, AppInfoResult, ThemeChangedPayload }
