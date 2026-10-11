import { createRequire } from 'node:module';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createServer } from 'vite';

const require = createRequire(import.meta.url);
// fileURLToPath, not url.pathname: the project directory name is non-ASCII and
// pathname percent-encodes it, so Electron would get a bogus cwd.
const ROOT = fileURLToPath(new URL('..', import.meta.url));
const electronBin = require('electron');

const server = await createServer({ root: ROOT, mode: 'development' });
await server.listen();

const url = server.resolvedUrls.local[0];
console.log(`vite dev server: ${url}`);

const child = spawn(electronBin, ['.'], {
  cwd: ROOT,
  stdio: 'inherit',
  env: { ...process.env, VITE_DEV_SERVER_URL: url }
});

child.on('exit', async (code, signal) => {
  await server.close();
  process.exit(signal ? 1 : (code ?? 0));
});
