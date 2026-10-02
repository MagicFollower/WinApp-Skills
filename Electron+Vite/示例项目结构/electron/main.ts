import { app, BrowserWindow, ipcMain, Menu, nativeTheme, screen } from 'electron'
import fs from 'node:fs'
import fsp from 'node:fs/promises'
import path from 'node:path'

const SELFTEST = process.argv.includes('--selftest')
const MAX_DOC_BYTES = 512 * 1024
const SEGMENT = /^[a-z0-9][a-z0-9_-]{0,40}$/

// 窗口标题的唯一出处；渲染层不许再设 document.title（见 page-title-updated）。
const APP_TITLE = 'Linux 命令手册'
const MIN_W = 880
const MIN_H = 560

let win: BrowserWindow | null = null

function boundsFile(): string {
  return path.join(app.getPath('userData'), 'window.json')
}

interface Bounds {
  x?: number
  y?: number
  width: number
  height: number
}

function loadBounds(): Bounds {
  try {
    const raw = JSON.parse(fs.readFileSync(boundsFile(), 'utf8')) as Partial<Bounds>
    return { x: raw.x, y: raw.y, width: raw.width ?? 1280, height: raw.height ?? 820 }
  } catch {
    return { width: 1280, height: 820 }
  }
}

// 恢复持久化几何必须夹紧：接过副屏、拔掉副屏后窗口会在可视区外复活。
function clampBounds(b: Bounds): Required<Bounds> {
  const x0 = b.x ?? 120
  const y0 = b.y ?? 120
  const wa = screen.getDisplayNearestPoint({ x: x0, y: y0 }).workArea
  const width = Math.max(MIN_W, Math.min(b.width, wa.width))
  const height = Math.max(MIN_H, Math.min(b.height, wa.height))
  return {
    x: Math.round(Math.min(Math.max(x0, wa.x), wa.x + wa.width - width)),
    y: Math.round(Math.min(Math.max(y0, wa.y), wa.y + wa.height - height)),
    width: Math.round(width),
    height: Math.round(height)
  }
}

function saveBounds(): void {
  if (!win) return
  if (win.isMaximized() || win.isMinimized()) return
  const b = win.getBounds()
  try {
    fs.writeFileSync(boundsFile(), JSON.stringify(b), 'utf8')
  } catch {
    // 写不进几何不该影响使用
  }
}

function editsRoot(): string {
  return path.join(app.getPath('userData'), 'edits')
}

function resolveDocPath(id: unknown): string | null {
  if (typeof id !== 'string') return null
  const parts = id.split('/')
  if (parts.length !== 2) return null
  if (!parts.every(part => SEGMENT.test(part))) return null
  return path.join(editsRoot(), parts[0], `${parts[1]}.md`)
}

type DocRead = { text: string | null; savedAt: number | null }

async function readDoc(id: unknown): Promise<DocRead> {
  const file = resolveDocPath(id)
  if (!file) throw new Error('invalid doc id')
  try {
    const text = await fsp.readFile(file, 'utf8')
    const stat = await fsp.stat(file)
    return { text, savedAt: stat.mtimeMs }
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return { text: null, savedAt: null }
    throw err
  }
}

async function saveDoc(id: unknown, text: unknown): Promise<{ savedAt: number }> {
  const file = resolveDocPath(id)
  if (!file) throw new Error('invalid doc id')
  if (typeof text !== 'string') throw new Error('invalid doc text')
  if (Buffer.byteLength(text, 'utf8') > MAX_DOC_BYTES) throw new Error('doc too large')
  await fsp.mkdir(path.dirname(file), { recursive: true })
  await fsp.writeFile(file, text, 'utf8')
  const stat = await fsp.stat(file)
  return { savedAt: stat.mtimeMs }
}

async function revertDoc(id: unknown): Promise<{ removed: boolean }> {
  const file = resolveDocPath(id)
  if (!file) throw new Error('invalid doc id')
  try {
    await fsp.unlink(file)
    return { removed: true }
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return { removed: false }
    throw err
  }
}

const SELFTEST_PROBE = `(() => ({
  viewport: window.innerWidth + 'x' + window.innerHeight,
  dark: window.matchMedia('(prefers-color-scheme: dark)').matches,
  dpr: window.devicePixelRatio,
  font: getComputedStyle(document.body).fontFamily.split(',')[0].replace(/["']/g, ''),
  rootPx: getComputedStyle(document.documentElement).fontSize,
  bodyPx: getComputedStyle(document.body).fontSize,
  hasApi: typeof window.api === 'object',
  dockItems: document.querySelectorAll('.dock-item').length,
  dockActive: document.querySelectorAll('.dock-item.is-active').length,
  listItems: document.querySelectorAll('.scroll-list .al-item').length,
  preview: Boolean(document.querySelector('.md-preview')),
  detailTitle: (document.querySelector('.detail-title') || {}).textContent || '',
  heading: (document.querySelector('.md-preview h1') || {}).textContent || ''
}))()`

