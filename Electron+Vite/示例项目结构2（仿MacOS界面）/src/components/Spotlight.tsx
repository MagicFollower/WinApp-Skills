import { useEffect, useMemo, useRef, useState } from 'react'
import { motion } from 'motion/react'
import { AppIcon, Glyph, type GlyphName, type Tone } from './icons/AppGlyph'

export interface SpotResult {
  kind: string
  id: string
  title: string
  sub: string
  tone: Tone
  glyph: GlyphName
  run: () => void
}

interface Props {
  search: (query: string) => SpotResult[]
  onClose: () => void
}

const MAX_ROWS = 9

export default function Spotlight({ search, onClose }: Props) {
  const [query, setQuery] = useState('')
  const [index, setIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  const results = useMemo(() => search(query).slice(0, MAX_ROWS), [search, query])

  useEffect(() => {
    setIndex(0)
  }, [query])

  const runAt = (i: number) => {
    const hit = results[i]
    if (!hit) return
    hit.run()
    onClose()
  }

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setIndex(i => Math.min(i + 1, results.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setIndex(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      runAt(index)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      onClose()
    }
  }

  return (
    <>
      <motion.div
        className="spotlight-scrim"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.16 }}
        onClick={onClose}
      />
      <motion.div
        className="spotlight"
        role="dialog"
        aria-label="聚焦搜索"
        initial={{ opacity: 0, y: -18, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={{ opacity: 0, y: -14, scale: 0.97 }}
        transition={{ type: 'spring', stiffness: 340, damping: 26 }}
      >
        <div className="spotlight-search">
          <Glyph name="search" size={19} />
          <input
            ref={inputRef}
            className="spotlight-input"
            placeholder="搜索应用、色板、壁纸、笔记与文件夹"
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={onKeyDown}
            spellCheck={false}
          />
          <kbd>esc</kbd>
        </div>
        {results.length ? (
          <div className="spotlight-results">
            {results.map((r, i) => (
              <button
                type="button"
                key={`${r.kind}-${r.id}`}
                className={`spotlight-row ${i === index ? 'is-on' : ''}`}
                onMouseEnter={() => setIndex(i)}
                onClick={() => runAt(i)}
              >
                <AppIcon tone={r.tone} glyph={r.glyph} size={26} glow={false} />
                <span className="sr-main">
                  <span className="sr-title">{r.title}</span>
                  <span className="sr-sub">{r.sub}</span>
                </span>
                <span className="pill">{r.kind}</span>
              </button>
            ))}
          </div>
        ) : (
          <div className="spotlight-empty">没有匹配项 — 试试「备忘」「synthwave」「壁纸」或某个文件夹名</div>
        )}
      </motion.div>
    </>
  )
}
