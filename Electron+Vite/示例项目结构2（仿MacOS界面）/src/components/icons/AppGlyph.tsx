import type { CSSProperties, ReactNode } from 'react'

export type GlyphName =
  | 'apple'
  | 'finder'
  | 'notes'
  | 'calculator'
  | 'appearance'
  | 'info'
  | 'folder'
  | 'file'
  | 'drive'
  | 'trash'
  | 'search'
  | 'wifi'
  | 'battery'
  | 'control'
  | 'grid'
  | 'chevron-left'
  | 'chevron-right'
  | 'arrow-up'
  | 'eye'
  | 'plus'
  | 'minus'
  | 'close'
  | 'expand'
  | 'check'

const PATHS: Record<GlyphName, string[]> = {
  apple: [
    'M16.4 12.6c0-2.3 1.9-3.4 2-3.5-1.1-1.6-2.7-1.8-3.3-1.9-1.4-.1-2.7.8-3.4.8-.7 0-1.8-.8-3-.8-1.5 0-3 .9-3.8 2.3-1.6 2.8-.4 7 1.2 9.2.8 1.1 1.7 2.3 2.9 2.2 1.2 0 1.6-.7 3-.7s1.8.7 3 .7c1.2 0 2-1.1 2.8-2.2.9-1.3 1.2-2.5 1.3-2.6-1.4-.6-2.7-2.1-2.7-3.5z',
    'M14.6 5.7c.6-.8 1.1-1.9 1-3-1 0-2.2.7-2.9 1.5-.6.7-1.1 1.8-1 2.9 1.1.1 2.2-.6 2.9-1.4z'
  ],
  finder: [
    'M4.5 5.5h15a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1v-11a1 1 0 0 1 1-1z',
    'M12 4.5v15',
    'M7.6 10.2v1.4',
    'M16.4 10.2v1.4',
    'M7.9 14.6c1.1 1.1 3 1.1 4.1 0'
  ],
  notes: [
    'M5.5 3.8h13a1.2 1.2 0 0 1 1.2 1.2v14a1.2 1.2 0 0 1-1.2 1.2h-13A1.2 1.2 0 0 1 4.3 19V5A1.2 1.2 0 0 1 5.5 3.8z',
    'M7.5 8h9',
    'M7.5 11.5h9',
    'M7.5 15h5.5'
  ],
  calculator: [
    'M6 3.5h12a1.5 1.5 0 0 1 1.5 1.5v14A1.5 1.5 0 0 1 18 20.5H6A1.5 1.5 0 0 1 4.5 19V5A1.5 1.5 0 0 1 6 3.5z',
    'M7.5 7h9v3.2h-9z',
    'M8 14h.01',
    'M12 14h.01',
    'M16 14h.01',
    'M8 17.2h.01',
    'M12 17.2h.01',
    'M16 17.2h.01'
  ],
  appearance: [
    'M12 3.6a8.4 8.4 0 1 0 0 16.8 8.4 8.4 0 0 0 0-16.8z',
    'M12 3.6v16.8',
    'M12 3.6a8.4 8.4 0 0 1 0 16.8'
  ],
  info: ['M12 3.8a8.2 8.2 0 1 0 0 16.4 8.2 8.2 0 0 0 0-16.4z', 'M12 11v5.4', 'M12 7.9h.01'],
  folder: ['M3.5 7v10.5h17V9.5h-8L11 7z', 'M3.5 7h4.7L9.6 5.3h3.2V7'],
  file: ['M6.5 3.8h7.2L18.5 8.5v11.7H6.5z', 'M13.4 3.8v5h5'],
  drive: [
    'M4.6 5.5h14.8a1.4 1.4 0 0 1 1.4 1.4v10.2a1.4 1.4 0 0 1-1.4 1.4H4.6a1.4 1.4 0 0 1-1.4-1.4V6.9a1.4 1.4 0 0 1 1.4-1.4z',
    'M6.2 9.2h11.6',
    'M8 13.4h4'
  ],
  trash: ['M5 7.5h14', 'M7 7.5V5.6h10v1.9', 'M6.4 7.5l.9 12h9.4l.9-12', 'M10 10.6v6', 'M14 10.6v6'],
  search: ['M10.8 4.4a6.4 6.4 0 1 0 0 12.8 6.4 6.4 0 0 0 0-12.8z', 'M15.6 15.6 20 20'],
  wifi: ['M4.6 9.4a10.6 10.6 0 0 1 14.8 0', 'M7.6 12.6a6.4 6.4 0 0 1 8.8 0', 'M10.4 15.6a2.6 2.6 0 0 1 3.2 0', 'M12 18.6h.01'],
  battery: ['M3.8 8.4h13.4a1.4 1.4 0 0 1 1.4 1.4v4.4a1.4 1.4 0 0 1-1.4 1.4H3.8a1.4 1.4 0 0 1-1.4-1.4V9.8a1.4 1.4 0 0 1 1.4-1.4z', 'M19.4 10.8v2.4', 'M5.6 10.6h6.2v2.8H5.6z'],
  control: ['M6.5 8.5h5M15.5 8.5h2', 'M6.5 15.5h2M11.5 15.5h5', 'M11.5 6.4v4.2', 'M8.5 13.4v4.2'],
  grid: ['M5 5h4v4H5zM15 5h4v4h-4zM5 15h4v4H5zM15 15h4v4h-4z'],
  'chevron-left': ['M14.5 6 8.5 12l6 6'],
  'chevron-right': ['M9.5 6l6 6-6 6'],
  'arrow-up': ['M12 19V5.6', 'M6.4 11.2 12 5.6l5.6 5.6'],
  eye: ['M2.8 12S6.4 6.4 12 6.4 21.2 12 21.2 12 17.6 17.6 12 17.6 2.8 12 2.8 12z', 'M12 14.4a2.4 2.4 0 1 0 0-4.8 2.4 2.4 0 0 0 0 4.8z'],
  plus: ['M12 6v12', 'M6 12h12'],
  minus: ['M6 12h12'],
  close: ['M7 7l10 10', 'M17 7 7 17'],
  expand: ['M14 5h5v5', 'M10 19H5v-5'],
  check: ['M5 12.6l4.4 4.4L19 7.4']
}

