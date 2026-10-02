import { useMemo } from 'react'
import Dock from './reactbits/Dock'
import { AppIcon, Glyph } from './icons/AppGlyph'
import { APPS, type AppId } from '../data/apps'

interface Props {
  running: string[]
  bouncing: string | null
  onLaunch: (id: AppId) => void
  onLaunchpad: () => void
}

export default function DockBar({ running, bouncing, onLaunch, onLaunchpad }: Props) {
  const items = useMemo(() => {
    const tiles = APPS.filter(a => a.inDock).map(a => ({
      icon: (
        <>
          <AppIcon tone={a.tone} glyph={a.glyph} size={44} />
          {running.includes(a.id) ? <span className="run-dot" /> : null}
        </>
      ),
      label: a.name,
      onClick: () => onLaunch(a.id),
      className: `${running.includes(a.id) ? 'is-running' : ''} ${bouncing === a.id ? 'is-bounce' : ''}`
    }))
    tiles.push({
      icon: (
        <span className="dock-tile launchpad-tile" style={{ background: 'transparent' }}>
          <Glyph name="grid" size={26} />
        </span>
      ),
      label: '启动台',
      onClick: onLaunchpad,
      className: bouncing === 'launchpad' ? 'is-bounce' : ''
    })
    return tiles
  }, [running, bouncing, onLaunch, onLaunchpad])

  return (
    <div className="dock-zone">
      <Dock items={items} panelHeight={62} dockHeight={104} baseItemSize={48} magnification={74} distance={150} />
    </div>
  )
}
