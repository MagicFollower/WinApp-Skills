#!/usr/bin/env node
/* ============================================================================
   prove-audit.mjs —— 证明 check-wiring.mjs 真的会失败(不是装饰性输出)

   做法:用 **本技能自己的 assets/** 现搭一个最小工程到临时目录,
   先跑一次审计当基线(应当 0 FAIL),再逐个注入 12 种已实测过的接线缺陷,
   每次只看"这一条改动让哪条判定翻转"。逐条归因,不会被别的变异污染。

   用法:
     node <skill>/scripts/prove-audit.mjs [--keep]        # --keep 保留临时工程便于手查
   退出码:0 = 基线干净且每条变异都被抓到(或按预期只是 WARN);1 = 审计脚本退化/漏抓。

   什么时候要跑:改了 check-wiring.mjs 之后;或换了目标工程的目录约定(扫描面变宽/变窄)之后。
   ========================================================================== */

import { mkdirSync, rmSync, cpSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';

const HERE = dirname(fileURLToPath(import.meta.url));
const SKILL = join(HERE, '..');
const AUDIT = join(SKILL, 'scripts', 'check-wiring.mjs');
const ASSETS = join(SKILL, 'assets');
const FX = join(tmpdir(), 'electw-chrome-fixture');
const MUT = join(tmpdir(), 'electw-chrome-mutant');

for (const f of ['WindowChrome.tsx', 'window-chrome.css', 'window-api.d.ts', 'preload-window-chrome.ts', 'window-chrome-main.ts']) {
  if (!existsSync(join(ASSETS, f))) {
    console.log(`FAIL 缺资产:${f}`);
    process.exit(1);
  }
}

/* ---------- 基线工程 = 资产原样落位 + 三行宿主代码 ---------- */
rmSync(FX, { recursive: true, force: true });
mkdirSync(join(FX, 'src', 'components'), { recursive: true });
mkdirSync(join(FX, 'electron'), { recursive: true });
cpSync(join(ASSETS, 'WindowChrome.tsx'), join(FX, 'src', 'components', 'WindowChrome.tsx'));
cpSync(join(ASSETS, 'window-chrome.css'), join(FX, 'src', 'window-chrome.css'));
cpSync(join(ASSETS, 'window-api.d.ts'), join(FX, 'src', 'window-chrome.d.ts'));
cpSync(join(ASSETS, 'preload-window-chrome.ts'), join(FX, 'electron', 'preload.ts'));
cpSync(join(ASSETS, 'window-chrome-main.ts'), join(FX, 'electron', 'window-chrome.ts'));

writeFileSync(join(FX, 'electron', 'main.ts'), `import { app, BrowserWindow } from 'electron';
import { join } from 'node:path';
import { attachWindowChrome } from './window-chrome';

function createWindow() {
  const win = new BrowserWindow({
    width: 1000,
    height: 700,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    webPreferences: { preload: join(__dirname, 'preload.js'), sandbox: true, contextIsolation: true },
  });
  attachWindowChrome(win);
  void win.loadFile(join(__dirname, '../dist/index.html'));
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => app.quit());
`);

writeFileSync(join(FX, 'src', 'App.tsx'), `import WindowChrome from './components/WindowChrome';

export default function App() {
  return (
    <div className="window">
      <header className="drag-region titlebar">
        <h1>示例应用</h1>
        <input className="no-drag" placeholder="带内控件要标 no-drag" />
      </header>
      <main>正文…</main>
      <WindowChrome />
    </div>
  );
}
`);

writeFileSync(join(FX, 'src', 'main.tsx'), `import { createRoot } from 'react-dom/client';
import App from './App';
import './window-chrome.css';

createRoot(document.getElementById('root')!).render(<App />);
`);

writeFileSync(join(FX, 'package.json'), `{
  "name": "electw-chrome-fixture",
  "version": "0.0.0",
  "private": true,
  "main": "dist-electron/main.js",
  "dependencies": { "react": "^18.3.1", "react-dom": "^18.3.1" },
  "devDependencies": { "electron": "^44.7.0" }
}
`);

/* ---------- 变异表:每条只改一处锚点 ---------- */
const MUTS = [
  ['M1  drag-start 里就还原(= 单击即恢复那个真 bug)', 'electron/window-chrome.ts',
    (t) => t.replace('dragAnchor = { wx: b.x', 'win.unmaximize();\n    dragAnchor = { wx: b.x'), 'FAIL:slop-gate'],
  ['M2  CSS 加回 -webkit-app-region: drag', 'src/window-chrome.css',
    (t) => t + '\n.drag-region {\n  -webkit-app-region: drag;\n}\n', 'FAIL:app-region'],
  ['M3  clamp 常量与 CSS 盒尺寸不符(120→128)', 'src/components/WindowChrome.tsx',
    (t) => t.replace('WIN_MENU_W = 120', 'WIN_MENU_W = 128'), 'FAIL:menu-box'],
  ['M4  组件没挂进渲染树', 'src/App.tsx', (t) => t.replace('<WindowChrome />', ''), 'FAIL:component-mounted'],
  ['M5  退场期间不提升合成层(只该 WARN)', 'src/window-chrome.css',
    (t) => t.replace('  will-change: transform, opacity;\n', ''), 'warn:exit-layer'],
  ['M6  收拢点不是底边中心', 'src/window-chrome.css',
    (t) => t.replace('transform-origin: 50% 100%;', 'transform-origin: 50% 50%;'), 'FAIL:exit-origin'],
  ['M7  preload 的 dragMove 改成传坐标', 'electron/preload.ts',
    (t) => t.replace("dragMove: () => ipcRenderer.send('window:drag-move')", "dragMove: (x, y) => ipcRenderer.send('window:drag-move', x, y)"), 'FAIL:drag-no-coords'],
  ['M8  根元素没有 transform 过渡', 'src/window-chrome.css',
    (t) => t.replace(/\.window \{\n  transition:[\s\S]*?\n\}\n/, '.window {\n  color: red;\n}\n'), 'FAIL:exit-transition'],
  ['M9  主进程不再推 restore', 'electron/window-chrome.ts',
    (t) => t.replace('win.webContents.send(restoreChannel)', 'void 0'), 'FAIL:restore-main'],
  ['M10 位移不走主进程光标', 'electron/window-chrome.ts',
    (t) => t.split('screen.getCursorScreenPoint()').join('fakeCursor()'), 'FAIL:cursor-in-main'],
  ['M11 maximize 只朝一个方向', 'electron/window-chrome.ts',
    (t) => t.replace('if (win.isMaximized()) win.unmaximize();\n  else win.maximize();', 'win.maximize();'), 'FAIL:max-toggle'],
  ['M12 摘类前不强制回流', 'src/components/WindowChrome.tsx',
    (t) => t.replace('void root.offsetWidth;', '/* noop */'), 'FAIL:comp:offsetWidth'],
];

function run(root) {
  const r = spawnSync('node', [AUDIT, '--root', root, '--verbose'], { encoding: 'utf8' });
  if (r.error) { console.log('审计脚本跑不起来:' + r.error.message); process.exit(1); }
  const ids = new Set();
  for (const line of r.stdout.split('\n')) {
    const m = /^\[(ok|FAIL|warn)\] (\S+)/.exec(line);
    if (m) ids.add(`${m[1] === 'ok' ? 'ok' : m[1]}:${m[2]}`);
  }
  return { rc: r.status, ids, out: r.stdout };
}

const base = run(FX);
const baseFails = [...base.ids].filter((x) => x.startsWith('FAIL:'));
console.log(`基线(资产直接落位):rc=${base.rc}  FAIL=${baseFails.length}${baseFails.length ? ' → ' + baseFails.join(', ') : ''}`);
if (base.rc !== 0) {
  console.log('资产本身接线不全,后面的变异测试没有意义 —— 先修 assets。');
  rmSync(MUT, { recursive: true, force: true });
  process.exit(1);
}

let missed = 0;
const lines = [];
for (const [name, rel, edit, expect] of MUTS) {
  rmSync(MUT, { recursive: true, force: true });
  cpSync(FX, MUT, { recursive: true });
  const p = join(MUT, rel);
  const src = readFileSync(p, 'utf8');
  const next = edit(src);
  if (next === src) { missed++; lines.push(`${name}\n   锚点没命中 ⇒ 变异等于没跑(这条不算覆盖,资产文本已变,要更新锚点)`); continue; }
  writeFileSync(p, next);
  const r = run(MUT);
  const flipped = [...r.ids].filter((x) => !base.ids.has(x));
  const got = flipped.find((x) => x === expect || (expect.startsWith('FAIL:') && x.startsWith(expect)));
  if (r.rc === base.rc && expect.startsWith('FAIL:')) { missed++; lines.push(`${name}\n   rc 仍是 ${r.rc} ⇒ 这条缺陷没被抓到(漏抓)`); continue; }
  if (!got) { missed++; lines.push(`${name}\n   期望 ${expect},实际翻转: ${flipped.join(' , ') || '(无)'}`); continue; }
  lines.push(`${name}\n   rc ${base.rc} → ${r.rc} | 抓到 ${got}`);
}

if (process.argv.includes('--keep')) console.log(`临时工程留在:${FX}`);
else { rmSync(FX, { recursive: true, force: true }); rmSync(MUT, { recursive: true, force: true }); }

console.log('');
for (const l of lines) console.log(l);
console.log('');
console.log(missed ? `${missed}/${MUTS.length} 条变异没被按预期抓到 —— 审计脚本不可信,别拿它的绿灯当验收。` :
  `${MUTS.length}/${MUTS.length} 条变异全部按预期翻转。审计脚本的绿灯是可信的(它确实会红)。`);
process.exit(missed ? 1 : 0);
