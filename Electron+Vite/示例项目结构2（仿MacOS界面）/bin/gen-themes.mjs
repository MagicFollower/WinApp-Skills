import fs from 'node:fs'
import path from 'node:path'
import process from 'node:process'
import { fileURLToPath } from 'node:url'

/**
 * 把 100themes/raw 里的 colors.toml 批量转成 CSS 自定义属性。
 *
 * 这是一个可直接拷进工程的参考主题库：raw/ 是 bjarneo/100-themes 的 500 份 colors.toml 原文种子，
 * manifest.json 钉着主题清单与 token 映射。顺序是本地优先：种子齐全时完全离线；缺文件才走 jsDelivr
 * 取单个 colors.toml，并把取回的内容写回 raw/，下一次运行就能离线。
 *
 *   node 100themes/gen-themes.mjs --root <工程目录>            本地优先，缺则联网补，写产物
 *   node 100themes/gen-themes.mjs --root <工程目录> --offline  禁止联网，缺文件直接报错列出缺口
 *   node 100themes/gen-themes.mjs --root <工程目录> --fetch-only  只把缺口补进 raw/，不写产物
 *   node 100themes/gen-themes.mjs --root <工程目录> abyss,lofi  只处理子集——注意：产物会被覆写成
 *                                                              只含该子集，补种子请用 --fetch-only
 *
 * 本工程相对参考实现多一条约定（改这版的理由见 doc/运行与构建（T0Level）.md 第 8 节）：
 *   位置参数缺省时读 100themes/preset.json 的 themes 当白名单，所以 `npm run themes`
 *   默认只把 24 个主题 × 5 变体写进产物，而 100themes/raw/ 仍保留 500 份全量种子。
 *   要出全量产物或补齐全量种子，加 --all 忽略白名单。
 *
 * 装进工程后把本脚本移到工程的脚本目录（Skill 约定是 bin/gen-themes.mjs），那时 ROOT 默认取脚本上级
 * 目录，不用再写 --root。不加 --root 又不在工程里跑，会被下面那个"根目录必须有 package.json"的守卫拦下，
 * 免得把产物写到仓库外层变成孤儿 src/styles/。
 *
 * 工程内目录约定（见 windows-electron-scaffold Skill Step 7b）：
 *   100themes/manifest.json        来源、100 个主题名、5 个变体、token 映射、种子计数
 *   100themes/raw/<主题>_<变体>.toml
 *   src/styles/themes.css  src/styles/themes.index.json   产物，消费方见 README.md
 */

const HERE = path.dirname(fileURLToPath(import.meta.url))
const argv = process.argv.slice(2)

function resolveRoot() {
  const i = argv.indexOf('--root')
  if (i >= 0) {
    const v = argv[i + 1]
    if (!v || v.startsWith('--')) throw new Error('--root 后面要跟一个目录')
    return path.resolve(v)
  }
  const eq = argv.find((a) => a.startsWith('--root='))
  if (eq) return path.resolve(eq.slice('--root='.length))
  return path.resolve(HERE, '..')
}

const ROOT = resolveRoot()
const rootValue = argv[argv.indexOf('--root') + 1] ?? null

if (!fs.existsSync(path.join(ROOT, 'package.json'))) {
  process.stderr.write(
    `工程根 ${ROOT} 没有 package.json。种子库在工程外时请加 --root <工程目录>，` +
      `或把本脚本拷进工程的 bin/ 后在工程里跑。\n`
  )
  process.exit(1)
}
// 脚本放在 100themes/ 里时种子就是脚本所在目录；移到工程 bin/ 里时种子是 <工程根>/100themes/。
const THEME_DIR = path.basename(HERE) === '100themes' ? HERE : path.join(ROOT, '100themes')
const MANIFEST = path.join(THEME_DIR, 'manifest.json')
const RAW = path.join(THEME_DIR, 'raw')
const OUT_CSS = path.join(ROOT, 'src', 'styles', 'themes.css')
const OUT_INDEX = path.join(ROOT, 'src', 'styles', 'themes.index.json')
// 工程侧白名单：raw/ 保持 500 份全量种子，产物只写这里列出的主题，改清单重跑即可。
const PRESET = path.join(THEME_DIR, 'preset.json')

