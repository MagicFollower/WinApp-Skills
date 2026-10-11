---
name: windows-electron-scaffold
description: 在 Windows 上从零搭建、整理并打包 Electron 44+ 桌面应用的可复现流程——显式获取 Electron 二进制、默认注入下载镜像、主进程与 preload 走 CJS + sandbox、Vite 用 .mts 配置与相对 base、electron-builder 同时产出 portable/nsis/dir、按 src/ + electron/ + bin/ + doc/（用外部色板时再加 100themes/）的目录约定收敛工程、把窗口尺寸与字体明暗自适应当编码期约束、交付 doc/运行与构建（T0Level）.md，并可在 bin/ 下交付启动/停止/构建安装包/构建便携版四个双击 bat 脚本、把 .gitignore 补成七组（依赖/构建产物/临时物/环境/编辑器/OS 垃圾 + 末尾反向放行）。当出现 npm install 退出码 0 但 node_modules/electron/dist/electron.exe 缺失、Electron 或 electron-builder 从 GitHub Releases 下载失败、需要配置 Windows 打包目标、打包产物秒退需要排查、要把既有 Electron 工程按目录约定重新整理、要写运行与构建/发布文档、要判定桌面端自适应是否达标、要把外部色板库（如 100-themes）离线固化进工程、工程缺 .gitignore 或交付资产被通配规则误伤、或要写双击即跑的 .bat 且它出现注释残片被当命令执行/某行 echo 没打印却把别的文件截断/批处理内 call npm.cmd 报 MODULE_NOT_FOUND 时使用。不用于普通 Web/React 页面开发。
---

# Windows Electron 工程搭建与打包

## Overview

在 Windows 上从空目录做到「三种交付物齐全」的固定路径。核心是两个反直觉点：`npm install` 成功不等于 Electron 运行时到位，且 npm registry 可达不等于二进制下载源可达。两者都要在写业务代码前验证掉，否则会在构建阶段以静默失败的形式冒出来。

## 何时使用

- 新建 Electron 桌面工程（Windows 宿主）。
- `npm install` 报成功，但 `node_modules/electron/dist/` 为空或缺 `electron.exe`。
- Electron 或 electron-builder 下载超时，日志里出现 `github.com` 或 `electron-vX.Y.Z-win32-x64.zip`。
- 配置 electron-builder 的 Windows target 与产物命名口径。
- 打包出来的 exe 双击无反应、秒退。
- 把既有 Electron 工程按本文目录约定重排（`src/` + `electron/` + `bin/` + `doc/`），并同步所有路径后重新跑验收。
- 要写 `doc/运行与构建（T0Level）.md`：启动、本地调试、打包启动、改标题、改图标、发布分发六类命令逐条带判据。
- 打包后要把目录收敛到“删干净仍能一把重建”的最小清单。
- 桌面端要窗口尺寸/字体/明暗自适应——这属于写代码前的口径决定，不是收尾补丁。
- 要把外部主题/色板库（例：`bjarneo/100-themes`，100 主题 × 5 变体 = 500 个 `colors.toml`）离线固化进工程，别让每次生成都去仓库拉几百个小文件。
- 给桌面端定 `.ico` 图标：默认本地生成像素风字母 monogram（不联网、不依赖 ImageMagick/Python），只有要品牌 logo 时才查 selfh.st/icons——见 Step 5。
- 给不打开终端的人交付「双击即跑」的 `bin\*.bat`（启动 / 停止 / 构建安装包 / 构建便携版）——bat 有四条静默咬人的硬坑，见 Step 9。
- 工程没有 `.gitignore`，或有但漏了产物 / 临时物 / OS 垃圾，或交付资产（雪碧图、图标）被 `release*/`、`*.webp` 这类规则误伤——见 Step 8b。

## 目录布局（新建工程先定）

新工程一律开在一个独立的新目录里。代码与 `bin/`、`doc/` 同级平放，不再套 `code/` 父目录；图标就近放在主进程目录里，不建 `assets/`：

```text
<proj>/
├── index.html                Vite 入口，必须在根
├── package.json  package-lock.json
├── vite.config.mts  tsconfig.json  electron-builder.yml  .gitignore（缺了按 Step 8b 补七组）
├── src/                      渲染层：组件、样式、lib、内容
├── electron/                 主进程 + preload + tsconfig.json + icon.ico
├── bin/                      脚本：dev.mjs、build.mjs、*.ps1；可选四个双击 *.bat（见 Step 9）
├── 100themes/                可选：外部色板种子（manifest.json + raw/），见 Step 7b
├── doc/                      运行与构建（T0Level）.md（必交）+ README、设计方案
└── dist/  dist-electron/  release/   产物；node_modules/ 保留
```

三个名字是工具链锁死的，不要改：Vite 的 `index.html` 在 root（否则要改 `root` 并把 `outDir` 算成相对 root 的上跳路径）；`package.json` 在 root（electron-builder 从那里读 `main`、`version`、`productName`）；`node_modules` 在 root。产物目录保留 `dist`、`dist-electron`、`release` 原名，本文的 `Test-Path` 与验收口径才能直接引用。

`bin/` 是自研脚本目录，与 npm 的 `node_modules/.bin`（包的可执行 shim，如 `vite.cmd`）无关；不要去 `bin/` 里找第三方命令行，也不要把脚本丢进 `.bin`。

## Step 0 — 先探测，再改环境

一、node 与 npm 在当前 shell 是否可见。终端会话的 PATH 可能快照于 Node 安装之前，`npm install` 派生的子进程会找不到 node。用全路径兜底：

    powershell.exe -NoProfile -Command "$n='C:\Program Files\nodejs\node.exe'; & $n 'C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js' -v"

这两条示例要在 PowerShell 里执行；从 Git Bash 跑会把 `$n`、`$u` 当成 shell 变量吹掉，带变量或循环的逻辑一律先写成 `.ps1` 再 `-File` 调用。

二、GitHub 二进制源不能拿探测结果当开关。实测：`HEAD https://github.com/electron/electron/releases` 返回 200，而 electron-builder 去 GET release 对象时 `connect ETIMEDOUT 20.205.243.166:443` —— 两者打的是不同主机，**探测证不住下载**；可达性还会在同一台机器上随网络变化。

    powershell.exe -NoProfile -Command "function P($u){try{(Invoke-WebRequest -Uri $u -Method Head -TimeoutSec 5 -UseBasicParsing).StatusCode}catch{'FAIL'}}; P 'https://registry.npmjs.org'; P 'https://github.com/electron/electron/releases'"

所以 Step 2 的镜像按“**默认注入**”处理，确认官方源真能下载时用 `-NoMirror` 显式退出。探测结果只当情报与排障线索，不当开关。

镜像是有代价的：`@electron/get` 的 SHASUMS 校验也走同一个镜像，注入即等于信任该源。能直连 GitHub 的机器上不要长期挂着镜像变量。

## Step 1 — package.json 骨架

顶层只留两个关键字段，其余依赖按常规填写：

- `main`：`dist-electron/main.js`，指向编译产物而不是 `electron/main.ts`。
- `scripts`：五条。`ensure-electron` = `install-electron`；`dev` = `node bin/dev.mjs`；`build` = `node bin/build.mjs`；`dist` = `node bin/build.mjs && electron-builder --win`；`typecheck` = `tsc --noEmit -p tsconfig.json && tsc --noEmit -p electron/tsconfig.json`。

不要加 `"type": "module"`：主进程与 preload 要保持 CJS（Step 3），Vite 的 ESM 需求改用 `.mts` 后缀解决（Step 4）。

