# 仿 macOS 桌面 零基础上手（T0Level）

生成于 2026-10-03，按 `windows-electron-scaffold` Skill（快照见同目录 `SKILL.md`）的交付要求整理。
本文件里每条命令都在本机 Windows PowerShell 5.1 + Git Bash 上真跑过，"判据"就是当时看到的输出；
没跑过的口径集中在最后一节，不要当成事实引用。

约定：所有命令在工程根目录执行。产品名 `MacOSDesktopDemo`，版本 `0.1.0`，Vite 端口 5222。

---

## 0. 前置

- Node：`v24.21.0`，装在 `C:\Program Files\nodejs`。
  **本机终端会话的 PATH 是 Node 安装之前的快照**，直接 `node -v` / `npm -v` 会失败，
  `npm install` 派生的子进程也会找不到 node（实测报 `'"node"' is not recognized`）。两种应对：
  - 构建脚本自己用全路径调 `node.exe` + `npm-cli.js`（见 `bin/electron-win-build.ps1`）；
  - 手动跑 npm 用透传包装：

    ```powershell
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run typecheck
    ```

  实测：同一条 `npm run typecheck`，不加 PATH 前置时退出码 1 并报上面那句，走 `bin\npm.ps1` 时退出码 0。

- 二进制源：npm registry 可达；GitHub 只能算半可达（`HEAD` 200 但 electron-builder 的 GET 会
  `connect ETIMEDOUT`），所以 `bin/electron-win-build.ps1` **默认注入镜像**：

  ```
  ELECTRON_MIRROR = https://npmmirror.com/mirrors/electron/
  ELECTRON_BUILDER_BINARIES_MIRROR = https://npmmirror.com/mirrors/electron-builder-binaries/
  ```

  本轮 Electron 二进制就是走 npmmirror 取回的。确认官方源可用时用 `-NoMirror`。
  代价：`@electron/get` 的 SHASUMS 校验也走同一个镜像，注入即等于信任该源。

- 不要提交：`node_modules/`、`dist/`、`dist-electron/`、`release/`、`_share/`、`*.log`、`*-out.txt`、`*-err.txt`
  （已在 `.gitignore`）。

## 1. 项目启动（首次构建）

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target dir
```

判据（本轮实测）：

```
==> project root: C:\Users\webtu\Desktop\EleAppTest\aaa
==> injecting binary mirrors (override with -NoMirror or by exporting the vars)
==> Electron binary present
==> building and packaging (dir)
==> done
```

全新机器上第二、三行之间会是 `==> Electron binary missing, fetching it`，属正常。
**`npm install` 退出码 0 不代表 Electron 到位**：实测 `npm install` 打 `added 307 packages in 40s`，
而 `node_modules/electron/dist/` 是空的（`ls | wc -l` = 0）。Electron 44.5.1 的 package.json
没有 `scripts` 字段，下载逻辑在 `bin.install-electron`，所以脚本显式跑 `npm run ensure-electron`。

唯一就绪口径：

```powershell
Test-Path node_modules\electron\dist\electron.exe   # 本轮 True，245,726,208 字节
```

两条无害告警，别误读成"没装好"：

```
npm warn deprecated boolean@3.2.0
npm warn install-scripts   electron-winstaller@5.4.0 (install: node ./script/select-7z-arch.js)
```

后者是 npm 11 的 allowScripts 默认跳过带 install 脚本的依赖，实测不影响 dir/portable/nsis 三口径出包。

类型检查（渲染层与主进程分别查）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run typecheck
```

实测：`typecheck-exit=0`，无输出即通过。

## 2. 本地调试

```powershell
npm run dev                      # 或 bin\npm.ps1 run dev
npm run dev -- --selftest        # 开发模式下跑同一套自证
npm run dev -- --selftest --shot-dir _shot   # 再加：在自证每个节点出一张 PNG
```

`bin/dev.mjs` 先 tsc 编译 `electron/`（`package.json` 的 `main` 指向 `dist-electron`），
再起 Vite（固定 5222、`strictPort`）并把地址通过 `VITE_DEV_SERVER_URL` 交给 Electron。