// 兜底清单：100themes/ 整个不存在时用这三组常量冷启动。清单来自仓库根目录列表，
// 已剔除 .github / assets / tools 三个非主题目录。
const FALLBACK = {
  source: {
    repo: 'bjarneo/100-themes',
    ref: 'main',
    filePattern: '<theme>/<variant>/colors.toml'
  },
  hosts: ['cdn.jsdelivr.net', 'fastly.jsdelivr.net', 'gcore.jsdelivr.net'],
  variants: ['dark', 'day', 'day-high-contrast', 'high-contrast', 'oled'],
  themes: [
    '8-bit', 'abyss', 'acid-house', 'amber-crt', 'ambient', 'arcade-carpet', 'arcade',
    'aurora', 'berlin-club', 'bioluminescent', 'black-hole', 'blood-moon', 'blues',
    'bubblegum', 'candy', 'cathode', 'chillwave', 'chiptune', 'coral-reef', 'cotton-candy',
    'cyberpunk', 'darkwave', 'dawn', 'deep-space', 'desert', 'disco', 'dreampop',
    'drum-and-bass', 'dubstep', 'dusk', 'ember', 'espresso', 'firefly', 'funk', 'galaxy',
    'glacier', 'glitchcore', 'goth', 'grape', 'grime', 'grunge', 'hacker', 'hazard',
    'hong-kong-rain', 'house', 'hyperpop', 'industrial', 'jazz-club', 'jellyfish', 'jungle',
    'lagoon', 'laser-tag', 'lemonade', 'lofi', 'mainframe', 'mars', 'matcha', 'metal',
    'miami-night', 'midnight', 'mint', 'mushroom', 'nebula', 'neon-tokyo', 'neon-vegas',
    'neon-wave', 'noir-rain', 'outrun', 'phonk', 'pinball', 'plasma-arc', 'poison',
    'polaroid', 'punk', 'racing', 'radioactive', 'rainforest', 'reggae', 'retrowave',
    'sakura', 'shoegaze', 'solar-flare', 'soul', 'stealth', 'supernova', 'swamp',
    'synthwave', 'techno', 'terminal-blue', 'terminal-green', 'toxic', 'trance', 'trap',
    'tropical', 'tundra', 'ultraviolet', 'vaporwave', 'vhs', 'volcano', 'watermelon'
  ],
  tokenMap: {
    background: 'bg',
    dark_background: 'bg-dark',
    darker_background: 'bg-darker',
    lighter_background: 'bg-lighter',
    foreground: 'fg',
    dark_foreground: 'fg-dim',
    light_foreground: 'fg-soft',
    bright_foreground: 'fg-strong',
    accent: 'accent',
    selection: 'selection',
    muted: 'muted',
    red: 'danger',
    green: 'success',
    yellow: 'warning',
    blue: 'info'
  }
}

function loadConfig() {
  if (!fs.existsSync(MANIFEST)) {
    return { ...FALLBACK, from: 'builtin fallback (100themes/manifest.json missing)' }
  }
  const m = JSON.parse(fs.readFileSync(MANIFEST, 'utf8'))
  return {
    source: { ...FALLBACK.source, ...m.source },
    hosts: m.hosts ?? FALLBACK.hosts,
    variants: m.variants ?? FALLBACK.variants,
    themes: m.themes ?? FALLBACK.themes,
    tokenMap: m.tokenMap ?? FALLBACK.tokenMap,
    from: '100themes/manifest.json'
  }
}

function parseColorsToml(text) {
  const out = {}
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim()
    if (!line || line.startsWith('#') || line.startsWith('[')) continue
    const eq = line.indexOf('=')
    if (eq <= 0) continue
    const key = line.slice(0, eq).trim()
    let value = line.slice(eq + 1).trim()
    if (value.startsWith('"') && value.endsWith('"')) value = value.slice(1, -1)
    out[key] = value
  }
  return out
}

