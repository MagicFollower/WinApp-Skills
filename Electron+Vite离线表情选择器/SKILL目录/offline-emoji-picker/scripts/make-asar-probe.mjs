// 不走 electron-builder，手工拼一份走 asar 路径的最小交付物，用来取"零网络"判据。
//
//   node <skill>/scripts/make-asar-probe.mjs --root <工程> [--main dist-electron/main.js]
//
// 为什么要它：electron-builder 在 Windows 上会卡在解包阶段的文件锁（EPERM rename
// win-unpacked.tmp / EBUSY unlink default_app.asar， Defender 与 IDE、Typora、网盘这类
// 常驻监视进程都会抓刚写出来的 app.asar）。等与换输出目录都只算碰运气，
// **绝不要去杀进程**。这条路径绕开 builder，同时照样经过 asar + file:// 两层。
//
// 产出：release-asar/probe/{electron.exe, resources/app.asar}
//      app.asar 的顶层只能是 dist、dist-electron、package.json 三族；多出 node_modules
//      说明有依赖被错登进 dependencies。
//
// 前置：工程已 npm run build；node_modules 里有 @electron/asar 与 electron。

import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const argv = process.argv.slice(2);
const opt = (name, dflt) => {
  const i = argv.indexOf('--' + name);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : dflt;
};
const ROOT = path.resolve(opt('root', process.cwd()));
const require = createRequire(path.join(ROOT, 'noop.js'));

const die = (m, h) => {
  console.error(`FAIL ${m}\n     ${h}`);
  process.exit(1);
};

let asar;
try {
  asar = require('@electron/asar');
} catch {
  die('工程里 require 不到 @electron/asar', 'npm i -D @electron/asar');
}

const DIST = path.join(ROOT, 'dist');
const DELE = path.join(ROOT, 'dist-electron');
if (!fs.existsSync(DELE)) die('没有 dist-electron/', '先 npm run build');
if (!fs.existsSync(DIST)) die('没有 dist/', '先 npm run build');

const RT = path.join(ROOT, 'node_modules/electron/dist');
if (!fs.existsSync(path.join(RT, 'electron.exe')))
  die('没有 node_modules/electron/dist/electron.exe', 'npm run ensure-electron（Electron 44 没有 postinstall，不显式跑就不落地）');

const OUT = path.join(ROOT, 'release-asar', 'probe');
const STAGE = path.join(ROOT, 'release-asar', 'stage');
fs.rmSync(path.join(ROOT, 'release-asar'), { recursive: true, force: true });
fs.cpSync(RT, OUT, { recursive: true });
for (const d of [STAGE, path.join(STAGE, 'dist'), path.join(STAGE, 'dist-electron')]) fs.mkdirSync(d, { recursive: true });
fs.cpSync(DIST, path.join(STAGE, 'dist'), { recursive: true });
fs.cpSync(DELE, path.join(STAGE, 'dist-electron'), { recursive: true });
fs.copyFileSync(path.join(ROOT, 'package.json'), path.join(STAGE, 'package.json'));

const dest = path.join(OUT, 'resources', 'app.asar');
await asar.createPackage(STAGE, dest);

const list = asar.listPackage(dest);
const fam = new Map();
for (const f of list) {
  const top = f.replace(/^[/\\]+/, '').split(/[\\/]/)[0];
  fam.set(top, (fam.get(top) || 0) + 1);
}
const stageBytes = list.reduce((s, f) => {
  const rel = f.replace(/^[/\\]+/, '').split(/[\\/]/).join(path.sep);
  const p = path.join(STAGE, rel);
  return s + (fs.existsSync(p) && fs.statSync(p).isFile() ? fs.statSync(p).size : 0);
}, 0);
const asarBytes = fs.statSync(dest).size;

console.log(`probe dir           : ${OUT}`);
console.log(`app.asar            : ${asarBytes.toLocaleString()} bytes, ${list.length} 条`);
console.log(`顶层族              : ${[...fam.keys()].sort().map((k) => `${k}=${fam.get(k)}`).join('  ')}`);
console.log(`载荷/头部           : ${stageBytes.toLocaleString()} + ${(asarBytes - stageBytes).toLocaleString()} 字节 asar 头`);
console.log('');
const extra = [...fam.keys()].filter((k) => !['dist', 'dist-electron', 'package.json'].includes(k));
if (extra.length) console.log(`FAIL 多出族 ${extra.join(', ')} —— 有运行时依赖被登进 dependencies，builder 会无条件带上 node_modules。`);
else console.log('ok   只有三族');
console.log(`\n跑判据：cd "${OUT}" && ./electron.exe --selftest --netprobe`);
console.log('  期望：network remote=0 local-files=N [...]，带 --netprobe 时 remote=1 且 control-fetch=REJECTED');
if (extra.length) process.exit(1);
