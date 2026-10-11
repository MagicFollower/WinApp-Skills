---
name: offline-emoji-picker
description: 在 React + Vite + Electron 工程里接入全离线（零 CDN、零运行时检索）的中文表情选择器：预制 Apple 雪碧图 atlas 与烘好拼音/英文别名词表作为资产直接落位，构建期脚本负责重拼与重烘，并用会自己失败的判据证明"运行时不联网"和"格子真的对齐"。当任务涉及 emoji-picker-react v5 的 primitives/BYOD 接入、emoji-datasource 位图离线化、表情拼音搜索（全拼/首字母/逐音节）、sprite atlas 分页与纹理上限、asar/file:// 交付体的零网络判据、或出现"表情全是空白框 / 最近使用是一条空栏 / 输 kaixin 搜不到 / 打包卡 EPERM asar 锁"时使用。
---

# 全离线表情选择器接入

## Overview

把 `emoji-picker-react` 只当**无数据的外壳**用（`emoji-picker-react/primitives` 子入口），中文词表、拼音、英文名在**构建期**烘进一份静态 JSON，Apple 位图在**构建期**拼成分页 WebP 雪碧图，运行时只读仓库里的静态资产、走 `file://`，检索代码为零行、网络请求为零。

必须先接受的三条事实，否则会在验收阶段以别的形式冒出来：

- 库的图片样式硬编码 `https://cdn.jsdelivr.net/npm/emoji-datasource-{style}/img/{style}/64/{unified}.png`，并且 **dist 里不存在 `assetPath` 之类的覆盖项**。所以离线只能 `emojiStyle="native"` + 自绘格子。
- 上游中文数据 **一个拼音词都没有**（6081 个去重 token 里纯 ASCII 只有 41 个），而库没有排序钩子。所以"高效搜索"必须在构建期解决，不能指望运行时。
- "产物里 grep 得到 CDN 字符串" ≠ "会联网"。这句话要有判据支撑，见 Step 5。

## 何时使用

- 新工程要接表情选择器，且要求离线/跨平台一致/中文拼音搜索。
- 已有工程用了图片样式，离线环境一片空白框。
- 要换 emoji 版本、补口语别名、或把 atlas 的格子尺寸/分页参数改掉。
- 要交付 Electron 包，并需要一条能跑的命令来证明运行时零网络。

不适用：贴纸/GIF/动态表情（本数据层只覆盖 Unicode emoji，自定义分组在源数据里是空的）；纯 Web 宿主可以做只读接入，但 Step 5 的零网络判据是 Electron 侧的。

## 交付形态

| 层 | 内容 | 位置 |
| --- | --- | --- |
| 源数据 | `atlas/*.webp`(10) + `atlas/index.json` + `data/picker.json` + `data/aliases.json` | 提交进仓库，`npm run build` **不会**重新生成 |
| 组件模板 | `atlas.ts`、`SpriteCell.tsx`、`Picker.tsx`、`picker.css` | 同上，可被宿主改 |
| 构建产物 | 一份哈希 JS + 一份 CSS + 逐字节不变的 10 张 WebP | `dist/` |
| 交付体 | `app.asar` 顶层只有 `dist`、`dist-electron`、`package.json` 三族 | `release*/` |
| 用户态 | `ep.recent`（宿主写）、`epr_suggested`（库写频次表） | localStorage，删掉不影响数据与搜索 |

## 资源结构

```text
offline-emoji-picker/
├── SKILL.md
├── scripts/
│   ├── install.mjs          落数据与模板到目标工程，幂等，带前置检查
│   ├── check-data.mjs       零依赖判据（15 项会判失败 + 3 项只报告），必须先全绿
│   ├── verify-atlas.mjs     像素级格子对齐（需 sharp + 上游位图，重拼后才跑）
│   ├── gen-atlas.mjs        重拼雪碧图（需 sharp + emoji-datasource-apple）
│   ├── gen-index.mjs        重烘拼音/英文/别名（需 pinyin-pro）
│   └── make-asar-probe.mjs  手拼 app.asar，绕开 builder 的文件锁取零网络判据
├── references/
│   ├── data-layer.md        字段字典、几何常量、上游硬边界、换版本清单
│   └── troubleshooting.md   症状 → 真因 → 处置
└── assets/
    ├── data/                atlas(10 webp + index.json) + picker.json + aliases.json + manifest.json
    ├── src/                 atlas.ts / SpriteCell.tsx / Picker.tsx / picker.css
    └── snippets/            offline-guard.ts（零网络断言）、host-usage.tsx（接线四段）
```

