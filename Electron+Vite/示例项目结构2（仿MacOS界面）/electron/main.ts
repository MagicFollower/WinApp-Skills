import { app, BrowserWindow, ipcMain, Menu, nativeTheme, screen } from 'electron'
import fs from 'node:fs'
import fsp from 'node:fs/promises'
import path from 'node:path'

const SELFTEST = process.argv.includes('--selftest')
const MAX_DOC_BYTES = 512 * 1024
const MAX_LIST_ENTRIES = 4000
const NOTE_ID = /^[a-z0-9][a-z0-9_-]{0,40}$/
const TEXT_EXT = new Set([
  '.md', '.txt', '.json', '.yaml', '.yml', '.toml', '.ini', '.csv', '.log',
  '.ts', '.tsx', '.js', '.jsx', '.css', '.html', '.xml', '.mjs', '.cjs'
])

// 窗口标题的唯一出处；渲染层不许再设 document.title（见 page-title-updated）。
const APP_TITLE = '仿 macOS 桌面'
const MIN_W = 900
const MIN_H = 560
const DEFAULT_BG = '#1b1a22'

// 变体名 -> 明暗。100themes 的 day* 变体在 colors.toml 里 mode = "light"，
// 其余是 dark；选中显式变体时主进程按这张表翻转 themeSource。
const VARIANT_MODE: Record<string, 'dark' | 'light'> = {
  dark: 'dark',
  oled: 'dark',
  'high-contrast': 'dark',
  day: 'light',
  'day-high-contrast': 'light'
}

type Scheme = 'system' | 'light' | 'dark'
type Variant = 'auto' | 'dark' | 'day' | 'oled' | 'high-contrast' | 'day-high-contrast'

interface Settings {
  scheme: Scheme
  palette: string
  variant: Variant
  wallpaper: string
  bg: string
}

const DEFAULT_SETTINGS: Settings = {
  scheme: 'system',
  palette: 'abyss',
  variant: 'auto',
  wallpaper: 'sonoma',
  bg: DEFAULT_BG
}

let win: BrowserWindow | null = null
let settings: Settings = { ...DEFAULT_SETTINGS }

function userDataFile(name: string): string {
  return path.join(app.getPath('userData'), name)
}

function loadSettings(): void {
  try {
    const raw = JSON.parse(fs.readFileSync(userDataFile('settings.json'), 'utf8')) as Partial<Settings>
    settings = sanitize({ ...DEFAULT_SETTINGS, ...raw })
  } catch {
    settings = { ...DEFAULT_SETTINGS }
  }
}

// 落盘前先消毒：任何越界的 scheme/variant/bg 都会把 themeSource 与底色带到不可预期值。
function sanitize(s: Partial<Settings>): Settings {
  const scheme: Scheme = s.scheme === 'light' || s.scheme === 'dark' ? s.scheme : 'system'
  const variant: Variant = s.variant === 'auto' || (s.variant && VARIANT_MODE[s.variant]) ? s.variant : 'auto'
  const palette = typeof s.palette === 'string' && /^[a-z0-9._-]{1,40}$/.test(s.palette) ? s.palette : DEFAULT_SETTINGS.palette
  const wallpaper = typeof s.wallpaper === 'string' && /^[a-z0-9._-]{1,40}$/.test(s.wallpaper) ? s.wallpaper : DEFAULT_SETTINGS.wallpaper
  const bg = typeof s.bg === 'string' && /^#[0-9a-fA-F]{6}$/.test(s.bg) ? s.bg : DEFAULT_BG
  return { scheme, palette, variant, wallpaper, bg }
}

function saveSettings(): void {
  try {
    fs.writeFileSync(userDataFile('settings.json'), JSON.stringify(settings, null, 2), 'utf8')
  } catch {
    // 写不进设置不该影响使用
  }
}

// themeSource 的真值口径：显式变体决定明暗（翻转），auto 时回到 scheme 三态。
function effectiveThemeSource(): Scheme {
  if (settings.variant === 'auto') return settings.scheme
  return VARIANT_MODE[settings.variant]
}

function applyThemeSource(): void {
  nativeTheme.themeSource = effectiveThemeSource()
}

function boundsFile(): string {
  return userDataFile('window.json')
}

interface Bounds {
  x?: number
  y?: number
  width: number
  height: number
}

function workAreaOf(x: number, y: number): Electron.Rectangle {
  return screen.getDisplayNearestPoint({ x, y }).workArea
}

