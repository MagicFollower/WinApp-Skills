# Linux 命令手册 零基础上手（T0Level）

生成于 2026-10-02，按 `windows-electron-scaffold` Skill 的交付要求整理。
本文件里每条命令都在本机 Windows PowerShell 5.1 上真跑过，"判据"就是当时看到的输出；
没跑过的口径集中列在最后一节，不要当成事实引用。

约定：所有命令在工程根目录执行。产品名 `LinuxCmdManual`，版本 `0.1.0`，Vite 端口 5211。

## 0. 前置

- Node：`v24.21.0`，装在 `C:\Program Files\nodejs`。
  **终端会话的 PATH 可能是 Node 安装之前的快照**，直接敲 `node -v` 会失败。两种应对：
  构建脚本自己用全路径调 `node.exe` + `npm-cli.js`；要手动跑 npm 就用透传包装：

  ```powershell
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run typecheck
  ```

- 二进制源：npm registry 可达；**GitHub 只能算"半可达"**——本机实测
  `HEAD https://github.com/electron/electron/releases` 返回 200，而 electron-builder 真去
  GET release 对象时会 `connect ETIMEDOUT 20.205.243.166:443`。HEAD 与 GET 打的是不同主机，
  探测证不住下载，所以 `bin/electron-win-build.ps1` **默认注入镜像**，不再拿 HEAD 结果决定。
  确认官方源可用时用 `-NoMirror`。
  注意：`@electron/get` 的 SHASUMS 校验也走同一个镜像，用镜像即等于信任该源。
- 不要提交：`node_modules/`、`dist/`、`dist-electron/`、`release/`、`_share/`、`*.log`
  （已在 `.gitignore`）。

## 1. 项目启动（首次构建）

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target dir
```

判据（本轮实测顺序）：

```
==> project root: ...\aaa
==> injecting binary mirrors (override with -NoMirror or by exporting the vars)
==> Electron binary present
==> building and packaging (dir)
==> done
```

全新机器上第三行会是 `==> Electron binary missing, fetching it`，属正常：
Electron 44 的包里没有 postinstall，`npm install` 退出码 0 不代表 `electron.exe` 落地，
所以脚本显式跑 `npm run ensure-electron`（= `install-electron`）并校验
`node_modules\electron\dist\electron.exe`。

类型检查（渲染层与主进程分别查）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run typecheck
```

实测：`typecheck-exit=0`，无输出即为通过。

## 2. 本地调试

```powershell
npm run dev                      # 或 bin\npm.ps1 run dev
npm run dev -- --selftest        # 开发模式下跑同一套自证
```

`bin/dev.mjs` 先 tsc 编译 `electron/`（`package.json` 的 `main` 指向 `dist-electron`），
再起 Vite（`vite.config.mts` 固定 5211、`strictPort`）并把地址通过 `VITE_DEV_SERVER_URL` 交给 Electron。

判据（实测）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-dev.ps1
```

```
dev-reached-selftest=True
[manual] adaptive themeSource=dark shouldUseDark=true rendererDark=true agrees=true font=system-ui rootPx=16px bodyPx=14px dpr=1 title=Linux 命令手册
torn-down: npm PID=20600
```

改主进程要重启 `npm run dev`；改渲染层热更新。
坑：`Start-Process` 把 `-ArgumentList` 用空格拼接且不自动加引号，带空格路径必须自己包双引号，
否则报 `Cannot find module 'C:\Program'`。

## 3. 打包与启动

三口径一起出：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\electron-win-build.ps1 -Target all
```

实测产物：

```
   234.4 MB  win-unpacked\LinuxCmdManual.exe
      97 MB  LinuxCmdManual_setup.exe
    96.8 MB  LinuxCmdManual_portable.exe
   387.9 MB  [installed directory] win-unpacked
```

先删空 `release/` 再判断"三种产物齐全"，否则上一轮的同名文件会蒙过验收（脚本不清空输出目录）。

