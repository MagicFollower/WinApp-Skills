import { useMemo, useState } from 'react'
import { motion } from 'motion/react'
import { AppIcon, type GlyphName, type Tone } from './icons/AppGlyph'

export interface LaunchItem {
  id: string
  name: string
  tone: Tone
  glyph: GlyphName
  blurb: string
  run: () => void
}

interface Props {
  items: LaunchItem[]
  onClose: () => void
}

export default function Launchpad({ items, onClose }: Props) {
  const [query, setQuery] = useState('')

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return items
    return items.filter(
      i => i.name.toLowerCase().includes(q) || i.blurb.toLowerCase().includes(q) || i.id.includes(q)
    )
  }, [items, query])

  return (
    <motion.div
      className="launchpad"
      initial={{ opacity: 0, backdropFilter: 'blur(0px)' }}
      animate={{ opacity: 1, backdropFilter: 'blur(34px)' }}
      exit={{ opacity: 0, backdropFilter: 'blur(0px)' }}
      transition={{ duration: 0.22 }}
      onClick={onClose}
    >
      <input
        className="lp-search"
        placeholder="搜索"
        value={query}
        autoFocus
        onChange={e => setQuery(e.target.value)}
        onClick={e => e.stopPropagation()}
        onKeyDown={e => {
          if (e.key === 'Escape') onClose()
        }}
        spellCheck={false}
      />
      <div className="lp-grid" onClick={e => e.stopPropagation()}>
        {shown.map((item, i) => (
          <motion.button
            type="button"
            key={item.id}
            className="lp-tile"
            initial={{ opacity: 0, scale: 0.82, y: 14 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            transition={{ delay: 0.03 * i, type: 'spring', stiffness: 260, damping: 22 }}
            onClick={() => {
              item.run()
              onClose()
            }}
          >
            <span className="lp-icon">
              <AppIcon tone={item.tone} glyph={item.glyph} size={68} />
            </span>
            <span className="lp-name">{item.name}</span>
          </motion.button>
        ))}
        {!shown.length ? <div className="spotlight-empty">没有匹配的应用</div> : null}
      </div>
    </motion.div>
  )
}