`assets/data/manifest.json` 记每个文件的字节与 sha256 前 16 位，`install.mjs` 据此拒绝落残缺资产。**`assets/` 是交付内容，绝不要把 6.3 MB（6,333,734 字节）的数据读进上下文**——只跑脚本。

## Step 1 — 先过三道前置闸门

不满足就先修，后面每一步都会以别的形式冒出来。

1. 宿主 `tsconfig.json` 要有 `"resolveJsonModule": true`（组件用 `import x from './data/picker.json'`，缺它 TS2732 直接编不过）和 `types` 里的 `"vite/client"`（否则 `import './picker.css'` 报找不到声明）。
2. Vite 配置要 `base: './'`。留默认 `/` 时打包后是白屏且控制台无报错——`file://` 会把 `/assets/...` 解析到磁盘根。
3. Electron 宿主要确认二进制真落地：判据是 `node_modules/electron/dist/electron.exe` **存在**，不是 `npm install` 退出码 0（Electron 44 没有 postinstall，得跑 `npm run ensure-electron`）。

## Step 2 — 落数据与组件

```bash
node <skill>/scripts/install.mjs --root "<工程目录>" [--prefix src/emoji] [--dry-run] [--force]
```

- 只读技能 `assets/`，只写 `<prefix>/` 下 17 个文件，不碰宿主其它源码、不改 `package.json`。
- 默认**不覆盖**已存在的 `.ts`/`.tsx`（宿主可能改过），它们会进 `blocked` 列表；数据文件字节相同则 `skip`。
- 退出码 0 且打印 `==> 落位完成` 才算过。它同时会检查 `tsconfig`、`package.json` 里缺不缺 `emoji-picker-react`/`react`，缺就 WARN。

需要 `emoji-picker-react@5.x`（组件模板从它的 `primitives` 子入口导入）和 `react`/`react-dom`。注意分栏：它们由 Vite 打进 `dist/`，所以放 **`devDependencies`** 就够；放进 `dependencies` 反而会让 builder 再附一份没人用的 `node_modules` 进 asar（见 Step 6）。**不需要** `sharp`、`pinyin-pro`、`emoji-datasource-apple`——那是 Step 7 重拼时才装的。

## Step 3 — 零依赖判据，必须先全绿

```bash
node <skill>/scripts/check-data.mjs --root "<工程目录>" [--prefix src/emoji]
```

15 项会判失败（含"文件存在"这道前置），另有 3 项已知情况只如实报告不判失败。全部不需要上游包：页文件与清单双向对应（缺页、多页都算失败）、每页字节数与清单吻合（抓截断）、页几何 = 行列 × `cell`、最大边 ≤ 4096、格子坐标落格且同页不重叠、格子总数 = Σ 各页、**词表 ⊆ 格子**（有词没图 = 点出来是空白）、每条都有关键词、无空串 token、分类名齐全、搜索面抽样（中文/全拼/首字母/英文各抽一个）、别名闭环。

```bash
# 判据本身也得验，否则"全绿"可能是空转：
node -e 'const fs=require("fs"),p="<工程>/src/emoji/atlas/flags.webp",b=fs.readFileSync(p);fs.writeFileSync(p,b.subarray(0,b.length-97))'
node <skill>/scripts/check-data.mjs --root "<工程>"     # 必须 FAIL 1 项 + 退出码 1
node <skill>/scripts/install.mjs --root "<工程>"        # 数据文件会被重新覆盖回去
```

参考值（本轮固化的那份）：`records 1903  categories 10  glyphs 3778  pages 10`，`ALL PASS 0 fail`。

## Step 4 — 接线

`Picker.tsx` 里五样必给，少一样就退化：

