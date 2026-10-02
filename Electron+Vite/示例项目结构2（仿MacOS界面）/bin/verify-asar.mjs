import { listPackage } from '@electron/asar'
import process from 'node:process'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

/**
 * 核对 app.asar 只含 electron-builder.yml 里 files 白名单的三族条目。
 * 不写白名单时源码目录与 node_modules 会一起进包（实测多带 1198 项 / 约 18 MB），
 * 所以这条断言是打包口径的守门，而不是格式检查。
 *
 *   node bin/verify-asar.mjs [release/win-unpacked/resources/app.asar]
 */

const HERE = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.join(HERE, '..')
const target = process.argv[2]
  ? path.resolve(ROOT, process.argv[2])
  : path.join(ROOT, 'release', 'win-unpacked', 'resources', 'app.asar')

if (!fs.existsSync(target)) {
  process.stderr.write(`!! asar not found: ${target}\n`)
  process.exit(1)
}

const ALLOWED = ['dist', 'dist-electron', 'package.json']
const entries = listPackage(target)
const bad = []

for (const raw of entries) {
  const rel = raw.replace(/^[\\/]+/, '').replace(/\\/g, '/')
  if (!rel) continue
  const top = rel.split('/')[0]
  if (!ALLOWED.includes(top)) bad.push(rel)
}

process.stdout.write(`asar=${path.relative(ROOT, target)}\n`)
process.stdout.write(`entries=${entries.length}\n`)
const families = [...new Set(entries.map(e => e.replace(/^[\\/]+/, '').replace(/\\/g, '/').split('/')[0]))].sort()
process.stdout.write(`families=${JSON.stringify(families)}\n`)
process.stdout.write(`outside-whitelist=${bad.length}\n`)
if (bad.length) {
  for (const b of bad.slice(0, 10)) process.stdout.write(`  ${b}\n`)
  process.exit(1)
}
process.stdout.write('result=ok\n')