function hexToRgb(hex) {
  let h = hex.replace('#', '')
  if (h.length === 3) h = h.split('').map((c) => c + c).join('')
  if (h.length < 6) return null
  const n = Number.parseInt(h.slice(0, 6), 16)
  if (!Number.isFinite(n)) return null
  return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 }
}

function withAlpha(hex, alpha) {
  const rgb = hexToRgb(hex)
  if (!rgb) return hex
  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${alpha})`
}

function mix(hexA, hexB, t) {
  const a = hexToRgb(hexA)
  const b = hexToRgb(hexB)
  if (!a || !b) return hexA
  const ch = (x, y) => Math.round(x + (y - x) * t)
  return `rgb(${ch(a.r, b.r)}, ${ch(a.g, b.g)}, ${ch(a.b, b.b)})`
}

/** 按相对亮度决定强调色上的前景，避免"浅底浅字"。 */
function readableOn(bgHex) {
  const rgb = hexToRgb(bgHex)
  if (!rgb) return '#ffffff'
  const lin = (c) => {
    const s = c / 255
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4
  }
  const L = 0.2126 * lin(rgb.r) + 0.7152 * lin(rgb.g) + 0.0722 * lin(rgb.b)
  return L > 0.45 ? '#111111' : '#ffffff'
}

function slug(theme, variant) {
  return `${theme}--${variant}`.toLowerCase().replace(/[^a-z0-9._-]+/g, '-')
}

function rawFile(theme, variant) {
  return path.join(RAW, `${theme}_${variant}`.replace(/[^a-z0-9._-]+/g, '-') + '.toml')
}

function buildBlock(theme, variant, colors, tokenMap) {
  const tokens = []
  for (const [tomlKey, tokenName] of Object.entries(tokenMap)) {
    const value = colors[tomlKey]
    if (value) tokens.push(`  --pf-${tokenName}: ${value};`)
  }
  const bg = colors.background ?? '#000000'
  const fg = colors.foreground ?? '#ffffff'
  const accent = colors.accent ?? fg
  const muted = colors.muted ?? fg
  tokens.push(`  --pf-accent-fg: ${readableOn(accent)};`)
  tokens.push(`  --pf-border: ${withAlpha(muted, 0.38)};`)
  tokens.push(`  --pf-border-strong: ${withAlpha(muted, 0.6)};`)
  tokens.push(`  --pf-hover: ${mix(bg, fg, 0.06)};`)
  tokens.push(`  --pf-active: ${mix(bg, fg, 0.12)};`)
  tokens.push(`  --pf-scrollbar: ${withAlpha(muted, 0.5)};`)
  tokens.push(`  --pf-mode: ${colors.mode ?? variant};`)
  return `[data-theme="${slug(theme, variant)}"] {\n${tokens.join('\n')}\n}`
}

const offline = process.argv.includes('--offline')
const fetchOnly = process.argv.includes('--fetch-only')
const useAll = process.argv.includes('--all')
const onlyArg = argv.find((a) => !a.startsWith('--') && a !== rootValue && !a.startsWith('--root='))

let pinnedTransport = null

function jsDelivrUrl(host, repoAtRef, rel) {
  return `https://${host}/gh/${repoAtRef}/${rel}`
}

function githubRawUrl(repo, ref, rel) {
  return `https://raw.githubusercontent.com/${repo}/${ref}/${rel}`
}

function transports(cfg) {
  const repoAtRef = `${cfg.source.repo}@${cfg.source.ref}`
  const list = cfg.hosts.map((h) => ({
    name: h,
    url: (theme, variant) => jsDelivrUrl(h, repoAtRef, `${theme}/${variant}/colors.toml`)
  }))
  list.push({
    name: 'raw.githubusercontent.com',
    url: (theme, variant) => githubRawUrl(cfg.source.repo, cfg.source.ref, `${theme}/${variant}/colors.toml`)
  })
  return list
}