| prop | 给什么 | 不给会怎样 |
| --- | --- | --- |
| `emojiData` | `import pickerJson from './data/picker.json'`，在**模块作用域**转成 `EmojiData` | 每次渲染换对象 → 数据集反复重载 |
| `emojiStyle` | `"native"` | 走 CDN 图片样式，离线全挂 |
| `components` | `{ Emoji: SpriteCell }` | 用系统字体画，Windows 灰轮廓、Linux 豆腐块 |
| `categoryIcons` | 每个分类一格 `spriteStyle(u, 18)` | 分类栏还是原生字形，跨平台不一致 |
| `labels` | 中文搜索占位/结果数/错误文案 | 界面中英混排 |

`SpriteCell` 的签名是 `({ emoji, children, ...managed }) => <button {...managed}>`。**`managed` 必须整个回落到 `<button>` 上**——库的选中态、键盘导航、虚拟化、无障碍全挂在这些 props 上，丢掉一个就静默坏掉一块。

样式只有三条，别自己重写：

```css
.ep-glyph { display: block; flex: none; }            /* 缺 flex:none 时固定 30px 被压成 24px，字形右缘被切 */
.ep-picker [data-epr-part='emoji'] { border: 0; }    /* 自绘 cell 是裸 button，会继承 UA 边框；background 归库 */
.ep-picker { width: 100% !important; height: 360px !important; }  /* 弹层由宿主绝对定位 */
```

宿主侧的四段（弹层锚点、插到光标处、最近使用按"能画"过滤、码位反解）照 `assets/snippets/host-usage.tsx` 抄，每段注释都写了踩过什么。

判据：`npm run typecheck` 退出码 0。

## Step 5 — 零网络断言（Electron 宿主）

把 `assets/snippets/offline-guard.ts` 贴进主进程，三条设计各防一种假绿：

- 同一条 `onBeforeRequest` **也数 `file://`**——钩子没挂上时 `remote=0` 同样成立，本地计数 >0 才证明它在收事件。
- 远程请求 `cancel: true`——只观测不阻断的话，CDN 真被请求到页面照样显示，判据就成事后诸葛。
- `--netprobe` 反向对照——主动打一次库里那条 CDN 地址，看钩子接不接得住。

只在 `file://` 路径上装（开发模式走 `http://localhost`，会被自己拦死）。期望输出：

```
network remote=0 local-files=10 [activities-….webp, …, index.html, …]     # 正常自证
netprobe control-fetch=REJECTED Failed to fetch
network remote=1 local-files=10 [...] ["https://cdn.jsdelivr.net/npm/emoji-datasource-apple@16.0.0/img/apple/64/1f600.png"]
```

从这份清单还能白拿三条结论：首屏只解码 10 页里的 **7** 页；库里那个 226 KB 的英文数据集分块**未被加载**；`decoded=` 的尺寸等于 `index.json` 里对应页的 `width×height`（取对了页）。

builder 在 Windows 上会卡在解包文件锁（`EPERM rename win-unpacked.tmp` / `EBUSY unlink default_app.asar`，Defender、索引器、IDE、网盘都会抓刚写出的 asar）。等和换输出目录都只算碰运气，**绝不要杀进程**——用 `make-asar-probe.mjs` 手拼一份走同样 `file://` 路径的交付体来取判据：

```bash
npm run build && node <skill>/scripts/make-asar-probe.mjs --root "<工程>"
cd "<工程>/release-asar/probe" && ./electron.exe --selftest --netprobe
```

纯 Web 宿主没有这条钩子时，改用 CSP（`default-src 'self'`）+ DevTools 网络面板，并如实说明"验的是浏览器侧"。

## Step 6 — 交付核对

| 判据 | 方法 | 参考值（本轮） |
| --- | --- | --- |
| asar 只有三族 | `@electron/asar` 的 `listPackage` 取顶层目录名 | `dist=16  dist-electron=3  package.json=1` |
| 没有 `node_modules` 混进包 | 族里多出 `node_modules` = 有依赖被错登进 `dependencies` | `package.json` 的 `dependencies` 应为 `{}` |
| 载荷字节配平 | 逐文件求和 vs asar 实际 | 6,914,597 + 4,596（asar 头）= 6,919,193 |
| WebP 未被二次压缩 | `dist/assets` 里 10 张字节合计 = atlas 源合计 | 5,573,302 |

`dependencies` 为空是这套方案能成立的结构性前提：builder 会按 `dependencies` 无条件附带 `node_modules`，`files` 白名单拦不住它。

## Step 7 — 重拼、重烘、补别名