**依赖分栏是打包口径，不是代码风格。** 这条路径上 `dependencies` 应当尽量空：渲染层的 react 与组件库由 Vite 打进 `dist/`，主进程与 preload 由 tsc 打进 `dist-electron/`，运行时真正 `require` 的只有 `electron` 与 `node:*` 内置模块。而 electron-builder 会把 `dependencies` 对应的 `node_modules` 无条件附带进 `app.asar`——Step 5 的 `files` 白名单管不住它。所以写反的两个方向都是静默故障：dev 工具错登进 `dependencies`，构建机上 `npm ci --omit=dev` 照样把它装上、包体还白胖一层；运行时真要的库错登进 `devDependencies`，`npm run dev` 一切正常（本地 `node_modules` 是全的），装出来的包才报 `Cannot find module`。反面样本实测：某工程把五个纯渲染层包（`motion`/`react`/`react-dom`/`react-markdown`/`remark-gfm`，磁盘 9.2 MB）登在 `dependencies`，结果 21,725,609 字节的 `app.asar` 里 2276 条条目有 2267 条是 `node_modules`，而主进程一个都没用上。

自查两条：`npm ls --omit=dev --depth=0` 剩下的必须都是交付物真需要的运行时包（这类工程通常输出为空）；`app.asar` 的顶层族只能有 `dist`、`dist-electron`、`package.json`（核对法见 Step 5）。确实需要主进程运行时依赖时，它的 `node_modules` 族**应当**出现在 asar 里——那时判据按包名放行，别当残留删掉，并用 `--selftest` 断言它 require 得到。

## Step 2 — 让 Electron 二进制真的落地

新版 Electron（实测 44.5.1）的 `node_modules/electron/package.json` 里没有 `scripts` 字段，即没有 postinstall；下载逻辑被改成了 `bin` 里的 `install-electron`（指向 `install.js`）。所以 `npm install` 只装下 `index.js`、`cli.js`、`install.js`，`dist/` 是空的，而且不报错，等到第一次构建或启动才暴露。

按 Step 1 把 `install-electron` 声明成 npm script，然后跑 `npm run ensure-electron`。必须走 npm script，不要用 `node node_modules/electron/install.js`：前者由 npm 解析 `node_modules/.bin` 与依赖，后者对 PATH 与执行策略的假设不稳定。

镜像默认注入（确认官方源真能下载才不注入），URL 可换、键名固定：

- `ELECTRON_MIRROR` — Electron 运行时 zip
- `ELECTRON_BUILDER_BINARIES_MIRROR` — electron-builder 的 nsis 等辅助二进制，只在打包阶段才失败，容易漏

唯一有效的就绪口径是 `Test-Path node_modules\electron\dist\electron.exe` 为 True，不要用 install 退出码 0 代替。

换版本时自查：看 `node_modules/electron/package.json` 有没有 `scripts.postinstall`、`bin` 里有没有 `install-electron`，别把本节的版本号当永久结论。

## Step 3 — 主进程与 preload：CJS + sandbox

`electron/tsconfig.json`：`target` ES2022、`module` CommonJS、`moduleResolution` node、`rootDir` `.`、`outDir` `../dist-electron`、`strict` true、`esModuleInterop` true、`types` `["node"]`、`include` `["*.ts"]`。

根 `tsconfig.json` 只管渲染层类型检查（产物由 Vite 出），`include` 指向 `src/**`。

窗口 `webPreferences` 固定为 `preload: path.join(__dirname, 'preload.js')`、`contextIsolation: true`、`nodeIntegration: false`、`sandbox: true`。

`sandbox: true` 下 preload 必须是 CJS，且只能用 `contextBridge` 与 `ipcRenderer`，`require` 其他模块会失败。因此 preload 保持薄：把需要的能力逐个包装成函数、内部转 `ipcRenderer.invoke`，再 `contextBridge.exposeInMainWorld('api', …)`，不在 preload 里放业务逻辑或第三方依赖。

主进程加载页面按环境分支：有 `VITE_DEV_SERVER_URL` 则 `loadURL(它)`，否则 `loadFile(path.join(__dirname, '..', 'dist', 'index.html'))`。

## Step 4 — Vite：`.mts` 与相对 base

配置文件名用 `vite.config.mts`。package.json 无 `"type": "module"` 时 Vite 把 `.ts` 配置按 CJS 加载并告警，`.mts` 显式声明 ESM 后即可与 Step 3 的 CJS 主进程共存。

配置项：`plugins: [react()]`、`base: './'`、`build.outDir: 'dist'`、`build.emptyOutDir: true`、`build.target: 'es2022'`、`build.sourcemap: false`、`server.strictPort: true`。

保持 `root` 为工程根（不改 Vite 的 root 最省事）：根 `index.html` 里用绝对路径引用入口 `<script type="module" src="/src/main.tsx">`，Vite 会从 root 解析它。确实需要原样拷进产物的静态文件放工程根的 `public/`（Vite 默认 `publicDir`，会被拷到 `dist/` 根）；只被构建或打包引用的资源（图标、字体）就近放代码目录，不进 `dist`。

`base: './'` 是硬要求：生产包里主进程用 `file://` 加载 `dist/index.html`，绝对路径 `/assets/...` 会被解析到磁盘根，表现为白屏且没有明显报错。

开发模式用一个 ESM 脚本自己拉，不依赖插件式启动器：`createRequire(import.meta.url)` 后取 `require('electron')`（返回 `electron.exe` 路径），`createServer({ root: ROOT, mode: 'development' })` 并 `listen()`，然后 `spawn(electronBin, ['.'], { cwd: ROOT, stdio: 'inherit', env: { ...process.env, VITE_DEV_SERVER_URL: server.resolvedUrls.local[0] } })`。子进程退出时 `server.close()` 再透传退出码。

打包脚本同理不要依赖 `npx`：直接 `spawnSync(process.execPath, [node_modules/typescript/bin/tsc, '-p', 'electron/tsconfig.json'])`，先跑 `vite build()`。

## Step 5 — electron-builder 配置

```yaml
directories:
  output: release
files:
  - dist/**
  - dist-electron/**
  - package.json
asar: true
win:
  icon: electron/icon.ico
  target:
    - portable
    - nsis
    - dir
  artifactName: ${productName}_${arch}.${ext}
portable:
  artifactName: ${productName}_portable.${ext}
nsis:
  artifactName: ${productName}_setup.${ext}
  oneClick: false
  perMachine: false
  allowToChangeInstallationDirectory: true
```

`files` 必须显式列白名单。不写时源码目录、脚本与工具链产物会一起进 asar，包体膨胀，还把构建机路径信息带进交付物。`electronVersion` 建议写定并与 `node_modules/electron` 的实际版本一致，否则 builder 会自己去下载另一份 Electron（受限网络上直接失败）。

有了那三行白名单，`src/`、`electron/`、`bin/`、`doc/` 天然不进包，不需要再写 exclude；新增目录时只往白名单里加项，不要用 `!` 反向排除。核对口径：`require('@electron/asar').listPackage('release/win-unpacked/resources/app.asar')` 应当只列出 `\dist\...`、`\dist-electron\...`、`\package.json` 三族条目（分隔符是反斜杠）。多出第四族 `node_modules` 不是白名单失效，而是 builder 按 `dependencies` 自动附带——分栏口径与自查见 Step 1。