/** 先探一个已知文件把可用源定下来，避免每个文件都把所有源串行试一遍。 */
async function resolveTransport(cfg) {
  if (pinnedTransport) return pinnedTransport
  const probeTheme = cfg.themes[0]
  const probeVariant = cfg.variants[0]
  for (const tr of transports(cfg)) {
    try {
      const res = await fetch(tr.url(probeTheme, probeVariant), { signal: AbortSignal.timeout(8000) })
      if (res.ok) {
        pinnedTransport = tr
        process.stdout.write(`  取文件用 ${tr.name}\n`)
        return tr
      }
      process.stdout.write(`  ${tr.name} 探测到 ${res.status}\n`)
    } catch (err) {
      process.stdout.write(`  ${tr.name} 探测失败：${err instanceof Error ? err.message : String(err)}\n`)
    }
  }
  throw new Error(`所有源都不可用：${transports(cfg).map((t) => t.name).join(', ')}`)
}

async function fetchMissing(cfg, jobs) {
  const first = await resolveTransport(cfg)
  const order = [first, ...transports(cfg).filter((t) => t !== first)]
  let wrote = 0
  const failures = []
  await pool(jobs, 10, async ({ theme, variant }) => {
    let last = 'no transport tried'
    for (const tr of order) {
      const url = tr.url(theme, variant)
      try {
        const res = await fetch(url, { signal: AbortSignal.timeout(10000) })
        if (!res.ok) {
          last = `${res.status} ${res.statusText} — ${url}`
          continue
        }
        const text = await res.text()
        fs.mkdirSync(RAW, { recursive: true })
        fs.writeFileSync(rawFile(theme, variant), text, 'utf8')
        wrote += 1
        return
      } catch (err) {
        last = `${err instanceof Error ? err.message : String(err)} — ${url}`
      }
    }
    failures.push(`${theme}_${variant}: ${last}`)
  })
  process.stdout.write(`  已取回 ${wrote} 个文件并写进 100themes/raw/，下次运行可离线\n`)
  if (failures.length) {
    process.stdout.write(`  取回失败 ${failures.length} 个，前 3 个真因：\n`)
    for (const f of failures.slice(0, 3)) process.stdout.write(`    ${f}\n`)
  }
}

/** 固定大小并发池：串行取几百个小文件在这个网络下要几十分钟。 */
async function pool(items, limit, worker) {
  let cursor = 0
  const runners = Array.from({ length: Math.min(limit, items.length) }, async () => {
    while (cursor < items.length) {
      const index = cursor++
      await worker(items[index], index)
    }
  })
  await Promise.all(runners)
}

function refreshSeed(cfg) {
  const files = fs.existsSync(RAW) ? fs.readdirSync(RAW).filter((f) => f.endsWith('.toml')) : []
  let bytes = 0
  let newest = 0
  for (const f of files) {
    const st = fs.statSync(path.join(RAW, f))
    bytes += st.size
    newest = Math.max(newest, st.mtimeMs)
  }
  cfg.seed = {
    themes: cfg.themes.length,
    variantsPerTheme: cfg.variants.length,
    files: files.length,
    bytes,
    capturedAt: newest ? new Date(newest).toISOString() : null
  }
  if (fs.existsSync(MANIFEST)) {
    const m = JSON.parse(fs.readFileSync(MANIFEST, 'utf8'))
    m.seed = cfg.seed
    fs.writeFileSync(MANIFEST, JSON.stringify(m, null, 2) + '\n', 'utf8')
  }
}

