import { useCallback, useEffect, useRef, useState } from 'react'
import AnimatedList from '../components/reactbits/AnimatedList'
import type { Note } from '../types/electron-api'

interface Props {
  /** 菜单栏/右键菜单点「新建备忘录」时 +1，Notes 收到就建一条并选中。 */
  createNonce: number
}

type Status = { kind: 'idle' | 'ok' | 'err'; msg: string }

function stamp(ms: number): string {
  if (!ms) return ''
  const d = new Date(ms)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getMonth() + 1}/${d.getDate()} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function summary(text: string): string {
  const lines = text.split(/\r?\n/).filter(l => l.trim() !== '')
  const rest = (lines[1] ?? lines[0] ?? '').replace(/^#+\s*/, '')
  return rest.slice(0, 46)
}

const BLANK = '# 备忘录\n\n正文写在这里，改完会自动存到 userData/notes。\n'

export default function Notes({ createNonce }: Props) {
  const [notes, setNotes] = useState<Note[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [text, setText] = useState(BLANK)
  const [status, setStatus] = useState<Status>({ kind: 'idle', msg: '加载中' })
  const [dirty, setDirty] = useState(false)
  const seq = useRef(0)
  const saveTimer = useRef<number | null>(null)

  const reload = useCallback(async (keep?: string) => {
    const my = ++seq.current
    try {
      const list = await window.api.notesList()
      if (my !== seq.current) return null
      setNotes(list)
      const next = keep && list.some(n => n.id === keep) ? keep : (list[0]?.id ?? null)
      setSelected(prev => (prev && list.some(n => n.id === prev) ? prev : next))
      setStatus(prev => (prev.kind === 'err' ? prev : { kind: 'idle', msg: list.length ? '' : '还没有备忘录' }))
      return list
    } catch (err) {
      if (my === seq.current) setStatus({ kind: 'err', msg: String(err) })
      return null
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  // 选中项变化时把正文装进编辑器；dirty 归零，否则上一条的未保存标记会跟到新条目上。
  useEffect(() => {
    const hit = notes.find(n => n.id === selected)
    setText(hit?.text ?? BLANK)
    setDirty(false)
  }, [selected, notes])

  const create = useCallback(async () => {
    const id = `n${Date.now().toString(36)}`
    try {
      const saved = await window.api.notesSave(id, BLANK)
      setStatus({ kind: 'ok', msg: '已新建' })
      await reload(id)
      setSelected(saved.id)
    } catch (err) {
      setStatus({ kind: 'err', msg: String(err) })
    }
  }, [reload])

  useEffect(() => {
    if (createNonce > 0) void create()
  }, [createNonce, create])

  const commit = useCallback(async () => {
    if (!selected || !dirty) return
    try {
      const saved = await window.api.notesSave(selected, text)
      setNotes(prev => prev.map(n => (n.id === saved.id ? saved : n)))
      setStatus({ kind: 'ok', msg: '已保存' })
      setDirty(false)
    } catch (err) {
      setStatus({ kind: 'err', msg: String(err) })
    }
  }, [dirty, selected, text])

  // 停止输入 700ms 后落盘：备忘录这类内容不该靠用户记得按保存。
  useEffect(() => {
    if (!dirty) return
    if (saveTimer.current) window.clearTimeout(saveTimer.current)
    saveTimer.current = window.setTimeout(() => void commit(), 700)
    return () => {
      if (saveTimer.current) window.clearTimeout(saveTimer.current)
    }
  }, [dirty, commit])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        void commit()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [commit])

  const remove = useCallback(async () => {
    if (!selected) return
    try {
      await window.api.notesRemove(selected)
      setStatus({ kind: 'ok', msg: '已删除' })
      setSelected(null)
      await reload()
    } catch (err) {
      setStatus({ kind: 'err', msg: String(err) })
    }
  }, [reload, selected])

  const active = notes.find(n => n.id === selected) ?? null

  return (
    <div className="app-pane">
      <div className="app-toolbar">
        <button type="button" className="text-btn is-primary" onClick={() => void create()}>
          新建
        </button>
        <button type="button" className="text-btn" disabled={!selected} onClick={() => void commit()}>
          保存
        </button>
        <button type="button" className="text-btn" disabled={!selected} onClick={() => void remove()}>
          删除
        </button>
        <span className="tb-spacer" />
        <span className="pill">{notes.length} 条</span>
      </div>

      <div className="notes">
        <div className="notes-list">
          <AnimatedList
            items={notes.map(n => ({ id: n.id, title: n.title, subtitle: `${summary(n.text)} · ${stamp(n.updatedAt)}` }))}
            activeIndex={notes.findIndex(n => n.id === selected)}
            itemClassName="note-item"
            displayScrollbar
            onItemSelect={item => setSelected(item.id)}
          />
        </div>

        <div className="notes-editor">
          <textarea
            className="notes-area"
            value={active ? text : ''}
            onChange={e => {
              setText(e.target.value)
              setDirty(true)
              setStatus({ kind: 'idle', msg: '编辑中' })
            }}
            spellCheck={false}
            placeholder={notes.length ? '从左侧选一条备忘录' : '还没有备忘录，点左上「新建」开始写'}
            disabled={!active}
          />
          <div className="notes-status">
            <span className={status.kind === 'err' ? 'st-err' : status.kind === 'ok' ? 'st-ok' : ''}>{status.msg}</span>
            <span style={{ marginLeft: 'auto' }}>
              {active ? `${active.title} · ${stamp(active.updatedAt)}` : '未选择备忘录'}
              {dirty ? ' · 未保存' : ''}
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}
