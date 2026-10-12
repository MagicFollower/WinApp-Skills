import { createRequire } from 'node:module';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { createServer } from 'vite';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(import.meta.url);
const electronBin = require('electron');

const server = await createServer({ root: ROOT, mode: 'development' });
await server.listen();
const url = server.resolvedUrls.local[0];
console.log('[dev] vite:', url);

const child = spawn(electronBin, ['.'], {
  cwd: ROOT,
  stdio: 'inherit',
  env: { ...process.env, VITE_DEV_SERVER_URL: url },
});

child.on('exit', async (code) => {
  await server.close();
  process.exit(code ?? 0);
});
