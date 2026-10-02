import { useEffect, useRef } from 'react'
import { motion } from 'motion/react'

export interface CtxRow {
  text: string
  disabled?: boolean
  sep?: boolean
  run?: () => void
}

interface Props {
  x: number
  y: number
  rows: CtxRow[]
  onClose: () => void
}

export default function ContextMenu({ x, y, rows, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) onClose()
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    // 延后一帧再挂，避免打开它的那次 pointerdown 立刻把它关掉。
    const t = window.setTimeout(() => window.addEventListener('mousedown', onDown), 0)
    window.addEventListener('keydown', onKey)
    return () => {
      window.clearTimeout(t)
      window.removeEventListener('mousedown', onDown)
      window.removeEventListener('keydown', onKey)
    }
  }, [onClose])

  return (
    <motion.div
      ref={ref}
      className="ctx-menu"
      style={{ left: x, top: y }}
      initial={{ opacity: 0, scale: 0.96 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.12 }}
    >
      {rows.map((row, i) =>
        row.sep ? (
          <div className="menu-sep" key={`sep-${i}`} />
        ) : (
          <button
            type="button"
            className="menu-row"
            key={`${row.text}-${i}`}
            disabled={row.disabled}
            onClick={() => {
              onClose()
              row.run?.()
            }}
          >
            <span>{row.text}</span>
          </button>
        )
      )}
    </motion.div>
  )
}