图标靠 `win.icon` 指到具体文件（`electron/icon.ico`），`win.icon` 是文件路径不是目录约定，写错会静默回落默认图标：缺合法 ico 时 builder 只打 `default Electron icon is used` 并继续打包，是告警不是失败。多尺寸 ico（16/24/32/48/64/128 经典 BMP + 256 PNG）可以用 `bin/make-icon.ps1` 这类纯 System.Drawing 脚本生成，不依赖 ImageMagick/Python；判据不靠肉眼，而是把 exe 内嵌图标取回来逐像素比（`[System.Drawing.Icon]::ExtractAssociatedIcon($exe)`）。

**图标默认本地生成，不联网**：一个像素风字母 monogram 就够交付——取 App 名首字母（A–Z / 0–9），用 5×7 点阵落在 11 格逻辑网格里，每格按 `floor(Size / 11)` 取整数倍像素，所以任何一挡都是硬边、无重采样无抗锯齿，这正是像素风的关键。实测判据（`_icon-probe/make-icon-pixel.ps1 -Letter L`）：七层 `16/24/32/48/64/128` BMP + `256` PNG，`cell=1/2/2/4/5/11/23px`、`glyph=5x7 … 115x161px`，`icon-loaded size=32x32`，16px 挡字母仍可读。

配套的三条口径：像素规则**只写一处**，BMP 层与 PNG 层共用同一个 argb buffer（旧脚本规则写两遍，改一处漏一处必然漂移）；`-Letter` 表外字符要直接报错退出，别画个空方块糊过去；字母色省略时按背景相对亮度自动选 `#111`/`#fff`——与主题 token 的 `--pf-accent-fg` 是同一条规则，底色默认取工程自己的 accent。

PowerShell 5.1 侧实测踩到的三个坑：变量名不区分大小写，`$bg = Parse-HexColor $Bg` 会把自己的参数覆盖掉（症状是汇总行打出数组而不是 hex）；函数返回数组要 `return , $arr`，否则被管道枚举展开；`[int[]]` 形参接 `UInt32[]` 实参会报"字符串格式不正确"，直接 `New-Object int[] 4` 构造最省事。

**（可选）要品牌 logo 才查 selfh.st/icons**（目录 `https://selfh.st/icons/`，源 `github.com/selfhst/icons`）。它按品牌/产品收录 logo：实测 2,951 个唯一图标、7,741 个 `.ico`（每个最多 standard/`-dark`/`-light` 三份），`ICO` 是 16/32/48/64/128 **五帧、没有 256**；自绘的 `bin/make-icon.ps1` 出的是含 256 的七帧。拿现成 ico 当 `win.icon` 合法也省事，但资源管理器大图标与任务栏缩放时 Windows 得放大 128 那帧——要么接受，要么自绘补 256。

**它是品牌 logo 库，不是泛用图标库**：`terminal`/`console`/`book`/`doc`/`man`/`bash` 的精确匹配实测全是 0（`man` 那 84 条含词都是 `caddymanager`、`cert-manager` 这类）。App 代表某个具体产品或技术栈时才优先取品牌位（Linux 命令手册这种就落在 `linux.ico`/`debian.ico`/`ubuntu.ico`），纯概念图标直接回自绘，别在这儿耗时间。

仓库约 378 MB（API `size_kb` 386,890），**别 clone**，取单文件：`https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<slug>.ico`（实测 200，9,117–19,800 字节）。slug 先核对再定：`https://api.github.com/repos/selfhst/icons/git/trees/main?recursive=1`（实测 200、`truncated:false`、7,741 条 `.ico`）。

**许可证实测 `CC-BY-4.0`，交付物必须署名**：在 `doc/` 里记下来源 URL、图标 slug 与取用日期（或写进关于对话框），换图标时同步改这行。这条最容易漏，后果是把带署名义务的 logo 静默打进 exe。

## Step 6 — 三口径的语义差别

| 口径 | 形态 | 代价 |
| --- | --- | --- |
| `portable` | 单 exe，拷了就跑 | 每次启动把整个应用解压到 temp。实测同一应用 portable 6139ms vs 安装后 465ms，13 倍，与 Chromium 无关 |
| `nsis` | 安装包 | 需要安装动作；装后目录约等于 `dir` 口径 |
| `dir` | `win-unpacked/` | 「安装后目录」口径，用它测启动与内存最公平 |

对比不同技术栈的体积或启动时间时先对齐这一栏，否则容易拿 A 的安装目录比 B 的单文件，得出量级错误的结论。

本文里的两组倍率数字来自两个不同应用各自实测，不是同一次测量：对照工程 6139ms vs 465ms（本表），最小验证工程 6561ms vs 533ms（验收清单第 7 条）。两者都落在 12–13 倍，引用时各自标清出处。

## Step 7 — 界面自适应是编码期约束

自适应不是收尾补丁，写第一行代码之前就要把下面几条定下来。

**开工前必须问用户主题方案**（三选一，不要自己拍）：只做浅色 / 只做深色 / 浅色+深色（默认跟随系统并支持切换）。答案直接决定三件事：要不要建两套 token、`nativeTheme.themeSource` 钉成什么值、`BrowserWindow({ backgroundColor })` 填什么底色。选了第三种才需要 `@media (prefers-color-scheme: …)` 分支；只做一个明暗时，把浅色那套干脆不写，比写半套好。

**明暗**：主进程 `nativeTheme.themeSource = 'system'`（注意属性名是 `themeSource`，不是 `theme`）；应用内给三态选择（跟随系统/亮/暗），用户选过就持久化到设置文件，下次启动直接赋那个值。渲染层只靠 CSS 感知：`:root { color-scheme: light dark }` + `@media (prefers-color-scheme: dark)` 覆盖 token，不去读 Electron API（sandbox 下也读不到）。判据：主进程 `nativeTheme.shouldUseDarkColors` 与渲染层 `matchMedia('(prefers-color-scheme: dark)').matches` 必须一致。

产品确定只做一个明暗时（比如浅色色板未定稿），把 `nativeTheme.themeSource` 显式钉成 `'dark'`，而不是留着默认 `'system'` 不管——否则系统切浅色时表单控件、滚动条与 `prefers-color-scheme` 会跟内容打架。这条要写成有意决策记进 `doc/运行与构建（T0Level）.md`，不是遗漏。

**尺寸**：`minWidth`/`minHeight` 定下限；多栏用 CSS grid 断点分档（实测 1400px 三列、1100px 两列、684px 一列），不靠 JS 量窗口。

**字体**：栈以 `system-ui` 打头（`system-ui, 'Segoe UI Variable Text', 'Segoe UI', sans-serif`），`html { font-size: 100% }`，正文字号用 rem（0.875rem = 14px，视觉不变但跟基准）。不要手动 `setZoomFactor`，让它跟 DPI 走；把 `screen.getDisplayMatching(bounds).scaleFactor` 与 `window.devicePixelRatio` 打点记录现场。

**布局尺寸保留 px**：Electron 的页面缩放走 zoomFactor 整页等比，rem 不像浏览器那样能救字号，把上百处 px 全改 rem 收益低、回归风险高（实测：只改字体栈与基准后 `rootPx=16px bodyPx=14px dpr=1`，Dock 与列表尺寸无变化）。全量 rem 只在真的做多档文字缩放时再上。

**标题只有一个真源**：`new BrowserWindow({ title: APP_TITLE })` 再加 `win.on('page-title-updated', (e) => e.preventDefault())`。不 preventDefault 时 `index.html` 的 `<title>` 或渲染层的 `document.title` 会盖掉主进程设定（实测）。

## Step 7b — 外部色板库：工程内种子优先，仓库兜底

