// 把技能里的离线表情数据与组件模板落进目标工程。
//
//   node <skill>/scripts/install.mjs [--root <工程目录>] [--prefix src/emoji] [--force] [--dry-run]
//
// 只读技能 assets/、只写 <prefix>/ 下的文件；不碰宿主其它源码，不改 package.json。
// 幂等：字节相同的文件报 skip，不同则报 write（--force 之外遇到已存在文件会被拦住）。

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const SKILL = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const ASSETS = path.join(SKILL, 'assets');

const argv = process.argv.slice(2);
const flag = (name, dflt) => {
  const i = argv.indexOf('--' + name);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : i >= 0 ? true : dflt;
};
const ROOT = path.resolve(flag('root', process.cwd()));
const PREFIX = String(flag('prefix', 'src/emoji'));
const FORCE = flag('force', false);
const DRY = flag('dry-run', false);

const die = (msg) => {
  console.error(`FAIL ${msg}`);
  process.exit(1);
};

if (!fs.existsSync(path.join(ROOT, 'package.json'))) {
  die(`目标不像工程根（没有 package.json）：${ROOT}\n     用 --root <工程目录> 指过去。`);
}

const manifest = JSON.parse(fs.readFileSync(path.join(ASSETS, 'data/manifest.json'), 'utf8'));

// data/atlas/* -> <prefix>/atlas/* ; data/<other> -> <prefix>/data/<other> ; src/* -> <prefix>/*
function targetOf(rel) {
  if (rel.startsWith('data/atlas/')) return path.join(PREFIX, 'atlas', rel.slice('data/atlas/'.length));
  if (rel.startsWith('data/')) return path.join(PREFIX, 'data', rel.slice('data/'.length));
  if (rel.startsWith('src/')) return path.join(PREFIX, rel.slice('src/'.length));
  return null; // snippets 由人读，不落工程
}

const written = [];
const skipped = [];
const blocked = [];
let missing = 0;

for (const item of manifest.files) {
  const rel = item.path;
  const tRel = targetOf(rel);
  if (!tRel) continue;
  const src = path.join(ASSETS, rel.split('/').join(path.sep));
  if (!fs.existsSync(src)) {
    console.log(`MISSING  ${rel}`);
    missing++;
    continue;
  }
  const buf = fs.readFileSync(src);
  const sha = crypto.createHash('sha256').update(buf).digest('hex').slice(0, 16);
  if (sha !== item.sha256) {
    console.log(`CORRUPT  ${rel} 期望 ${item.sha256} 实得 ${sha}`);
    missing++;
    continue;
  }
  const dst = path.join(ROOT, tRel.split('/').join(path.sep));
  const exists = fs.existsSync(dst);
  if (exists && fs.readFileSync(dst).equals(buf)) {
    skipped.push(tRel);
    continue;
  }
  if (exists && !FORCE && /\.(ts|tsx)$/.test(tRel)) {
    // 组件模板可能被宿主改过，默认不覆盖。
    blocked.push(tRel);
    continue;
  }
  if (!DRY) {
    fs.mkdirSync(path.dirname(dst), { recursive: true });
    fs.writeFileSync(dst, buf);
  }
  written.push(`${tRel} (${item.bytes.toLocaleString()} B)`);
}

const report = (label, rows) => {
  if (!rows.length) return;
  console.log(`\n${label} ${rows.length}`);
  rows.forEach((r) => console.log('  ' + r));
};
report(DRY ? 'would write' : 'written', written);
report('unchanged (skip)', skipped);
report('exists, 未覆盖（加 --force 才动）', blocked);

if (missing) die(`${missing} 个资产缺失或哈希不符 —— 技能目录本身坏了，重新取一份。`);

// 前置条件检查：这两条不满足时是编译期或运行期的静默故障，值得现在就报。
const tsPath = path.join(ROOT, 'tsconfig.json');
if (fs.existsSync(tsPath)) {
  const raw = fs.readFileSync(tsPath, 'utf8').replace(/\/\/.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
  if (!/"resolveJsonModule"\s*:\s*true/.test(raw)) {
    console.log('\nWARN tsconfig.json 没有 "resolveJsonModule": true');
    console.log('     组件用 `import x from "./data/picker.json"`，缺它会 TS2732 直接编不过。');
  }
  if (!/"vite\/client"/.test(raw)) {
    console.log('\nWARN tsconfig 的 types 里没有 "vite/client"');
    console.log('     import 那个 .css 会报找不到模块声明。');
  }
}
const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, 'package.json'), 'utf8'));
const deps = { ...pkg.dependencies, ...pkg.devDependencies };
for (const need of ['emoji-picker-react', 'react', 'react-dom']) {
  if (!deps[need]) console.log(`WARN package.json 缺依赖 ${need}（要 5.x / 19.x）`);
}
if (deps['emoji-picker-react'] && !/^(\^|~)?5\./.test(deps['emoji-picker-react'])) {
  console.log(`WARN emoji-picker-react 版本是 ${deps['emoji-picker-react']}，本技能按 5.x 的 primitives 子入口写）`);
}

console.log('\n==> 落位完成');
console.log(`    ${PREFIX}/atlas/            10 张 WebP + index.json`);
console.log(`    ${PREFIX}/data/            picker.json + aliases.json`);
console.log(`    ${PREFIX}/atlas.ts         sprite 定位`);
console.log(`    ${PREFIX}/SpriteCell.tsx   自绘格子`);
console.log(`    ${PREFIX}/Picker.tsx       选择器组合`);
console.log(`    ${PREFIX}/picker.css       需要的最小样式`);
console.log('\n下一步（顺序有意义）：');
console.log(`  1) 在宿主入口里引一次样式：${PREFIX}/picker.css`);
console.log('     （按入口文件的相对位置改写，例如 src/main.tsx 里写 "./emoji/picker.css"）');
console.log(`  2) node <skill>/scripts/check-data.mjs --root "${ROOT}"   # 零依赖判据，必须先全绿`);
console.log('  3) 宿主自己跑 typecheck / build');
console.log('  4) 接线四个易错点见 assets/snippets/host-usage.tsx；离线断言见 assets/snippets/offline-guard.ts');
