// 零依赖的数据层判据 —— 不需要 sharp、不需要上游位图包，只验"这套文件自洽且够用"。
//
//   node <skill>/scripts/check-data.mjs [--root <工程目录>] [--prefix src/emoji]
//
// 全绿才允许往下走：任何一项 FAIL 都退出码 1。
// 像素级的"格子对齐"不在这里，那是 verify-atlas.mjs（需要 emoji-datasource-apple）。

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const argv = process.argv.slice(2);
const opt = (name, dflt) => {
  const i = argv.indexOf('--' + name);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : dflt;
};
const ROOT = path.resolve(opt('root', process.cwd()));
const PREFIX = opt('prefix', 'src/emoji');
const ATLAS = path.join(ROOT, PREFIX, 'atlas');
const DATA = path.join(ROOT, PREFIX, 'data');

let fails = 0;
const ok = (label, detail) => console.log(`ok    ${label.padEnd(26)} ${detail}`);
const fail = (label, detail) => {
  console.log(`FAIL  ${label.padEnd(26)} ${detail}`);
  fails++;
};
const need = (p) => {
  if (!fs.existsSync(p)) {
    fail('文件存在', `缺 ${p.replace(ROOT + path.sep, '')} —— 先跑 install.mjs`);
    return false;
  }
  return true;
};

if (!need(path.join(ATLAS, 'index.json')) || !need(path.join(DATA, 'picker.json'))) process.exit(1);

const idx = JSON.parse(fs.readFileSync(path.join(ATLAS, 'index.json'), 'utf8'));
const pick = JSON.parse(fs.readFileSync(path.join(DATA, 'picker.json'), 'utf8'));
const records = Object.values(pick.emojis).flat();
const CJK_RE = /[\u4e00-\u9fff\u3000-\u303f\uff00-\uffef]/;
const isCjk = (t) => CJK_RE.test(t);

// 1. 页文件齐、且没有多余页
const declared = idx.atlases.map((a) => a.file);
const present = fs.readdirSync(ATLAS).filter((f) => f.endsWith('.webp')).sort();
const missingPages = declared.filter((f) => !present.includes(f));
const extraPages = present.filter((f) => !declared.includes(f));
if (missingPages.length) fail('页文件齐全', `缺 ${missingPages.join(', ')}`);
else if (extraPages.length) fail('页文件齐全', `多出无人引用的 ${extraPages.join(', ')}`);
else ok('页文件齐全', `${present.length} 张 WebP 与 index.json 一一对应`);

// 2. 每张页的字节数与清单一致（能抓到拷贝被截断）
const byteMismatch = idx.atlases.filter((a) => fs.statSync(path.join(ATLAS, a.file)).size !== a.bytes);
if (byteMismatch.length)
  fail('页字节对得上', byteMismatch.map((a) => `${a.file} 期望${a.bytes} 实得${fs.statSync(path.join(ATLAS, a.file)).size}`).join('; '));
else ok('页字节对得上', `合计 ${idx.atlases.reduce((s, a) => s + a.bytes, 0).toLocaleString()} 字节`);

// 3. 页几何自洽 + 不越纹理上限
const CELL = idx.cell;
const geoBad = idx.atlases.filter(
  (a) => a.width !== a.cols * CELL || a.height !== a.rows * CELL || a.glyphs > a.cols * a.rows
);
if (geoBad.length) fail('页几何', geoBad.map((a) => `${a.file}: ${a.cols}x${a.rows} vs ${a.width}x${a.height}`).join('; '));
else ok('页几何', `cell=${CELL}，每页尺寸 = 行列数 × cell`);

const MAX_EDGE = Math.max(...idx.atlases.map((a) => Math.max(a.width, a.height)));
if (MAX_EDGE > 4096) fail('纹理上限', `最大边 ${MAX_EDGE}px 顶穿 4096，Chromium 会画不出这页`);
else ok('纹理上限', `最大边 ${MAX_EDGE}px（≤4096）`);

// 4. 格子坐标对齐 + 同页不重叠
let unaligned = 0;
let outOfBounds = 0;
const cells = new Map();
let overlap = 0;
for (const [u, g] of Object.entries(idx.glyphs)) {
  const a = idx.atlases[g.a];
  if (!a) {
    outOfBounds++;
    continue;
  }
  if (g.x % CELL || g.y % CELL) unaligned++;
  if (g.x + CELL > a.width || g.y + CELL > a.height) outOfBounds++;
  const key = `${g.a}:${g.x}:${g.y}`;
  if (cells.has(key)) overlap++;
  else cells.set(key, u);
}
if (unaligned) fail('格子坐标对齐', `${unaligned} 个格子的 x/y 不是 ${CELL} 的整数倍`);
else ok('格子坐标对齐', `${Object.keys(idx.glyphs).length} 个格子全部落格`);
if (outOfBounds) fail('格子不越页', `${outOfBounds} 个格子超出所属页`);
else ok('格子不越页', '每个格子都在自己页的尺寸内');
if (overlap) fail('格子不重叠', `${overlap} 处两个码位抢同一格 —— 拼版一定串行过`);
else ok('格子不重叠', `${cells.size} 个格子各属一个码位`);

// 5. 清单计数与页数吻合
const sumGlyphs = idx.atlases.reduce((s, a) => s + a.glyphs, 0);
if (sumGlyphs !== Object.keys(idx.glyphs).length)
  fail('格子总数吻合', `页声明 ${sumGlyphs}，映射表 ${Object.keys(idx.glyphs).length}`);
