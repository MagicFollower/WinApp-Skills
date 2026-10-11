# 100themes — 可移植的参考主题库

`bjarneo/100-themes` 的离线种子：100 个主题 × 5 个变体 = 500 份 `colors.toml` 原文，加一个把它们编译成 CSS 自定义属性的生成脚本。设计目标是**装进工程后完全不联网**：种子命中就零请求，只有目录缺失或不全才回源取，取回的内容再写回 `raw/`。

约定与坑的规范版本见 Skill `windows-electron-scaffold` 的 Step 7b（本机快照在 `aaa/doc/SKILL.md`）。

## 形态

```
100themes/
├── README.md                      本文
├── gen-themes.mjs                 生成脚本（装进工程后建议移到工程的 bin/）
├── manifest.json                  source + hosts + variants + themes[100] + tokenMap + seed
└── raw/<主题>_<变体>.toml          500 份色板原文
```

产物写在工程里，不在种子目录：`src/styles/themes.css`（500 个 `[data-theme]` 块）、`src/styles/themes.index.json`（500 条 `{id, theme, variant, mode}`）。

`manifest.json` 的 `seed.capturedAt` 取的是 `raw/` 里最新的文件 mtime，也就是**这批种子落到当前目录的时间**，不是上游仓库的取回时间（本批原文最初取回于 2026-10-02）。拷贝目录会刷新它，属正常。

## 装进一个新工程

方式 A（推荐，随仓库分发）：

1. 整个 `100themes/` 拷到 `<proj>/100themes/`。
2. 把 `gen-themes.mjs` 移到 `<proj>/bin/gen-themes.mjs`（脚本在 `100themes/` 里也能跑；移到 `bin/` 是为了目录被删时兜底逻辑仍可达）。
3. `package.json` 加三条脚本：

       "themes": "node bin/gen-themes.mjs",
       "themes:offline": "node bin/gen-themes.mjs --offline",
       "themes:seed": "node bin/gen-themes.mjs --fetch-only"

4. 渲染层入口引一次 `import './styles/themes.css'`，主题列表读 `themes.index.json`，切换只改 `<html data-theme="<主题>--<变体>">`。

方式 B（不动工程目录，从外面引用）：

    node <路径>/100themes/gen-themes.mjs --root <工程目录> --offline

`--root` 省略时工程根取脚本的上级目录。脚本会先确认那个根里有 `package.json`，没有就退出码 1 并提示——这是为了不把产物写成仓库外层的孤儿 `src/styles/`。

## token 契约

每个 `[data-theme]` 块给出：映射自 `colors.toml` 的 `--pf-bg`、`--pf-bg-dark`、`--pf-bg-darker`、`--pf-bg-lighter`、`--pf-fg`、`--pf-fg-dim`、`--pf-fg-soft`、`--pf-fg-strong`、`--pf-accent`、`--pf-selection`、`--pf-muted`、`--pf-danger`、`--pf-success`、`--pf-warning`、`--pf-info`，以及生成时算出的派生值 `--pf-accent-fg`（按相对亮度选 `#111`/`#fff`，避免浅底浅字）、`--pf-border`、`--pf-border-strong`、`--pf-hover`、`--pf-active`、`--pf-scrollbar`、`--pf-mode`（`dark`/`light`，用来分深浅组）。

要接自己的 token 名，改 `manifest.json` 的 `tokenMap` 再重跑生成即可，`raw/` 不用动。工程侧的纪律是：基础样式里不写字面色值，全走 token，否则换主题只改一半。

## 命令与实测判据

    node gen-themes.mjs --root <proj> --offline      # 判据：本地命中 500/500，未联网；退出码 0
    node gen-themes.mjs --root <proj> --fetch-only   # 只补缺口进 raw/，不写产物
    node gen-themes.mjs --root <proj> abyss,lofi     # 子集——会把产物覆写成只含子集，慎用

本机实测（Node 24）：种子在工程外和整目录拷进工程两种方式跑出的 `themes.css` 都是 12,513 行、500 个选择器，去掉三行头部后按行排序取 md5 = `c3f633b3c399a1d9ef4cd8fcc3d0109f`，与 `react-app` 里当年联网生成的结果全等；`--fetch-only abyss` 打 `本地命中 5/5，未联网`、缺口 0/5 且不写产物。兜底路径实测：删掉 `raw/watermelon_oled.toml` 后 `--offline` 退出码 1 报出缺口名，去掉 `--offline` 则打 `取文件用 cdn.jsdelivr.net` 并写回同一份内容（md5 与删除前一致）。

## 已踩过的坑

- **jsDelivr 的 URL 必须有 `/gh/` 段**：`https://cdn.jsdelivr.net/gh/<repo>@<ref>/<主题>/<变体>/colors.toml`。漏成 `https://cdn.jsdelivr.net/<repo>@<ref>/...` 时三个镜像全 404，脚本会报成"所有源都不可用"，看着像断网。先直接 GET 一个已知文件看真实状态码。
- **目录清单接口不可依赖**：`data.jsdelivr.com` 与 CDN 目录列表实测 403，只有单文件 GET 能用，所以主题名固定在 `manifest.json`，不运行时枚举。
- **子集运行覆写产物**：补种子走 `--fetch-only`，收尾再跑一次全量。
- **500 个小文件的代价是块开销不是字节**：`raw/` 表观 343 KB、落盘 2.3 MB（每文件占一个 4 KB 簇），另有 500 次目录项与 checkout/拷贝往返。只想要产物时可以不带 `raw/`，但换 token 映射就得回源。
- 取回顺序：jsDelivr `cdn.` → `fastly.` → `gcore.` → `raw.githubusercontent.com`（末位源 URL 形状不同，可当对照组）。