要用第三方色板库（例：`bjarneo/100-themes`，100 个主题 × 5 个变体 = 500 个 `colors.toml`）时，别把仓库当运行时依赖：原始文件当种子落进工程，日常生成完全离线，`100themes/` 不存在或不全才回源取。

```text
<proj>/100themes/
├── manifest.json              source + hosts + variants + themes[100] + tokenMap + seed{themes,variantsPerTheme,files,bytes,capturedAt} + generatedBy
├── raw/<主题>_<变体>.toml      种子原文
├── gen-themes.mjs             可选：整目录当模板拷进新工程时自带脚本，否则放 bin/（见下段）
└── README.md                  本节口径在本工程的落地记录 + 完整字段与 token 清单
产物（不进种子目录）：src/styles/themes.css 的 [data-theme="<主题>--<变体>"] + --pf-* token、src/styles/themes.index.json
```

生成脚本有两种位置，各有代价：放 `bin/gen-themes.mjs` 时，种子目录被删（或被 Step 8 当残留清掉）兜底逻辑仍可达；放 `100themes/gen-themes.mjs` 时，整个目录就是可直接拷进新工程的自包含模板，代价是目录没了脚本也没了。脚本按 `basename(脚本所在目录) === '100themes'` 判断种子在哪，两种位置都能跑——但只认真源一处，别两边各留一份副本。工程外的种子用 `--root <工程目录>` 指过去（省略时取脚本上级目录），根目录没有 `package.json` 就退出码 1 并提示，免得把产物写成仓库外层的孤儿 `src/styles/`。主题清单钉在 `manifest.json` 里而不是运行时枚举——`data.jsdelivr.com` 与 CDN 的目录列表接口实测 403，只有单文件 GET 能用。

三种口径（缺文件时按 jsDelivr 三个镜像 → `raw.githubusercontent.com` 的顺序试，取回的内容一律写回 `raw/`，下次就离线）：

- 默认：本地命中就零联网。实测 `本地命中 500/500，未联网`；种子在工程外时加 `--root <工程目录>` 指过去，实测两种跑法的产物按行排序 md5 相同（`bin/` 与 `100themes/` 两种脚本位置也都验过）。
- `--offline`：禁网，有缺口就退出码 1 并列出缺口名。既是 CI 口径，也是「这个目录真能替代仓库」的判据。
- `--fetch-only`：只补种子不写产物。带主题名跑子集（`node bin/gen-themes.mjs watermelon`）会把 `themes.css` 整个覆写成只含那 5 块（实测），所以补种子必须走这条，收尾再跑全量。

拼 URL 的必踩坑：jsDelivr 需要 `/gh/` 段——`https://cdn.jsdelivr.net/gh/<repo>@<ref>/<主题>/<变体>/colors.toml`。漏成 `https://cdn.jsdelivr.net/<repo>@<ref>/...` 时三个镜像全 404，脚本却把它报成「所有源都不可用」，看着像断网（实测踩过）。先直接 GET 一个已知文件看真实状态码，再决定怀疑网络。

等价性验法（别只比体积）：去掉生成物头部几行后按行排序取 md5，离线版与联网版同为 `c3f633b3c399a1d9ef4cd8fcc3d0109f`（12,513 行、500 个选择器、500 条索引全等），剩下只有头部文案与 `arcade`/`arcade-carpet` 先后变字典序这两处。再删一个种子文件走一遍 `--offline` → `--fetch-only`，取回内容 md5 与删除前一致，兜底路径才算通。

体积按实测记：500 个文件表观 343 KB、落盘 2.3 MB，代价主要在 4 KB 簇与 500 次目录项，不在字节。只要产物不要再生能力时，留 `themes.css`（324,785 字节）+ `themes.index.json`（56,002 字节）就能跑，但换 token 映射或补变体就得回源。

## Step 8 — 打包后把目录收敛到最小清单

`release/` 出完后，工程目录应当只剩“代码 + 文档 + 脚本 + 配置 + 产物”，其余删掉。

删：`dist/`、`dist-electron/`（`npm run build` 能重建的中间物），以及自证产生的 `*.log`、`*-out.txt`、`*-err.txt` 和临时分发目录（本地当共享盘用的 `_share/` 之类）。

留：`src/`、`electron/`、`bin/`、`doc/`、根上的 `index.html`+`package.json`+`package-lock.json`+`tsconfig.json`+`vite.config.mts`+`electron-builder.yml`+`.gitignore`，以及 `node_modules/` 与 `release/`。用过外部色板库的工程里 `100themes/` 也归进这一类：它是源数据不是中间物（Step 7b），删了就只能联网重建；而 `.theme-cache/`、`*.log` 这类生成物副本归不进五类，属可删。

两条判据（均实测）：

1. 已打好的包不依赖中间目录——删掉 `dist/` 与 `dist-electron/` 后 `release\win-unpacked\<Name>.exe --selftest` 照样全绿，因为两者已被拷进 `resources/app.asar`。
2. 删完能一把重建：`bin\electron-win-build.ps1 -Target all -SkipInstall` 从最小清单重新产出全部产物，`typecheck` 仍为 0。

自查口径：`ls` 结果里除 `node_modules/` 与 `release/` 外，每一项都应能归进“代码 / 文档 / 脚本 / 配置 / 产物”五类之一；归不进去的就是残留。

## Step 8b — .gitignore：没有就补一份完整的，有就在原文件上补缺

先 `Test-Path .gitignore`。有则**只追加补缺，不整份覆写**——宿主自己已有的规则是信息，不是待清理的脏数据；先读原文件、把缺的分组补进去、再把反向放行挪到末尾。没有才按下面的七组从零写。落盘口径：LF、UTF-8 无 BOM；`git` 对编码不敏感，但 Windows 编辑器会顺手加 BOM/CRLF，别用它保存这份文件。

七组，顺序固定（宽泛规则在前，反向放行在最后）：

1. 依赖：`node_modules/`
2. 构建产物：`dist/`、`dist-electron/`、`release*/`、`*.tsbuildinfo`、`.vite*/`。`release*` 带星号是有讲究的：一次盖住 `release-check/`、`release-asar/` 这类换过输出目录名的临时打包树（写死 `release/` 就漏）。
3. 临时物：`*.log`、`*-out.txt`、`*-err.txt`、`*.tmp`、`*.pid`、`*-probe*.txt`（自证产生的回显文件），再按本工程脚本名枚举一遍 `vite.config.mts.timestamp-*.mjs` 这类工具副产物。
4. 环境：`.env`、`.env.local`、`.env.production`
5. 编辑器：`.vscode/`、`.idea/`、`*.swo`、`*.swp`
6. OS 垃圾：`Thumbs.db`、`desktop.ini`、`$RECYCLE.BIN/`、`.DS_Store`，以及 `nul`——`>nul` 的目标写错就会在目录里落一个真叫 `nul` 的文件，Windows 普通 API 认它是保留名、删不掉，得走 `del "\\?\C:\路径\nul"`。
7. **末尾反向放行块**：被构建/打包引用但属于交付物的资产，例：`!src/emoji/atlas/**`、`!src/emoji/data/**`、`!electron/icon.ico`。

`package-lock.json` 不忽略，并在注释里写明原因（这套 atlas / 拼音索引靠钉死的依赖版本才能重跑出同一份产物）。