else ok('格子总数吻合', `${sumGlyphs} = Σ 各页`);

// 6. 词表引用的每个码位都有格子（这条反着挂过：有词没图 = 点出来是空白）
const noGlyph = records.filter((r) => !idx.glyphs[r.u]);
const noGlyphVar = records.flatMap((r) => (r.v || []).filter((v) => !idx.glyphs[v]));
if (noGlyph.length || noGlyphVar.length)
  fail('词表 ⊆ 格子', `基形缺 ${noGlyph.length}（${noGlyph.slice(0, 3).map((r) => r.u)}），变体缺 ${noGlyphVar.length}`);
else ok('词表 ⊆ 格子', `${records.length} 条 + ${records.reduce((s, r) => s + (r.v?.length || 0), 0)} 变体全部有格子`);

// 7. 未被引用的图（不致命，但说明拼版和词表漂移了）
const orphans = [...cells.keys()].length - (records.length + records.reduce((s, r) => s + (r.v?.length || 0), 0));
if (orphans !== 0) ok('格子数与词表差', `${orphans > 0 ? '多出' : '少于'} ${Math.abs(orphans)} 个（有图无词，可接受，记一笔）`);
else ok('格子数与词表差', '完全吻合');

// 8. 词表内容健康：非空、无空串、类型正确、无重复
const empty = records.filter((r) => !Array.isArray(r.n) || r.n.length === 0);
const blankTok = records.flatMap((r) => r.n.filter((t) => typeof t !== 'string' || t === ''));
const spaceTok = records.flatMap((r) => r.n.filter((t) => typeof t === 'string' && t !== '' && t.trim() === ''));
const punctTok = [...new Set(records.flatMap((r) => r.n.filter((t) => /^[^\w\u4e00-\u9fa5]+$/.test(t))))];
if (empty.length) fail('每条都有关键词', `${empty.length} 条 n 为空`);
else ok('每条都有关键词', `${records.length} 条`);
if (blankTok.length) fail('无空串 token', `${blankTok.length} 个空串 —— 子串匹配下它会匹配任何输入`);
else ok('无空串 token', `${spaceTok.length} 个纯空白 token 归到下面标点噪声里，不判失败`);
if (punctTok.length)
  ok('标点噪声（已知）', `${punctTok.length} 种纯标点/空白 token（${punctTok.slice(0, 4).map((t) => JSON.stringify(t)).join(' ')}），来自 pinyin() 原样返回标点；不影响中文输入，但搜它会有几百条命中`);
else ok('标点噪声', '无');

// 9. 分类闭环：emojis 的每个 key 都在 categories 里有名字（否则库回落英文标签）
const catKeys = Object.keys(pick.emojis);
const nameless = catKeys.filter((k) => !pick.categories[k] || !pick.categories[k].name);
const nonLatin = catKeys.concat(Object.keys(pick.categories)).filter((k) => pick.categories[k] && /[A-Za-z]/.test(pick.categories[k].name));
if (nameless.length) fail('分类名齐全', `${nameless.join(', ')} 没有 name`);
else ok('分类名齐全', `${Object.keys(pick.categories).length} 个分类都有中文名`);
if (nonLatin.length) ok('分类名含英文（已知）', `${nonLatin.length} 个（${nonLatin.slice(0, 4).join(' ')}）—— 上游本来就带拉丁字母，不是回归`);

// 10. 搜索面抽样：四类各抽一个必须存在
const tok = new Set(records.flatMap((r) => r.n));
const samples = [
  ['中文原词', '笑脸'],
  ['整词全拼', 'kaixin'],
  ['首字母', 'kx'],
  ['英文', 'heart']
];
const absent = samples.filter(([, t]) => !tok.has(t)).map(([, t]) => t);
if (absent.length) fail('搜索面抽样', `缺 ${absent.join(', ')}`);
else ok('搜索面抽样', samples.map(([l, t]) => `${l}=${t}`).join(' '));

// 11. 别名闭环：aliases.json 里每条都得真的落进对应记录
const aliasPath = path.join(DATA, 'aliases.json');
if (fs.existsSync(aliasPath)) {
  const list = JSON.parse(fs.readFileSync(aliasPath, 'utf8'));
  const dropped = list.filter((a) => !records.some((r) => a.u.includes(r.u) && r.n.includes(a.t)));
  if (dropped.length) fail('别名闭环', `${dropped.length} 条别名没进词表：${dropped.map((a) => a.t).join(', ')}`);
  else ok('别名闭环', `${list.length} 条全部生效`);
}

const allTok = records.flatMap((r) => r.n);
console.log('');
console.log(`records ${records.length}  categories ${Object.keys(pick.categories).length}  glyphs ${Object.keys(idx.glyphs).length}  pages ${idx.atlases.length}`);
console.log(`tokens total ${allTok.length}  distinct ${tok.size}  avg ${(allTok.length / records.length).toFixed(1)}  CJK ${allTok.filter((t) => isCjk(t)).length}  ascii ${allTok.filter((t) => /^[a-z0-9+]+$/.test(t)).length}`);
console.log(fails ? `\nFAIL ${fails} 项 —— 这套数据不能用，先修上面标出来的那条。` : '\nALL PASS 0 fail');
process.exit(fails ? 1 : 0);