安装后目录自证（stdout 能拿到）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke.ps1
```

实测：

```
RESULT=exited after 2,569 ms
[manual] ready-to-show saved={"x":120,"y":120,"width":1280,"height":820} clamped=... bounds=... insideWorkArea=true workArea=2560x1392 scale=1
[manual] dom {"viewport":"1264x781","dark":true,"dpr":1,"font":"system-ui","rootPx":"16px","bodyPx":"14px","hasApi":true,"dockItems":8,"dockActive":1,"listItems":10,"preview":true,"detailTitle":"ls","heading":"ls — 列出目录内容"}
[manual] adaptive themeSource=dark shouldUseDark=true rendererDark=true agrees=true font=system-ui rootPx=16px bodyPx=14px dpr=1 title=Linux 命令手册
[manual] storage roundtrip ok
remaining-processes=0
```

`dockItems=8`（八个主题）、`dockActive=1`、`listItems` 随主题变化、`storage roundtrip ok`
即覆盖层"写→读→删"契约成立；自证还会点一次 Dock 磁贴与一次列表项，验证点击链路。

便携单文件：见最后一节"未验证"。

## 4. 修改标题

真源只有一处：`electron/main.ts` 的 `APP_TITLE`，传给 `new BrowserWindow({ title: APP_TITLE })`，
并且必须保留

```ts
win.on('page-title-updated', event => event.preventDefault())
```

`index.html` 里也写着 `<title>Linux 命令手册</title>`；不 preventDefault 时页面标题会盖掉主进程设定，
改标题就会出现"改了没用"的假象。

判据：`bin/smoke.ps1` 的 `adaptive` 行里 `title=` 是新值。

## 5. 修改图标

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\make-icon.ps1
```

实测输出（7 层，16/24/32/48/64/128 为经典 BMP，256 为 PNG）：

```
ico=electron\icon.ico bytes=105982 reserved=0 type=1 count=7
icon-loaded size=32x32
```

`electron-builder.yml` 已配 `win.icon: electron/icon.ico`（`win.icon` 是文件路径，写错会静默回落默认图标）。
当前图标是重排时生成的**占位图**（蓝底+琥珀内块+白色斜纹），不是产品定稿；换图标只需覆盖这个文件，
或用设计稿导出多尺寸 ico。缺合法 ico 时 builder 只打 `default Electron icon is used` 并继续，是告警不是失败。

判据不靠肉眼，把 exe 内嵌图标取回来逐像素比：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-icon.ps1
```

```
extracted=32x32
corner-0-0       B=FF G=FF R=FF A=FF
inner-15-15      B=32 G=B8 R=E8 A=FF
outer-2-5        B=6E G=3B R=1F A=FF
outer-blue-fraction=60.9%
version=0.1.0 product=LinuxCmdManual fileDescription=LinuxCmdManual
```

## 6. 发布 / 分发

分发目标是 `<Share>\LinuxCmdManual\<version>\`，可以先只看要拷什么：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share _share -DryRun
powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\publish.ps1 -Share \\server\apps -KeepVersions 2
```

判据（实测，本轮用本地 `_share` 目录代替共享盘）：

```
share-target=_share\LinuxCmdManual\0.1.0
      96.8 MB  v0.1.0  sha256=96D28B0C1C13D4E4  LinuxCmdManual_portable.exe
      97.0 MB  v0.1.0  sha256=3454CE067775DF23  LinuxCmdManual_setup.exe
retained-versions=1
```

即两个 exe + 两个 `.sha256`；超过 `-KeepVersions` 的旧版本目录被淘汰。

版本发布流程：改 `package.json` 的 `version` → **重新打包** → `publish.ps1`。
守门会比对产物的 `VersionInfo.FileVersion` 与 `package.json`，实测能拦住"只改号不重打包"：

```
!! artifact LinuxCmdManual_portable.exe was built as 0.1.0 but package.json says 0.2.0 - rebuild before publishing
```

另两条 PS 5.1 硬约束：本文件与 `bin/*.ps1` 里若有中文，脚本必须存成带 BOM 或保持 ASCII-only；
`publish.ps1` 读 `package.json` 必须带 `-Encoding UTF8`，否则中文 description 被按 ANSI 解码，
`ConvertFrom-Json` 直接失败（实测踩过）。