function loadBounds(): Bounds {
  try {
    const raw = JSON.parse(fs.readFileSync(boundsFile(), 'utf8')) as Partial<Bounds>
    const wa = workAreaOf(raw.x ?? 0, raw.y ?? 0)
    return {
      x: raw.x,
      y: raw.y,
      width: raw.width ?? wa.width,
      height: raw.height ?? wa.height
    }
  } catch {
    const wa = screen.getPrimaryDisplay().workArea
    return { x: wa.x, y: wa.y, width: wa.width, height: wa.height }
  }
}

// 恢复持久化几何必须夹紧：接过副屏、拔掉副屏后窗口会在可视区外复活。
function clampBounds(b: Bounds): Required<Bounds> {
  const x0 = b.x ?? 0
  const y0 = b.y ?? 0
  const wa = workAreaOf(x0, y0)
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
  if (!win || win.isMaximized() || win.isMinimized()) return
  try {
    fs.writeFileSync(boundsFile(), JSON.stringify(win.getBounds()), 'utf8')
  } catch {
    // 同上
  }
}

function notesRoot(): string {
  return path.join(app.getPath('userData'), 'notes')
}

function noteFile(id: unknown): string | null {
  if (typeof id !== 'string' || !NOTE_ID.test(id)) return null
  return path.join(notesRoot(), `${id}.json`)
}

interface Note {
  id: string
  title: string
  text: string
  updatedAt: number
}