第 7 组必须待在文件最后几行——`.gitignore` 是后匹配者赢。实测同一份文件两种摆放：`*.webp` 加在中部那份 `!src/emoji/atlas/**` **之后**，`git check-ignore -v src/emoji/atlas/flags.webp` 打出 `.gitignore:70:*.webp`（资产被吃掉）；把三条反向放行挪到末尾（此时落在 68 行），同一命令打出 `.gitignore:68:!src/emoji/atlas/**`。所以注释里要钉一句「以后追加规则一律加在这三条之前」。

核对方法（不碰用户仓库）：在 `%TEMP%` 建同形空树——`git init -q .` + 拷进待验的 `.gitignore` + 两份清单。清单 A 由真工程 `find` 出「必须被跟踪」的文件（排除 `node_modules`、`dist`、`dist-electron`、`release*`），这份清单本身就是首次提交该有的一切；清单 B 手写一批「必须被忽略」的合成路径，覆盖面要从产物形态取（`release/win-unpacked/resources/app.asar`、`release-asar/probe/electron.exe`、`dist/assets/flags-*.webp`、`node_modules/.vite/deps/react.js`、`tsconfig.tsbuildinfo`、`npm-debug.log.12345`、`bin/smoke-out.txt`、`net-selftest-err.txt`、`.skill-gen-tmp/…`、`.env.production`、`Thumbs.db`、`nul`、`vite.config.mts.timestamp-*.mjs`）。两份各跑一次 `git check-ignore -q --no-index "$p"`：退出码 0 = 被忽略、非 0 = 不忽略，方向不符就计一条误判。实测 47 条必须跟踪 + 35 条必须忽略，两个方向都是 0 误判。

判据本身有个坑：**别拿 `check-ignore` 的退出码当"被忽略"**。不带 `-q` 时它对反向放行也打印匹配行并返回 0（实测上面两种摆放都返回 0）——「有条规则匹配上了」不等于「文件被忽略」。判忽略只用 `-q --no-index` 的退出码，`-v` 只用来回答「是哪条规则赢了」。

最后一句要交代给使用者：`.gitignore` 只对未跟踪文件生效，`git init` 之前或之后已经 `git add` 过的文件，得 `git rm --cached <path>` 才退出索引。

## Step 9 — bin/ 四个双击脚本（bat）：四条会静默咬人的硬坑

给不打开终端的人交付四个 `.bat`，双击即跑。它们只是薄封装，真逻辑仍在前八步的脚本里，额外参数用 `%*` 原样转递：

| 文件 | 封装的命令 | 脚本里自带的判据 |
| --- | --- | --- |
| `start.bat` | `npm run dev` | 先判 `node_modules\electron\dist\electron.exe` 是否存在（判文件不判码，见 Step 2），缺了才 `run ensure-electron`；再 `call "<npm 绝对路径>" run dev`，末尾按 `%RC%` 报告 |
| `stop.bat` | `bin\stop-dev.ps1` | 打 `killed=N remaining=M`，M 不为 0 提示再双击一次 |
| `build-setup.bat` | `bin\electron-win-build.ps1 -Target nsis %*` | 数 `release\*_setup.exe`，为 0 直接 `exit /b 1`；否则逐个打 名字 / `%%~zF` 字节 / `%%~tF` 改动时间 |
| `build-portable.bat` | 同上 `-Target portable` | 同上，通配换 `*_portable.exe`；并提醒便携版 stdout 不冒泡、启动比 dir 口径慢 12–13 倍（Step 6） |

骨架（`build-setup.bat` 的核对段；四个文件同构，首行都 `cd /d "%~dp0.."` 落到工程根）：

```bat
@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0.." || (echo FAIL cannot reach project root & pause & exit /b 1)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "bin\electron-win-build.ps1" -Target nsis %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo !! build failed, exit code %RC%
  pause
  exit /b %RC%
)
set "N=0"
for %%F in ("release\*_setup.exe") do set /a N+=1
if "%N%"=="0" (
  echo FAIL no *_setup.exe under release\
  pause
  exit /b 1
)
for %%F in ("release\*_setup.exe") do echo   %%~nxF   %%~zF bytes   modified %%~tF
pause
exit /b 0
```

四条硬规矩都是实测撞出来的，违反时**没有语法错误、只有错行为**：

1. **整个文件不许有任何非 ASCII 字节**，`rem` 注释也算，硬编码的含中文工程路径也算。默认代码页非 65001 的机器（实测本机 936）上 `chcp` 属于中途换页，cmd 可能按字符数而非字节数重定位后续行，注释残片被当命令执行（实测打出 `'：npm' is not recognized`），且**非确定性**——同一文件跑 3 次只有 1 次坏；另一例：探针文件唯一一处中文是写死的 `cd /d "…\emoji前端组件"`（nonAscii=12），执行在第二条 echo 之后就悄悄断了。工程根一律用 `%~dp0..` 取，提示文字用英文。
2. **`echo` 文本里不许出现裸 `>`**，`==>` 和 `-->` 都算。cmd 在 `>` 处切行、把后一个 token 当文件名、剩下的当载荷写进去。实测两个后果：一个 957 字节的 `.ps1` 被截成 4 字节（内容 `==`），以及在工程根造出 `running`（21 字节，内容 `-- bin\stop-dev.ps1`）。这一行本身什么都不打印，所以"没看到输出"不等于"没执行"。动作行前缀改 `[*]`、失败行用 `!!`；判据就是那行 echo 有没有出现在输出里。
3. **`(` 块内不 `goto`，块内 `echo` 不带 `)`**。右括号提前闭合块：实测 `if "%RC%"=="0" ( echo ok dev exited cleanly (0) ) else (…)` 两个分支全打，且文本被截成 `ok  dev exited cleanly (0`。改成两条单行 `if`，或写 `^)`。
4. **bat 里调 npm 必须用绝对路径**。裸名 `call "npm.cmd"` 时，npm.cmd 自己的 `%~dp0` 展开成**调用方的 cwd**（arg0 没有目录部分），于是它去工程目录找 `node_modules\npm\bin\npm-prefix.js` 并报 MODULE_NOT_FOUND。2×2 探针（chcp 开/关 × ASCII/中文 cwd）四格全灭，而同一条命令在顶层 `cmd /c "call npm.cmd run dev"` 里是好的——差别只在"是否在批处理内用裸名调用"，所以别拿"手工那条能跑"当结论。取绝对路径：

```bat
set "NPMCMD="
for /f "delims=" %%P in ('where npm.cmd 2^>nul') do if not defined NPMCMD set "NPMCMD=%%P"
if defined NPMCMD goto :check_npm
set "NPMCMD=C:\Program Files\nodejs\npm.cmd"
:check_npm
if not exist "%NPMCMD%" ( echo FAIL npm.cmd not found & pause & exit /b 1 )
```

停止侧口径：`stop-dev.ps1` 只按「映像路径是否落在工程目录下」筛进程，绝不 `taskkill /IM electron.exe`（会连坐别的工程与编辑器）。实测 `killing pid=… ×4` → `killed=4 remaining=0`，随后 node / npm / cmd 全归零、Vite 端口释放——杀掉 Electron 主进程后 `bin/dev.mjs` 的子进程退出回调自己关服务器，级联成立，不必在 ps1 里再按命令行匹配 node（它的 `node.exe` 在 Program Files，路径过滤本来抓不到）。

bat 自己也要跑，不能只静态看：产物核对那段拿一个有匹配、一个没匹配的目录各跑一次（实测 `found 2 installer(s)` 并正确跳过 `not_an_installer.txt` / `FAIL-branch-taken` 且退出码 1）。取退出码时别接管道——`cmd | tr` 之后的 `$?` 是 `tr` 的（实测把 rc=1 读成 rc=0），一律 `> file 2>&1` 落盘再读。

