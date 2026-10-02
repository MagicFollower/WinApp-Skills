import React, {
  useRef,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
  type MouseEventHandler,
  type UIEvent
} from 'react'
import { motion, useInView } from 'motion/react'
import './AnimatedList.css'

export type ListItemData = {
  id: string
  title: string
  subtitle?: string
}

interface AnimatedItemProps {
  children: ReactNode
  delay: number
  index: number
  onMouseEnter?: MouseEventHandler<HTMLDivElement>
  onMouseLeave?: MouseEventHandler<HTMLDivElement>
  onClick?: MouseEventHandler<HTMLDivElement>
}

const AnimatedItem: React.FC<AnimatedItemProps> = ({
  children,
  delay = 0,
  index,
  onMouseEnter,
  onMouseLeave,
  onClick
}) => {
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { amount: 0.5, once: false })
  return (
    <motion.div
      ref={ref}
      data-index={index}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
      onClick={onClick}
      initial={{ scale: 0.7, opacity: 0 }}
      animate={inView ? { scale: 1, opacity: 1 } : { scale: 0.7, opacity: 0 }}
      transition={{ duration: 0.2, delay }}
      className="al-item"
    >
      {children}
    </motion.div>
  )
}

interface AnimatedListProps {
  items: ListItemData[]
  onItemSelect?: (item: ListItemData, index: number) => void
  activeIndex?: number
  showGradients?: boolean
  className?: string
  itemClassName?: string
  displayScrollbar?: boolean
}

const AnimatedList: React.FC<AnimatedListProps> = ({
  items,
  onItemSelect,
  activeIndex = -1,
  showGradients = true,
  className = '',
  itemClassName = '',
  displayScrollbar = true
}) => {
  const listRef = useRef<HTMLDivElement>(null)
  const [hoverIndex, setHoverIndex] = useState<number>(-1)
  const [topGradientOpacity, setTopGradientOpacity] = useState<number>(0)
  const [bottomGradientOpacity, setBottomGradientOpacity] = useState<number>(1)

  const handleItemClick = useCallback(
    (item: ListItemData, index: number) => {
      onItemSelect?.(item, index)
    },
    [onItemSelect]
  )

  const handleScroll = useCallback((e: UIEvent<HTMLDivElement>) => {
    const target = e.target as HTMLDivElement
    const { scrollTop, scrollHeight, clientHeight } = target
    setTopGradientOpacity(Math.min(scrollTop / 50, 1))
    const bottomDistance = scrollHeight - (scrollTop + clientHeight)
    setBottomGradientOpacity(scrollHeight <= clientHeight ? 0 : Math.min(bottomDistance / 50, 1))
  }, [])

  useEffect(() => {
    if (activeIndex < 0 || !listRef.current) return
    const container = listRef.current
    const active = container.querySelector(`[data-index="${activeIndex}"]`) as HTMLElement | null
    if (!active) return
    const box = active.getBoundingClientRect()
    const listBox = container.getBoundingClientRect()
    if (box.top < listBox.top || box.bottom > listBox.bottom) {
      active.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
    }
  }, [activeIndex, items])

  return (
    <div className={`scroll-list-container ${className}`}>
      <div ref={listRef} className={`scroll-list ${!displayScrollbar ? 'no-scrollbar' : ''}`} onScroll={handleScroll}>
        {items.map((item, index) => (
          <AnimatedItem
            key={item.id}
            delay={0.04}
            index={index}
            onMouseEnter={() => setHoverIndex(index)}
            onMouseLeave={() => setHoverIndex(-1)}
            onClick={() => handleItemClick(item, index)}
          >
            <div
              className={`item ${index === activeIndex ? 'selected' : ''} ${index === hoverIndex ? 'hovered' : ''} ${itemClassName}`}
            >
              <p className="item-title">{item.title}</p>
              {item.subtitle ? <p className="item-subtitle">{item.subtitle}</p> : null}
            </div>
          </AnimatedItem>
        ))}
      </div>
      {showGradients && (
        <>
          <div className="top-gradient" style={{ opacity: topGradientOpacity }}></div>
          <div className="bottom-gradient" style={{ opacity: bottomGradientOpacity }}></div>
        </>
      )}
    </div>
  )
}

export default AnimatedList