function clickProbe(selector: string, index: number): string {
  return `(() => {
  const nodes = document.querySelectorAll(${JSON.stringify(selector)})
  const node = nodes[${index}]
  if (!node) return 'missing(count=' + nodes.length + ')'
  node.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }))
  return 'clicked(count=' + nodes.length + ')'
})()`
}

const delay = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms))

async function runSelfTest(): Promise<void> {
  try {
    const contents = (win as BrowserWindow).webContents
    const first = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[manual] dom ${JSON.stringify(first)}\n`)

    const probed = first as { dark: boolean; rootPx: string; bodyPx: string; font: string; dpr: number }
    process.stdout.write(
      `[manual] adaptive themeSource=${nativeTheme.themeSource} shouldUseDark=${nativeTheme.shouldUseDarkColors} ` +
        `rendererDark=${probed.dark} agrees=${nativeTheme.shouldUseDarkColors === probed.dark} ` +
        `font=${probed.font} rootPx=${probed.rootPx} bodyPx=${probed.bodyPx} dpr=${probed.dpr} ` +
        `title=${(win as BrowserWindow).getTitle()}\n`
    )

    // Click the 6th dock tile, then the 4th list row: proves Dock onClick -> theme swap
    // and list onClick -> doc swap without moving the real mouse.
    process.stdout.write(`[manual] dock-click ${await contents.executeJavaScript(clickProbe('.dock-item', 5), true)}\n`)
    await delay(700)
    process.stdout.write(`[manual] dom-after-dock ${JSON.stringify(await contents.executeJavaScript(SELFTEST_PROBE, true))}\n`)

    process.stdout.write(`[manual] list-click ${await contents.executeJavaScript(clickProbe('.scroll-list .al-item', 3), true)}\n`)
    await delay(700)
    process.stdout.write(`[manual] dom-after-list ${JSON.stringify(await contents.executeJavaScript(SELFTEST_PROBE, true))}\n`)

    const id = 'files/zz-selftest'
    await saveDoc(id, '# selftest\n')
    const saved = await readDoc(id)
    const reverted = await revertDoc(id)
    const after = await readDoc(id)
    const ok = saved.text === '# selftest\n' && reverted.removed && after.text === null
    process.stdout.write(`[manual] storage roundtrip ${ok ? 'ok' : 'FAIL'}\n`)
    process.stdout.write(`[manual] editsDir ${editsRoot()}\n`)
  } catch (err) {
    process.stdout.write(`[manual] selftest-error ${String(err)}\n`)
  } finally {
    app.quit()
  }
}

function createWindow(): void {
  const saved = loadBounds()
  const b = clampBounds(saved)

  win = new BrowserWindow({
    x: b.x,
    y: b.y,
    width: b.width,
    height: b.height,
    minWidth: MIN_W,
    minHeight: MIN_H,
    show: false,
    backgroundColor: '#120F17',
    autoHideMenuBar: true,
    title: APP_TITLE,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  })

  // 标题单一真源：不拦下页面标题更新的话，index.html 的 <title> 会盖掉上面的设定。
  win.on('page-title-updated', event => {
    event.preventDefault()
  })

  const devUrl = process.env.VITE_DEV_SERVER_URL
  if (devUrl) {
    void win.loadURL(devUrl)
  } else {
    void win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))
  }

  win.once('ready-to-show', () => {
    win?.show()
    const real = (win as BrowserWindow).getBounds()
    const disp = screen.getDisplayMatching(real)
    process.stdout.write(
      `[manual] ready-to-show saved=${JSON.stringify(saved)} clamped=${JSON.stringify(b)} bounds=${JSON.stringify(real)} ` +
        `insideWorkArea=${real.x >= 0 && real.y >= 0 && real.x + real.width <= disp.workArea.width && real.y + real.height <= disp.workArea.height} ` +
        `workArea=${disp.workArea.width}x${disp.workArea.height} scale=${disp.scaleFactor}\n`
    )
    if (SELFTEST) void runSelfTest()
  })

  win.on('resized', () => {
    if (win && !win.isMaximized()) saveBounds()
  })
  win.on('moved', () => {
    if (win && !win.isMaximized()) saveBounds()
  })
  win.on('close', saveBounds)

  win.on('closed', () => {
    win = null
  })
}

ipcMain.handle('doc:read', (_event, id: unknown) => readDoc(id))
ipcMain.handle('doc:save', (_event, id: unknown, text: unknown) => saveDoc(id, text))
ipcMain.handle('doc:revert', (_event, id: unknown) => revertDoc(id))
ipcMain.handle('app:info', () => ({
  version: app.getVersion(),
  userData: app.getPath('userData'),
  editsDir: editsRoot()
}))

void app.whenReady().then(() => {
  // 本产品有意固定深色（浅色色板未定稿），所以不跟随系统：themeSource 会把
  // prefers-color-scheme、表单控件与滚动条都钉成 dark，避免系统切浅色时半深半浅。
  nativeTheme.themeSource = 'dark'
  Menu.setApplicationMenu(null)
  createWindow()
})

app.on('window-all-closed', () => {
  app.quit()
})