## 故障速查

| 症状 | 真因 | 处置 |
| --- | --- | --- |
| `npm install` 全绿，构建时报找不到 electron | 无 postinstall，`dist/` 从未下载 | Step 2 |
| 下载卡在 `electron-v…-win32-x64.zip` | GitHub Releases 不可达 | 注入 `ELECTRON_MIRROR` |
| 打包阶段卡在 nsis / 7zip 下载 | 漏了第二个镜像变量 | 注入 `ELECTRON_BUILDER_BINARIES_MIRROR` |
| 渲染层白屏、控制台无错误 | `base` 仍是 `/`，`file://` 解析到磁盘根 | `base: './'` |
| Vite 启动告警配置按 CJS 加载 | 无 `"type":"module"` 时用了 `vite.config.ts` | 改名 `.mts` |
| preload 里 `require` 报错 | `sandbox: true` 限制 | preload 只用 contextBridge 与 ipcRenderer |
| 双击 exe 秒退且退出码为 0 | `requestSingleInstanceLock` 失败导致 `app.quit()`，常见于上一次运行没清掉子进程 | 先按路径过滤杀掉残留进程再判定，别急着怀疑打包产物 |
| PowerShell 中文提示乱码并吞掉引号 | Win PowerShell 5.1 把无 BOM 文件按 ANSI 解码 | 构建脚本保持 ASCII-only |
| `nativeTheme.theme = 'dark'` 报 TS2339 | 该属性不存在，设置项叫 `nativeTheme.themeSource` | 用 `themeSource`，读取用 `shouldUseDarkColors` |
| 构建已失败但整条管道命令退出码为 0 | `cmd \| grep` 的退出码是 grep 的 | 验证时先落文件再看 `$?`，戒掉用管道接关键步骤 |
| `Start-Process` 传带空格的路径时报 `Cannot find module 'C:\Program'` | `-ArgumentList` 用空格拼接且不自动加引号 | 路径自己包一层双引号，或改用 `& $exe $arg` 直接调用 |
| 便携版 exe 跑起来了但 stdout 什么也捕不到 | portable 宿主只负责解壳到 temp 再拉子进程，子进程输出不冒泡 | 自证断言在 `win-unpacked` 口径做；portable 用标记文件验存活 |
| `HEAD github.com/electron/.../releases` 返回 200，electron-builder 却 `connect ETIMEDOUT` | HEAD 与 GET 打的是不同主机，探测证不住下载 | 镜像默认注入，`-NoMirror` 才走官方源 |
| `ConvertFrom-Json` 报错、package.json 里的中文显示成乱码 | PS 5.1 的 `Get-Content` 默认按 ANSI 读 UTF-8，收尾引号被吞 | 读 package.json 一律 `-Encoding UTF8` |
| `npm warn install-scripts … electron-winstaller` | npm 11 的 allowScripts 默认跳过带 install 脚本的依赖 | 无害，实测不影响 dir/portable/nsis 三口径出包；别误读成 Electron 没装好 |
| 只跑 dir 口径后 `release/` 里仍有上轮的 portable/setup exe | electron-builder 只覆盖本次构建的 target，不清空 output 目录 | 验收前先删空 `release/`，否则会被上轮产物蒙过 |
| 取主题种子报「所有源都不可用」，但同机其他站点能访问 | jsDelivr 的 URL 漏了 `/gh/` 段，三个镜像全 404；404 被当成了“源不可用”，而不是超时 | 先直接 GET 一个已知文件看真实状态码；`raw.githubusercontent.com` 的 URL 形状不同，可当对照组 |
| 从 selfh.st/icons 取 `ico/<slug>.ico` 返回 404 | slug 猜错，或那库里根本没有这个概念（它只收品牌 logo） | 先用 GitHub tree API 列名字再定，不要改镜像 |
| 传了 `-Bg` 却打出数组、颜色不对 | PowerShell 变量名不区分大小写，局部 `$bg` 把参数 `$Bg` 覆盖了 | 局部变量换名（如 `$bgArgb`），别与 param 同名 |
| `themes.css` 整份变成只含一个主题 | 带主题名跑了子集，产物被覆写 | 补种子用 `--fetch-only`，收尾再跑一次全量 |
| 包体多出十几 MB，`listPackage` 的顶层族多了 `node_modules` | dev 依赖被错登进 `dependencies`，builder 按生产依赖自动附带 | 移回 `devDependencies`，删空 `release/` 重打包，族数回到三族 |
| `npm run dev` 正常，装出来的包报 `Cannot find module 'x'` | x 是主进程运行时依赖，却被放进 `devDependencies`，builder 不附带 | 移到 `dependencies`，再用 `--selftest` 断言它真能 require 到 |
| bat 双击后打出 `'：xxx' is not recognized`，且同一文件时好时坏 | 默认代码页非 65001 时 `chcp` 是中途换页，cmd 按字符数重定位后续行 | bat 全文 ASCII-only，提示文字用英文，工程根用 `%~dp0..`（Step 9） |
| bat 里某行 echo 没打印，而它提到的那个文件被截断成几个字节 | `echo` 文本里的裸 `>` 被当重定向（`==>`、`-->` 都算） | 动作行用 `[*]`；判据看那行 echo 有没有出现在输出里（Step 9） |
| if/else 两个分支都打印，或末行文本被截断 | `(` 块内的 `echo` 载荷带了 `)`，提前闭合了块 | 改两条单行 `if`，或写 `^)`（Step 9） |
| bat 里 `call npm.cmd` 报 `Cannot find module '<工程根>\node_modules\npm\bin\npm-prefix.js'`，同一条命令在命令行直接跑却好的 | 批处理内用裸名调用时，npm.cmd 的 `%~dp0` 等于调用方 cwd | bat 里一律用 `where npm.cmd` 取到的绝对路径（Step 9） |
| 写了 `!` 放行，交付资产（雪碧图 / 图标）还是没进提交 | `.gitignore` 后匹配者赢，中部的 `!` 块被后面的规则盖掉 | 反向放行挪到文件末尾，并注释禁止在其后追加（Step 8b） |
| `git check-ignore -v` 返回 0 就断定「这文件被忽略了」 | 不带 `-q` 时反向放行同样打印匹配行且退出码 0；「有规则匹配」≠「被忽略」 | 判忽略只用 `git check-ignore -q --no-index` 的退出码，`-v` 只用来查哪条规则赢（Step 8b） |
| 目录里多出一个叫 `nul` 的文件，资源管理器删不掉 | `>nul` 的目标写错，落成真实存在的保留名文件 | `del "\\?\C:\路径\nul"`；同时把 `nul` 写进 `.gitignore` 的 OS 垃圾组（Step 8b） |
| `git init` 后发现 `dist/` 仍在待提交列表里 | `.gitignore` 只对未跟踪文件生效，已被 `git add` 的不受管 | `git rm --cached -r dist dist-electron release` 再提交（Step 8b） |

## 验收清单

