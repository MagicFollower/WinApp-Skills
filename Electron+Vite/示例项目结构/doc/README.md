# Linux 命令手册（Electron 桌面应用）

底部 Dock 选主题 → 左侧命令列表 → 右侧 markdown 文档，可预览也可编辑，改动存到自己机器的用户目录。

## 界面结构

```
┌───────────────────────────────────────────────┐
│ topbar  主题名 · 简介 · 条数        [内置/用户] │
├───────────────┬───────────────────────────────┤
│ 命令列表       │ 文档标题 / 预览·编辑 / 保存·还原 │
│ (AnimatedList)├───────────────────────────────┤
│ 命令名        │                               │
│ 一句话说明     │ markdown 预览 或 textarea 编辑  │
│ …             │                               │
├───────────────┴───────────────────────────────┤
│ statusbar  保存状态 · 上次保存时间 · 未保存提示   │
├───────────────────────────────────────────────┤
│  Dock（8 个主题，panelHeight=70 magnification=80）│
└───────────────────────────────────────────────┘
```

- **Dock**：8 个主题——文件与目录、文本处理、进程与服务、网络、压缩与归档、权限与用户、磁盘与性能、Shell 与快捷键。参数按 `panelHeight=70`、`magnification=80`；当前主题磁贴带紫色描边与底部圆点。
- **左侧列表**：AnimatedList 改造版，每项两行（命令名 + 一句话说明），点击切换右侧文档；每个主题各自记住选中项。
- **右侧文档**：`预览` 用 react-markdown + remark-gfm 渲染，`编辑` 是 textarea。`Ctrl+S` 保存，脏改动会在状态条提示；`还原内置` 删除本地覆盖、回到随包内容。

## 内容从哪来

80 篇内置手册在 `src/content/<主题>/<命令>.md`，构建时由 `import.meta.glob('...?raw')` 直接打进 JS 包，随包只读。目录条目（主题、命令名、一句话说明）的唯一出处是 `src/data/manual.ts`——**加命令要先在这里登记，再建同名 md**，否则 Dock/列表看不到。

用户编辑不改动随包文件，而是写成覆盖层：

```
%APPDATA%\linux-command-manual\edits\<主题>\<命令>.md
```

启动时按 `<主题>/<命令>` 读覆盖层，存在就显示用户版本（右上角标「用户版本」），不存在就显示内置内容。docId 在主进程侧按 `^[a-z0-9][a-z0-9_-]{0,40}$` 两段校验，避免 `../` 穿越。

## reactbits 组件的接入方式

reactbits 不是 npm 包，走的是「拷源码」。这里用的是官方仓库 `DavidHDev/react-bits` 的 **`src/ts-default/`（TypeScript + 纯 CSS）** 变体，只需要 `motion` 一个运行时依赖，**不需要 Tailwind**，所以直接 vendor 进 `src/components/reactbits/`：

| 组件 | 处理方式 |
| --- | --- |
| `Dock.tsx` / `Dock.css` | 原样保留（去掉 RSC 的 `'use client'`），选中态用 `item.className` + 宿主 CSS 加 `.dock-item.is-active` |
| `AnimatedList.tsx` / `.css` | 两处改造：条目从 `string` 换成 `{id,title,subtitle}` 渲染双行；**删掉上游挂在 `window` 上的 `ArrowUp/ArrowDown/Tab/Enter` 全局监听**——不删的话，在右侧 textarea 里按方向键或 Tab 会同时移动左侧选中项 |

Dock 另外传了 `dockHeight={124}`：官方默认 256 是让 hover 时外层撑到 256px 高的透明区（网页里贴屏幕底部看不出来），在这里会盖住工作区下沿并吞鼠标事件。

## 目录结构

代码与 `bin/`、`doc/` 同级平放，图标就近放主进程目录，约定来自 `windows-electron-scaffold` Skill：

