// Builds an asar-code-path probe without running electron-builder: copies the
// local Electron runtime to a fresh directory and drops a hand-packed
// resources/app.asar next to it, so Chromium reads the delivery artifacts
// through asar rather than a plain directory.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const asar = require('@electron/asar');
const ROOT = fileURLToPath(new URL('..', import.meta.url));
const OUT = path.join(ROOT, 'release-asar', 'asar-probe');
const STAGE = path.join(ROOT, 'release-asar', 'stage');

fs.rmSync(path.join(ROOT, 'release-asar'), { recursive: true, force: true });
fs.cpSync(path.join(ROOT, 'node_modules/electron/dist'), OUT, { recursive: true });
for (const d of [STAGE, path.join(STAGE, 'dist'), path.join(STAGE, 'dist-electron')]) fs.mkdirSync(d, { recursive: true });
fs.cpSync(path.join(ROOT, 'dist'), path.join(STAGE, 'dist'), { recursive: true });
fs.cpSync(path.join(ROOT, 'dist-electron'), path.join(STAGE, 'dist-electron'), { recursive: true });
fs.copyFileSync(path.join(ROOT, 'package.json'), path.join(STAGE, 'package.json'));

const dest = path.join(OUT, 'resources', 'app.asar');
await asar.createPackage(STAGE, dest);

const list = asar.listPackage(dest);
const families = new Map();
for (const f of list) {
  const top = f.replace(/^[/\\]+/, '').split(/[\\/]/)[0];
  families.set(top, (families.get(top) || 0) + 1);
}
console.log(`app.asar        : ${fs.statSync(dest).size.toLocaleString()} bytes`);
console.log(`asar entries    : ${list.length}, top-level families: ${[...families.keys()].sort().join(', ')}`);
console.log(`per family      : ${[...families.entries()].sort().map(([k, v]) => `${k}=${v}`).join('  ')}`);
console.log(`webp inside     : ${list.filter((f) => f.endsWith('.webp')).length}`);
console.log(`runtime copied  : ${OUT}`);
console.log(`electron.exe    : ${fs.existsSync(path.join(OUT, 'electron.exe'))}`);
