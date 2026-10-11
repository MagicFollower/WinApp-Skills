# 数据层参考：形状、常量、口径

只在需要改生成逻辑、核对数字或回答"为什么是这样"时读这份。接入步骤本身不需要它。

## 1. 四份文件的分工

| 文件 | 落位（默认 `src/emoji/`） | 谁读 | 回答的问题 |
| --- | --- | --- | --- |
| `atlas/*.webp` | `atlas/` | CSS `background-image` | 码位长什么样 |
| `atlas/index.json` | `atlas/` | `atlas.ts` 静态 import | 码位在哪一页哪个 x,y |
| `data/picker.json` | `data/` | `Picker.tsx` 静态 import | 搜什么出什么、分类叫什么、有无肤色变体 |
| `data/aliases.json` | `data/` | `gen-index.mjs` 构建期输入 | 上游缺的口语别名（唯一需要人维护的） |

记录契约只有四个字段（库的 `DataEmoji` 声明，见 `node_modules/emoji-picker-react/dist/data/emojis-zh.d.ts`）：

```ts
{ n: string[]; u: string; v?: string[]; a: string }
// n 关键词（唯一搜索面） u 码位串 v 肤色变体码位串 a Emoji 版本（added_in）
```

`picker.json` 顶层是 `{ generatedAt, cell, categories, emojis }`，`emojis` 按分类 id 分组。

## 2. 已固化数据的口径（2026-10-11 取）

| 指标 | 值 |
| --- | --- |
| 上游中文记录 / 实际发货 | 1906 / 1903（差 3，见 §4） |
| atlas 格子 | 3778 = 基形 1903 + 肤色变体 1875 |
| atlas 页数 | 10，其中 `smileys_people` 占 3 页 |
| atlas 总字节 | 5,573,302（index.json 另 164,707） |
| picker.json | 580,819 字节，1903 条 |
| 关键词 | 总 51,985 / 去重 18,310 / 每条均 27.3 / 最多 84（`1f972`） |
| 构建期插入量 | 拼音 44,652、英文 13,761、别名 1 |
| 上游版本 | emoji-picker-react 5.0.0、emoji-datasource-apple 16.0.0、pinyin-pro 3.29.5、sharp 0.35.5 |

## 3. atlas 几何常量与它们的由来

```
CELL      64    上游 img/apple/64 的原生尺寸；渲染时只用 CSS 缩放，不重采样位图
MAX_EDGE  2048  GPU 纹理上限通常 4096，取一半留余量
PER_AXIS  32    floor(MAX_EDGE / CELL)
PER_PAGE  1024  PER_AXIS²
```

- **`MAX_EDGE` 必须同时约束宽和高。** 只卡宽会排出 2048×4864 这种图，纵向顶穿 4096，表现是"部分格子永远不显示"。`gen-atlas.mjs` 末尾的 `over-limit pages` 自检会让脚本自己失败，别把人眼看日志当判据。
- 每页尺寸恒等于 `cols × CELL`、`rows × CELL`；末页取 `ceil(sqrt(n))` 再夹到 `PER_AXIS`，所以是接近正方形的图，不留空白矩形。
- 先按分类切、再按 `PER_PAGE` 切，所以同一分类的格子连续排在相邻页；翻页只影响该分类。
- 全部 1875 个肤色变体都落在 `smileys_people`（其余 7 组变体数为 0），555 + 1875 = 2430 = 1024 + 1024 + 382，这就是它需要 3 页的原因。

## 4. 三个上游硬边界（不是本组件的 bug）

`2640-fe0f` ♀️、`2642-fe0f` ♂️、`2695-fe0f` ⚕️ 没有 Apple 位图。它们在 `emoji_pretty.json` 里正是 `has_img_apple: false`，且全文件为 false 的**只有这三个**。所以：

- `gen-atlas` 报 `unresolved base: 3`；
- `gen-index` 报 `dropped (no sprite): 3`；
- `symbols` 分类从 224 条变 221 条。

另有 150 个肤色组合（Apple 对某些"人 × 肤色"没出图）被静默过滤，所以基形带 `v` 的有 329 条、实际发货 323 条 —— 6 条的变体被清空后不留空 `v`（`gen-index.mjs` 里 `...(v.length ? { v } : {})`），否则会渲染出一排点不出图的肤色按钮。

反向还有 15 张 PNG 没被用：5 个单独肤色修饰符（`1f3fb`–`1f3ff`，只作为后缀存在）与 10 个 zh 词表未收的 ZWJ 序列。

## 5. 上游数据的四个已知形状问题

| 现象 | 事实 | 处置 |
| --- | --- | --- |
| 零拼音 | zh 的 6081 个去重 token 里纯 ASCII 只有 41 个 | 构建期用 `pinyin-pro`（`toneType:'none'`）烘全拼/首字母/逐音节进 `n` |
| 繁简混排 | `食物飲料`、`旅遊地點` 是繁体，其余简体 | `gen-index.mjs` 的 7 条 `nameOverrides` |
| 分类名不对题 | `smileys_people`=人体、`objects`=物件、`symbols`=人的符号 | 同上 |
| 特殊分类无记录 | `suggested`/`custom` 在 `emojis` 里没有条目 | 只在 `categories` 里补名字，否则库回落英文 "Frequently Used" |

## 6. 两处数据卫生缺口（当前带着，可接受）

1. **`ü` 没做 ASCII 归一。** `pinyin-pro` 给"绿"返回 `lü`，所以 `lüse` 有 7 个结果而用户实际会打的 `lvse` 是 0。修法：为含 `ü/ö/ä/ñ` 的 token 追加 ASCII 变体。（数据里那个 `lv` 是巧合，来自英文短名 `flag_lv`。）
2. **标点被当拼音混进 `n`。** 判"这条要不要展拼音"用的是整串含汉字，于是 `男人: 红发` 整串进了 `pinyin()`，而它会把 `":"`、`" "` 原样返回当音节。结果 336 条带 `:`、349 条带空格。子串匹配下正常中文输入不受影响，但搜 `:` 命中 336 条。修法：加进集合前只留汉字或字母数字。

`check-data.mjs` 会把这两处如实打印出来但不判失败——它们是已知项，不是回归。

## 7. 换 emoji 版本要动的地方

1. `package.json` 里 `emoji-datasource-apple`（和 `emoji-picker-react`）的版本；
2. `npm i -D sharp pinyin-pro` 后依次跑 `gen-atlas.mjs` → `gen-index.mjs` → `check-data.mjs`；
3. `gen-atlas.mjs` 写进 `index.json` 的 `source` 字段现在是从上游 `package.json` 读出来的版本号，不用手改；
4. 重跑一次交付核对：旧版存进 `localStorage` 的码位可能在新 atlas 里没有格子，宿主的"最近使用"必须按能画出图的数量做 gate（见 `host-usage.tsx` 的 `drawable`）。

## 8. 许可

上游两个包都是 MIT：`emoji-picker-react` © ealush，`emoji-datasource-apple` © 2013 Cal Henderson（`github.com/iamcal/emoji-data`）。生成的 atlas 与词表是派作品，MIT 要的是保留版权声明——落进关于面板或 `doc/` 一行即可。位图本身来自 Apple 平台素材，商用交付前自己确认一遍分发达到的司法辖区。
