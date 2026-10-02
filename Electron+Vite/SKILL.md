---
name: windows-electron-scaffold
description: 在 Windows 上从零搭建、整理并打包 Electron 44+ 桌面应用的可复现流程——显式获取 Electron 二进制、默认注入下载镜像、主进程与 preload 走 CJS + sandbox、Vite 用 .mts 配置与相对 base、electron-builder 同时产出 portable/nsis/dir、按 src/ + electron/ + bin/ + doc/ 的目录约定收敛工程、把窗口尺寸与字体明暗自适应当编码期约束、交付 doc/运行与构建（T0Level）.md。当出现 npm install 退出码 0 但 node_modules/electron/dist/electron.exe 缺失、Electron 或 electron-builder 从 GitHub Releases 下载失败、需要配置 Windows 打包目标、打包产物秒退需要排查、要把既有 Electron 工程按目录约定重新整理、要写运行与构建/发布文档、或要判定桌面端自适应是否达标时使用。不用于普通 Web/React 页面开发。
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

## 目录布局（新建工程先定）

新工程一律开在一个独立的新目录里。代码与 `bin/`、`doc/` 同级平放，不再套 `code/` 父目录；图标就近放在主进程目录里，不建 `assets/`：

```text
<proj>/
├── index.html                Vite 入口，必须在根
├── package.json  package-lock.json
├── vite.config.mts  tsconfig.json  electron-builder.yml  .gitignore
├── src/                      渲染层：组件、样式、lib、内容
├── electron/                 主进程 + preload + tsconfig.json + icon.ico
├── bin/                      脚本：dev.mjs、build.mjs、*.ps1
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

有了那三行白名单，`src/`、`electron/`、`bin/`、`doc/` 天然不进包，不需要再写 exclude；新增目录时只往白名单里加项，不要用 `!` 反向排除。核对口径：`require('@electron/asar').listPackage('release/win-unpacked/resources/app.asar')` 应当只列出 `\dist\...`、`\dist-electron\...`、`\package.json` 三族条目（分隔符是反斜杠）。

图标靠 `win.icon` 指到具体文件（`electron/icon.ico`），`win.icon` 是文件路径不是目录约定，写错会静默回落默认图标：缺合法 ico 时 builder 只打 `default Electron icon is used` 并继续打包，是告警不是失败。多尺寸 ico（16/24/32/48/64/128 经典 BMP + 256 PNG）可以用 `bin/make-icon.ps1` 这类纯 System.Drawing 脚本生成，不依赖 ImageMagick/Python；判据不靠肉眼，而是把 exe 内嵌图标取回来逐像素比（`[System.Drawing.Icon]::ExtractAssociatedIcon($exe)`）。

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

## Step 8 — 打包后把目录收敛到最小清单

`release/` 出完后，工程目录应当只剩“代码 + 文档 + 脚本 + 配置 + 产物”，其余删掉。

删：`dist/`、`dist-electron/`（`npm run build` 能重建的中间物），以及自证产生的 `*.log`、`*-out.txt`、`*-err.txt` 和临时分发目录（本地当共享盘用的 `_share/` 之类）。

留：`src/`、`electron/`、`bin/`、`doc/`、根上的 `index.html`+`package.json`+`package-lock.json`+`tsconfig.json`+`vite.config.mts`+`electron-builder.yml`+`.gitignore`，以及 `node_modules/` 与 `release/`。

两条判据（均实测）：

1. 已打好的包不依赖中间目录——删掉 `dist/` 与 `dist-electron/` 后 `release\win-unpacked\<Name>.exe --selftest` 照样全绿，因为两者已被拷进 `resources/app.asar`。
2. 删完能一把重建：`bin\electron-win-build.ps1 -Target all -SkipInstall` 从最小清单重新产出全部产物，`typecheck` 仍为 0。

自查口径：`ls` 结果里除 `node_modules/` 与 `release/` 外，每一项都应能归进“代码 / 文档 / 脚本 / 配置 / 产物”五类之一；归不进去的就是残留。

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

## doc/运行与构建（T0Level）.md（新工程必交）

`doc/运行与构建（T0Level）.md` 是给“从没碰过这个工程的人”的上手文档。六个小节缺一不可，每条命令必须本机跑过并把输出贴上去；没跑过的行要显式标“未验证”，不要写想象中的命令。

模板里的 `bin/dev.mjs`、`bin/smoke.ps1`、`bin/smoke-marked.ps1`、`bin/verify-icon.ps1`、`bin/verify-geometry.ps1`、`bin/publish.ps1` 都是新工程自己落地的脚本（本文 Resources 只附了打包主脚本），落的时候照本文各自的判据写；文件名不必完全一致，但六节与“每条命令带判据”不能缩。

六节之外再附一小节「交付前精简」：按 Step 8 删完中间目录与临时输出后，把 `ls` 清单与“删完仍能一把重建”的结果写进去。

```markdown
# <项目名> 零基础上手

## 0. 前置
- Node <实测版本>（装在哪、不靠 PATH 时脚本怎么用全路径）
- registry 是否可达；二进制实际走的源（默认镜像，`-NoMirror` 才官方源），以及本次跑出来的那一行提示
- 仓库里不应该提交的：node_modules、dist、dist-electron、release

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
放多尺寸 `electron/icon.ico`（16/24/32/48/64/128 经典 BMP + 256 PNG），用 `bin/make-icon.ps1` 这类纯 System.Drawing 脚本生成，`electron-builder.yml` 里 `win.icon: electron/icon.ico` 指到这个文件。
判据不靠肉眼，而是把 exe 内嵌图标取回来逐像素比：
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1
实测输出：`extracted=32x32`，条纹/内块/外圈三个取样点颜色全部命中，`outer-blue-fraction=60.9%`。

## 6. 发布 / 分发
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <共享目录或 \\server\apps> -DryRun
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share <同上> -KeepVersions 2
判据：`<Share>\<Name>\<version>\` 下两个 exe 加两个 .sha256，末尾 `retained-versions=2`。
版本发布流程：改 package.json 的 version → 重新打包 → publish（守门比对产物 FileVersion）→ 旧版按 KeepVersions 淘汰。
坑：只 bump 版本号不重打包会被拒（实测），因为 `release/` 里仍坐着旧构建。
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
