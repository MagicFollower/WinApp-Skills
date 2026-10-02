import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import Dock from './components/reactbits/Dock'
import AnimatedList, { type ListItemData } from './components/reactbits/AnimatedList'
import MarkdownView from './components/MarkdownView'
import ThemeGlyph from './components/ThemeGlyph'
import { BUILTIN_DOCS, THEMES, docId } from './data/manual'

type Mode = 'preview' | 'edit'
type Status = { kind: 'idle' | 'ok' | 'error'; msg: string }

const initialSlugMap = (): Record<string, string> =>
  Object.fromEntries(THEMES.map(theme => [theme.id, theme.commands[0].slug]))

function formatTime(ms: number): string {
  const d = new Date(ms)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export default function App() {
  const [themeId, setThemeId] = useState<string>(THEMES[0].id)
  const [slugOf, setSlugOf] = useState<Record<string, string>>(initialSlugMap)
  const [mode, setMode] = useState<Mode>('preview')
  const [text, setText] = useState('')
  const [committed, setCommitted] = useState('')
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const [hasOverride, setHasOverride] = useState(false)
  const [status, setStatus] = useState<Status>({ kind: 'idle', msg: '' })
  const [editsDir, setEditsDir] = useState('')
  const loadSeq = useRef(0)
  const draftRef = useRef<Record<string, string>>({})

  const theme = useMemo(() => THEMES.find(t => t.id === themeId) ?? THEMES[0], [themeId])
  const command = useMemo(
    () => theme.commands.find(c => c.slug === slugOf[theme.id]) ?? theme.commands[0],
    [theme, slugOf]
  )
  const docKey = docId(theme.id, command.slug)
  const activeIndex = theme.commands.findIndex(c => c.slug === command.slug)
  const dirty = text !== committed

  useEffect(() => {
    draftRef.current = drafts
  }, [drafts])

  useEffect(() => {
    const seq = ++loadSeq.current
    const base = BUILTIN_DOCS[docKey] ?? ''
    const draft = draftRef.current[docKey]
    setCommitted(base)
    setText(draft ?? base)
    setHasOverride(false)
    setSavedAt(null)
    setStatus({ kind: 'idle', msg: base ? '' : `内置文档缺失：src/content/${docKey}.md` })

    void window.api
      .readDoc(docKey)
      .then(res => {
        if (seq !== loadSeq.current || res.text === null) return
        setCommitted(res.text)
        setHasOverride(true)
        setSavedAt(res.savedAt)
        // A live draft wins over the stored file, otherwise switching docs would drop it.
        if (draftRef.current[docKey] === undefined) setText(res.text)
      })
      .catch(err => {
        if (seq === loadSeq.current) setStatus({ kind: 'error', msg: String(err) })
      })
  }, [docKey])

  useEffect(() => {
    void window.api.appInfo().then(info => setEditsDir(info.editsDir))
  }, [])

  const edit = useCallback(
    (value: string) => {
      setText(value)
      setDrafts(prev => ({ ...prev, [docKey]: value }))
    },
    [docKey]
  )

  const save = useCallback(async () => {
    if (text === committed) return
    try {
      const res = await window.api.saveDoc(docKey, text)
      setCommitted(text)
      setHasOverride(true)
      setSavedAt(res.savedAt)
      setStatus({ kind: 'ok', msg: '已保存到你的用户目录' })
    } catch (err) {
      setStatus({ kind: 'error', msg: String(err) })
    }
  }, [committed, docKey, text])

  const revert = useCallback(async () => {
    try {
      await window.api.revertDoc(docKey)
      const base = BUILTIN_DOCS[docKey] ?? ''
      setCommitted(base)
      setText(base)
      setHasOverride(false)
      setSavedAt(null)
      setMode('preview')
      setDrafts(prev => {
        const next = { ...prev }
        delete next[docKey]
        return next
      })
      setStatus({ kind: 'ok', msg: '已还原为内置内容' })
    } catch (err) {
      setStatus({ kind: 'error', msg: String(err) })
    }
  }, [docKey])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        void save()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [save])

  const dockItems = useMemo(
    () =>
      THEMES.map(t => ({
        icon: <ThemeGlyph icon={t.icon} />,
        label: t.label,
        onClick: () => {
          setThemeId(t.id)
          setMode('preview')
        },
        className: t.id === theme.id ? 'is-active' : ''
      })),
    [theme.id]
  )

  const listItems = useMemo<ListItemData[]>(
    () => theme.commands.map(c => ({ id: docId(theme.id, c.slug), title: c.name, subtitle: c.summary })),
    [theme]
  )

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar-left">
          <span className="topbar-mark">
            <ThemeGlyph icon={theme.icon} size={17} />
          </span>
          <span className="topbar-name">{theme.label}</span>
          <span className="topbar-blurb">
            {theme.blurb} · {theme.commands.length} 条
          </span>
        </div>
        <div className="topbar-source" title={editsDir ? `用户改动存放于 ${editsDir}` : undefined}>
          {hasOverride ? '用户版本' : '内置内容'}
        </div>
      </header>

      <main className="workspace">
        <aside className="sidebar">
          <AnimatedList
            items={listItems}
            activeIndex={activeIndex}
            className="cmd-list"
            onItemSelect={(_item, index) =>
              setSlugOf(prev => ({ ...prev, [theme.id]: theme.commands[index].slug }))
            }
          />
        </aside>

        <section className="detail">
          <div className="detail-bar">
            <h2 className="detail-title">{command.name}</h2>
            <span className="detail-summary" title={command.summary}>{command.summary}</span>
            <div className="detail-actions">
              <div className="seg">
                <button
                  type="button"
                  className={mode === 'preview' ? 'seg-btn is-on' : 'seg-btn'}
                  onClick={() => setMode('preview')}
                >
                  预览
                </button>
                <button
                  type="button"
                  className={mode === 'edit' ? 'seg-btn is-on' : 'seg-btn'}
                  onClick={() => setMode('edit')}
                >
                  编辑
                </button>
              </div>
              <button type="button" className="btn" disabled={!dirty} onClick={() => void save()}>
                保存
              </button>
              <button type="button" className="btn btn-ghost" disabled={!hasOverride} onClick={() => void revert()}>
                还原内置
              </button>
            </div>
          </div>

          <div className={mode === 'edit' ? 'detail-body is-edit' : 'detail-body'}>
            {mode === 'preview' ? (
              <MarkdownView source={text} />
            ) : (
              <textarea
                className="md-editor"
                value={text}
                onChange={e => edit(e.target.value)}
                spellCheck={false}
              />
            )}
          </div>

          <div className={`statusbar statusbar-${status.kind}`}>
            <span className="statusbar-msg">{status.msg}</span>
            <span className="statusbar-hint">
              {savedAt !== null ? `上次保存 ${formatTime(savedAt)}` : ''}
              {dirty ? ' · 未保存（Ctrl+S）' : ''}
            </span>
          </div>
        </section>
      </main>

      <footer className="dock-bar">
        <Dock items={dockItems} panelHeight={70} magnification={80} dockHeight={124} />
      </footer>
    </div>
  )
}
