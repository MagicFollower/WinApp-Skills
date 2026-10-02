export interface WallpaperSpec {
  id: string
  label: string
  note: string
}

// 壁纸只是 class（.wall-<id>），颜色全部来自当前色板的 token，所以换主题壁纸跟着换调子。
export const WALLPAPERS: WallpaperSpec[] = [
  { id: 'sonoma', label: 'Sonoma', note: '三色团缓慢漂移' },
  { id: 'sequoia', label: 'Sequoia', note: '锥形光斑 + 反向漂移' },
  { id: 'bigsur', label: 'Big Sur', note: '斜向光带，静态' },
  { id: 'ventura', label: 'Ventura', note: '中心球体呼吸' },
  { id: 'flat', label: '素色', note: '纯渐变，省电' }
]

export function wallpaperClass(id: string): string {
  return `wall-${id}`
}