## 7. 交付前精简

打包完成后把目录收敛到最小清单。两条判据，均本机实测：

1. **已打好的包不依赖中间目录**：删掉 `dist/`、`dist-electron/` 与临时输出（`*.log`、`*-out.txt`、`*-err.txt`）后，
   `bin\smoke.ps1` 仍然通过——`[manual] adaptive … agrees=true`、`[manual] storage roundtrip ok`。
   原因是两者已被拷进 `release\win-unpacked\resources\app.asar`。
2. **删完能一把重建**：`bin\electron-win-build.ps1 -Target all -SkipInstall` 退出码 0，
   `release/` 重新出齐 `LinuxCmdManual_portable.exe`、`LinuxCmdManual_setup.exe`（+ blockmap）、`win-unpacked/`。

收敛后的清单（除 `node_modules/` 与 `release/`，每一项都能归进"代码 / 文档 / 脚本 / 配置 / 产物"）：

```
.gitignore  package.json  package-lock.json  index.html  tsconfig.json  vite.config.mts  electron-builder.yml
src/  electron/  bin/  doc/
release/  node_modules/
```

对外交付就拷 `release/` 里的 exe（或走 `bin/publish.ps1`）；本地临时当共享盘用的 `_share/` 之类不留。

## 自适应实现与判据

已满足：

- 窗口几何持久化到 `%APPDATA%\LinuxCmdManual\window.json`，恢复时按 `screen` 的 workArea 夹紧。
  负向用例实测（伪造屏幕外坐标）：

  ```
  wrote-saved={"x":-24000,"y":-24000,"width":9000,"height":9000}
  [manual] ready-to-show saved={"x":-24000,...} clamped={"x":0,"y":0,"width":2560,"height":1392} bounds={"x":0,"y":0,"width":2560,"height":1392} insideWorkArea=true
  restored original window.json
  ```

- `minWidth=880 / minHeight=560`，`autoHideMenuBar`。
- 字体：`font-family: system-ui, 'Segoe UI Variable Text', 'Segoe UI', 'Microsoft YaHei', ...`，
  `html { font-size: 100% }`，正文 `0.875rem`（= 14px，视觉尺寸不变）。实测生效值 `font=system-ui rootPx=16px bodyPx=14px`。
- 明暗：`nativeTheme.themeSource = 'dark'`，**这是有意的产品决定**（浅色色板未定稿），
  不是跟随系统。判据 `agrees=true` 表示主进程与渲染层对"深色"的认知一致。
  要改成跟随系统：换成 `'system'`，并在 `src/styles/base.css` 补一套
  `@media (prefers-color-scheme: light)` 的 token。
- 布局仍是 px 口径：Electron 的页面缩放走 `zoomFactor`，整页等比放大，px 不阻断自适应，
  所以没有把 115 处 px 全量改 rem（避免无收益的大 diff 与 Dock/行高回归）。

## 未验证 / 环境受限

1. **便携单文件（`LinuxCmdManual_portable.exe`）的运行时自证**：portable 的宿主进程只解壳到 temp
   再拉子进程，stdout 不冒泡，本工程的 `--selftest` 打点拿不到。参照实现里用"标记文件"验过
   （`window-shown`/`probed`/`checks-done`，实测 dir 533ms vs portable 6561ms）；本工程若要同样验证，
   需要在 `main.ts` 里加写标记文件的分支。
2. **系统 DPI 高缩放（125% / 150%）下的布局**：本机单屏 `scaleFactor=1`，只验证了读取链路
   与 `zoomFactor` 未被手动改写。
3. **真实网络共享盘**：`publish.ps1` 用本地 `_share` 跑的，`\\server\share` 的权限与延迟行为未测。
4. **nsis 安装到目标机后的体验**：只验了安装包能产出与哈希一致，没执行安装。
5. **浅色主题**：有意不做，见上一节。
