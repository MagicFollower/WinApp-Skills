// 重烘搜索面 —— 把中文词 + 全拼 + 首字母 + 逐音节 + 英文名 + 手写别名并进每条记录的 n。
//
//   node <skill>/scripts/gen-index.mjs --root <工程> [--prefix src/emoji]
//
// 前置：目标工程能 require 到 pinyin-pro，且 <prefix>/atlas/index.json 已就位（先跑 gen-atlas）。
//   npm i -D pinyin-pro
//
// 为什么必须构建期做：库的匹配器只对 n 做子串匹配，没有拼音运行时，也没有排序钩子。
// 上游 zh 数据的 6081 个去重 token 里纯 ASCII 只有 41 个，所以不烘就是"只能搜中文词"。
//
// 顺序不能颠倒：先 gen-atlas 再 gen-index —— 本脚本按 atlas/index.json 筛掉没格子的字形，
// 方向是"图缺则词删"，反过来就会出现"搜得到却画不出"的空白格。

import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const argv = process.argv.slice(2);
const opt = (name, dflt) => {
  const i = argv.indexOf('--' + name);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : dflt;
};
const ROOT = path.resolve(opt('root', process.cwd()));
const PREFIX = opt('prefix', 'src/emoji');
const require = createRequire(path.join(ROOT, 'noop.js'));

const die = (m, h) => {
  console.error(`FAIL ${m}\n     ${h}`);
  process.exit(1);
};

let pinyin;
try {
  ({ pinyin } = require('pinyin-pro'));
} catch {
  die('目标工程 require 不到 pinyin-pro', 'npm i -D pinyin-pro');
}

const ZH = path.join(ROOT, 'node_modules/emoji-picker-react/dist/data/emojis-zh.json');
const DS = opt('pretty', path.join(ROOT, 'node_modules/emoji-datasource-apple/emoji_pretty.json'));
const ATLAS = path.join(ROOT, PREFIX, 'atlas/index.json');
const ALIASES = path.join(ROOT, PREFIX, 'data/aliases.json');
const OUT = path.join(ROOT, PREFIX, 'data/picker.json');

for (const [label, p] of [['上游中文词表', ZH], ['atlas 清单', ATLAS]]) {
  if (!fs.existsSync(p)) die(`缺${label}`, `${p}\n     先装 emoji-picker-react@5 / 跑 gen-atlas.mjs`);
}
if (!fs.existsSync(DS)) {
  console.log(`WARN 没有 ${DS}`);
  console.log('     英文名那一路补不进词表（heart/flag/thumbs 会搜不到），只出中文+拼音版。');
}

const CJK = /[\u4e00-\u9fff]/;
const zh = JSON.parse(fs.readFileSync(ZH, 'utf8'));
const ds = fs.existsSync(DS) ? JSON.parse(fs.readFileSync(DS, 'utf8')) : [];
const atlas = JSON.parse(fs.readFileSync(ATLAS, 'utf8'));

const enNames = new Map(ds.map((e) => [e.unified.toLowerCase(), e]));
const aliasList = fs.existsSync(ALIASES) ? JSON.parse(fs.readFileSync(ALIASES, 'utf8')) : [];
const aliasByUnified = new Map();
for (const a of aliasList) for (const u of a.u) {
  if (!aliasByUnified.has(u)) aliasByUnified.set(u, []);
  aliasByUnified.get(u).push(a.t);
}

// 上游 zh 的分类名繁简混排且不对题（smileys_people 叫"人体"、objects 叫"物件"），
// 而 suggested/custom 在 emojis 里没有条目 —— 不显式给名字，库会回落英文标签。
const nameOverrides = {
  smileys_people: '笑脸与人物',
  food_drink: '食物饮料',
  travel_places: '旅游地点',
  objects: '物品',
  symbols: '符号',
  suggested: '常用',
  custom: '自定义表情'
};

const categories = {};
const emojis = {};
let pinyinInsertions = 0;
let englishInsertions = 0;
let aliasInsertions = 0;
let dropped = 0;

for (const id of Object.keys(zh.categories)) {
  if (!(id in nameOverrides)) continue;
  categories[id] = { category: id, name: nameOverrides[id] };
}

for (const cat of Object.keys(zh.emojis)) {
  const kept = [];
  for (const rec of zh.emojis[cat] || []) {
    if (!atlas.glyphs[rec.u]) {
      dropped++;
      continue;
    }
    const names = new Set(rec.n.filter((x) => typeof x === 'string'));
    for (const t of [...names]) {
      if (!CJK.test(t)) continue;
      const syl = pinyin(t, { toneType: 'none', type: 'array' });
      // 注意：pinyin() 会把标点与空格原样当音节返回（"男人: 红发" → nan ren :  hong fa），
      // 于是 ':' ' ' '，' 也被当成 token 混进词表。已知噪声，实测 336/349 条记录带它们；
      // 要清就在 add 前加一句"只留汉字或字母数字"的过滤。
      names.add(syl.join(''));
      names.add(syl.map((s) => s[0]).join(''));
      for (const s of syl) names.add(s);
      pinyinInsertions += 2 + syl.length;
    }
    const e = enNames.get(rec.u);
    if (e) {
      const words = [e.short_name, ...(e.short_names || []), e.name]
        .filter(Boolean)
        .flatMap((s) => s.toLowerCase().split(/[^a-z0-9+]+/))
        .filter((w) => w.length > 1);
      for (const w of words) names.add(w);
      names.add(words.join(''));
      englishInsertions += words.length + 1;
    }
    for (const t of aliasByUnified.get(rec.u) || []) {
      names.add(t);
      aliasInsertions++;
    }
    const v = (rec.v || []).filter((x) => atlas.glyphs[x]);
    kept.push({ n: [...names], u: rec.u, a: rec.a, ...(v.length ? { v } : {}) });
  }
  if (!kept.length) continue;
  if (!categories[cat]) categories[cat] = { category: cat, name: zh.categories[cat]?.name ?? cat };
  emojis[cat] = kept;
}

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, JSON.stringify({ generatedAt: new Date().toISOString().slice(0, 10), cell: atlas.cell, categories, emojis }));

const all = Object.values(emojis).flat();
const distinct = new Set(all.flatMap((r) => r.n));
console.log(`records shipped       : ${all.length} across ${Object.keys(emojis).length} categories`);
console.log(`dropped (no sprite)   : ${dropped}`);
console.log(`variations shipped    : ${all.reduce((s, r) => s + (r.v?.length || 0), 0)}`);
console.log(`names per record      : avg ${(all.reduce((s, r) => s + r.n.length, 0) / all.length).toFixed(1)}, distinct ${distinct.size}`);
console.log(`  pinyin/English/alias: ${pinyinInsertions} / ${englishInsertions} / ${aliasInsertions}`);
console.log(`picker.json           : ${fs.statSync(OUT).size.toLocaleString()} bytes`);
const byToken = new Map();
for (const r of all) for (const t of r.n) byToken.set(t, (byToken.get(t) || 0) + 1);
const amb = [...byToken.entries()].filter(([t, c]) => /^[a-z]{2}$/.test(t) && c >= 10);
console.log(`ambiguous 2-letter    : ${amb.length} tokens with >=10 records（首字母只能保"搜得到"，排序靠常用分类）`);
console.log('\n下一步：check-data.mjs 收尾。');
