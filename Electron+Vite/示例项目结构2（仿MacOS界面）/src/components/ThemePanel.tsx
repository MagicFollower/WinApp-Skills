import { useEffect, useMemo, useState } from 'react'
import { PALETTES, VARIANT_CHOICES, readSwatch, resolveThemeId, type Mode, type Swatch } from '../lib/theme'
import { WALLPAPERS } from '../data/wallpapers'
import type { Scheme, Settings, Variant } from '../types/electron-api'

interface Props {
  settings: Settings
  mode: Mode
  appliedId: string
  appliedBg: string
  onScheme: (scheme: Scheme) => void
  onVariant: (variant: Variant) => void
  onPalette: (theme: string) => void
  onWallpaper: (id: string) => void
}

const SCHEMES: { value: Scheme; label: string; hint: string }[] = [
  { value: 'system', label: '跟随系统', hint: 'nativeTheme.themeSource = system' },
  { value: 'light', label: '浅色', hint: 'nativeTheme.themeSource = light' },
  { value: 'dark', label: '深色', hint: 'nativeTheme.themeSource = dark' }
]

export default function ThemePanel(p: Props) {
  // 变体为 auto 时按当前明暗挑 dark/day，否则用显式变体——与 App 的生效口径保持一致。
  const wantedVariant: Variant = p.settings.variant

  const [swatches, setSwatches] = useState<Swatch[]>([])
  useEffect(() => {
    const next = PALETTES.map(pal => {
      const { id, mode } = resolveThemeId(pal.theme, wantedVariant, p.mode)
      return readSwatch(id, pal.theme, mode)
    })
    setSwatches(next)
  }, [wantedVariant, p.mode])

  const grouped = useMemo(() => {
    const dark: Swatch[] = []
    const light: Swatch[] = []
    for (const s of swatches) (s.mode === 'light' ? light : dark).push(s)
    return { dark, light }
  }, [swatches])

  return (
    <div className="theme-pane">
      <section className="tp-block">
        <h3 className="tp-title">明暗来源</h3>
        <div className="seg" role="group" aria-label="明暗来源">
          {SCHEMES.map(s => (
            <button
              type="button"
              key={s.value}
              className={`seg-btn ${p.settings.scheme === s.value ? 'is-on' : ''}`}
              title={s.hint}
              onClick={() => p.onScheme(s.value)}
            >
              {s.label}
            </button>
          ))}
        </div>
        <p className="tp-note">
          变体选「自动」时，这一栏决定跟随系统的哪个方向；选了具体变体后，由变体自己的 mode 反过来翻转
          themeSource，所以滚动条、表单控件和内容不会一半深一半浅。
        </p>
      </section>

      <section className="tp-block">
        <h3 className="tp-title">变体</h3>
        <div className="tp-chips">
          {VARIANT_CHOICES.map(v => (
            <button
              type="button"
              key={v.value}
              className={`tp-chip ${p.settings.variant === v.value ? 'is-on' : ''}`}
              title={v.hint}
              onClick={() => p.onVariant(v.value as Variant)}
            >
              {v.label}
            </button>
          ))}
        </div>
      </section>

      <section className="tp-block">
        <h3 className="tp-title">色板 · {PALETTES.length} 套（100themes 白名单）</h3>
        <div className="tp-grid">
          {[...grouped.dark, ...grouped.light].map(s => {
            const on = s.theme === p.settings.palette
            const label = PALETTES.find(x => x.theme === s.theme)?.label ?? s.theme
            return (
              <button type="button" key={s.id} className={`tp-card ${on ? 'is-on' : ''}`} onClick={() => p.onPalette(s.theme)}>
                <span className="tp-mode">{s.mode === 'light' ? '亮' : '暗'}</span>
                <span className="tp-swatches">
                  <span className="tp-swatch" style={{ background: s.bg }} />
                  <span className="tp-swatch" style={{ background: s.fg }} />
                  <span className="tp-swatch" style={{ background: s.accent }} />
                </span>
                <span className="tp-name">{label}</span>
                <span className="tp-meta">{s.id.split('--')[1]}</span>
              </button>
            )
          })}
        </div>
      </section>

      <section className="tp-block">
        <h3 className="tp-title">壁纸</h3>
        <div className="tp-chips">
          {WALLPAPERS.map(w => (
            <button
              type="button"
              key={w.id}
              className={`tp-chip ${p.settings.wallpaper === w.id ? 'is-on' : ''}`}
              title={w.note}
              onClick={() => p.onWallpaper(w.id)}
            >
              {w.label}
            </button>
          ))}
        </div>
      </section>

      <section className="tp-block">
        <h3 className="tp-title">当前生效</h3>
        <dl className="about-dl">
          <dt>data-theme</dt>
          <dd>{p.appliedId}</dd>
          <dt>--pf-bg</dt>
          <dd>{p.appliedBg || '（未读到）'}</dd>
          <dt>themeSource</dt>
          <dd>{p.settings.variant === 'auto' ? p.settings.scheme : `${p.settings.variant} → ${p.mode}`}</dd>
        </dl>
      </section>
    </div>
  )
}
