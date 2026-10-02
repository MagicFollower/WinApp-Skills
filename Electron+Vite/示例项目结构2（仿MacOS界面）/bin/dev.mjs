import { createServer } from 'vite'
import { spawn, spawnSync } from 'node:child_process'
import { createRequire } from 'node:module'
import process from 'node:process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const require = createRequire(import.meta.url)
const electronBin = require('electron')

const HERE = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.join(HERE, '..')

// package.json "main" points at dist-electron/, so the CJS entry must exist before Electron starts.
const tsc = path.join(ROOT, 'node_modules', 'typescript', 'bin', 'tsc')
const compiled = spawnSync(process.execPath, [tsc, '-p', path.join(ROOT, 'electron', 'tsconfig.json')], {
  stdio: 'inherit',
  cwd: ROOT
})
if (compiled.status !== 0) {
  process.stderr.write('electron tsconfig compile failed\n')
  process.exit(compiled.status ?? 1)
}

const server = await createServer({ root: ROOT, mode: 'development' })
await server.listen()

const url = server.resolvedUrls?.local[0]
if (!url) {
  process.stderr.write('vite gave no local url\n')
  await server.close()
  process.exit(1)
}

const child = spawn(electronBin, ['.', ...process.argv.slice(2)], {
  cwd: ROOT,
  stdio: 'inherit',
  env: { ...process.env, VITE_DEV_SERVER_URL: url }
})

child.on('exit', code => {
  void server.close().then(() => process.exit(code ?? 0))
})
