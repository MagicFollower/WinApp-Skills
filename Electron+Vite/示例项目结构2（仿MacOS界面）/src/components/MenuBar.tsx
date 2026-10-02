import { useEffect, useRef, useState } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Glyph } from './icons/AppGlyph'
import { WALLPAPERS } from '../data/wallpapers'
import { VARIANT_CHOICES, type Mode } from '../lib/theme'
import type { AppId } from '../data/apps'
import type { Scheme, Settings, Variant } from '../types/electron-api'

type Row =
  | { kind: 'sep' }
  | { kind: 'item'; text: string; key?: string; disabled?: boolean; run?: () => void }

interface Menu {
  id: string
  label: string
  strong?: boolean
  rows: Row[]
}

const item = (text: string, opts: { key?: string; disabled?: boolean; run?: () => void } = {}): Row => ({
  kind: 'item',
  text,
  ...opts
})

const sep = (): Row => ({ kind: 'sep' })

interface Props {
  appName: string
  settings: Settings
  mode: Mode
  onOpenApp: (id: AppId) => void
  onNewNote: () => void
  onCloseFront: () => void
  onMinimizeFront: () => void
  onShowAll: () => void
  onSpotlight: () => void
  onScheme: (scheme: Scheme) => void
  onVariant: (variant: Variant) => void
  onWallpaper: (id: string) => void
  onQuit: () => void
}

const WEEK = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

