import { build } from 'vite'
import { spawnSync } from 'node:child_process'
import process from 'node:process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.join(HERE, '..')

await build({ root: ROOT, logLevel: 'info' })

const tsc = path.join(ROOT, 'node_modules', 'typescript', 'bin', 'tsc')
const res = spawnSync(process.execPath, [tsc, '-p', path.join(ROOT, 'electron', 'tsconfig.json')], {
  stdio: 'inherit',
  cwd: ROOT
})

if (res.status !== 0) {
  process.stderr.write('main process tsc failed\n')
  process.exit(res.status ?? 1)
}
