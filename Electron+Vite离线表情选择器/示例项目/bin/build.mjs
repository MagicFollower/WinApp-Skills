import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import { build } from 'vite';

const require = createRequire(import.meta.url);
// fileURLToPath, not url.pathname: the project directory name is non-ASCII and
// pathname percent-encodes it, which makes every path below miss.
const ROOT = fileURLToPath(new URL('..', import.meta.url));

if (!existsSync(join(ROOT, 'node_modules/electron/dist/electron.exe'))) {
  console.error('!! electron.exe missing - run: npm run ensure-electron');
  process.exit(1);
}

console.log('==> vite build (renderer)');
await build({ root: ROOT, mode: 'production' });

console.log('==> tsc (electron main + preload)');
const tsc = require.resolve('typescript/bin/tsc');
const r = spawnSync(process.execPath, [tsc, '-p', 'electron/tsconfig.json'], {
  cwd: ROOT,
  stdio: 'inherit'
});
if (r.status !== 0) {
  console.error('!! electron tsc failed');
  process.exit(r.status ?? 1);
}
console.log('==> build done');