function clockText(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${WEEK[d.getDay()]} ${d.getMonth() + 1}月${d.getDate()}日 ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const SCHEMES: { value: Scheme; label: string }[] = [
  { value: 'system', label: '跟随系统' },
  { value: 'light', label: '浅色' },
  { value: 'dark', label: '深色' }
]

export default function MenuBar(p: Props) {
  const [openId, setOpenId] = useState<string | null>(null)
  const [cc, setCc] = useState(false)
  const [now, setNow] = useState(() => new Date())
  const rootRef = useRef<HTMLElement>(null)

  useEffect(() => {
    const t = window.setInterval(() => setNow(new Date()), 10_000)
    return () => window.clearInterval(t)
  }, [])

  useEffect(() => {
    if (!openId && !cc) return
    const close = () => {
      setOpenId(null)
      setCc(false)
    }
    const onDown = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) close()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close()
    }
    window.addEventListener('mousedown', onDown)
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [openId, cc])

  const menus: Menu[] = [
    {
      id: 'apple',
      label: 'apple',
      rows: [
        item('关于本机', { run: () => p.onOpenApp('about') }),
        item('系统设置…', { key: '⌘,', run: () => p.onOpenApp('appearance') }),
        sep(),
        item('启动台', { run: p.onShowAll }),
        item('聚焦搜索', { key: '⌘空格', run: p.onSpotlight }),
        sep(),
        // 这三条是 macOS 的形状，本工程不控制电源，所以显式禁用而不是装了不响应。
        item('睡眠', { disabled: true }),
        item('重新启动…', { disabled: true }),
        item('关机…', { disabled: true }),
        sep(),
        item('退出仿 macOS 桌面', { key: '⌘Q', run: p.onQuit })
      ]
    },
    {
      id: 'app',
      label: p.appName,
      strong: true,
      rows: [
        item(`关于 ${p.appName}`, { run: () => p.onOpenApp('about') }),
        item('设置…', { key: '⌘,', run: () => p.onOpenApp('appearance') }),
        sep(),
        item(`隐藏 ${p.appName}`, { run: p.onMinimizeFront }),
        item('关闭窗口', { key: '⌘W', run: p.onCloseFront }),
        sep(),
        item(`退出 ${p.appName}`, { key: '⌘Q', run: p.onQuit })
      ]
    },
    {
      id: 'file',
      label: '文件',
      rows: [
        item('新建备忘录', { key: '⌘N', run: p.onNewNote }),
        item('新建访达窗口', { run: () => p.onOpenApp('finder') }),
        sep(),
        item('关闭窗口', { key: '⌘W', run: p.onCloseFront })
      ]
    },
    {
      id: 'view',
      label: '显示',
      rows: [
        item('跟随系统明暗', { run: () => p.onScheme('system') }),
        item('总是浅色', { run: () => p.onScheme('light') }),
        item('总是深色', { run: () => p.onScheme('dark') }),
        sep(),
        item(`当前生效：${p.mode === 'light' ? '浅色' : '深色'} · 变体 ${p.settings.variant}`, { disabled: true })
      ]
    },
    {
      id: 'window',
      label: '窗口',
      rows: [item('最小化', { run: p.onMinimizeFront }), item('显示所有窗口', { run: p.onShowAll })]
    },
    {
      id: 'help',
      label: '帮助',
      rows: [item('外观与色板说明', { run: () => p.onOpenApp('appearance') })]
    }
  ]

  const pick = (row: Extract<Row, { kind: 'item' }>) => {
    setOpenId(null)
    row.run?.()
  }

  return (
    <header className="menubar" ref={rootRef}>
      {menus.map(menu => {
        const isOpen = openId === menu.id
        return (
          <div key={menu.id} className="mb-slot" onMouseEnter={() => openId && setOpenId(menu.id)}>
            <button
              type="button"
              className={`mb-item ${menu.id === 'apple' ? 'mb-apple' : ''} ${menu.strong ? 'mb-app-name' : ''} ${isOpen ? 'is-open' : ''}`}
              aria-expanded={isOpen}
              onClick={() => setOpenId(isOpen ? null : menu.id)}
            >
              {menu.id === 'apple' ? <Glyph name="apple" size={15} /> : menu.label}
            </button>
            <AnimatePresence>
              {isOpen && (
                <motion.div
                  className="menu-pop"
                  initial={{ opacity: 0, y: -6, scale: 0.98 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: -6, scale: 0.98 }}
                  transition={{ duration: 0.14, ease: 'easeOut' }}
                >
                  {menu.rows.map((row, i) =>
                    row.kind === 'sep' ? (
                      <div className="menu-sep" key={`sep-${i}`} />
                    ) : (
                      <button
                        type="button"
                        className="menu-row"
                        key={`${menu.id}-${i}`}
                        disabled={row.disabled}
                        onClick={() => pick(row)}
                      >
                        <span>{row.text}</span>
                        {row.key ? <span className="mr-key">{row.key}</span> : null}
                      </button>
                    )
                  )}
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )
      })}

      <div className="mb-spacer" />

      <div className="mb-right">
        <button type="button" className="mb-glyph menubar-search" title="聚焦搜索" onClick={p.onSpotlight}>
          <Glyph name="search" size={14} />
        </button>

        <div className="mb-slot">
          <button
            type="button"
            className={`mb-glyph ${cc ? 'is-open' : ''}`}
            title="控制中心"
            aria-expanded={cc}
            onClick={() => {
              setOpenId(null)
              setCc(v => !v)
            }}
          >
            <Glyph name="control" size={15} />
          </button>
          <AnimatePresence>
            {cc && (
              <motion.div
                className="menu-pop cc-pop"
                initial={{ opacity: 0, y: -6, scale: 0.97 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: -6, scale: 0.97 }}
                transition={{ duration: 0.15, ease: 'easeOut' }}
              >
                <div className="tp-title">明暗来源</div>
                <div className="seg" role="group" aria-label="明暗来源">
                  {SCHEMES.map(s => (
                    <button
                      type="button"
                      key={s.value}
                      className={`seg-btn ${p.settings.scheme === s.value ? 'is-on' : ''}`}
                      onClick={() => p.onScheme(s.value)}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>

                <div className="tp-title mt">变体</div>
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

                <div className="tp-title mt">壁纸</div>
                <div className="tp-chips">
                  {WALLPAPERS.map(w => (
                    <button
                      type="button"
                      key={w.id}
                      className={`tp-chip ${p.settings.wallpaper === w.id ? 'is-on' : ''}`}
                      onClick={() => p.onWallpaper(w.id)}
                    >
                      {w.label}
                    </button>
                  ))}
                </div>

                <button
                  type="button"
                  className="text-btn is-primary mt"
                  onClick={() => {
                    setCc(false)
                    p.onOpenApp('appearance')
                  }}
                >
                  打开外观设置
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        <span className="mb-glyph" title="Wi-Fi：示意图标，本工程不联网">
          <Glyph name="wifi" size={15} />
        </span>
        <span className="mb-glyph" title="电池：示意图标，沙箱里读不到电量">
          <Glyph name="battery" size={16} />
        </span>
        <button type="button" className="mb-clock" title={now.toLocaleString('zh-CN')} onClick={() => setCc(v => !v)}>
          {clockText(now)}
        </button>
      </div>
    </header>
  )
}