1. `Test-Path node_modules\electron\dist\electron.exe` 为 True。
2. `npm run typecheck` 渲染层与主进程双侧通过。
3. 先删空 `release/`，再跑一次构建；确认三种产物都是本轮产出，且 `win-unpacked/` 存在。
4. 产物自证（不靠人眼看弹窗）：给 `main.ts` 加一个 `--selftest` 分支，在 `ipcMain.handle` 里打一行标记后 `app.quit()`，渲染层启动就 `window.api.ping()`。这样只有“渲染层从 `file://` 加载成功 且 contextBridge 与 IPC 往返都通”才会打出该标记。
5. 用 `Start-Process -PassThru -RedirectStandardOutput` 跑 `win-unpacked\<Name>.exe --selftest`，断言标记出现且残留进程数为 0；再把各产物体积与 `win-unpacked` 总大小打出来与交付口径对上。
6. 自适应四组断言（在 `--selftest` 里用 `webContents.executeJavaScript` 回读渲染层）：`themeSource` 三态下主进程 `shouldUseDarkColors` 与渲染层 `matchMedia` 一致且背景色真的翻转（只做单明暗的工程改成断言 `themeSource` 已显式钉住、两边一致且背景 token 用的是那套色板）；伪造 `{"x":-24000,"y":-24000,"width":9000,"height":9000}` 的持久化几何后窗口仍完整落在某块 `workArea` 内；宽度收到 684px 时栅格列档位数下降；强行 `setBounds(100x100)` 后仍不低于 `minWidth/minHeight`。
7. 便携版单独验：stdout 不冒泡，改用标记文件（`window-shown` / `probed` / `checks-done` 三段），并记下从 `Start-Process` 到首段标记的耗时——实测便携 6561ms vs `win-unpacked` 533ms。
8. 发布守门：产物的 `VersionInfo.FileVersion` 必须等于 `package.json` 的 version 才允许拷贝（实测能抓到“版本号已 bump 但 `release/` 里仍是旧构建”这种情形）。
9. 按 Step 8 收敛目录：删掉 `dist/`、`dist-electron/` 与临时输出后，已打好的包仍能自证通过，再用 `-SkipInstall` 重建能全部产出；`ls` 清单里每一项都归得了类。
10. 用了外部色板库的工程（Step 7b）：`--offline` 生成零联网全绿，产物与联网版按行排序 md5 相同；`100themes/raw/` 文件数等于 manifest 里的 themes × variants（实测 500）；再删一个种子文件验证兜底能取回并写回，md5 与删除前一致。
11. 依赖分栏核对：`npm ls --omit=dev --depth=0` 只剩交付物真需要的运行时包（Vite + tsc 这条路径上通常为空），且 `app.asar` 的顶层族没有多余的 `node_modules`；有则顺着 `dependencies` 找错栏的那个包（成因见 Step 1）。
12. 交付了四个双击 bat（Step 9）时：`start.bat` 要看到 Vite 本地地址 + `window-shown` + IPC 往返标记，`stop.bat` 要看到 `killed=N remaining=0` 且随后 node/electron/cmd 归零、端口释放；两个构建 bat 至少各验一条分支（真失败时 `!!` + 退出码非 0；产物核对段用有匹配/没匹配的构造目录各跑一次，实测 `found 2` 与 `FAIL-branch-taken` rc=1）。退出码一律落盘再读，别接管道。静态审四个文件：非 ASCII 字节 = 0、`CR==LF`、括号配平、`echo` 载荷里没有裸 `>`、块内没有 `goto`。
13. `.gitignore` 到位（Step 8b）：在 `%TEMP%` 同形空树里跑 `git check-ignore -q --no-index` 对两份清单双向核对，「必须跟踪」与「必须忽略」两条都 0 误判（实测 47 + 35），且「必须跟踪」那份清单逐条看一眼——它就是首次提交该有的一切；反向放行三条必须在文件末尾（用 `check-ignore -v` 打出赢的那条规则行号自证）。

## doc/运行与构建（T0Level）.md（新工程必交）

`doc/运行与构建（T0Level）.md` 是给“从没碰过这个工程的人”的上手文档。六个小节缺一不可，每条命令必须本机跑过并把输出贴上去；没跑过的行要显式标“未验证”，不要写想象中的命令。

模板里的 `bin/dev.mjs`、`bin/smoke.ps1`、`bin/smoke-marked.ps1`、`bin/verify-icon.ps1`、`bin/verify-geometry.ps1`、`bin/publish.ps1` 都是新工程自己落地的脚本（本文 Resources 只附了打包主脚本），落的时候照本文各自的判据写；文件名不必完全一致，但六节与“每条命令带判据”不能缩。

六节之外再附一小节「交付前精简」：按 Step 8 删完中间目录与临时输出后，把 `ls` 清单与“删完仍能一把重建”的结果写进去。用了外部色板库的工程（Step 7b）再加一小节「换主题 / 补色板」，写清三条命令与离线判据。交付了 Step 9 的四个 bat 时，在第 1/2/3 节的命令行下面各补一行「或直接双击 `bin\build-setup.bat` / `bin\start.bat`」，并把「双击脚本的判据与四条硬规矩」链到 Step 9——bat 的提示文字是英文这件事也要写明，别让人以为可以随手改成中文。

```markdown
# <项目名> 零基础上手

## 0. 前置
- Node <实测版本>（装在哪、不靠 PATH 时脚本怎么用全路径）
- registry 是否可达；二进制实际走的源（默认镜像，`-NoMirror` 才官方源），以及本次跑出来的那一行提示
- 仓库里不应该提交的：node_modules、dist、dist-electron、release*（完整七组与末尾反向放行见 Step 8b；带星号才盖得住换名的临时打包树）

## 1. 项目启动（首次构建）
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target dir
判据：末尾出现 `==> done`，且 `release\win-unpacked\<Name>.exe` 存在。
首次构建多一步拉 Electron 二进制（`==> Electron binary missing, fetching it`），属正常。

## 2. 本地调试
    npm run dev
判据：Vite 打出本地地址，窗口出现且主进程打点行落到终端；改渲染层代码能热重载。
注意：用 Start-Process 启动 node 时要给带空格的路径自己包双引号，否则报 `Cannot find module 'C:\Program'`。

## 3. 打包与启动
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all
三口径的差别与代价见上文的“三口径”一节。
起安装后目录自证：
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke.ps1
判据：stdout 出现 `ready-to-show`/`title=`/自适应四组打点，末尾 `remaining-processes=0`。
起便携版自证（stdout 不冒泡，靠标记文件）：
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke-marked.ps1 -Exe release\<Name>_portable.exe
实测参考：dir 533ms / portable 6561ms 到 `window-shown`。退出后可能残留 1 个子进程，脚本按进程名清掉自己启动的那几个。

## 4. 修改标题
真源只有一处：`new BrowserWindow({ title: APP_TITLE })`，再加一行
    win.on('page-title-updated', (e) => e.preventDefault())
没有它，index.html 的 <title> 或渲染层的 document.title 会盖掉主进程设定。
判据：smoke 输出 `title=<新标题>`。

## 5. 修改图标
图标默认本地生成：`bin\make-icon.ps1 -Letter <App名首字母>` 出像素字母 monogram（5×7 点阵落进 11 格逻辑网格、整数倍格、无抗锯齿），判据看各层 `cell=Npx` 与末行 `icon-loaded size=32x32`。要品牌 logo 才从 selfh.st/icons 取现成 ico（`https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<slug>.ico`，slug 先用 GitHub tree API 核对）拷成 `electron/icon.ico`，并在 `doc/` 记下来源 URL + 图标 slug + 取用日期——该库 CC-BY-4.0，署名算交付项，且它的 ico 只有 16–128 五帧、没有 256。两种方式最后都要把 `electron-builder.yml` 的 `win.icon` 指到这个文件。
判据不靠肉眼，而是把 exe 内嵌图标取回来逐像素比：
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1
实测输出：`extracted=32x32`，条纹/内块/外圈三个取样点颜色全部命中，`outer-blue-fraction=60.9%`。

## 6. 发布 / 分发
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <共享目录或 \\server\apps> -DryRun
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <同上> -KeepVersions 2
判据：`<Share>\<Name>\<version>\` 下两个 exe 加两个 .sha256，末尾 `retained-versions=2`。
版本发布流程：改 package.json 的 version → 重新打包 → publish（守门比对产物 FileVersion）→ 旧版按 KeepVersions 淘汰。
坑：只 bump 版本号不重打包会被拒（实测），因为 `release/` 里仍坐着旧构建。

## 7. 换主题 / 补色板（用了外部色板库才需要，见 Step 7b）
    npm run themes:offline
判据：打 `本地命中 500/500，未联网` 且退出码 0——这一步同时证明 100themes/ 真能替代仓库。
    npm run themes:seed
只补缺口进 100themes/raw，不写产物。换 token 映射或补变体时先 :seed 再跑全量；
带主题名的子集运行会把 themes.css 覆写成只含那几块，别拿它补种子。
```

