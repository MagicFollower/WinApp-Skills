# 症状 → 真因 → 处置

按症状查。前四列都在"接入"或"验收"时会遇到；标 **判据** 的那行说明这条是靠跑出来的，不是靠想出来的。

| 症状 | 真因 | 处置 |
| --- | --- | --- |
| 表情格全是空白框/网络图挂叉 | 走了库的图片样式：它硬编码 `https://cdn.jsdelivr.net/npm/emoji-datasource-{style}/img/{style}/64/{unified}.png`，且 **dist 里没有 `assetPath` 可覆盖** | `emojiStyle="native"` + `components={{Emoji: SpriteCell}}`；两者缺一必挂 |
| 编译报 TS2732 / 找不到 `./data/picker.json` 的默认导出 | 宿主 `tsconfig.json` 没有 `"resolveJsonModule": true` | 加上（`install.mjs` 会 WARN 提醒） |
| 入口 `import './picker.css'` 报找不到模块声明 | `tsconfig` 的 `types` 里没有 `"vite/client"` | 加上 |
| 打包后白屏且控制台没报错 | `vite.config.mts` 的 `base` 还是 `/`，`file://` 把 `/assets/...` 解析到磁盘根 | `base: './'` |
| 字形右边缘或下边缘被切掉一小条 | `.ep-glyph` 少了 `flex: none`：库的 cell 是 flex 容器，固定 30px 被压成 24px | 用 `picker.css`，别自己写 |
| 最近使用那一栏变成一条空白横条 | 两个原因之一：① 存了没有格子的码位；② `<span>` 没设 `display:block`，行内元素宽高不生效，整条高度塌成 0 | ① 按"能画出图"的条数做 gate（见 `host-usage.tsx`）；② 靠 `picker.css` |
| 部分格子永远不显示 | atlas 单边超过 GPU 纹理上限（曾排出 2048×4864） | 让 `MAX_EDGE` 同时约束宽高；`gen-atlas` 的 `over-limit pages` 必须为 0 **判据** |
| 分类标签突然变回英文（Frequently Used 等） | `picker.json` 的 `categories` 缺该 id 的 `name`；`suggested`/`custom` 在 `emojis` 里没有条目，按"有字形才发名字"会漏 | 用带 7 条 `nameOverrides` 的 `gen-index`；`check-data` 有这条检查 **判据** |
| 输 `kaixin`/`kx` 搜不到 | 词表没烘拼音（上游 zh 的 6081 个去重 token 里纯 ASCII 只有 41 个） | 跑 `gen-index.mjs`；`check-data` 的搜索面抽样会各抽一类 **判据** |
| 输 `:` 或空格命中几百条 | `pinyin()` 把标点原样当音节返回，`:`/空格被当成 token（336/349 条） | 已知项。要清就在 token 入集合前过滤"只留汉字或字母数字" |
| 输 `lvse` 搜不到但 `lüse` 能搜到 | `pinyin-pro` 返回 `lü`，数据没做 `ü → v` 归一 | 已知项。要为 ASCII 习惯补一份变体 token |
| ♀️♂️⚕️ 搜不到 | 上游没有这三个的 Apple 位图（`emoji_pretty.json` 里 `has_img_apple=false` 的恰好就这三个） | 源数据边界，别去改合成脚本 |
| 明明 `npm install` 退出码 0，构建时却找不到 electron | Electron 44 的包没有 postinstall，`dist/` 从没下载 | `npm run ensure-electron`；就绪判据是 `node_modules/electron/dist/electron.exe` **存在**，不是退出码 |
| 打包卡在 `EPERM rename win-unpacked.tmp` 或 `EBUSY unlink default_app.asar` | Windows 上 Defender/索引器/IDE/网盘等常驻进程抓刚写出的 asar。等与换输出目录都只算碰运气 | 用 `make-asar-probe.mjs` 手拼 asar 走同一条 `file://` 路径取判据；**不要杀进程** |
| 产物里 grep 得到 `cdn.jsdelivr.net` | 库的图片样式代码被打进来了，属于死路径（`emojiStyle="native"` 时不会被调用） | 别把"字符串在产物里"当"会联网"；跑 `--netprobe` 让钩子接一次真实请求来证明钩子有效 **判据** |
| 包里多出一个没人加载的 226 KB `emojis-*.js` | 库的英文数据集是动态 import 目标，即使传了 `emojiData` 打包器仍产出该分块 | 用 `--selftest` 的 `local-files` 清单确认它未被加载；要剔掉得改库导入形态 |
| 报"零网络"但没人验证过 | 只观测不阻断，或钩子压根没挂上时 `remote=0` 同样成立 | 一条钩子同时数 `file://`（本地计数 >0 才证明钩子在收事件），远程 `cancel:true`，再加 `--netprobe` 反向对照 **判据** |
| 自证旗标传了但进程静默不出现 | 用了 `--type=` 这类 Chromium 保留开关 | 自定义旗标换名（本工程用 `--query=`） |
| `npm run build \| tail -1` 看着成功其实失败了 | 管道整体退出码是 `tail` 的 | 关键命令先落文件再看 `$?` |
| 中文路径下脚本全部找不到文件 | `new URL(...).pathname` 把非 ASCII 目录百分号编码了 | 用 `fileURLToPath()` |
| `check-data` 报 `CORRUPT`/`页字节对得上` FAIL | 拷贝被截断或资产被改过 | 重跑 `install.mjs`（数据文件会被覆盖，`.ts`/`.tsx` 模板要 `--force` 才动） |

## 三条容易忽略的验收顺序

1. `check-data.mjs` 全绿 → 才谈"数据可用"。
2. `npm run typecheck` + `npm run build` 退出码 0 → 才谈"接上了"。
3. `electron.exe --selftest` 打出 `renderer-probe ... decoded=<宽>x<高>` 且该尺寸**等于 `index.json` 里对应页的 width×height** → 才谈"雪碧图真的解码并对上了页"。只看到 DOM 里有 `.ep-glyph` 不算，那只能证明 React 挂上了。
