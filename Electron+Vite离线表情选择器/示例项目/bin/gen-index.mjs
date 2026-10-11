// Builds the picker dataset: emoji-picker-react's `EmojiData` shape, with
// pinyin and English search names baked into each record's `n` array.
//
//   in : node_modules/emoji-picker-react/dist/data/emojis-zh.json  (Chinese keywords)
//        node_modules/emoji-datasource-apple/emoji_pretty.json     (English names)
//        src/emoji/atlas/index.json                                (only atlased glyphs ship)
//        src/emoji/data/aliases.json                               (colloquial gaps, hand-edited)
//   out: src/emoji/data/picker.json
//
// Pinyin expansion happens here, at build time, so the renderer ships no
// pinyin runtime. The picker's own matcher then searches over `n`, which now
// carries Chinese + full pinyin + initials + per-syllable pinyin + English.

import fs from 'node:fs';
import path from 'node:path';
import { pinyin } from 'pinyin-pro';

const ROOT = path.resolve(import.meta.dirname, '..');
const ZH = path.join(ROOT, 'node_modules/emoji-picker-react/dist/data/emojis-zh.json');
const DS = path.join(ROOT, 'node_modules/emoji-datasource-apple/emoji_pretty.json');
const ATLAS = path.join(ROOT, 'src/emoji/atlas/index.json');
const ALIASES = path.join(ROOT, 'src/emoji/data/aliases.json');
const OUT = path.join(ROOT, 'src/emoji/data/picker.json');

const CJK = /[\u4e00-\u9fa5]/;
const zh = JSON.parse(fs.readFileSync(ZH, 'utf8'));
const ds = JSON.parse(fs.readFileSync(DS, 'utf8'));
const atlas = JSON.parse(fs.readFileSync(ATLAS, 'utf8'));

const enNames = new Map(ds.map((e) => [e.unified.toLowerCase(), e]));
const aliasList = fs.existsSync(ALIASES) ? JSON.parse(fs.readFileSync(ALIASES, 'utf8')) : [];
const aliasByUnified = new Map();
for (const a of aliasList) for (const u of a.u) {
  if (!aliasByUnified.has(u)) aliasByUnified.set(u, []);
  aliasByUnified.get(u).push(a.t);
}

const categories = {};
const emojis = {};
let pinyinInsertions = 0;
let englishInsertions = 0;
let aliasInsertions = 0;
let dropped = 0;

// The upstream zh dataset mixes scripts (食物飲料 / 旅遊地點 are Traditional)
// and names smileys_people 人体 ("human body"), which is not what the category
// holds. suggested/custom carry no glyphs of their own, so a loop over
// `emojis` alone would omit them and let the picker fall back to its English
// built-in label.
const nameOverrides = {
  smileys_people: '笑脸与人物',
  food_drink: '食物饮料',
  travel_places: '旅游地点',
  objects: '物品',
  symbols: '符号',
  suggested: '常用',
  custom: '自定义表情'
};
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
      names.add(syl.join(''));                    // 开心 -> kaixin
      names.add(syl.map((s) => s[0]).join(''));   // 开心 -> kx
      for (const s of syl) names.add(s);          // 开心 -> kai, xin
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

const out = {
  generatedAt: new Date().toISOString().slice(0, 10),
  cell: atlas.cell,
  categories,
  emojis
};
fs.writeFileSync(OUT, JSON.stringify(out));

const all = Object.values(emojis).flat();
const distinct = new Set(all.flatMap((r) => r.n));
console.log('');
console.log(`category labels     : ${Object.entries(categories).map(([k, v]) => `${k}=${v.name}`).join('  ')}`);
console.log(`records shipped       : ${all.length} across ${Object.keys(emojis).length} categories`);
console.log(`dropped (no sprite)   : ${dropped}`);
console.log(`variations shipped    : ${all.reduce((s, r) => s + (r.v?.length || 0), 0)}`);
console.log(`names per record      : avg ${(all.reduce((s, r) => s + r.n.length, 0) / all.length).toFixed(1)}, distinct tokens ${distinct.size}`);
console.log(`  pinyin insertions   : ${pinyinInsertions}`);
console.log(`  english insertions  : ${englishInsertions}`);
console.log(`  alias insertions    : ${aliasInsertions} of ${aliasList.length} alias entries`);
console.log(`picker.json           : ${(fs.statSync(OUT).size / 1024).toFixed(0)} KB`);

// Diagnostics for the two failure modes measured during design: initials are
// heavily ambiguous, so ranking cannot come from the static data alone.
const byToken = new Map();
for (const r of all) for (const t of r.n) byToken.set(t, (byToken.get(t) || 0) + 1);
const amb = [...byToken.entries()].filter(([t, c]) => /^[a-z]{2}$/.test(t) && c >= 10);
console.log(`ambiguous 2-letter    : ${amb.length} tokens with >=10 records (needs frequency rerank)`);
console.log(`worst tokens          : ${[...byToken.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5).map(([t, c]) => `${t}=${c}`).join('  ')}`);