## Resources

`electron-win-build.ps1` 把 Step 0 到 Step 5 收成一个入口：定位 node.exe、默认注入二进制下载镜像（`-NoMirror` 才走 GitHub 官方源）、`npm install`、缺二进制才 `ensure-electron`、构建、打包、列出各产物体积。`all`/`portable`/`nsis`/`dir` 四个分支均已实测。拷进工程的 `bin/` 并纳入版本控制，让构建机与本地走同一份脚本（脚本取自身上级目录当默认 `-Root`，放 `bin/` 时算出来仍是工程根）：

```powershell
<#
  One-shot Windows Electron build helper.

  In order: locate node.exe, inject the binary mirrors (pass -NoMirror to pull from
  GitHub instead), npm install, fetch the Electron runtime binary when
  node_modules/electron/dist/electron.exe is missing, build, package, print artifact sizes.

  Kept ASCII-only on purpose: Windows PowerShell 5.1 decodes BOM-less files as ANSI, which
  silently corrupts non-ASCII literals and breaks quote pairing.
#>

param(
  [string]$Root = '',
  [ValidateSet('all', 'portable', 'nsis', 'dir')]
  [string]$Target = 'all',
  [switch]$SkipInstall,
  [switch]$NoMirror,
  [string]$NodeDir = 'C:\Program Files\nodejs',
  [string]$ElectronMirror = 'https://npmmirror.com/mirrors/electron/',
  [string]$BuilderBinariesMirror = 'https://npmmirror.com/mirrors/electron-builder-binaries/'
)

$ErrorActionPreference = 'Stop'

function Step($msg) { Write-Host "==> $msg" }
function Fail($msg) { Write-Host "!! $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host "    $msg" }

if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Split-Path -Parent $PSScriptRoot }
$Root = (Resolve-Path -Path $Root).Path
if (-not (Test-Path (Join-Path $Root 'package.json'))) {
  Fail "no package.json under $Root - pass -Root <project dir>"
}
Step "project root: $Root"

$NodeExe = Join-Path $NodeDir 'node.exe'
if (-not (Test-Path $NodeExe)) {
  $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
  if ($cmd) { $NodeExe = $cmd.Source; $NodeDir = Split-Path -Parent $cmd.Source }
}
if (-not (Test-Path $NodeExe)) { Fail "node.exe not found (looked in $NodeDir and PATH)" }

$NpmCli = Join-Path $NodeDir 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path $NpmCli)) { Fail "npm-cli.js not found at $NpmCli" }

Info ('node: ' + (& $NodeExe -v))
# This shell's PATH may predate the Node install; npm child processes need node resolvable.
$env:Path = $NodeDir + [IO.Path]::PathSeparator + $env:Path

function Invoke-Npm([string[]]$npmArgs) {
  & $NodeExe $NpmCli @npmArgs
  if ($LASTEXITCODE -ne 0) { Fail "npm $($npmArgs -join ' ') exited with $LASTEXITCODE" }
}

if ($NoMirror) {
  Step 'mirror injection skipped (-NoMirror): binaries come from GitHub Releases'
} else {
  # Measured: HEAD https://github.com/electron/electron/releases answers 200, yet
  # electron-builder's GET of the release object times out (HEAD and GET hit different
  # hosts). A reachable HEAD therefore does NOT prove the download works, so mirrors are
  # injected by default and -NoMirror is the explicit opt-out.
  Step 'injecting binary mirrors (override with -NoMirror or by exporting the vars)'
  if (-not $env:ELECTRON_MIRROR) {
    $env:ELECTRON_MIRROR = $ElectronMirror
    Info "ELECTRON_MIRROR = $ElectronMirror"
  }
  if (-not $env:ELECTRON_BUILDER_BINARIES_MIRROR) {
    $env:ELECTRON_BUILDER_BINARIES_MIRROR = $BuilderBinariesMirror
    Info "ELECTRON_BUILDER_BINARIES_MIRROR = $BuilderBinariesMirror"
  }
}

Push-Location $Root
try {
  if (-not (Test-Path (Join-Path $Root 'node_modules'))) {
    if ($SkipInstall) { Fail 'node_modules missing but -SkipInstall was passed' }
    Step 'installing dependencies'
    Invoke-Npm @('install', '--no-audit', '--no-fund', '--loglevel=warn')
  }

  # npm install exiting 0 does NOT mean the binary landed: current Electron has no
  # postinstall, the download lives in bin/install-electron.
  $electronExe = Join-Path $Root 'node_modules\electron\dist\electron.exe'
  if (Test-Path $electronExe) {
    Step 'Electron binary present'
  } else {
    Step 'Electron binary missing, fetching it'
    # -Encoding UTF8: PS 5.1 defaults to ANSI and mangles a package.json containing
    # non-ASCII, which then makes ConvertFrom-Json fail on the broken quotes.
    $pkg = Get-Content (Join-Path $Root 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $pkg.scripts.'ensure-electron') {
      Fail 'package.json has no scripts["ensure-electron"] - add: "ensure-electron": "install-electron"'
    }
    Invoke-Npm @('run', 'ensure-electron')
    if (-not (Test-Path $electronExe)) { Fail 'still missing after ensure-electron' }
  }

  Step "building and packaging ($Target)"
  if ($Target -eq 'all') {
    Invoke-Npm @('run', 'dist')
  } else {
    Invoke-Npm @('run', 'build')
    Invoke-Npm @('exec', 'electron-builder', '--', '--win', $Target)
  }

  $release = Join-Path $Root 'release'
  if (-not (Test-Path $release)) { Fail 'no release directory was produced' }

  Step 'artifacts'
  Get-ChildItem -Path $release -Recurse -File |
    Where-Object { $_.Extension -in '.exe', '.yml' } |
    Sort-Object Length -Descending |
    ForEach-Object {
      Info ("{0,9} MB  {1}" -f [math]::Round($_.Length / 1MB, 1), $_.FullName.Substring($release.Length + 1))
    }

  $unpacked = Join-Path $release 'win-unpacked'
  if (Test-Path $unpacked) {
    $bytes = (Get-ChildItem -Path $unpacked -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Info ("{0,9} MB  [installed directory] win-unpacked" -f [math]::Round($bytes / 1MB, 1))
  }
}
finally {
  Pop-Location
}

Step 'done'
```

跑法（`-Root` 省略时取脚本上级目录）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target portable
```

镜像值仅在环境未设置时写入，不覆盖已有配置；`-NoMirror` 完全不注入，直接用脚本运行时的环境。失败时一律先打印真因再退出，不会把「没装二进制」说成「构建失败」。