function titleOf(text: string): string {
  const first = text.split(/\r?\n/).find(line => line.trim() !== '') ?? ''
  return first.replace(/^#+\s*/, '').slice(0, 60) || '无标题'
}

async function listNotes(): Promise<Note[]> {
  let files: string[] = []
  try {
    files = await fsp.readdir(notesRoot())
  } catch {
    return []
  }
  const out: Note[] = []
  for (const f of files) {
    if (!f.endsWith('.json')) continue
    try {
      const data = JSON.parse(await fsp.readFile(path.join(notesRoot(), f), 'utf8')) as Partial<Note>
      const id = f.slice(0, -'.json'.length)
      const text = typeof data.text === 'string' ? data.text : ''
      out.push({
        id,
        title: typeof data.title === 'string' && data.title ? data.title : titleOf(text),
        text,
        updatedAt: typeof data.updatedAt === 'number' ? data.updatedAt : 0
      })
    } catch {
      // 单条损坏不影响其余
    }
  }
  return out.sort((a, b) => b.updatedAt - a.updatedAt)
}

async function saveNote(id: unknown, text: unknown): Promise<Note> {
  const file = noteFile(id)
  if (!file) throw new Error('invalid note id')
  if (typeof text !== 'string') throw new Error('invalid note text')
  if (Buffer.byteLength(text, 'utf8') > MAX_DOC_BYTES) throw new Error('note too large')
  const note: Note = { id: (id as string), title: titleOf(text), text, updatedAt: Date.now() }
  await fsp.mkdir(notesRoot(), { recursive: true })
  await fsp.writeFile(file, JSON.stringify(note, null, 2), 'utf8')
  return note
}

async function removeNote(id: unknown): Promise<{ removed: boolean }> {
  const file = noteFile(id)
  if (!file) throw new Error('invalid note id')
  try {
    await fsp.unlink(file)
    return { removed: true }
  } catch (err) {
    if ((err as NodeJS.ErrnoException).code === 'ENOENT') return { removed: false }
    throw err
  }
}

function normalizePath(p: string): string {
  return p.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase()
}

// 只读浏览的安全边界：目标 realpath 必须落在用户主目录内，越界一律拒绝。
function withinHome(target: string): boolean {
  const home = normalizePath(fs.realpathSync(app.getPath('home')))
  let real = normalizePath(path.resolve(target))
  try {
    real = normalizePath(fs.realpathSync(path.resolve(target)))
  } catch {
    // 不存在的路径按 resolve 结果判断，后面 stat 会给出真因
  }
  return real === home || real.startsWith(home + '/')
}

interface FsEntry {
  name: string
  isDir: boolean
  size: number
  mtimeMs: number
  hidden: boolean
}

async function listDir(dir: unknown, showHidden: unknown): Promise<{ dir: string; entries: FsEntry[] }> {
  if (typeof dir !== 'string' || dir.length === 0) throw new Error('invalid dir')
  const abs = path.resolve(dir)
  if (!withinHome(abs)) throw new Error('outside home directory')
  const hidden = showHidden === true
  const names = await fsp.readdir(abs)
  const entries: FsEntry[] = []
  for (const name of names) {
    if (!hidden && name.startsWith('.')) continue
    let stat: fs.Stats
    try {
      stat = await fsp.stat(path.join(abs, name))
    } catch {
      continue // 断链与权限问题直接跳过，不让整列失败
    }
    entries.push({
      name,
      isDir: stat.isDirectory(),
      size: stat.size,
      mtimeMs: stat.mtimeMs,
      hidden: name.startsWith('.')
    })
    if (entries.length >= MAX_LIST_ENTRIES) break
  }
  entries.sort((a, b) => (a.isDir === b.isDir ? a.name.localeCompare(b.name, 'zh-CN') : a.isDir ? -1 : 1))
  return { dir: abs, entries }
}

async function readTextFile(file: unknown): Promise<{ name: string; text: string; truncated: boolean }> {
  if (typeof file !== 'string') throw new Error('invalid file')
  const abs = path.resolve(file)
  if (!withinHome(abs)) throw new Error('outside home directory')
  const ext = path.extname(abs).toLowerCase()
  if (!TEXT_EXT.has(ext)) throw new Error(`preview not supported for ${ext || 'extensionless'} files`)
  const stat = await fsp.stat(abs)
  if (stat.size > 4 * 1024 * 1024) throw new Error('file too large to preview')
  const buf = await fsp.readFile(abs)
  const truncated = buf.length > MAX_DOC_BYTES
  return {
    name: path.basename(abs),
    text: buf.subarray(0, MAX_DOC_BYTES).toString('utf8'),
    truncated
  }
}

function quickAccess(): { label: string; path: string }[] {
  const home = app.getPath('home')
  const pick = (name: string) => path.join(home, name)
  const known = [
    { label: '主目录', path: home },
    { label: '桌面', path: app.getPath('desktop') },
    { label: '文档', path: app.getPath('documents') },
    { label: '下载', path: app.getPath('downloads') },
    { label: '图片', path: pick('Pictures') },
    { label: '音乐', path: pick('Music') }
  ]
  return known.filter(item => fs.existsSync(item.path))
}

function appInfo() {
  return {
    version: app.getVersion(),
    name: app.name,
    userData: app.getPath('userData'),
    notesDir: notesRoot(),
    home: app.getPath('home'),
    settings,
    runtime: {
      electron: process.versions.electron,
      chrome: process.versions.chrome,
      node: process.versions.node,
      platform: `${process.platform} ${process.arch}`,
      osRelease: process.getSystemVersion()
    }
  }
}

type Info = ReturnType<typeof appInfo>

const SELFTEST_PROBE = `(() => ({
  viewport: window.innerWidth + 'x' + window.innerHeight,
  dark: window.matchMedia('(prefers-color-scheme: dark)').matches,
  dpr: window.devicePixelRatio,
  font: getComputedStyle(document.body).fontFamily.split(',')[0].replace(/["']/g, ''),
  rootPx: getComputedStyle(document.documentElement).fontSize,
  bodyPx: getComputedStyle(document.body).fontSize,
  hasApi: typeof window.api === 'object',
  dataTheme: document.documentElement.dataset.theme || '',
  bg: getComputedStyle(document.documentElement).getPropertyValue('--pf-bg').trim(),
  deskClass: (document.querySelector('.desktop') || {}).className || '',
  wallpaper: getComputedStyle(document.querySelector('.desktop') || document.body, '::before').backgroundImage.slice(0, 26),
  dockItems: document.querySelectorAll('.dock-item').length,
  dockRunning: document.querySelectorAll('.dock-item .run-dot').length,
  menuBar: Boolean(document.querySelector('.menubar')),
  clock: (document.querySelector('.mb-clock') || {}).textContent || '',
  windows: document.querySelectorAll('.app-window').length,
  frontmost: (document.querySelector('.app-window.is-front .win-title') || {}).textContent || '',
  finderRows: document.querySelectorAll('.finder-row').length,
  noteItems: document.querySelectorAll('.note-item').length
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

// ready-to-show 只代表页面开始加载，React 还没把 data-theme 写上去。
// 不等到色板生效就探针，读回来的是 token 未解析的空值，截图也会是全黑。
async function waitForThemeApplied(timeoutMs = 6000): Promise<boolean> {
  const contents = (win as BrowserWindow).webContents
  const until = Date.now() + timeoutMs
  while (Date.now() < until) {
    const applied = await contents.executeJavaScript(`document.documentElement.dataset.theme || ''`, true)
    if (typeof applied === 'string' && applied.includes('--')) return true
    await delay(100)
  }
  return false
}

// portable 宿主不冒泡子进程 stdout，所以额外写标记文件证存活。
function markDir(): string | null {
  const i = process.argv.indexOf('--mark-dir')
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : null
}

function writeMark(name: string): void {
  const dir = markDir()
  if (!dir) return
  try {
    fs.mkdirSync(dir, { recursive: true })
    fs.writeFileSync(path.join(dir, name), `${Date.now()}\n`, 'utf8')
  } catch {
    // 标记写不进就退回 stdout 口径
  }
}

// --shot-dir <dir>：在自证的每个节点用 capturePage 出一张 PNG。
// 截图由应用自己完成，时序确定，也不需要把窗口抢到前台（会打扰同一台机器上的其他应用）。
function shotDir(): string | null {
  const i = process.argv.indexOf('--shot-dir')
  return i >= 0 && process.argv[i + 1] ? path.resolve(process.argv[i + 1]) : null
}

async function shot(name: string): Promise<void> {
  const dir = shotDir()
  if (!dir || !win) return
  try {
    const image = await win.webContents.capturePage()
    fs.mkdirSync(dir, { recursive: true })
    fs.writeFileSync(path.join(dir, `${name}.png`), image.toPNG())
    process.stdout.write(`[desktop] shot ${name}.png ${path.join(dir, `${name}.png`)}\n`)
  } catch (err) {
    process.stdout.write(`[desktop] shot-error ${name} ${String(err)}\n`)
  }
}

async function runSelfTest(): Promise<void> {
  const contents = (win as BrowserWindow).webContents
  // 自证会显式改 variant，跑完必须还原成用户原来的设置，否则测试把人的主题钉住了。
  const originalSettings: Settings = { ...settings }
  try {
    const painted = await waitForThemeApplied()
    process.stdout.write(`[desktop] theme-painted ${painted ? 'ok' : 'TIMEOUT'}\n`)
    const first = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[desktop] dom ${JSON.stringify(first)}\n`)

    const probed = first as { dark: boolean; bg: string; dataTheme: string; rootPx: string; bodyPx: string; font: string; dpr: number }
    process.stdout.write(
      `[desktop] adaptive themeSource=${nativeTheme.themeSource} shouldUseDark=${nativeTheme.shouldUseDarkColors} ` +
        `rendererDark=${probed.dark} agrees=${nativeTheme.shouldUseDarkColors === probed.dark} ` +
        `dataTheme=${probed.dataTheme} bg=${probed.bg} font=${probed.font} rootPx=${probed.rootPx} bodyPx=${probed.bodyPx} ` +
        `dpr=${probed.dpr} title=${(win as BrowserWindow).getTitle()}\n`
    )
    writeMark('window-shown')
    await shot('01-desktop-idle')

    // 点第一个 Dock 磁贴（访达）：证 Dock onClick -> 窗口打开 -> IPC 目录列表回来
    process.stdout.write(`[desktop] dock-click ${await contents.executeJavaScript(clickProbe('.dock-item', 0), true)}\n`)
    await delay(900)
    const afterDock = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[desktop] dom-after-dock ${JSON.stringify(afterDock)}\n`)
    await shot('02-finder-open')

    // 流量灯：黄=最小化、绿=最大化、红=关闭。AnimatePresence 的退场动画会把节点留
    // 在 DOM 里几百毫秒，所以每一步都等满 900ms 再数，否则会把"正在飞走"数成"还开着"。
    const before = (afterDock as { windows: number }).windows
    process.stdout.write(`[desktop] minimize ${await contents.executeJavaScript(clickProbe('.win-btn.minimize', 0), true)}\n`)
    await delay(900)
    const afterMin = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[desktop] minimized-visible=${afterMin.windows} dockRunning=${afterMin.dockRunning}\n`)
    process.stdout.write(`[desktop] minimize-back ${await contents.executeJavaScript(clickProbe('.dock-item', 0), true)}\n`)
    await delay(900)
    const afterRestore = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[desktop] minimize-cycle before=${before} after-minimize=${afterMin.windows} after-restore=${afterRestore.windows}\n`)
    process.stdout.write(`[desktop] maximize ${await contents.executeJavaScript(clickProbe('.win-btn.maximize', 0), true)}\n`)
    await delay(500)
    process.stdout.write(
      `[desktop] maximized ${await contents.executeJavaScript(
        `(() => {
  const w = document.querySelector('.app-window')
  if (!w) return 'missing'
  const b = w.getBoundingClientRect()
  const d = document.querySelector('.desk-area').getBoundingClientRect()
  return 'fillsArea=' + (Math.abs(b.width - d.width) < 2 && Math.abs(b.height - d.height) < 2) +
    ' class=' + w.className
})()`,
        true
      )}\n`
    )
    process.stdout.write(`[desktop] close ${await contents.executeJavaScript(clickProbe('.win-btn.close', 0), true)}\n`)
    await delay(900)
    process.stdout.write(`[desktop] dom-after-close ${JSON.stringify(await contents.executeJavaScript(SELFTEST_PROBE, true))}\n`)

    // Spotlight：点菜单栏搜索 -> 输入前缀 -> 断言结果条数
    process.stdout.write(`[desktop] spotlight-open ${await contents.executeJavaScript(clickProbe('.menubar-search', 0), true)}\n`)
    await delay(300)
    process.stdout.write(
      `[desktop] spotlight-results ${await contents.executeJavaScript(
        `(() => {
  const input = document.querySelector('.spotlight-input')
  if (!input) return 'no-spotlight'
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set
  setter.call(input, '备')
  input.dispatchEvent(new Event('input', { bubbles: true }))
  return 'query=' + input.value + ' rows=' + document.querySelectorAll('.spotlight-row').length
})()`,
        true
      )}\n`
    )
    await shot('03-spotlight')
    process.stdout.write(`[desktop] spotlight-launch ${await contents.executeJavaScript(clickProbe('.spotlight-row', 0), true)}\n`)
    await delay(900)
    process.stdout.write(`[desktop] dom-after-spotlight ${JSON.stringify(await contents.executeJavaScript(SELFTEST_PROBE, true))}\n`)

    // 明暗翻转：显式选 day 变体 -> themeSource 应翻成 light 且 --pf-bg 真的变。
    // 基准必须现取：probed 是首帧读数，那时色板还没应用，拿它比会得出"没变"的假结论。
    const darkBg = (await contents.executeJavaScript(SELFTEST_PROBE, true)).bg
    await applyVariantForTest('day')
    await delay(700)
    const dayProbe = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(
      `[desktop] flip-day themeSource=${nativeTheme.themeSource} shouldUseDark=${nativeTheme.shouldUseDarkColors} ` +
        `rendererDark=${dayProbe.dark} agrees=${nativeTheme.shouldUseDarkColors === dayProbe.dark} ` +
        `dataTheme=${dayProbe.dataTheme} bg=${dayProbe.bg} darkBg=${darkBg} changed=${dayProbe.bg !== darkBg}\n`
    )
    await shot('04-day-variant')
    await applyVariantForTest('dark')
    await delay(700)
    const darkProbe = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(
      `[desktop] flip-dark themeSource=${nativeTheme.themeSource} agrees=${nativeTheme.shouldUseDarkColors === darkProbe.dark} ` +
        `dataTheme=${darkProbe.dataTheme} restored=${darkProbe.bg === darkBg}\n`
    )

    // 外观面板：Dock 第 4 个磁贴（0 起）是外观，开起来截一张色板网格，然后逐个关掉
    process.stdout.write(`[desktop] open-appearance ${await contents.executeJavaScript(clickProbe('.dock-item', 3), true)}\n`)
    await delay(800)
    const withPanels = await contents.executeJavaScript(SELFTEST_PROBE, true)
    process.stdout.write(`[desktop] dom-with-panels ${JSON.stringify(withPanels)}\n`)
    await shot('05-appearance-panel')
    process.stdout.write(`[desktop] close-first ${await contents.executeJavaScript(clickProbe('.win-btn.close', 0), true)}\n`)
    await delay(900)
    process.stdout.write(`[desktop] close-second ${await contents.executeJavaScript(clickProbe('.win-btn.close', 0), true)}\n`)
    await delay(900)
    process.stdout.write(`[desktop] dom-final ${JSON.stringify(await contents.executeJavaScript(SELFTEST_PROBE, true))}\n`)

    // 备忘录：写 -> 列表 -> 读 -> 删，走渲染层同一套 IPC 契约
    const id = 'zz-selftest'
    const saved = await saveNote(id, '# selftest note\nhello')
    const listed = await listNotes()
    const removed = await removeNote(id)
    const after = await listNotes()
    const ok =
      saved.title === 'selftest note' &&
      listed.some(n => n.id === id) &&
      removed.removed &&
      !after.some(n => n.id === id)
    process.stdout.write(`[desktop] notes roundtrip ${ok ? 'ok' : 'FAIL'}\n`)

    const qa = quickAccess()
    const listed0 = await listDir(qa[0].path, false)
    process.stdout.write(
      `[desktop] finder quickAccess=${qa.length} first=${listed0.dir} entries=${listed0.entries.length} ` +
        `dirs=${listed0.entries.filter(e => e.isDir).length}\n`
    )
    const escaped = await listDir('C:\\Windows', false).then(() => 'ALLOWED').catch(e => `blocked:${(e as Error).message}`)
    process.stdout.write(`[desktop] finder-jail ${escaped}\n`)

    const info: Info = appInfo()
    process.stdout.write(
      `[desktop] runtime electron=${info.runtime.electron} chrome=${info.runtime.chrome} node=${info.runtime.node} ` +
        `os=${info.runtime.osRelease} userData=${info.userData}\n`
    )
    writeMark('probed')
    process.stdout.write('[desktop] checks-done\n')
    writeMark('checks-done')
  } catch (err) {
    process.stdout.write(`[desktop] selftest-error ${String(err)}\n`)
  } finally {
    settings = sanitize({ ...originalSettings })
    applyThemeSource()
    saveSettings()
    process.stdout.write(
      `[desktop] settings-restored palette=${settings.palette} variant=${settings.variant} wallpaper=${settings.wallpaper} bg=${settings.bg}\n`
    )
    app.quit()
  }
}

async function applyVariantForTest(variant: Variant): Promise<void> {
  settings = sanitize({ ...settings, variant })
  applyThemeSource()
  saveSettings()
  // 走的是与用户点外观面板完全同一条路：主进程广播，渲染层重算 data-theme。
  win?.webContents.send('theme:changed', { settings, mode: effectiveThemeSource() })
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
    frame: false,
    backgroundColor: settings.bg,
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
      `[desktop] ready-to-show saved=${JSON.stringify(saved)} clamped=${JSON.stringify(b)} bounds=${JSON.stringify(real)} ` +
        `insideWorkArea=${real.x >= disp.workArea.x && real.y >= disp.workArea.y && real.x + real.width <= disp.workArea.x + disp.workArea.width && real.y + real.height <= disp.workArea.y + disp.workArea.height} ` +
        `workArea=${disp.workArea.width}x${disp.workArea.height} scale=${disp.scaleFactor} bg=${settings.bg}\n`
    )
    writeMark('window-shown')
    if (SELFTEST) void runSelfTest()
  })

  win.on('resized', saveBounds)
  win.on('moved', saveBounds)
  win.on('close', saveBounds)
  win.on('closed', () => {
    win = null
  })
}

ipcMain.handle('settings:get', () => ({ settings, mode: effectiveThemeSource() }))

ipcMain.handle('settings:set', (_event, patch: unknown) => {
  if (!patch || typeof patch !== 'object') throw new Error('invalid settings patch')
  const p = patch as Partial<Settings>
  const next = sanitize({ ...settings, ...p })
  const bgChanged = next.bg !== settings.bg
  settings = next
  applyThemeSource()
  saveSettings()
  // 底色只影响下次启动的 backgroundColor，运行中不改已存在的窗口，避免闪白。
  if (bgChanged && win && !win.isDestroyed()) win.setBackgroundColor(settings.bg)
  return { settings, mode: effectiveThemeSource() }
})

ipcMain.handle('notes:list', () => listNotes())
ipcMain.handle('notes:save', (_event, id: unknown, text: unknown) => saveNote(id, text))
ipcMain.handle('notes:remove', (_event, id: unknown) => removeNote(id))
ipcMain.handle('fs:home', () => ({ home: app.getPath('home'), quickAccess: quickAccess() }))
ipcMain.handle('fs:list', (_event, dir: unknown, showHidden: unknown) => listDir(dir, showHidden))
ipcMain.handle('fs:read', (_event, file: unknown) => readTextFile(file))
ipcMain.handle('app:info', () => appInfo())
ipcMain.handle('app:quit', () => {
  app.quit()
})

nativeTheme.on('updated', () => {
  win?.webContents.send('theme:changed', { settings, mode: effectiveThemeSource() })
})

void app.whenReady().then(() => {
  // 明暗是产品能力：scheme 三态（跟随系统/亮/暗）持久化，显式变体会翻转 themeSource。
  loadSettings()
  applyThemeSource()
  Menu.setApplicationMenu(null)
  createWindow()
})

app.on('window-all-closed', () => {
  app.quit()
})