判据：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-dev.ps1
```

```
dev-reached-full-selftest=True
[desktop] adaptive themeSource=dark shouldUseDark=true rendererDark=true agrees=true dataTheme=candy--dark bg=#1c0712 font=system-ui rootPx=16px bodyPx=14px dpr=1 title=仿 macOS 桌面
[desktop] flip-day themeSource=light shouldUseDark=false rendererDark=false agrees=true dataTheme=candy--day bg=#feeff5 darkBg=#1c0712 changed=true
[desktop] flip-dark themeSource=dark agrees=true dataTheme=candy--dark restored=true
[desktop] notes roundtrip ok
[desktop] finder quickAccess=6 first=C:\Users\webtu entries=37 dirs=25
[desktop] finder-jail blocked:outside home directory
```

改主进程要重启 `npm run dev`；改渲染层热更新。

两个坑：

1. `Start-Process` 把 `-ArgumentList` 用空格拼接且不自动加引号，带空格路径必须自己包双引号，
   否则报 `Cannot find module 'C:\Program'`（`bin/verify-dev.ps1` 里就是这么写的）。
2. 从 Git Bash 看 PowerShell 打印的中文会成 `?` 乱码（控制台代码页），**不是应用的问题**：
   同一个 `selftest-out.txt` 按 UTF-8 读回来 `title=仿 macOS 桌面` 是完好的。要看准就用
   `node -e "console.log(require('fs').readFileSync('selftest-out.txt','utf8'))"`。

## 3. 打包与启动

三口径一起出（先删空 `release/`，否则上一轮的同名文件会蒙过验收——builder 不清空输出目录）：

```powershell
Remove-Item -Recurse -Force release -ErrorAction SilentlyContinue
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all
```

实测产物：

```
   234.4 MB  win-unpacked\MacOSDesktopDemo.exe
    95.7 MB  MacOSDesktopDemo_setup.exe
    95.5 MB  MacOSDesktopDemo_portable.exe
     0.1 MB  win-unpacked\resources\elevate.exe
   367.7 MB  [installed directory] win-unpacked