const FILLED: Partial<Record<GlyphName, boolean>> = { apple: true }

export function Glyph({
  name,
  size = 16,
  strokeWidth = 1.5,
  className
}: {
  name: GlyphName
  size?: number
  strokeWidth?: number
  className?: string
}) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {PATHS[name].map((d, i) =>
        FILLED[name] ? (
          <path key={i} d={d} fill="currentColor" stroke="none" />
        ) : (
          <path key={i} d={d} />
        )
      )}
    </svg>
  )
}

export type Tone = 'accent' | 'info' | 'warn' | 'ok' | 'deep'

const TONE_BG: Record<Tone, string> = {
  accent: 'linear-gradient(165deg, color-mix(in srgb, var(--c-accent) 96%, transparent), color-mix(in srgb, var(--c-accent) 62%, var(--c-deep)))',
  info: 'linear-gradient(165deg, color-mix(in srgb, var(--c-info) 96%, transparent), color-mix(in srgb, var(--c-info) 58%, var(--c-deep)))',
  warn: 'linear-gradient(165deg, color-mix(in srgb, var(--c-warn) 94%, transparent), color-mix(in srgb, var(--c-warn) 56%, var(--c-deep)))',
  ok: 'linear-gradient(165deg, color-mix(in srgb, var(--c-ok) 94%, transparent), color-mix(in srgb, var(--c-ok) 56%, var(--c-deep)))',
  deep: 'linear-gradient(165deg, var(--c-raised), var(--c-deep))'
}

export function AppIcon({
  tone,
  glyph,
  size = 44,
  className,
  glow = true
}: {
  tone: Tone
  glyph: GlyphName
  size?: number
  className?: string
  glow?: boolean
}) {
  const style: CSSProperties = {
    width: size,
    height: size,
    borderRadius: Math.round(size * 0.26),
    background: TONE_BG[tone],
    color: 'var(--c-text-strong)',
    boxShadow: glow
      ? `inset 0 1px 0 color-mix(in srgb, var(--c-text) 26%, transparent), 0 4px 12px -4px color-mix(in srgb, var(--c-deep) 80%, transparent)`
      : 'inset 0 1px 0 color-mix(in srgb, var(--c-text) 22%, transparent)'
  }
  return (
    <div className={`app-icon ${className ?? ''}`} style={style}>
      <Glyph name={glyph} size={Math.round(size * 0.56)} strokeWidth={1.55} />
    </div>
  )
}

export function AppTile({ tone, glyph, size = 44, label }: { tone: Tone; glyph: GlyphName; size?: number; label?: ReactNode }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
      <AppIcon tone={tone} glyph={glyph} size={size} />
      {label ? <span style={{ fontSize: 11.5, color: 'var(--c-text-dim)' }}>{label}</span> : null}
    </div>
  )
}
