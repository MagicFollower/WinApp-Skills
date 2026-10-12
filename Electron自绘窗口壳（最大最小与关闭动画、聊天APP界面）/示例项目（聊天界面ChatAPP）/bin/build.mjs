import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { build } from 'vite';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const tsc = spawnSync(
  process.execPath,
  [path.join(ROOT, 'node_modules', 'typescript', 'bin', 'tsc'), '-p', 'electron/tsconfig.json'],
  { cwd: ROOT, stdio: 'inherit' },
);
if (tsc.status !== 0) process.exit(tsc.status ?? 1);

await build({ root: ROOT });
console.log('[build] done');
