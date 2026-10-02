import { useRef, type ReactNode } from 'react'
import { motion } from 'motion/react'
import { Glyph } from './icons/AppGlyph'

export interface WinState {
  id: string
  x: number
  y: number
  w: number
  h: number
  z: number
  minimized: boolean
  maximized: boolean
  /** 最大化之前的几何，点绿键还原时用 */
  restore?: { x: number; y: number; w: number; h: number }
  /** 打开动画的来源：新开=中心缩放，从 Dock 还原=底部飞起 */
  origin: 'open' | 'restore'
}

interface Props {
  win: WinState
  areaH: number
  minW: number
  minH: number
  front: boolean
  title: string
  onClose: () => void
  onMinimize: () => void
  onToggleMax: () => void
  onFocus: () => void
  onMove: (dx: number, dy: number) => void
  onResize: (dw: number, dh: number) => void
  children: ReactNode
}

export default function DesktopWindow({
  win,
  areaH,
  minW,
  minH,
  front,
  title,
  onClose,
  onMinimize,
  onToggleMax,
  onFocus,
  onMove,
  onResize,
  children
}: Props) {
  const dragLast = useRef<{ x: number; y: number } | null>(null)
  const mode = useRef<'move' | 'resize' | null>(null)

  const start = (kind: 'move' | 'resize') => (e: React.PointerEvent<HTMLElement>) => {
    if (e.button !== 0) return
    if (kind === 'move' && (e.target as HTMLElement).closest('.win-btn')) return
    mode.current = kind
    dragLast.current = { x: e.clientX, y: e.clientY }
    ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
    e.currentTarget.classList.add(kind === 'move' ? 'is-dragging' : 'is-resizing')
    onFocus()
  }

  const move = (e: React.PointerEvent<HTMLElement>) => {
    if (!mode.current || !dragLast.current) return
    const dx = e.clientX - dragLast.current.x
    const dy = e.clientY - dragLast.current.y
    dragLast.current = { x: e.clientX, y: e.clientY }
    if (mode.current === 'move') onMove(dx, dy)
    else onResize(dx, dy)
  }

  const end = (e: React.PointerEvent<HTMLElement>) => {
    mode.current = null
    dragLast.current = null
    e.currentTarget.classList.remove('is-dragging', 'is-resizing')
  }

  const entrance =
    win.origin === 'restore'
      ? { opacity: 0, scale: 0.42, y: Math.max(0, areaH - win.y) }
      : { opacity: 0, scale: 0.9, y: 10 }

  return (
    <motion.div
      className={`app-window ${front ? 'is-front' : 'is-inactive'} ${win.maximized ? 'is-maximized' : ''}`}
      data-win-id={win.id}
      style={{ left: win.x, top: win.y, width: win.w, height: win.h, zIndex: win.z }}
      initial={entrance}
      animate={{ opacity: 1, scale: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.36, y: Math.max(0, areaH - win.y) }}
      transition={{ type: 'spring', stiffness: 300, damping: 27, mass: 0.6 }}
      onPointerDownCapture={onFocus}
    >
      <div
        className="win-bar"
        onPointerDown={start('move')}
        onPointerMove={move}
        onPointerUp={end}
        onPointerCancel={end}
        onDoubleClick={onToggleMax}
      >
        <div className="win-lights">
          <button type="button" className="win-btn close" title="关闭" onClick={onClose} aria-label={`关闭 ${title}`}>
            <Glyph name="close" size={8} strokeWidth={2.6} />
          </button>
          <button type="button" className="win-btn minimize" title="最小化" onClick={onMinimize} aria-label={`最小化 ${title}`}>
            <Glyph name="minus" size={8} strokeWidth={2.6} />
          </button>
          <button type="button" className="win-btn maximize" title="缩放" onClick={onToggleMax} aria-label={`缩放 ${title}`}>
            <Glyph name="expand" size={8} strokeWidth={2.4} />
          </button>
        </div>
        <div className="win-title">{title}</div>
        <div className="win-tools" />
      </div>

      <div className="win-body">{children}</div>

      {!win.maximized && win.w > minW + 8 && win.h > minH + 8 ? (
        <div
          className="win-resize"
          onPointerDown={start('resize')}
          onPointerMove={move}
          onPointerUp={end}
          onPointerCancel={end}
          aria-hidden="true"
        />
      ) : null}
    </motion.div>
  )
}
