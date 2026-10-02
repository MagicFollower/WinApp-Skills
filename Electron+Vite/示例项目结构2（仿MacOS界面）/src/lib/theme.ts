import RAW from '../styles/themes.index.json'

export type Mode = 'dark' | 'light'
export type VariantName = 'dark' | 'day' | 'oled' | 'high-contrast' | 'day-high-contrast'

export interface PaletteEntry {
  id: string
  theme: string
  variant: VariantName
  mode: Mode
}

const INDEX = RAW as PaletteEntry[]

// 与主进程 VARIANT_MODE 同一张表：变体自带明暗，选它就要把原生控件一起翻过去。
export const VARIANT_MODE: Record<VariantName, Mode> = {
  dark: 'dark',
  oled: 'dark',
  'high-contrast': 'dark',
  day: 'light',
  'day-high-contrast': 'light'
}

// 面板里"变体"这一排的可选项；auto 表示跟随当前明暗在 dark / day 之间自动挑。
export const VARIANT_CHOICES: { value: 'auto' | VariantName; label: string; hint: string }[] = [
  { value: 'auto', label: '自动', hint: '跟随明暗：亮色走 day，暗色走 dark' },
  { value: 'dark', label: 'Dark', hint: '深色基准' },
  { value: 'day', label: 'Day', hint: '亮色版，会把系统控件一起翻成 light' },
  { value: 'oled', label: 'OLED', hint: '纯黑底，省电费那种' },
  { value: 'high-contrast', label: '高对比', hint: '暗底高对比' },
  { value: 'day-high-contrast', label: '亮高对比', hint: '亮底高对比' }
]

const byTheme = new Map<string, Map<VariantName, PaletteEntry>>()
for (const entry of INDEX) {
  let variants = byTheme.get(entry.theme)
  if (!variants) {
    variants = new Map()
    byTheme.set(entry.theme, variants)
  }
  variants.set(entry.variant, entry)
}

export interface Palette {
  theme: string
  label: string
  hasDark: boolean
  hasDay: boolean
}

export const PALETTES: Palette[] = [...byTheme.keys()]
  .sort((a, b) => a.localeCompare(b))
  .map(theme => ({
    theme,
    label: theme.replace(/-/g, ' ').replace(/\b[a-z]/g, c => c.toUpperCase()),
    hasDark: byTheme.get(theme)?.has('dark') ?? false,
    hasDay: byTheme.get(theme)?.has('day') ?? false
  }))

/**
 * data-theme 的真值口径：显式变体直接用那个变体；auto 时按当前明暗挑 dark/day。
 * 缺该变体就退回任一存在的变体，保证不会出现 data-theme 指向不存在的块。
 */
export function resolveThemeId(palette: string, variant: 'auto' | VariantName, mode: Mode): { id: string; mode: Mode } {
  const variants = byTheme.get(palette) ?? byTheme.get(PALETTES[0].theme)
  if (!variants) return { id: 'abyss--dark', mode: 'dark' }
  const wanted: VariantName =
    variant === 'auto' ? (mode === 'light' ? 'day' : 'dark') : variant
  const hit = variants.get(wanted) ?? variants.get('dark') ?? variants.get('day') ?? [...variants.values()][0]
  return { id: hit.id, mode: hit.mode }
}

// 色卡需要显示"别的主题"的颜色，而那些颜色只存在于 [data-theme] 块里，
// 所以借一个隐藏节点把该块的自定义属性读回来，不额外维护一份颜色 JSON。
let probe: HTMLDivElement | null = null

function probeEl(): HTMLDivElement {
  if (probe) return probe
  probe = document.createElement('div')
  probe.setAttribute('aria-hidden', 'true')
  probe.style.cssText = 'position:absolute;width:0;height:0;opacity:0;pointer-events:none;left:-9999px;top:-9999px;'
  document.body.appendChild(probe)
  return probe
}

export interface Swatch {
  theme: string
  id: string
  mode: Mode
  bg: string
  fg: string
  accent: string
}

export function readSwatch(id: string, theme: string, mode: Mode): Swatch {
  const el = probeEl()
  el.setAttribute('data-theme', id)
  const style = getComputedStyle(el)
  const pick = (name: string) => style.getPropertyValue(name).trim()
  return { theme, id, mode, bg: pick('--pf-bg'), fg: pick('--pf-fg'), accent: pick('--pf-accent') }
}