async function main() {
  const cfg = loadConfig()
  const known = new Set(cfg.themes)
  let presetList = null
  if (!onlyArg && !useAll && fs.existsSync(PRESET)) {
    const p = JSON.parse(fs.readFileSync(PRESET, 'utf8'))
    if (!Array.isArray(p.themes)) throw new Error(`${PRESET} 里没有 themes 数组`)
    const unknown = p.themes.filter((t) => !known.has(t))
    if (unknown.length) throw new Error(`preset.json 有 ${unknown.length} 个主题名不在 manifest 里：${unknown.join(', ')}`)
    presetList = p.themes
  }
  const only = onlyArg ? new Set(onlyArg.split(',')) : presetList ? new Set(presetList) : null
  const themes = only ? cfg.themes.filter((t) => only.has(t)) : cfg.themes
  if (!themes.length) throw new Error('主题名清单与命令行筛选没有交集')

  process.stdout.write(`工程根：${ROOT}\n种子目录：${THEME_DIR}\n`)
  process.stdout.write(`主题清单来源：${cfg.from}\n`)
  if (presetList) process.stdout.write(`  白名单 100themes/preset.json：${themes.length}/${cfg.themes.length} 个主题\n`)
  if (onlyArg) process.stdout.write(`  位置参数子集：${onlyArg}（产物会被覆写成只含该子集）\n`)
  process.stdout.write(`处理 ${themes.length} 个主题 × ${cfg.variants.length} 个变体\n`)

  const jobs = themes.flatMap((theme) => cfg.variants.map((variant) => ({ theme, variant })))
  const missing = jobs.filter((j) => !fs.existsSync(rawFile(j.theme, j.variant)))

  if (missing.length) {
    if (offline) {
      const names = missing.slice(0, 10).map((j) => `${j.theme}_${j.variant}`).join(', ')
      throw new Error(
        `--offline 且 100themes/raw 缺 ${missing.length} 个文件（前 10 个：${names}${missing.length > 10 ? ' …' : ''}）`
      )
    }
    process.stdout.write(`  本地缺 ${missing.length}/${jobs.length} 个，走 CDN 兜底\n`)
    await fetchMissing(cfg, missing)
  } else {
    process.stdout.write(`  本地命中 ${jobs.length}/${jobs.length}，未联网\n`)
  }

  if (fetchOnly) {
    const stillMissing = jobs.filter((j) => !fs.existsSync(rawFile(j.theme, j.variant)))
    refreshSeed(cfg)
    process.stdout.write(`--fetch-only：只补 100themes/raw，不写产物；缺口 ${stillMissing.length}/${jobs.length}\n`)
    if (stillMissing.length) process.exitCode = 1
    return
  }

  const results = new Map()
  let failed = 0
  for (const { theme, variant } of jobs) {
    const file = rawFile(theme, variant)
    if (!fs.existsSync(file)) {
      failed += 1
      continue
    }
    const colors = parseColorsToml(fs.readFileSync(file, 'utf8'))
    if (colors.background) results.set(slug(theme, variant), colors)
  }

  const blocks = []
  const index = []
  for (const theme of themes) {
    for (const variant of cfg.variants) {
      const id = slug(theme, variant)
      const colors = results.get(id)
      if (!colors) continue
      blocks.push(buildBlock(theme, variant, colors, cfg.tokenMap))
      index.push({ id, theme, variant, mode: colors.mode ?? variant })
    }
  }

  const whitelist = presetList ? '100themes/preset.json' : onlyArg ? '命令行子集' : 'manifest 全量'
  const header = `/* 由 bin/gen-themes.mjs 自动生成，数据源 ${cfg.source.repo}@${cfg.source.ref}，种子 100themes/raw。
   白名单 ${whitelist}，主题数 ${index.length}，取用失败 ${failed} 个变体。不要手工编辑。 */

:root {
  --pf-radius: 8px;
  --pf-radius-sm: 6px;
  --pf-gap: 10px;
  --pf-gap-sm: 6px;
  --pf-pane-left: 200px;
  --pf-pane-mid: 300px;
  --pf-font: "Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI", system-ui, sans-serif;
  --pf-font-mono: "Cascadia Mono", Consolas, "Microsoft YaHei Mono", monospace;
}

`

  fs.mkdirSync(path.dirname(OUT_CSS), { recursive: true })
  fs.writeFileSync(OUT_CSS, header + blocks.join('\n\n') + '\n', 'utf8')
  fs.writeFileSync(OUT_INDEX, JSON.stringify(index, null, 2), 'utf8')

  refreshSeed(cfg)
  process.stdout.write(
    `已写出 src/styles/themes.css（${index.length} 个主题块）与 themes.index.json（${index.length} 条）\n`
  )
  if (failed) process.stdout.write(`注意：${failed} 个变体没有可用的 background 键，已跳过\n`)
}

main().catch((err) => {
  process.stderr.write(`生成失败：${err instanceof Error ? err.message : String(err)}\n`)
  process.exit(1)
})
