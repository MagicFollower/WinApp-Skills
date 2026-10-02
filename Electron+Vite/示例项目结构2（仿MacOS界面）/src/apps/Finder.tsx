import { useCallback, useEffect, useState } from 'react'
import { Glyph, AppIcon } from '../components/icons/AppGlyph'
import type { DirListing, FsEntry, HomeResult, QuickAccessItem, TextPreview } from '../types/electron-api'

interface Props {
  /** 桌面图标或 Spotlight 指定的进入目录；nonce 变一次跳一次。 */
  seed: { path: string; nonce: number } | null
}

function shortPath(p: string, home: string): string {
  if (p === home) return '主目录'
  if (p.startsWith(home + '\\') || p.startsWith(home + '/')) return p.slice(home.length + 1).replace(/[\\/]/g, ' › ')
  return p
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

function formatDate(ms: number): string {
  const d = new Date(ms)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const TEXTABLE = /\.(md|txt|json|ya?ml|toml|ini|csv|log|ts|tsx|js|jsx|css|html|xml|mjs|cjs)$/i

export default function Finder({ seed }: Props) {
  const [home, setHome] = useState<HomeResult | null>(null)
  const [cwd, setCwd] = useState('')
  const [listing, setListing] = useState<DirListing | null>(null)
  const [showHidden, setShowHidden] = useState(false)
  const [selected, setSelected] = useState<string | null>(null)
  const [preview, setPreview] = useState<TextPreview | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [history, setHistory] = useState<string[]>([])

  useEffect(() => {
    void window.api
      .home()
      .then(info => {
        setHome(info)
        setCwd(prev => prev || info.quickAccess[0]?.path || info.home)
      })
      .catch(err => setError(String(err)))
  }, [])

  const go = useCallback(async (dir: string, pushHistory = true) => {
    setBusy(true)
    setError('')
    try {
      const res = await window.api.listDir(dir, showHidden)
      setListing(res)
      setCwd(dir)
      setPreview(null)
      setSelected(null)
      if (pushHistory) setHistory(h => [...h.filter(x => x !== dir), dir].slice(-12))
    } catch (err) {
      setError(String(err))
      setListing(null)
    } finally {
      setBusy(false)
    }
  }, [showHidden])

  useEffect(() => {
    if (!cwd) return
    void go(cwd, false)
    // 初始进入与 showHidden 切换都走这里；go 已经按 showHidden 重建请求
  }, [cwd, showHidden, go])

  useEffect(() => {
    if (seed && home) void go(seed.path)
  }, [seed, home, go])

  const open = (entry: FsEntry) => {
    const full = joinPath(cwd, entry.name)
    if (entry.isDir) void go(full)
    else if (TEXTABLE.test(entry.name)) {
      void window.api
        .readFile(full)
        .then(setPreview)
        .catch(err => setError(String(err)))
    } else {
      setError(`只读浏览：${entry.name} 不是文本文件，不预览`)
    }
  }

  const back = () => {
    const prev = history[history.length - 2]
    if (!prev) return
    setHistory(h => h.slice(0, -1))
    void go(prev, false)
  }

  const up = () => {
    const parent = parentOf(cwd, home?.home ?? '')
    if (parent !== cwd) void go(parent)
  }

  return (
    <div className="app-pane">
      <div className="app-toolbar">
        <button type="button" className="text-btn" onClick={back} disabled={history.length < 2} title="后退">
          <Glyph name="chevron-left" size={13} />
        </button>
        <button type="button" className="text-btn" onClick={up} title="上一级">
          <Glyph name="arrow-up" size={13} />
        </button>
        <span className="crumb" title={cwd}>
          {home ? shortPath(cwd, home.home) : cwd}
        </span>
        <span className="tb-spacer" />
        <button
          type="button"
          className={`text-btn ${showHidden ? 'is-primary' : ''}`}
          onClick={() => setShowHidden(v => !v)}
          title="显示隐藏文件"
        >
          <Glyph name="eye" size={13} />
        </button>
        <button type="button" className="text-btn" onClick={() => void go(cwd, false)} title="重新载入">
          重新载入
        </button>
        <span className="pill">{busy ? '读取中' : `${listing?.entries.length ?? 0} 项`}</span>
      </div>

      {error ? (
        <div className="app-toolbar" style={{ color: 'var(--c-err)' }}>
          {error}
        </div>
      ) : null}

      {preview ? (
        <div className="app-pane">
          <div className="app-toolbar">
            <button type="button" className="text-btn" onClick={() => setPreview(null)}>
              <Glyph name="chevron-left" size={13} /> 返回 {preview.name}
            </button>
            <span className="tb-spacer" />
            {preview.truncated ? <span className="pill">超过 512 KB，已截断</span> : null}
          </div>
          <pre className="plain-text">{preview.text}</pre>
        </div>
      ) : (
        <div className="finder">
          <aside className="finder-side">
            <div className="fs-caption">个人收藏</div>
            {(home?.quickAccess ?? []).map((qa: QuickAccessItem) => (
              <button
                type="button"
                key={qa.path}
                className={`fs-item ${qa.path === cwd ? 'is-on' : ''}`}
                onClick={() => void go(qa.path)}
              >
                <AppIcon tone="info" glyph="folder" size={16} glow={false} />
                <span className="fi-name">{qa.label}</span>
              </button>
            ))}
            <div className="fs-caption">浏览上限</div>
            <div className="fs-note">主目录之外的路径由主进程拒绝，这里列不出"电脑"整盘。</div>
          </aside>

          <div className="finder-main">
            <div className="finder-head">
              <span style={{ flex: '1 1 auto' }}>名称</span>
              <span style={{ flex: '0 0 84px', textAlign: 'right' }}>大小</span>
              <span style={{ flex: '0 0 116px', textAlign: 'right' }}>修改日期</span>
            </div>
            {!listing?.entries.length ? <div className="finder-empty">{busy ? '读取中…' : '空文件夹'}</div> : null}
            {(listing?.entries ?? []).map(entry => {
              const isSel = selected === entry.name
              return (
                <div
                  key={entry.name}
                  className={`finder-row ${isSel ? 'is-sel' : ''}`}
                  onClick={() => setSelected(entry.name)}
                  onDoubleClick={() => open(entry)}
                >
                  <span className="fr-name">
                    <Glyph name={entry.isDir ? 'folder' : 'file'} size={14} />
                    {entry.name}
                  </span>
                  <span className="fr-size">{entry.isDir ? '' : formatSize(entry.size)}</span>
                  <span className="fr-date">{formatDate(entry.mtimeMs)}</span>
                </div>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}

function joinPath(dir: string, name: string): string {
  if (dir.endsWith('\\') || dir.endsWith('/')) return dir + name
  return dir + (dir.includes('/') && !dir.includes('\\') ? '/' : '\\') + name
}

function parentOf(dir: string, home: string): string {
  if (dir === home) return home
  const trimmed = dir.replace(/[\\/]+$/, '')
  const i = Math.max(trimmed.lastIndexOf('\\'), trimmed.lastIndexOf('/'))
  if (i <= 2) return trimmed.slice(0, i + 1) || trimmed
  return trimmed.slice(0, i)
}
