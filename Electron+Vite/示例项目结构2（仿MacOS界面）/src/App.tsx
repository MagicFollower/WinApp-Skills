import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { AnimatePresence } from 'motion/react'
import MenuBar from './components/MenuBar'
import DockBar from './components/DockBar'
import DesktopWindow, { type WinState } from './components/DesktopWindow'
import Spotlight, { type SpotResult } from './components/Spotlight'
import Launchpad, { type LaunchItem } from './components/Launchpad'
import ContextMenu, { type CtxRow } from './components/ContextMenu'
import ThemePanel from './components/ThemePanel'
import AboutBox from './components/AboutBox'
import { AppIcon, Glyph } from './components/icons/AppGlyph'
import Finder from './apps/Finder'
import Notes from './apps/Notes'
import Calculator from './apps/Calculator'
import { APPS, appSpec, type AppId } from './data/apps'
import { WALLPAPERS } from './data/wallpapers'
import { PALETTES, VARIANT_MODE, resolveThemeId, type Mode } from './lib/theme'
import type { AppInfoResult, HomeResult, Note, Scheme, Settings, ThemeChangedPayload, Variant } from './types/electron-api'

const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(v, Math.max(lo, hi)))

export default function App() {
  const [settings, setSettings] = useState<Settings | null>(null)
  const [dark, setDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches)
  const [applied, setApplied] = useState<{ id: string; mode: Mode; bg: string }>({ id: '', mode: 'dark', bg: '' })
  const [windows, setWindows] = useState<WinState[]>([])
  const [frontId, setFrontId] = useState<string | null>(null)
  const [area, setArea] = useState({ w: 1280, h: 720 })
  const [spotOpen, setSpotOpen] = useState(false)
  const [padOpen, setPadOpen] = useState(false)
  const [ctx, setCtx] = useState<{ x: number; y: number } | null>(null)
  const [bouncing, setBouncing] = useState<string | null>(null)
  const [info, setInfo] = useState<AppInfoResult | null>(null)
  const [home, setHome] = useState<HomeResult | null>(null)
  const [notes, setNotes] = useState<Note[]>([])
  const [noteNonce, setNoteNonce] = useState(0)
  const [finderSeed, setFinderSeed] = useState<{ path: string; nonce: number } | null>(null)

  const areaRef = useRef(area)
  const windowsRef = useRef<WinState[]>([])
  const zRef = useRef(10)
  const deskRef = useRef<HTMLDivElement>(null)
  const bounceTimer = useRef<number | null>(null)

  useEffect(() => {
    areaRef.current = area
  }, [area])

  useEffect(() => {
    windowsRef.current = windows
  }, [windows])

  // ---------- 启动：读设置与运行时信息 ----------
  useEffect(() => {
    void window.api.getSettings().then(res => setSettings(res.settings))
    void window.api.appInfo().then(setInfo)
    void window.api.home().then(setHome)
  }, [])

  // ---------- 工作区尺寸：窗口拖拽边界与最大化都按它算 ----------
  useEffect(() => {
    const el = deskRef.current
    if (!el) return
    const ro = new ResizeObserver(() => setArea({ w: el.clientWidth, h: el.clientHeight }))
    ro.observe(el)
    setArea({ w: el.clientWidth, h: el.clientHeight })
    return () => ro.disconnect()
  }, [settings])

  // ---------- 明暗感知：只读 CSS 媒体查询，不碰 Electron API ----------
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const onChange = () => setDark(mq.matches)
    mq.addEventListener('change', onChange)
    const sub = window.api.onThemeChanged(payload => {
      const next = payload as ThemeChangedPayload
      setSettings(next.settings)
      setDark(window.matchMedia('(prefers-color-scheme: dark)').matches)
    })
    return () => {
      mq.removeEventListener('change', onChange)
      window.api.offThemeChanged(sub)
    }
  }, [])

  // ---------- 把色板写进 DOM，并把底色回写给主进程（下次启动的 backgroundColor） ----------
  useEffect(() => {
    if (!settings) return
    const mode: Mode = settings.variant === 'auto' ? (dark ? 'dark' : 'light') : VARIANT_MODE[settings.variant]
    const resolved = resolveThemeId(settings.palette, settings.variant, mode)
    document.documentElement.dataset.theme = resolved.id
    document.documentElement.dataset.mode = resolved.mode
    const bg = getComputedStyle(document.documentElement).getPropertyValue('--pf-bg').trim()
    setApplied({ id: resolved.id, mode: resolved.mode, bg })
    if (/^#[0-9a-fA-F]{6}$/.test(bg) && bg.toLowerCase() !== settings.bg.toLowerCase()) {
      void window.api.setSettings({ bg }).then(res => setSettings(res.settings))
    }
  }, [settings, dark])

  const patch = useCallback((next: Partial<Settings>) => {
    void window.api.setSettings(next).then(res => setSettings(res.settings))
  }, [])

  // ---------- 窗口管理 ----------
  const focus = useCallback((id: string) => {
    zRef.current += 1
    setFrontId(id)
    setWindows(prev => prev.map(w => (w.id === id ? { ...w, z: zRef.current } : w)))
  }, [])

  const bounce = useCallback((id: string) => {
    setBouncing(id)
    if (bounceTimer.current) window.clearTimeout(bounceTimer.current)
    bounceTimer.current = window.setTimeout(() => setBouncing(null), 640)
  }, [])

  const openApp = useCallback(
    (id: AppId) => {
      const box = areaRef.current
      const spec = appSpec(id)
      const cur = windowsRef.current
      const existing = cur.find(w => w.id === id)
      zRef.current += 1
      const z = zRef.current

      if (existing) {
        setWindows(prev => prev.map(w => (w.id === id ? { ...w, minimized: false, origin: 'restore', z } : w)))
      } else {
        const w = Math.min(spec.w, Math.max(spec.minW, box.w - 48))
        const h = Math.min(spec.h, Math.max(spec.minH, box.h - 48))
        const step = cur.length
        setWindows(prev => [
          ...prev,
          {
            id,
            w,
            h,
            x: clamp(Math.round((box.w - w) / 2) + step * 28 - 28, 8, Math.max(8, box.w - w - 8)),
            y: clamp(Math.round((box.h - h) / 2) + step * 22 - 60, 8, Math.max(8, box.h - h - 8)),
            z,
            minimized: false,
            maximized: false,
            origin: 'open'
          }
        ])
      }
      setFrontId(id)
      bounce(id)
    },
    [bounce]
  )

  const closeApp = useCallback((id: string) => {
    const rest = windowsRef.current.filter(w => w.id !== id)
    setWindows(prev => prev.filter(w => w.id !== id))
    setFrontId(current => {
      if (current !== id) return current
      if (!rest.length) return null
      return rest.reduce((top, w) => (w.z > top.z ? w : top)).id
    })
  }, [])

  const minimizeApp = useCallback((id: string) => {
    setWindows(prev => prev.map(w => (w.id === id ? { ...w, minimized: true } : w)))
    setFrontId(current => (current === id ? null : current))
  }, [])

  const toggleMax = useCallback((id: string) => {
    const box = areaRef.current
    setWindows(prev =>
      prev.map(w => {
        if (w.id !== id) return w
        if (w.maximized) {
          const r = w.restore ?? { x: 60, y: 60, w: appSpec(w.id as AppId).w, h: appSpec(w.id as AppId).h }
          return { ...w, maximized: false, x: r.x, y: r.y, w: r.w, h: r.h }
        }
        return { ...w, maximized: true, restore: { x: w.x, y: w.y, w: w.w, h: w.h }, x: 0, y: 0, w: box.w, h: box.h }
      })
    )
  }, [])

  const moveWin = useCallback((id: string, dx: number, dy: number) => {
    const box = areaRef.current
    setWindows(prev =>
      prev.map(w => {
        if (w.id !== id || w.maximized) return w
        return {
          ...w,
          x: clamp(w.x + dx, -w.w + 90, box.w - 90),
          y: clamp(w.y + dy, 0, Math.max(0, box.h - 34))
        }
      })
    )
  }, [])

  const resizeWin = useCallback((id: string, dw: number, dh: number) => {
    const box = areaRef.current
    setWindows(prev =>
      prev.map(w => {
        if (w.id !== id || w.maximized) return w
        const spec = appSpec(id as AppId)
        return {
          ...w,
          w: clamp(w.w + dw, spec.minW, box.w - w.x),
          h: clamp(w.h + dh, spec.minH, box.h - w.y)
        }
      })
    )
  }, [])

  // 工作区变小时，把超出边界的窗口拉回来；最大化中的直接跟随新尺寸。
  useEffect(() => {
    setWindows(prev =>
      prev.map(w => {
        if (w.maximized) return { ...w, x: 0, y: 0, w: area.w, h: area.h }
        const spec = appSpec(w.id as AppId)
        const w2 = clamp(w.w, spec.minW, Math.max(spec.minW, area.w - 16))
        const h2 = clamp(w.h, spec.minH, Math.max(spec.minH, area.h - 16))
        return { ...w, w: w2, h: h2, x: clamp(w.x, -w2 + 90, Math.max(0, area.w - 90)), y: clamp(w.y, 0, Math.max(0, area.h - 34)) }
      })
    )
  }, [area])

  const frontApp: AppId = (frontId ?? 'finder') as AppId
  // Dock 的运行指示点覆盖"已打开"，最小化的也算开着（macOS 就是这个口径）。
  const runningApps = windows.map(w => w.id)

  // ---------- Spotlight ----------
  const refreshNotes = useCallback(() => {
    void window.api.notesList().then(setNotes).catch(() => setNotes([]))
  }, [])

  useEffect(() => {
    if (spotOpen || padOpen) refreshNotes()
  }, [padOpen, refreshNotes, spotOpen])

  const search = useCallback(
    (query: string): SpotResult[] => {
      const q = query.trim().toLowerCase()
      const hit = (s: string) => s.toLowerCase().includes(q)
      const out: SpotResult[] = []

      for (const a of APPS) {
        if (!q || a.name.toLowerCase().includes(q) || a.id.includes(q) || a.keywords.some(k => k.toLowerCase().includes(q))) {
          out.push({ kind: '应用', id: a.id, title: a.name, sub: a.blurb, tone: a.tone, glyph: a.glyph, run: () => openApp(a.id) })
        }
      }
      for (const pal of PALETTES) {
        if (!q || hit(pal.theme) || hit(pal.label)) {
          out.push({
            kind: '色板',
            id: pal.theme,
            title: pal.label,
            sub: `切到 ${pal.theme}，明暗沿用当前变体`,
            tone: 'accent',
            glyph: 'appearance',
            run: () => patch({ palette: pal.theme })
          })
        }
      }
      for (const wp of WALLPAPERS) {
        if (!q || hit(wp.id) || hit(wp.label)) {
          out.push({
            kind: '壁纸',
            id: wp.id,
            title: wp.label,
            sub: wp.note,
            tone: 'deep',
            glyph: 'grid',
            run: () => patch({ wallpaper: wp.id })
          })
        }
      }
      for (const qa of home?.quickAccess ?? []) {
        if (!q || hit(qa.label) || hit(qa.path)) {
          out.push({
            kind: '文件夹',
            id: qa.path,
            title: qa.label,
            sub: qa.path,
            tone: 'info',
            glyph: 'folder',
            run: () => {
              setFinderSeed({ path: qa.path, nonce: Date.now() })
              openApp('finder')
            }
          })
        }
      }
      for (const note of notes) {
        if (!q || hit(note.title) || hit(note.text)) {
          out.push({
            kind: '备忘录',
            id: note.id,
            title: note.title,
            sub: note.text.replace(/^#+\s*/, '').split('\n').slice(1).join(' ').slice(0, 40) || '（空）',
            tone: 'warn',
            glyph: 'notes',
            run: () => openApp('notes')
          })
        }
      }
      if (!q) return out
      return out.sort((a, b) => a.title.toLowerCase().indexOf(q) - b.title.toLowerCase().indexOf(q))
    },
    [home, notes, openApp, patch]
  )

  // ---------- 快捷键 ----------
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.code === 'Space') {
        e.preventDefault()
        setSpotOpen(v => !v)
        return
      }
      if (e.key === 'F4') {
        e.preventDefault()
        setPadOpen(v => !v)
        return
      }
      if (e.key === 'Escape') {
        setSpotOpen(false)
        setPadOpen(false)
        setCtx(null)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  // ---------- 启动台条目 ----------
  const launchItems: LaunchItem[] = useMemo(
    () =>
      APPS.map(a => ({
        id: a.id,
        name: a.name,
        tone: a.tone,
        glyph: a.glyph,
        blurb: a.blurb,
        run: () => openApp(a.id)
      })),
    [openApp]
  )

  const cyclePalette = () => {
    if (!settings) return
    const i = PALETTES.findIndex(p => p.theme === settings.palette)
    const next = PALETTES[(i + 1) % PALETTES.length]
    patch({ palette: next.theme })
  }

  const cycleWallpaper = () => {
    if (!settings) return
    const i = WALLPAPERS.findIndex(w => w.id === settings.wallpaper)
    patch({ wallpaper: WALLPAPERS[(i + 1) % WALLPAPERS.length].id })
  }

  const ctxRows: CtxRow[] = [
    { text: '新建备忘录', run: () => { openApp('notes'); setNoteNonce(n => n + 1) } },
    { text: '打开访达', run: () => openApp('finder') },
    { text: '', sep: true },
    { text: '下一套色板', run: cyclePalette },
    { text: '下一张壁纸', run: cycleWallpaper },
    { text: '外观设置…', run: () => openApp('appearance') },
    { text: '', sep: true },
    { text: '显示所有窗口', run: () => setPadOpen(true) },
    { text: '关于本机', run: () => openApp('about') }
  ]

  const content = (id: AppId) => {
    if (!settings) return null
    switch (id) {
      case 'finder':
        return <Finder seed={finderSeed} />
      case 'notes':
        return <Notes createNonce={noteNonce} />
      case 'calculator':
        return <Calculator />
      case 'appearance':
        return (
          <ThemePanel
            settings={settings}
            mode={applied.mode}
            appliedId={applied.id}
            appliedBg={applied.bg}
            onScheme={(scheme: Scheme) => patch({ scheme, variant: 'auto' })}
            onVariant={(variant: Variant) => patch({ variant })}
            onPalette={(palette: string) => patch({ palette })}
            onWallpaper={(wallpaper: string) => patch({ wallpaper })}
          />
        )
      case 'about':
        return <AboutBox info={info} appliedId={applied.id} />
    }
  }

  if (!settings) {
    return (
      <div className="desktop wall-flat">
        <div className="boot-hint">正在读取设置…</div>
      </div>
    )
  }

  return (
    <div className={`desktop wall-${settings.wallpaper}`}>
      <MenuBar
        appName={appSpec(frontApp).name}
        settings={settings}
        mode={applied.mode}
        onOpenApp={openApp}
        onNewNote={() => {
          openApp('notes')
          setNoteNonce(n => n + 1)
        }}
        onCloseFront={() => frontId && closeApp(frontId)}
        onMinimizeFront={() => frontId && minimizeApp(frontId)}
        onShowAll={() => setPadOpen(true)}
        onSpotlight={() => setSpotOpen(true)}
        onScheme={(scheme: Scheme) => patch({ scheme, variant: 'auto' })}
        onVariant={(variant: Variant) => patch({ variant })}
        onWallpaper={(wallpaper: string) => patch({ wallpaper })}
        onQuit={() => void window.api.quit()}
      />

      <div
        className="desk-area"
        ref={deskRef}
        onContextMenu={e => {
          if ((e.target as HTMLElement).closest('.app-window')) return
          e.preventDefault()
          setCtx({ x: e.clientX, y: e.clientY - 28 })
        }}
      >
        <div className="desk-icons">
          <button
            type="button"
            className="desk-icon"
            onClick={() => {
              setFinderSeed({ path: home?.home ?? '', nonce: Date.now() })
              openApp('finder')
            }}
          >
            <AppIcon tone="accent" glyph="drive" size={46} />
            <span className="di-label">Macintosh HD</span>
          </button>
          {(home?.quickAccess ?? [])
            .filter(q => q.label === '桌面')
            .map(q => (
              <button
                type="button"
                key={q.path}
                className="desk-icon"
                onClick={() => {
                  setFinderSeed({ path: q.path, nonce: Date.now() })
                  openApp('finder')
                }}
              >
                <AppIcon tone="info" glyph="folder" size={46} />
                <span className="di-label">{q.label}</span>
              </button>
            ))}
        </div>

        <AnimatePresence>
          {windows
            .filter(w => !w.minimized)
            .map(w => {
              const spec = appSpec(w.id as AppId)
              return (
                <DesktopWindow
                  key={w.id}
                  win={w}
                  areaH={area.h}
                  minW={spec.minW}
                  minH={spec.minH}
                  front={frontId === w.id}
                  title={spec.name}
                  onClose={() => closeApp(w.id)}
                  onMinimize={() => minimizeApp(w.id)}
                  onToggleMax={() => toggleMax(w.id)}
                  onFocus={() => focus(w.id)}
                  onMove={(dx, dy) => moveWin(w.id, dx, dy)}
                  onResize={(dw, dh) => resizeWin(w.id, dw, dh)}
                >
                  {content(w.id as AppId)}
                </DesktopWindow>
              )
            })}
        </AnimatePresence>
      </div>

      <DockBar running={runningApps} bouncing={bouncing} onLaunch={openApp} onLaunchpad={() => setPadOpen(true)} />

      <AnimatePresence>
        {spotOpen && <Spotlight search={search} onClose={() => setSpotOpen(false)} />}
      </AnimatePresence>

      <AnimatePresence>{padOpen && <Launchpad items={launchItems} onClose={() => setPadOpen(false)} />}</AnimatePresence>

      {ctx && <ContextMenu x={ctx.x} y={ctx.y} rows={ctxRows} onClose={() => setCtx(null)} />}

      {/* 空桌面时给一条弱提示，免得看起来像没加载出来 */}
      {!windows.length ? (
        <div className="desk-hint">
          <Glyph name="grid" size={13} /> 点底部 Dock 打开应用 · 右键桌面看菜单 · Ctrl+空格 聚焦搜索
        </div>
      ) : null}
    </div>
  )
}