```

包内容白名单核查（`electron-builder.yml` 的 `files` 只有三行）：

```powershell
node bin\verify-asar.mjs
```

```
asar=release\win-unpacked\resources\app.asar
entries=9
families=["dist","dist-electron","package.json"]
outside-whitelist=0
result=ok
```

这一条是**修出来的**：`react`/`react-dom`/`motion` 原本写在 `dependencies`，
electron-builder 会按生产依赖树把整个 `node_modules` 塞进 asar，实测 `entries=1198`、
`families` 里出现 `\node_modules\framer-motion\...`，win-unpacked 385.6 MB。
渲染层依赖全部由 Vite 打进 `dist/assets/*.js`，主进程只 `require` 内置模块与 `electron`，
所以把这三个包移到 `devDependencies` 才是正解（不是加 `!node_modules/**` 反向排除）：
改完 `entries=9`、win-unpacked 367.7 MB，`package-lock.json` 里非 dev 包数 = 0。

安装后目录自证（stdout 能拿到）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke.ps1
```

实测（关键行）：

```
RESULT=exited after 10,098 ms
[desktop] theme-painted ok
[desktop] ready-to-show saved={"x":0,"y":0,"width":2560,"height":1392} ... insideWorkArea=true workArea=2560x1392 scale=1 bg=#1c0712
[desktop] dom {"viewport":"2560x1392","dark":true,"hasApi":true,"dataTheme":"candy--dark","bg":"#1c0712","deskClass":"desktop wall-bigsur","wallpaper":"linear-gradient(115deg, rg","dockItems":5,"dockRunning":0,"menuBar":true,"clock":"周六 10月3日 00:20","windows":0,...}
[desktop] adaptive themeSource=dark shouldUseDark=true rendererDark=true agrees=true ... title=仿 macOS 桌面
[desktop] dock-click clicked(count=5)
[desktop] dom-after-dock {...,"dockRunning":1,"windows":1,"frontmost":"访达","finderRows":37,...}
[desktop] minimize-cycle before=1 after-minimize=0 after-restore=1
[desktop] maximized fillsArea=true class=app-window is-front is-maximized
[desktop] close-first clicked(count=2) / close-second clicked(count=1)
[desktop] spotlight-results query=备 rows=1
[desktop] flip-day ... agrees=true dataTheme=candy--day bg=#feeff5 changed=true
[desktop] flip-dark ... agrees=true restored=true
[desktop] notes roundtrip ok
[desktop] finder quickAccess=6 first=C:\Users\webtu entries=37 dirs=25
[desktop] finder-jail blocked:outside home directory
[desktop] runtime electron=44.5.1 chrome=152.0.7977.130 node=24.21.0 os=10.0.26300
[desktop] checks-done
[desktop] settings-restored palette=candy variant=dark wallpaper=bigsur bg=#1c0712
remaining-processes=1
```

`10,098 ms` 是整条自证的耗时（脚本里刻意留了 900ms 的动画等待），不是启动时间——
启动时间看下面"三口径"那一栏。`remaining-processes=1` 后脚本按名清掉了自己拉起的那个残留；
另一次干净复跑是 `remaining-processes=0`。

三口径的语义差别与代价：

| 口径 | 形态 | 本轮实测到 `window-shown` | 代价 |
| --- | --- | --- | --- |
| `dir` | `win-unpacked/` | 549 ms | 测启动与内存最公平的口径 |
| `portable` | 单 exe | 6,572 ms | 每次启动整包解压到 temp，实测 12.0 倍 |
| `nsis` | 安装包 | 未测（没执行安装） | 装后目录≈`dir` |

便携版单独验（stdout 不冒泡，靠标记文件）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke-marked.ps1 -Exe release\MacOSDesktopDemo_portable.exe
```

```
window-shown after 6,572 ms
probed after 16,014 ms
checks-done after 16,014 ms
marks=3/3
shown-to-checks-done=9,442 ms
remaining-processes=0
```

同一脚本对 `dir` 口径跑出的对照：`window-shown after 549 ms`、`marks=3/3`。

坑：`-MarkDir` 传相对路径时便携版会 `marks=0/3`——portable 宿主解壳到 temp 后用别的工作目录起子进程。
脚本现在把 `-MarkDir` 一律解析成绝对路径并显式给 `Start-Process -WorkingDirectory`，
用相对路径复跑一次确认 `marks=3/3` 才算过。

## 4. 修改标题

真源只有一处：`electron/main.ts` 的 `APP_TITLE`，传给 `new BrowserWindow({ title: APP_TITLE })`，
并且必须保留

```ts
win.on('page-title-updated', event => event.preventDefault())
```

`index.html` 里也写着 `<title>仿 macOS 桌面</title>`；不 preventDefault 时页面标题会盖掉主进程设定，
改标题会出现"改了没用"的假象。窗口本身是 `frame: false`，标题只出现在任务栏与窗口管理器里。

判据：`bin/smoke.ps1` 的 `adaptive` 行里 `title=` 是新值。本轮实测 `title=仿 macOS 桌面`。

## 5. 修改图标

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\make-icon.ps1
```

实测输出（16/24/32/48/64/128 为经典 BMP，256 为 PNG）：

```
ico=electron\icon.ico bytes=103991 reserved=0 type=1 count=7
  layer 16x16 BMP bytes=1128
  layer 32x32 BMP bytes=4264
  layer 256x256 PNG bytes=1841
icon-loaded size=32x32
```

图案是"桌面 + 菜单栏 + Dock 三条磁贴"的确定性像素规则，BMP 与 PNG 层共用同一个
`Get-LayerPixel`，所以能用采样点核对。`electron-builder.yml` 里 `win.icon: electron/icon.ico`
指到这个文件（`win.icon` 是文件路径不是目录约定，写错只会打 `default Electron icon is used` 告警并继续打包）。

判据不靠肉眼，把 exe 内嵌图标取回来逐像素比：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1
```

```
extracted=32x32
menubar-16-2     B=F2 G=F5 R=F8 A=FF
body-24-12       B=B2 G=6E R=1F A=FF
mark-10-12       B=FF G=FF R=FF A=FF
band-4-25        B=2A G=1E R=14 A=FF
tile-16-25       B=FF G=FF R=FF A=FF
corner-0-0       B=00 G=00 R=00 A=00
body-blue-fraction=63.6%
version=0.1.0 product=MacOSDesktopDemo fileDescription=MacOSDesktopDemo
```

六个取样点全部命中规则值，圆角外 `A=00`，`version` 与 `package.json` 一致。

## 6. 发布 / 分发

分发目标是 `<Share>\MacOSDesktopDemo\<version>\`，先只看要拷什么：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share _share -DryRun
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share _share -KeepVersions 2
```

判据（实测，本机用 `_share` 目录当共享盘替身）：

```
share-target=_share\MacOSDesktopDemo\0.1.0
      95.5 MB  v0.1.0  sha256=D46132462C941D66  MacOSDesktopDemo_portable.exe
      95.7 MB  v0.1.0  sha256=4D185C10A0D933E3  MacOSDesktopDemo_setup.exe
retained-versions=1
```

落盘四个文件：两个 exe + 两个 `.sha256`。超过 `-KeepVersions` 的旧版本目录会被淘汰。

版本发布流程：改 `package.json` 的 `version` → **重新打包** → `publish.ps1`。
守门比对产物的 `VersionInfo.FileVersion`，实测拦住了"只改号不重打包"：

```
"version": "0.2.0",
!! artifact MacOSDesktopDemo_portable.exe was built as 0.1.0 but package.json says 0.2.0 - rebuild before publishing
gate-exit=1
```

（验完把 `version` 改回 `0.1.0`，产物与源码重新对齐。）

另两条 PS 5.1 硬约束：`bin/*.ps1` 保持 ASCII-only（含中文的脚本必须带 BOM，否则被按 ANSI 解码，
引号配对会被吞掉）；`publish.ps1` 读 `package.json` 带 `-Encoding UTF8`，否则中文 `description`
按 ANSI 解码后 `ConvertFrom-Json` 直接失败。

## 7. 交付前精简

按 Skill Step 8 收敛。两条判据，均本机实测：

1. **已打好的包不依赖中间目录**：`rm -rf dist dist-electron` 之后
   `bin\smoke.ps1` 仍然全绿——`RESULT=exited after 10,395 ms`、`theme-painted ok`、
   `notes roundtrip ok`、`finder-jail blocked`、`remaining-processes=0`。
   原因是两者已被拷进 `release\win-unpacked\resources\app.asar`。
2. **删完能一把重建**：`bin\electron-win-build.ps1 -Target all -SkipInstall` 退出码 0，
   重新产出 `MacOSDesktopDemo_portable.exe`、`MacOSDesktopDemo_setup.exe`（+ blockmap）、`win-unpacked/`，
   体积与上一轮一致（95.5 / 95.7 / 367.7 MB）；`bin\npm.ps1 run typecheck` 仍为 0。

删掉的中间物：`dist/`、`dist-electron/`、`_share/`（本地当共享盘的替身）、
`mark-probe/`、`_marks-dir/`、`_marks-portable/`、`_shot/`（截图脚手架）、
以及自证产生的 `*-out.txt`、`*-err.txt`。

留着的每一项都能归进"代码 / 文档 / 脚本 / 配置 / 产物"：

```
.gitignore  package.json  package-lock.json  index.html  tsconfig.json  vite.config.mts  electron-builder.yml
src/        代码：桌面壳 components/ + 应用 apps/ + 样式 styles/ + lib/ + data/
electron/   代码：main.ts preload.ts tsconfig.json icon.ico
bin/        脚本：dev.mjs build.mjs gen-themes.mjs electron-win-build.ps1 npm.ps1 smoke.ps1
            smoke-marked.ps1 verify-dev.ps1 verify-geometry.ps1 verify-icon.ps1 verify-asar.mjs make-icon.ps1 publish.ps1
100themes/  种子：raw/(500) manifest.json preset.json README.md
doc/        文档：本文件 + README.md + SKILL.md
release/    产物；node_modules/ 依赖
```

对外交付就拷 `release/` 里的 exe（或走 `bin/publish.ps1`）。

## 8. 换主题 / 补色板

色板来自 `bjarneo/100-themes`，以离线种子形式固化在 `100themes/`（详见 `100themes/README.md`）。
本工程的 `bin/gen-themes.mjs` 是种子里那份脚本的**改过一版**：位置参数缺省时读
`100themes/preset.json` 当白名单，所以默认只把 24 个主题 × 5 变体写进产物，而 `raw/` 仍保留 500 份全量。
加 `--all` 忽略白名单出全量。

```powershell
npm run themes:offline    # 判据：本地命中 120/120，未联网；退出码 0
npm run themes:seed       # 只补缺口进 100themes/raw，不写产物
npm run themes            # 全量重生成产物（走白名单）
```

实测三条：

```
白名单 100themes/preset.json：24/100 个主题
处理 24 个主题 × 5 个变体
  本地命中 120/120，未联网
已写出 src/styles/themes.css（120 个主题块）与 themes.index.json（120 条）
```

**白名单产物与全量产物等价性**（不能只比体积）：先跑 `--all --offline` 出全量
（`本地命中 500/500`、500 个选择器、12,514 行），再跑白名单版，逐块比对：

```
preset blocks=120 not-in-full=0
index preset=120 all entries in full index=true
```

即 120 个 `[data-theme]` 块与全量文件里对应块**逐字节相同**，索引 120 条也全是全量子集。

**改过的脚本没改坏生成逻辑**：拿未打补丁的 `../100themes/gen-themes.mjs --root . --offline` 做对照，
去掉三行头部后按行排序取 md5，两边同为 `10edb3f0fa202fce330dc0013eaec0a8`，
`themes.index.json` 逐字节相同，差异只在头部注释那一行（`bin/` vs `scripts/`、多写"白名单"）。

> 更正一处仓库内文档的口径：`100themes/README.md` 记的"离线基准 md5 `c3f633b3…`"在本机
> 用六种头部裁剪口径（drop 0..5）与两种排序拼接口径都没能复现。本机可复现的基准是上面那个
> `10edb3f0…`（patched 与 reference 同值）。别拿 `c3f633b3…` 当验收判据，也别据此怀疑种子坏了。

**兜底路径**（种子缺文件时能不能回源）：删掉 `100themes/raw/watermelon_oled.toml` →

```
生成失败：--offline 且 100themes/raw 缺 1 个文件（前 10 个：watermelon_oled）    # 退出码 1，缺口点名
node bin/gen-themes.mjs --fetch-only
  本地缺 1/120 个，走 CDN 兜底
  取文件用 cdn.jsdelivr.net
  已取回 1 个文件并写进 100themes/raw/，下次运行可离线
--fetch-only：只补 100themes/raw，不写产物；缺口 0/120
md5-after=854ff8e10277bee5a1b54732de416b8c   # 与删除前一致
```

收尾再跑 `npm run themes:offline` → `本地命中 120/120，未联网`。
补种子必须走 `--fetch-only`：带主题名的子集运行会把 `themes.css` 整个覆写成只含那几块。

## 自适应实现与判据

- **明暗是产品能力，两套 token 由色板变体提供**：`变体=自动` 时按 `prefers-color-scheme`
  在 `<主题>--dark` / `<主题>--day` 之间挑；选了具体变体（`day`/`oled`/`high-contrast`/…）
  就由该变体自带的 `mode` 反向翻转 `nativeTheme.themeSource`。所以 `themeSource` 不是钉死的，
  判据是两边一致：`flip-day ... agrees=true`、`flip-dark ... agrees=true`，
  且背景真的翻转（`bg=#1c0712` → `#feeff5` → 还原 `restored=true`）。
  渲染层只读 CSS：`window.matchMedia('(prefers-color-scheme: dark)')`，不碰 Electron API。
  `<html data-mode>` 同步 `color-scheme`，管住原生滚动条与表单控件。
- **窗口几何**：`minWidth=900 / minHeight=560`，几何持久化到 `%APPDATA%\MacOSDesktopDemo\window.json`，
  恢复时按 `screen` 的 workArea 夹紧。负向用例实测（`bin\verify-geometry.ps1`）：

  ```
  wrote-saved={"x":-24000,"y":-24000,"width":9000,"height":9000}
  [desktop] ready-to-show saved={...} clamped={"x":0,"y":0,"width":2560,"height":1392} bounds={"x":0,"y":0,"width":2560,"height":1392} insideWorkArea=true workArea=2560x1392 scale=1
  after-run-window.json={"x":0,"y":0,"width":2560,"height":1392}
  restored original window.json
  ```

- **桌面壳本身铺满 workArea**（`frame:false` + 无边框），页内窗口的拖拽/缩放边界由
  `ResizeObserver` 盯着 `.desk-area` 算，工作区变小时超界窗口会被拉回（自证里
  `maximized fillsArea=true`）。
- **字体**：`system-ui, 'Segoe UI Variable Text', 'Segoe UI', 'Microsoft YaHei', -apple-system, sans-serif`，
  `html { font-size: 100% }`，正文 `0.875rem`。实测生效值 `font=system-ui rootPx=16px bodyPx=14px dpr=1`。
  没有手动 `setZoomFactor`，缩放跟 DPI 走。
- **布局尺寸保留 px**：Electron 页面缩放走 zoomFactor 整页等比，把上百处 px 改 rem 收益低、
  回归面大（Dock 磁贴、菜单栏高度、窗口标题栏都靠 px 对齐）。

## 未验证 / 环境受限

1. **nsis 安装到目标机后的体验**：只验了安装包能产出、`FileVersion` 与哈希一致，没执行安装。
2. **高 DPI（125%/150%）下的布局**：本机单屏 `scaleFactor=1`，只验证了读取链路与
   `zoomFactor` 未被改写。
3. **真实网络共享盘**：`publish.ps1` 用本地 `_share` 跑的，`\\server\share` 的权限与延迟未测。
4. **菜单栏里的 Wi-Fi / 电池 / 睡眠 / 重启 / 关机**：只有形状或显式禁用，不联网也不控制电源。
   页内窗口不是原生窗口，所以 Windows 任务栏的"每窗口一个条目"、真实 Alt-Tab 列表不在本工程的实现范围里。
5. **Dock 放大镜动画的触控板/触屏表现**：`motion` 的磁贴放大走 `onMouseMove`，只验过鼠标。