只有换 emoji 版本 / 改格子尺寸 / 改分页参数 / 补词时才需要，此时才装构建期依赖：

```bash
npm i -D sharp pinyin-pro emoji-datasource-apple@16
node <skill>/scripts/gen-atlas.mjs --root "<工程>" [--cell 64] [--max-edge 2048]
node <skill>/scripts/gen-index.mjs --root "<工程>"
node <skill>/scripts/check-data.mjs --root "<工程>"        # 收尾必跑
node <skill>/scripts/verify-atlas.mjs --root "<工程>"      # 像素级对齐
SHIFT_CELLS=1 node <skill>/scripts/verify-atlas.mjs --root "<工程>"   # 期望它大面积 SUSPECT 且退出码 1
```

顺序不能颠倒：`gen-index` 拿 `atlas/index.json` 筛词表，方向是**图缺则词删**；反过来就出现"搜得到却画不出"的空白格。`gen-atlas` 末尾的 `over-limit pages` 会让脚本自己失败——超限时别只删掉那行日志。

补一个口语别名往 `data/aliases.json` 加一条，然后只重跑 `gen-index` + `check-data`：

```json
[{ "t": "大拇指", "u": ["1f44d"], "why": "zh 数据里 👍 的词没有「大拇指」" }]
```

`check-data` 的"别名闭环"会验它真的进了词表——写错码位时这条会 FAIL，而不是静默没生效。

## 已知缺口（别当 bug 修）

| 现象 | 成因 | 出处 |
| --- | --- | --- |
| ♀️♂️⚕️ 搜不到 | 上游这三个没有 Apple 位图（`has_img_apple=false` 的恰好只有这三个） | `references/data-layer.md` §4 |
| 输 `lvse` 0 结果、`lüse` 7 结果 | `pinyin-pro` 返回 `lü`，没做 `ü → v` 的 ASCII 归一 | 同上 §6 |
| 搜 `:` 命中 336 条、搜空格命中 349 条 | `pinyin()` 把标点原样当音节返回，被当成 token | 同上 §6 |
| 首字母结果排序不对 | 库无排序钩子；283 个双字母 token 各有 ≥10 条记录（最极端 427 条） | 靠"常用"分类置顶绕开，不要试图重排搜索结果 |

## 判据速查

| 判据 | 命令 | 期望 | 防的是哪种假绿 |
| --- | --- | --- | --- |
| 数据自洽 | `check-data.mjs` | `ALL PASS 0 fail` | 拷贝被截断、清单漂移、有词没图 |
| 判据本身有效 | 截断一张 webp 再跑 | 必须 FAIL + 退出码 1 | 检查项其实没在看东西 |
| 编译与打包 | `typecheck` / `build` | 退出码均 0 | 管道吞退出码（`\| tail` 的码是 tail 的） |
| 雪碧图真的解码 | `--selftest` | `decoded=<W>x<H>` 等于该页尺寸 | DOM 里有 `.ep-glyph` 只证明 React 挂上了 |
| 搜索面 | `--selftest --query=<词>` | 结果数 >0，读库自己的播报 | `cells=` 是 DOM 计数，会比播报多 1，别当命中数 |
| 零网络 | `--selftest` / `--netprobe` | `remote=0 local-files=N` / `remote=1` 且 `REJECTED` | 钩子没挂上、只观测不阻断 |
| 格子对齐 | `verify-atlas.mjs` | `suspect: 0`，最大差 <6/255 | 尺寸计数全对但取错格 |
| 对齐判据会失败 | `SHIFT_CELLS=1` | 大面积 SUSPECT + 退出码 1 | 邻居对照失效（基形旁就是它的肤色变体，几乎同款） |

## Resources

- `scripts/`、`references/`、`assets/` 的清单见上面「资源结构」。
- 需要"为什么是这样"时读 `references/data-layer.md`（字段字典、几何常量、上游硬边界、换版本清单、许可）。
- 报错或对不上号时读 `references/troubleshooting.md`（20 条症状 → 真因 → 处置）。
- 数据取自 `emoji-picker-react@5.0.0` 的 `emojis-zh.json` 与 `emoji-datasource-apple@16.0.0`（两者均 MIT，版权行见 `references/data-layer.md` §8）。固化时间见 `assets/data/manifest.json` 的 `capturedAt`。
