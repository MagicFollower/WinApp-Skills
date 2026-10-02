import type { Tone, GlyphName } from '../components/icons/AppGlyph'

export type AppId = 'finder' | 'notes' | 'calculator' | 'appearance' | 'about'

export interface AppSpec {
  id: AppId
  name: string
  tone: Tone
  glyph: GlyphName
  w: number
  h: number
  minW: number
  minH: number
  blurb: string
  keywords: string[]
  /** 该应用需要多实例还是单实例：桌面壳里全部按单实例处理（再点前置）。 */
  inDock: boolean
}

export const APPS: AppSpec[] = [
  {
    id: 'finder',
    name: '访达',
    tone: 'info',
    glyph: 'finder',
    w: 780,
    h: 500,
    minW: 420,
    minH: 280,
    blurb: '只读浏览主目录内的文件',
    keywords: ['finder', '文件', '访达', 'folder', 'disk'],
    inDock: true
  },
  {
    id: 'notes',
    name: '备忘录',
    tone: 'warn',
    glyph: 'notes',
    w: 700,
    h: 470,
    minW: 420,
    minH: 280,
    blurb: '笔记存到 userData/notes，写读删都走 IPC',
    keywords: ['notes', '备忘', '笔记', 'md'],
    inDock: true
  },
  {
    id: 'calculator',
    name: '计算器',
    tone: 'ok',
    glyph: 'calculator',
    w: 300,
    h: 430,
    minW: 260,
    minH: 360,
    blurb: '键盘可输入',
    keywords: ['calculator', '计算', '算术'],
    inDock: true
  },
  {
    id: 'appearance',
    name: '外观',
    tone: 'accent',
    glyph: 'appearance',
    w: 700,
    h: 540,
    minW: 460,
    minH: 360,
    blurb: '24 套色板 × 5 变体、明暗三态与壁纸',
    keywords: ['appearance', '主题', '外观', 'theme', '色板', '壁纸', 'dark', 'light'],
    inDock: true
  },
  {
    id: 'about',
    name: '关于本机',
    tone: 'deep',
    glyph: 'info',
    w: 360,
    h: 440,
    minW: 300,
    minH: 320,
    blurb: '运行时版本与设置文件位置',
    keywords: ['about', '关于', '版本', 'runtime'],
    inDock: false
  }
]

export const DOCK_APPS: AppId[] = APPS.filter(a => a.inDock).map(a => a.id)

export function appSpec(id: AppId): AppSpec {
  return APPS.find(a => a.id === id) ?? APPS[0]
}