```
<proj>/
├── index.html  package.json  package-lock.json
├── vite.config.mts  tsconfig.json  electron-builder.yml  .gitignore
├── src/          渲染层（App、components、content、data、styles）
├── electron/     主进程 + preload + tsconfig.json + icon.ico
├── bin/          脚本：build.mjs、dev.mjs、electron-win-build.ps1、验证与发布脚本
├── doc/          本文、运行与构建（T0Level）.md
└── dist/  dist-electron/  release/   产物，目录名固定；node_modules/ 只本地保留
```

零基础上手（含每条命令的实测判据）见 [运行与构建（T0Level）.md](运行与构建（T0Level）.md)。

## 命令

Node 装在 `C:\Program Files\nodejs`，但终端会话的 PATH 可能取的是安装之前的快照，所以 `bin/electron-win-build.ps1` 自己定位 `node.exe` 并把 Node 目录前置进 PATH。

```powershell
# 一步到位：装依赖 → 确保 electron.exe 落地 → 构建 → 打包三口径
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all
```

本机实测坑：`HEAD https://github.com/electron/electron/releases` 返回 200，但 electron-builder 真正去拉 release 对象时会 `connect ETIMEDOUT 20.205.243.166:443`——HEAD 与 GET 打的是不同主机，**探测证不住下载**。所以脚本现在默认注入镜像（不再拿 HEAD 结果决定）：

- `ELECTRON_MIRROR`、`ELECTRON_BUILDER_BINARIES_MIRROR`（默认 npmmirror）
- `-NoMirror` 显式走 GitHub；环境里已设的值不被覆盖
- 注意镜像与 SHASUMS 校验同源，用镜像即等于信任该源；官方源确实可达时才用 `-NoMirror`

手动预设等价于：

```powershell
$env:ELECTRON_MIRROR = 'https://npmmirror.com/mirrors/electron/'
$env:ELECTRON_BUILDER_BINARIES_MIRROR = 'https://npmmirror.com/mirrors/electron-builder-binaries/'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all -SkipInstall
```

分开跑：

```powershell
npm run dev        # 先 tsc 编译 electron/（package.json main 指向 dist-electron），再起 Vite + Electron
npm run typecheck  # 渲染层与主进程两侧 tsc
npm run build      # vite build + tsc 编译 electron/
npm run dist       # build + electron-builder --win
```

改主进程要重启 `npm run dev`；改渲染层热更新。`npm run dev -- --selftest` 可以在开发模式下跑同一套自证。

## 交付口径

`release/` 下同时产出三种（口径含义不同，比较体积/启动时间前先对齐）：

| 口径 | 形态 | 说明 |
| --- | --- | --- |
| `portable` | 单 exe | 拷了就跑，但每次启动把整个应用解压到 temp，启动明显慢 |
| `nsis` | 安装包 | 需安装，装后目录≈`dir` |
| `dir` | `win-unpacked/` | 「安装后目录」，测启动与内存最公平 |

主进程 `sandbox: true` + `contextIsolation: true` + `nodeIntegration: false`，preload 只做 `contextBridge` 转发；Vite `base: './'`（生产用 `file://` 加载，绝对路径会白屏）。

## 自证

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-geometry.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1
```

`--selftest` 会打印：`ready-to-show`（含持久化几何、夹紧结果、是否落在工作区、scaleFactor）、
`adaptive`（`themeSource` 与渲染层 `prefers-color-scheme` 是否一致、生效字体、根/正文字号、dpr、窗口标题）、
DOM 探针（Dock 磁贴数、列表项数、预览区、当前文档标题）、覆盖层写→读→删往返结果与 `editsDir`，然后自动退出。

明暗是**有意固定深色**：`nativeTheme.themeSource = 'dark'`，不跟随系统，因为浅色色板未定稿。
要改成跟随系统，把那行换成 `'system'` 并补 `@media (prefers-color-scheme: light)` 的一套 token。
