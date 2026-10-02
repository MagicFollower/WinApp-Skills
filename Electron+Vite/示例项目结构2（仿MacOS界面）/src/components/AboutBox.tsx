import { AppIcon } from './icons/AppGlyph'
import type { AppInfoResult } from '../types/electron-api'

interface Props {
  info: AppInfoResult | null
  appliedId: string
}

export default function AboutBox({ info, appliedId }: Props) {
  return (
    <div className="about-pane">
      <div className="about-mark">
        <AppIcon tone="accent" glyph="apple" size={46} glow={false} />
      </div>
      <div className="about-name">仿 macOS 桌面</div>
      <div className="pill">示例工程 · 单窗口桌面壳</div>

      <dl className="about-dl">
        <dt>版本</dt>
        <dd>{info?.version ?? '读取中'}</dd>
        <dt>Electron</dt>
        <dd>{info?.runtime.electron ?? ''}</dd>
        <dt>Chromium</dt>
        <dd>{info?.runtime.chrome ?? ''}</dd>
        <dt>Node</dt>
        <dd>{info?.runtime.node ?? ''}</dd>
        <dt>平台</dt>
        <dd>{info?.runtime.platform ?? ''}</dd>
        <dt>系统</dt>
        <dd>{info?.runtime.osRelease ?? ''}</dd>
        <dt>色板</dt>
        <dd>{appliedId || '（未应用）'}</dd>
        <dt>用户目录</dt>
        <dd>{info?.userData ?? ''}</dd>
        <dt>笔记目录</dt>
        <dd>{info?.notesDir ?? ''}</dd>
        <dt>主目录</dt>
        <dd>{info?.home ?? ''}</dd>
      </dl>

      <p className="tp-note" style={{ textAlign: 'center' }}>
        运行时信息只在本窗口内以文本呈现，不弹模态框。
      </p>
    </div>
  )
}
