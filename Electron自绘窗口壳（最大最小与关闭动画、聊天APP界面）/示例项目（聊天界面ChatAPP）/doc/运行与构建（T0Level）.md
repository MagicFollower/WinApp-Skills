# ChatAPP 零基础上手

## 0. 前置

- Node:v24.18.0,装在 `C:\Program Files\nodejs`,Bash 与 PowerShell 均可直接命中 `node`/`npm`(已实测)。
- registry 已设为 npmmirror(`npm config set registry https://registry.npmmirror.com`);Electron 与 electron-builder 二进制默认走镜像注入(`ELECTRON_MIRROR`、`ELECTRON_BUILDER_BINARIES_MIRROR`),`bin\electron-win-build.ps1 -NoMirror` 才走 GitHub 官方源。
- 仓库里不应该提交的:node_modules、dist、dist-electron、release(见 `.gitignore`)。
- **主题方案(有意决策)**:只做浅色——复刻目标是一张固定的浅色多彩原型,`nativeTheme.themeSource` 显式钉为 `'light'`,不做暗色分支,不跟随系统。

## 1. 项目启动(首次构建)

开发模式(最常用):

```
bin\start.bat
```

打包全部口径(便携版 + 安装版 + 安装目录):

```
bin\dist.bat
```

判据:末尾出现 `==> done`,且 `release\ChatAPP_portable.exe`、`release\ChatAPP_setup.exe`、`release\win-unpacked\ChatAPP.exe` 三者齐备。
首次构建多一步拉 Electron 二进制(`==> Electron binary missing, fetching it`),属正常。

> 状态:构建与打包链路**已实测跑通**(见第 3 节,产物在临时输出目录);`bin\dist.bat` 写默认 `release\` 目前被本机一个外部进程占用挡住,处置见第 8 节。

## 2. 本地调试

```
bin\start.bat
```

实测输出:

```
==> starting ChatAPP dev (vite + electron) from C:\Users\webtu\Desktop\ChatAPP
> chatapp@0.1.0 dev
> node bin/dev.mjs
[dev] vite: http://localhost:5173/
ready-to-show
```

判据:`[dev] vite:` 打出本地地址 + `ready-to-show`,Electron 窗口出现;改 `src/` 代码热重载生效。

停止:

```
bin\stop.bat
```

实测输出:

```
port 5173: 1 process(es)
  pid=7236 name=node
electron (project path): 0 process(es)
node dev.mjs (project path): 0 process(es)
==> stop done
```

判据:随后 `Get-NetTCPConnection -LocalPort 5173 -State Listen` 计数为 0、工程路径下 `electron.exe` 计数为 0(均已实测)。
停止逻辑只按「占用 5173 端口的进程」+「可执行路径/命令行落在本工程内的 electron/node」两类精确命中,绝不按进程名批量杀。

纯浏览器快速预览(不调窗口 API 时):

```
npx vite --port 5173 --strictPort
```

判据:访问 `http://localhost:5173` 出现橙粉桌面背景上悬浮的三栏窗口(非 Electron 环境自动加 `browser` 预览壳)。

## 3. 打包与启动

```
npm run build
```

实测输出(css/js 哈希名随构建变化):

```
dist/index.html                   0.40 kB
dist/assets/index-*.css          10.43 kB
dist/assets/index-*.js          157.33 kB
✓ built in 526ms
[build] done
```

开发态自证(file:// 加载 + contextBridge + IPC 往返全通才会打标记):

```
powershell -NoProfile -Command "$p = Start-Process -FilePath '.\node_modules\electron\dist\electron.exe' -ArgumentList '.','--selftest' -WorkingDirectory (Get-Location) -PassThru -RedirectStandardOutput 'selftest-out.txt'; $p.WaitForExit(20000) | Out-Null; Get-Content selftest-out.txt"
```

实测输出:`selftest-ok` + `ready-to-show`(跑完删除 `selftest-out.txt` 等临时文件)。

### 三口径打包(已实测)

`bin\dist.bat` 等价于 `bin\dist.ps1 -Target all`(内部调 `electron-win-build.ps1`,注入两个镜像 + 使用工程内缓存)。
本轮因默认 `release\` 被外部进程占用,用同一链路的另一输出目录验证:

```
set ELECTRON_BUILDER_CACHE=C:\Users\webtu\Desktop\ChatAPP\.cache\electron-builder
npx electron-builder --win --config.directories.output=release-check
```

实测产物(2026-10-11):

```
ChatAPP_portable.exe   103,267,559 B   便携版:拷了就跑,启动解壳到 temp
ChatAPP_setup.exe      103,495,195 B   安装版(nsis,可选安装目录)
win-unpacked\ChatAPP.exe               安装目录口径,测启动/内存最公平
```

安装目录口径自证:

```
powershell -NoProfile -Command "$p = Start-Process -FilePath '.\release-check\win-unpacked\ChatAPP.exe' -ArgumentList '--selftest' -PassThru -RedirectStandardOutput 'pkg-selftest.txt'; $p.WaitForExit(25000) | Out-Null; Get-Content pkg-selftest.txt"
```

实测输出:`selftest-ok` + `ready-to-show`。

> 这次验证**没有**注入 `ELECTRON_BUILDER_BINARIES_MIRROR`,nsis 走 GitHub 直连,1.3 MB 下了 48.48s——`bin\dist.bat` 会注入镜像,别省这个变量。

## 4. 修改标题

真源只有一处:`electron/main.ts` 的 `new BrowserWindow({ title: APP_TITLE })`,并已有 `win.on('page-title-updated', (e) => e.preventDefault())` 防 `index.html` 的 `<title>` 覆盖。
判据:窗口任务栏/Alt-Tab 显示新标题;`--selftest` 输出不受影响。

## 5. 修改图标

当前未配置自定义图标(electron-builder 会用默认 Electron 图标并打一条告警,不阻塞打包)。
要自定义时:生成或取一个多尺寸 `.ico` 放到 `electron/icon.ico`,并在 `electron-builder.yml` 的 `win` 下加 `icon: electron/icon.ico`。
判据:打包日志不出现 `default Electron icon is used`;资源管理器中 exe 显示新图标。

## 6. 发布 / 分发

**未验证**(本机 `release/` 目录当前被占用,尚未在默认输出目录出过包)。流程约定:改 `package.json` 的 `version` → `bin\dist.bat` 重新打包 → 分发 `release/` 下产物。只 bump 版本号不重打包等于发旧构建。

## 7. 交付前精简

`dist/`、`dist-electron/` 是 `npm run build` 可重建的中间物,交付前可删;`node_modules/`、`release/`、`.cache/electron-builder/`(winCodeSign 种子,见第 8 节)保留。
判据:删除中间目录后 `npm run build` 一次重建成功,`npm run typecheck` 为 0(本条已实测 typecheck 双侧 0 错误)。

## 8. 本机已踩到的坑

### 8.1 winCodeSign 解包要符号链接权限

现象:`bin\dist.bat` 走到打包中段报

```
ERROR: Cannot create symbolic link : 客户端没有所需的权限 :
  ...\electron-builder\Cache\winCodeSign\<rand>\darwin\10.12\lib\libcrypto.dylib
```

成因:electron-builder 解 `winCodeSign-2.6.0.7z` 时要建两个 **darwin** `.dylib` 符号链接,普通会话(非管理员、未开开发者模式)无 `SeCreateSymbolicLinkPrivilege`。失败的只有 mac 侧文件,Windows 打包要用的 `rcedit-*.exe`、`windows-6`、`windows-10` 均已解出。

处置(已固化进 `bin\dist.ps1`):把已解出的可用副本 robocopy 成工程内缓存 `.cache\electron-builder\winCodeSign\winCodeSign-2.6.0`,并设 `ELECTRON_BUILDER_CACHE` 指过去,builder 直接复用、不再解包。
判据:日志出现 `==> local electron-builder cache already seeded` 后继续走到 `building target=portable`,不再出现 symbolic link 错误(已实测)。
替代方案:开 Windows 开发者模式,或以管理员身份跑一次让它把 `%LOCALAPPDATA%` 的缓存解完整。

### 8.2 默认 `release\` 被外部进程占用

现象:`electron-builder` 在 `EnsureEmptyDir` 阶段抛 Go 栈,底层是

```
remove ...\release\win-unpacked\resources\app.asar: The process cannot access the file because it is being used by another process.
```

同一文件 `rm`/`Rename-Item` 均报占用,而工程路径下 `electron.exe`/`ChatAPP.exe`/`node.exe`/`app-builder.exe` 计数全为 0 → 持有者是外部进程(杀软实时扫描或文件索引器一类)。

处置:`bin\dist.ps1` 已加预检,遇到占用直接以可读中文提示退出(退出码 1),不再让 builder 抛底层栈。
清锁办法:关掉正在运行的 ChatAPP 窗口 → 仍不行则把 `release/` 加入杀软/索引排除 → 再不行重启后立刻跑 `bin\dist.bat`。
本轮的产物暂存在 `release-check/`(同一链路、仅输出目录不同),锁清掉后可直接删。

### 8.3 数组 splatting 把参数送错位置

现象:`bin\dist.bat` 报 `Resolve-Path : 找不到路径"C:\...\ChatAPP\-Target"`——`-Target` 被当成第一个位置参数灌进了 `$Root`。

成因:`& script.ps1 @('-Target','all')` 里数组 splat **按位置绑定**,不认参数名。

处置:改用 hashtable splat(`$buildParams = @{ Target = $Target }; & $build @buildParams`)。
判据:`bin\dist.bat` 能走到 `==> Electron binary present` 与 `==> building and packaging (all)`(已实测,后续确实进了 electron-builder)。

## 9. bin 下脚本的编写规范

`bin/*.ps1` 一律按 powershell-reliable 口径写,新增脚本照抄这套骨架:

1. **`param()` 打头**,再 `$ErrorActionPreference = 'Stop'`,再 `. (Join-Path $PSScriptRoot '_common.ps1')` 取共享 helper。
2. **不用 `&&`**:顺序执行用 `;`,需要中断就 `try/catch` + `Write-Fail`(它自带 exit 1)。
3. **路径一律 `Join-Path` / `-LiteralPath`**,不拼字符串、不混斜杠方向。
4. **参数用 hashtable splatting**:`& $build @buildParams`。数组 splat 是**按位置**绑定的,`@('-Target','all')` 会把 `-Target` 塞进第一个参数(本轮真实踩过,见 8.3)。
5. **不用 `$args` 作变量名**:它是 PowerShell 自动变量,赋值不生效。
6. **函数定义在调用之前**:脚本自上而下解析,先调后定义会运行时报找不到命令。
7. **写文件必须带编码**:`... | Out-File -LiteralPath $path -Encoding UTF8`;读带中文/非 ASCII 的 json 用 `Get-Content -Encoding UTF8`(PS 5.1 默认按 ANSI 读会吞引号)。
8. **超过 60s 的操作要有阶段存档**:`dist.ps1` 把最后完成的阶段写进 `.cache\electron-builder\dist-checkpoint.json`,重跑时先打印上次停在哪一步;内容只有阶段名与时间戳。
9. **杀进程只按枚举到的 pid**,且限定「占用目标端口」或「可执行路径/命令行落在本工程内」两种来源;绝不按进程名批量杀。停止后用 `Invoke-Retry` 复查端口是否真释放。
10. **脚本本体 ASCII-only**(含注释):无 BOM 的非 ASCII 文件会被 PS 5.1 按 ANSI 解码成乱码并破坏引号配对。
11. **bat 只做薄壳**:`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0xxx.ps1"`,并以 `endlocal & exit /b %ERRORLEVEL%` 把退出码透传出去。

### 判据自查(本机实测,2026-10-11)

```
powershell -NoProfile -Command "Get-ChildItem bin\*.ps1 | ForEach-Object { $t=Get-Content -LiteralPath $_.FullName -Raw; Write-Output ('{0,-24} amp={1} errh={2} joinpath={3} outfile-enc={4} secrets={5}' -f $_.Name, ([regex]::Matches($t,'&&')).Count, ([regex]::Matches($t,'try|catch|ErrorAction')).Count, ([regex]::Matches($t,'Join-Path')).Count, ([regex]::Matches($t,'Out-File.*Encoding')).Count, ([regex]::Matches($t,'(?i)password|token|secret')).Count) }"
```

实测输出:

```
dist.ps1                 amp=0 errh=8  joinpath=9 outfile-enc=1 secrets=0
electron-win-build.ps1   amp=0 errh=3  joinpath=8 outfile-enc=0 secrets=0
start.ps1                amp=0 errh=5  joinpath=1 outfile-enc=0 secrets=0
stop.ps1                 amp=0 errh=14 joinpath=3 outfile-enc=0 secrets=0
_common.ps1              amp=0 errh=8  joinpath=1 outfile-enc=0 secrets=0
```

`amp=0`(无 `&&`)、`secrets=0`(无凭据字样)、每个脚本都有错误处理与 `Join-Path`。
`outfile-enc` 只有 `dist.ps1` 为 1:它是唯一写文件的脚本(阶段存档),其余四个不落地文件,该项不适用。
解析检查:`bin\*.ps1` 五个文件 `parse-errors=0`。

## 10. 取 / 换 reactbits 组件

渲染层的动效件来自 React Bits,以 **vendored 源码**放在 `src/components/bits/<Name>/`(不是 npm 组件库)。许可、已接入清单与被否决项及其实测依据见 `doc/第三方组件许可.md`。

取源不走 `npx shadcn add`——它要 `components.json` 且默认按 Tailwind 项目配置;本工程是纯 CSS,直接取 registry JSON:

```
curl -s https://reactbits.dev/r/<Name>-TS-CSS
```

返回体的 `files[]` 每项含 `path` 与 `content`,原样写出即可。判据:响应里能看到 `dependencies` 数组与 `files[].content`,且落盘后 `npm run typecheck` 为 0(本次 7 个候选全部命中)。

**变体后缀必须选对**:`-TS-CSS` / `-TS-TW` / `-JS-CSS` / `-JS-TW` 四种都有,本工程只用 `-TS-CSS`。

**装依赖的口径**:先看组件源码里真实 `import` 了什么再装,别照抄 registry 的 `dependencies`(它是候选集)。当前进产物的运行时依赖只有 `ogl`(SoftAurora)与 `gsap`(AnimatedContent、FoldText),且全部进 **devDependencies**——`npm ls --omit=dev --depth=0` 仍应为空。`motion` 曾为 BlurText 而装,2026-10-11 换 FoldText 后工程内再无引用(包未卸,理由见许可文档),GooeyNav 则是**零依赖**(`dependencies: []`,自带 WebGL2 SDF 管线,不用 ogl)。

**换件时的三步**:① 取源落盘 → ② 冲突改 `app.css` 的「reactbits 动效层适配」段(不改组件源码)→ ③ 复跑 2.8.1 那四挡窗口宽的量表,要求逐格等于基线。第 ③ 步不是形式:BorderGlow 那 1px 卡片边框就是靠它抓出来的(吃掉内容宽 2px),Magnet 的高度链断掉则是把发送钮压成 52×22。

> **量表口径变更(2026-10-11 本轮11,适用下文所有"四挡"条目)**:第 11/12/13/15 节里的 `1280/1121/1120/960` 四挡与那组数字是**当时**的验收记录,原样保留不改;自本轮起 1120/960 两挡在真窗口里**已不可达**(`minWidth: 1248`),浮层分支也已删除。新口径:`npx electron . --cols` 扫 11 挡,判据 `overlap` 恒负、`sideMinusRight` 恒 0、`chatOverSide ≥ 2`(基线见 手册 2.8.1 与本文第 18 节)。

**真窗口截图**(绕开「GPU 合成客户区抓不到屏」):

```
powershell -NoProfile -Command "$p=Start-Process -FilePath '.\node_modules\electron\dist\electron.exe' -ArgumentList '.','--shot','.cache\shot.png' -PassThru -WorkingDirectory (Get-Location); $p.WaitForExit(40000)|Out-Null; Get-Content .cache\shot.txt"
```

判据:stdout 出现 `shot-saved .cache\shot.png 1280x800`。
加 Chromium 旗标 `--force-prefers-reduced-motion` 可实测降级路径(判据:背景变静态 `--flow`、群名不再逐词浮现)——已实测通过。

## 11. G2 圆角(曲率连续轮廓)的验证与回退

可见轮廓是超椭圆,由原生 `corner-shape: superellipse(v)` 一条声明生成(指数 **p = 2^v** → `superellipse(2)` = p4 = squircle),取值集中在 `tokens.css` 的 `--g2-shape`,作用元素是 `app.css` 文末那条规则列出的 14 个圆角矩形(过滤行换成 GooeyNav 后 `.filter-tab` 已摘除,见第 12 节;中间列成卡 + 右键窗口控制条后又补进 `.chat-area`/`.win-menu`/`.win-menu-btn`,见第 14 节)。设计依据、实测数字与被否决的两条旧路在 手册 4.1.1,这里只给复现命令。

**① 形状真的生效了吗**(不猜内核,读 computed 回来):

```js
getComputedStyle(document.querySelector('.sidebar')).cornerShape
```

判据(实测,Chromium 152):返回 `superellipse(2)`。把 `--g2-shape` 改成 `round` 再读,返回 `superellipse(1)`——两态读数不同才算这条开关真的换过形状;读回 `none` 或空串说明这个内核不接受 `corner-shape`,画面就是 `border-radius` 的圆弧(整条声明被忽略,不需要 `@supports` 分支)。**更旧内核的读回值本机未测**,只确认了接受侧。浏览器预览里手动跑,或直接用下面 ④ 的 `--frames`(它自带这两个读数)。

**② 几何常数的来源**(外扩量,以及旧 JS 路径留下的采样段数/命中区代价):

```
node .cache/g2-derive.mjs
```

判据(实测):p=4 角部沿对角外扩 `0.18921r`(r=32 → 6.05px、16 → 3.03、8 → 1.51)。文件里 SEG 采样与命中区两节属于**被否决的 `clip-path: path()` 路线**,留作对照:按**角度**等分采样才不戳进圆弧(按参数等分会差 `1.2e-2r`),`clip-path` 每角吃掉 `r²(1−0.927037)` 的热区。原生 `corner-shape` 这两项代价都是零。

**③ 像素级真值**:先 `npx electron . --shot .cache\g2-a.png`,再

```
node .cache/g2-fit.mjs .cache/g2-a.png window
node .cache/g2-fit.mjs .cache/g2-a.png sidebar
```

判据(实测):逐行取边后,**对超椭圆最大偏差 0.6~2.1px、对圆弧最大偏差 10.6~12.4px**。这两列必须同时看:只报"偏差很小"是假绿——若形状根本没换,测到的会正好贴住圆弧那一列。边界按**行中心**判定,角弧在切点附近一个行内能走掉 16px,按行上沿比会把 row0 误判成偏差 16px。右卡片量不到边(玻璃与 aurora 亮度同档,阈值分不开),它靠 ① 的 computed 读数 + 与左卡片同一条规则覆盖。

`.cache` 里这几轮的取证文件按来源分,别混:常态 `g2-f.png`(带 `--frames` 那轮)、reduced-motion `g2-fr.png`(`g2f2.log` 记的命令行)、`g2-cs.png`(与 `g2-f.png` 同态的独立一次)、`g2-tok.png`(圆角字面量收进令牌之后复测)——这四张各量窗口四角,16 个读数逐位相同;`g2-cs/g2-f/g2-tok` 三张再各量左卡片四角,12 个读数同样逐位相同 = 可复现。`g2-a.png`/`g2-b.png` 是**被否决的 `clip-path: path()` 路线**的改前/改后留档,对应的产物体积在 `g2-v.log`(CSS 19.60 kB / JS 459.91 kB,比最终形态重 2.65 kB)。

**④ 帧间隔 A/B**(真窗口内自己跟自己比,含前置断言):

```
npx electron . --frames 3000
```

判据(实测):stdout 的 `frames {...}` 里 `on.shape="superellipse(2)"` / `off.shape="superellipse(1)"`(断言两态确实切换过),两侧 `n`/`p50`/`p95`/`max` 相同——本机各 432 帧、p50 6.9ms / p95 7.0ms / max 7.1ms。`vis` 字段留作对照:`hidden` 页的 rAF 会被饿死,那种读数不能当稳态。

**⑤ 阴影与磨砂没被裁掉**:同一张 PNG 上沿对角在轮廓外 1~6px 取样,窗口 alpha 仍有 7~11(`--shadow-window` 在);卡片左带亮度 253→248 渐变到卡身 41(`--shadow-float` 在)。五处 `backdrop-filter` 计算值原样(`.window` = `blur(28px) saturate(1.4)`,其余四处 = `blur(20px)`)。这是判据"打在缺陷可见的那一层"的那一半:③ 只证形状换了,①/⑤ 才证阴影和玻璃没跟着丢。

**⑥ 布局回归**:浏览器预览里改 `.window` 宽度跑 1280/1121/1120/960 四挡,**每挡在 `superellipse(2)` 与 `round` 两种状态下各量一次**,要求逐格相同(本机 chatW 632/473/786/626、pillActionsGap 121.4/41.9/92.4/12.4、inputW 348/189/194/34)。这条抓到过真缺陷:旧 `clip-path` 路线里共享规则写的 `position: relative`(特异度 0,2,1)盖过了 `@container` 的 `.right-panel { position: absolute }`(0,1,0),窄窗浮层整个失效——只在功能开启态下量完全看不出来,是 on/off 对照抓到的。

**回退**:`tokens.css` 里 `--g2-shape: round` 是唯一开关(整条声明删掉等价),即回到 4.1 的圆弧形状;`--r-window` 单独可回 20px。内核不支持 `corner-shape` 时不需要任何代码分支——声明被忽略,画面本身就是圆弧。

## 12. GooeyNav(左栏过滤行)的验证与回退

左栏的四个过滤器由 React Bits 的 **GooeyNav** 渲染(选中项是一枚会碎成水银再汇合的胶囊)。取源与逐件适配见 手册 5.5 与 `doc/第三方组件许可.md`,这里只给复现命令与判据。

**① 落盘与上游逐字节一致**

```
node -e "const fs=require('fs');const j=JSON.parse(fs.readFileSync('.cache/gn-ts-css.json','utf8'));for(const f of j.files){const p='src/components/bits/GooeyNav/'+f.path.split('/').pop();console.log(p, Buffer.compare(Buffer.from(f.content,'utf8'),fs.readFileSync(p))===0)}"
```

判据(实测):两行都 `true`(`GooeyNav.tsx` 29,938 字节、`GooeyNav.css` 2,366 字节)。registry 变体后缀必须是 `-TS-CSS`(见 §10)。

**② 排布:不换行 + 等宽 + 铺满**(本轮最可能翻车的点)

`npx electron . --tabs` 读 4 个 `.gooey-nav__item` 的 `getBoundingClientRect()` 与逐层右沿:

判据(实测,2026-10-11 修留白后):`top` 四格全等(单行)、`width` 四格全等 = **65.5px**、`.gooey-nav__list` 高 = 单行 **25.59px**、`gapToNavInner` = **0**(末格 right 294 = nav 内沿 294)。上游 md 档按内容宽排格(带图标 ≈84px/格 → 4 格 348px > 侧栏可用 276px),而它的 list 是 `flex-wrap: wrap`,**不改尺寸必换行**;适配段把它拉成 `width:100%` + `item flex:1 1 0` + `link width:100% justify-content:center`,并把 `--gn-font/--gn-x/--gn-y/--gn-pad/--gn-gap` 收到 13/8/5/4/2px。pill 几何是按 `li` 的 rect 量的,所以拉伸安全。

> **更正(2026-10-11)**:这条判据原先记的是"四格等宽 60.94px 即算铺满"——**等宽 ≠ 铺满**。60.94 是"四格按 max-content 收缩后的等分",右侧还剩 **18.2px** 空档。成因见本节末 ⑩。

**③ WebGL2 管线真的起来了**(防"退化当生效")

```js
[getComputedStyle(document.querySelector('.gooey-nav__fallback')).display,
 getComputedStyle(document.querySelector('.gooey-nav__canvas')).display]
```

判据(实测):`['none','block']`。管线失败时组件会 `fallback.display='block'` 退回 CSS 方胶囊——截图上"有个白胶囊"看着一样,所以不能只看图。

**④ 交互联动**

点第 3 格后读:`.gooey-nav__item[data-active]` 的下标 = 2、该格 `button` 的 `aria-current='page'`、`.chat-item` 数 **8 → 7**、`.gooey-nav__list--active` 的 `clip-path` 变成 `path("M 148.672 0 H 184.016 A 14.7969 …")`(单位无,数值落在第 3 格区间)。

**⑤ 水珠真的吃了 `--glow-*`**(区分性实验,慢放把时序钉死)

把调用点 `animationTime` 临时改 **30000**(30s,保证任何时刻截图都在飞行中),点一格,`--shot`/截图,再:

```
node .cache/nav-tint.mjs .cache/nav-t2.png
```

判据(实测):飞行帧在过滤行带内命中 `--glow-1` **441** 像素、`--glow-2` **763** 像素(包围盒 x 184..229 / y 71..98,正是两格之间),而**静止帧三档全 0** —— 侧栏底 `--sidebar-bg` 是不透明紫渐变,所以这三档色只可能来自水珠。`--glow-3`(#3b9df8)在 tol 70 下采不到、tol 160 才出现,因为它与紫底距离太近,这条只算弱证据。测完把 `animationTime` 还原 450。

**⑥ 布局回归**:同 §11 ⑥ 那四挡重跑。判据(实测):chatW 632/473/786/626、pillActionsGap 121.4/41.9/92.4/12.4、inputW 348/189/194/34 逐格等于基线,`.right-panel` 的 `position` 在 1121→1120 之间由 `relative` 翻成 `absolute`。**坑**:`inputW` 量的是 `.composer-input` 里的 `<input>` 本体;量外层胶囊会四格各多 96px(444/285/290/130),看着像回归其实是你换了定义。

**⑦ 帧间隔与"偶发长帧"（上一轮的归因已更正）**

`npx electron . --frames 3000` 判据(实测):两侧 p50 **6.9ms** / p95 **7.0ms** 与基线相同。偶尔某条腿多一个长帧:上一轮三次都在 off 腿(27.7 / 34.8ms,428 vs 432 帧),当时用三腿对照(不翻 7.1 / 翻完立刻采 13.9 / 翻完等 700ms 再采 7.1;再把 `.filter-tabs` 摘掉翻 = 7.1)归因成"改 `--g2-shape` 触发 GooeyNav 的 ResizeObserver 重测"。**本轮换 FoldText 后再跑三次:一次长帧落在 on 腿(13.9ms)、两次两腿都干净(432/432,max 7.1)** ⇒ 那个归因下得太重:每种配置只采了 1 个样本,而长帧本身偶发、两条腿都会中。站得住的只有两条:**p50/p95 不受影响;长帧是偶发噪声(环境/GC 级)**,真要归因得每条腿跑 ≥5 次。生产运行期不改令牌,反正不落用户路径。

**⑧ 降级**:`npx electron . --shot .cache/nav-rm.png --force-prefers-reduced-motion` 判据(实测):外框与白胶囊都在、排布不变。"reduced 时不放水珠"这条是**代码级**证据(`GooeyNav.tsx:365` mount 读 `matchMedia`、`:754` reduced 直接 `settle()`、`:687` 阻尼置 1),不是像素级——初始挂载本来就不产生水珠,静止截图分不出两态。

**⑨ 体积**:CSS 19.23 → **21.07 kB**(gzip 4.71 → 5.17),JS 457.26 → **473.66 kB**(gzip 157.15 → 163.88)。

**⑩ 末格右侧 18.2px 空档(2026-10-11 修)**

症状:选中最后一格"空间"时,白胶囊右边到磨砂外框内沿之间空一段(截图框出)。

成因:`.gooey-nav` 是 `display:inline-flex`,而 `.gooey-nav__stage` 是它的 **flex 子级**,默认 `flex:0 1 auto` = 按 max-content 收缩(实测 **249.75px**)。`nav width:100%` 只把**外框**拉到 276px,里面这一层没跟着长,于是 `item flex:1 1 0` 分不到可分配的确定宽度——四格只在收缩后的 249.75px 里"等宽",右侧剩 **18.2px**。等宽判据当时全绿,所以这条躲过了一整轮。

修法(适配段一条,不动组件源码):

```css
.filter-nav .gooey-nav__stage { flex: 1 1 auto; min-width: 0; }
```

判据(实测):`npx electron . --tabs` → stage/list 宽 **268px**(= nav 内容宽)、四格 **65.5px**、`gapToNavInner` **0**、`distinctTops` 1(没换行)、`.filter-tabs` 高 **45.6px** 与改前同值(下方列表首行不位移)。

留白本身在 DOM 层量得到,胶囊位置量不到(水珠是 canvas 画的;`.gooey-nav__fallback` 只在无 WebGL 时才同步几何,WebGL 路径下它 `display:none` 且 transform 是旧值,不能当代理)——所以补了像素层 A/B:

```
npx electron . --tabs --tabs-click 3        # 点末格、等落定,按 nav±12px 裁出 .cache/tabs-on.png / tabs-off.png
node .cache/tabs-pill.mjs .cache/tabs-on.png 10 56 228.5 294
node .cache/tabs-pill.mjs .cache/tabs-off.png 10 56 214.8 275.8
```

`--tabs-click` 会顺手把 stage 用内联样式打回 `flex:0 1 auto`(= 改前的默认值)再拍一张 `tabs-off.png`,同一矩形、可逐像素比。判据(实测):on 白色 pill 包围盒 **x 229..294**(末格 228.5..294,偏差 +0.5 / 0),off **x 215..276**(末格 214.8..275.8,偏差 +0.2 / +0.2)——两态 pill 都紧贴各自的格,而 off 那张右侧确有 18px 空框,像素上复现了症状。体积:CSS 25.77 → **25.83 kB**(gzip 6.24 → 6.25),JS 356.47 kB 不变(只加了一条 CSS 规则)。

**已知未取证**:① 无 WebGL2 环境下的 CSS fallback 外观(不造缺陷场景);② `--gn-frame` 用的是上游 `rgba(38,35,46,0.66)` + `blur(20px) saturate(1.6)`,在深底侧栏上是否够显眼只做了目视,没做量化;③ `--shot` 偶发抓在 BlurText 群名浮现之前(实测三张里一张标题带深色像素为 0,另两张 438/440),那是既有动效件的采样时序,不是本轮引入。

**回退**:`Sidebar.tsx` 换回手搓 4 个 button + `app.css` 恢复 `.filter-tab*` 四条与 G2 清单里那行即可;`bits/GooeyNav/` 留着不进 bundle(未被 import)。没有运行期开关——它是视图层替换,不是全局令牌。

## 13. 头部群名 FoldText 与会话条目同步的验证

顶部信息卡的群名由 **FoldText**(reactbits,逐字横向翻入)渲染,内容全部来自当前选中的会话条目(`SEED_CHATS` 的 `avatar/name/members/qq`)。组件说明见 手册 5.5,这里给复现命令。

**① 落盘与上游逐字节一致**

```
node -e "const fs=require('fs');const j=JSON.parse(fs.readFileSync('.cache/ft-ts-css.json','utf8'));for(const f of j.files){const p='src/components/bits/FoldText/'+f.path.split('/').pop();console.log(p,Buffer.compare(Buffer.from(f.content,'utf8'),fs.readFileSync(p))===0)}"
```

判据(实测):两行 `true`(`FoldText.tsx` 6,750 字节 / 229 行,`FoldText.css` 2,110 字节 / 93 行)。`dependencies: ["gsap@^3.13.0"]` —— 工程已装 gsap ^3.15.0,**零新增**。

**② 动画真的在重播**(不是"文字直接换了")

```
npx electron . --fold 3
```

自证探针在真窗口里点第 N 条会话,连采三帧(点前 / 点后 150ms / 再等 1.4s)。判据(实测,`--fold 3`):`before` = 8 片、opMin/opMax 1、`matrix(1,0,0,1,0,0)`(落定);`during` = 13 片、**opMin 0 / opMax 0.837**、`matrix3d(-0.0349,0,-0.9994,…)`(rotateY 92°)—— 各片透明度不一致 = stagger 在跑;`after` = 13 片、opMin/opMax 1、单位阵。`vis` 必须是 `visible`:浏览器预览的页一旦最小化,rAF 被饿死,gsap 时间线冻在起始值,量出来永远是"opacity 0 + 92°"(本轮实测踩过,`requestAnimationFrame` 泵 600ms 直接超时)。

**③ 头部与列表同源**

同一条命令的回读里就有:点 c0 → `🔥 / Q谈国际(新闻) / 2 成员 QQ:515604586 / rgb(245,166,35)`;点第 4 行 → `🌸 / 蔚蓝档案⭐收容物之智能体群 / 142 成员 QQ:774512038 / rgb(214,51,132)`;点 personal 那条(`--fold 8`)→ `🍃 / 东方树叶.G3 / **QQ:802341567**`(不带成员数,`chatSubtitle()` 按类型组)。列表条目数 9。

**④ 长名裁切(必须 `display:block`)**

`.header-pill .fold-text` 若留组件默认的 `inline-block`,它按内容收缩 ⇒ `.title` 的 `overflow:hidden` 永不生效:960 挡实测群名戳出胶囊右沿 **47.6px**。改 `display:block` 后同一挡 `clientWidth 120 < scrollWidth 201`(裁切生效),标题盒右沿在胶囊右沿内 18px(= pill 的 `padding-right`)。**`text-overflow:ellipsis` 仍不生效**——子级是一串 inline-block 面板,长名到边硬切,不出省略号。

**⑤ 降级**:`npx electron . --fold 3 --force-prefers-reduced-motion` 判据(实测):三帧 `midTransform` 全是 **`none`**(不旋转),opacity 仍走 0 → 0.899 → 1(时长 ≤0.22s 的淡入)。组件 JS 侧(`matchMedia` → 关掉 rotateX/Y、duration 压到 ≤0.22)与 CSS 侧 `@media (prefers-reduced-motion: reduce){ transform:none!important }` 双保险,宿主没再套 `motionPref`。

**⑥ 布局回归要固定选中项**:四挡量表(chatW 632/473/786/626、inputW 348/189/194/34、`.right-panel` 在 1121→1120 翻浮层)与基线逐格相同 —— **前提是默认选中 c0**。上一轮基线是在"头部固定 8 字群名"下量的;头部跟着选中项走之后,选中 13 字的长群名会让 `pillActionsGap` 从 121.4 掉到 76.7(胶囊变宽 44.7px),那是**内容效应不是回归**。量之前先把选中项复位(或直接 reload)。**注:本条的 `inputW` 四个数自第 15 节起作废**(撤掉输入胶囊右端那颗 ◯↓ 后每挡 +34),以 15 节 ⑤ 为准。

**⑦ 体积与依赖**:撤下 BlurText 连带把 `motion`(framer-motion)整包挤出 bundle —— JS 473.66 → **352.33 kB**(gzip 163.88 → **124.04**,−39.84 kB),CSS 21.07 → **22.78 kB**(gzip 5.17 → 5.57,FoldText 的 93 行样式)。`src/lib/motionPref.ts` 仍在用(背景/右栏/发送钮),`bits/BlurText/` 文件留着不进 bundle(工程不是 git 仓库,留作回退)。

**回退**:`ChatArea.tsx` 换回 `BlurText + ACTIVE_CHAT` 那一段,`types.ts`/`seed.ts`/`stores/app.ts` 的 `qq` 字段、c0 条目与初值还原即可;`bits/FoldText/` 留着不 import 就不进产物。没有运行期开关。

## 14. 三卡齐平、上下渐隐、右键窗口控制条的验证与回退

本轮四项:① 左栏 tab 切换的水珠改白;② 头部右侧撤掉窗口三钮,改成可拖区右键浮出;③ 中间列做成第三张卡;④ 头部/输入栏底下加"渐变 + 模糊"两条带。全是视图层,没有新增第三方组件。

**① 三卡齐平(判据打在"高度与 inset 相等"这一层)**:`npx electron . --winmenu`(探针顺带回读布局)判据(实测):`heights` = side/chat/right **780/780/780**,`topInsets` = **10/10/10**,底边 gap 同样三栏 10;`.chat-area` 计算值 `border-radius:32px`、`cornerShape:superellipse(2)`。浏览器预览四挡(1121/1120/960)三栏 top/bottom inset 也全 =10。
**坑**:浏览器预览这一路**量不到 1280 挡** —— 视口只有 1234 CSS px,给 `.window` 写 `width:1280px` 会被 `#root`(`flex:0 1 auto`)收缩吃掉,实测 `winW` 仍是 1184.6、chatW 少 95.4(正好等于 shortfall)。1280 那一挡改在真窗口里读(`--winmenu` 的 `layout`),数字与基线 632/121.4/348 逐格相同。

**② 上下渐隐带**:几何用浏览器预览读 —— 上带 `top` 距卡片 0、高 **68**,下带贴卡片底、高 **64**,与 `.chat-header`(68)和 `.composer-wrap`(64)**等高**;`.message-list` 计算 `margin:-68px 0 -64px` + `padding:76px`,盒高 = 整卡(740/780)⇒ 内容确实穿到两条底下。
> **2026-10-11 更正**:本节口径是"渐变 + 模糊双层",其中**模糊那一层(`backdrop-filter: blur(10px)`)同日已撤掉** —— 它按 fade 自己的矩形盒合成,卡片的圆角裁不住,弧外露出一块没有转角的软方角。下面那组 on/off 数字是撤之前测的,"带区大面积变化 + 对照区≈0"的分层结论仍然成立(渐隐那一层没动)。详见第 17 节。
效果用 **on/off 逐像素对照**取证(脚本 `.cache/fade-diff.mjs`,自带对照区防假绿):把 `.chat-fade` 设 `display:none` 前后各出一张图,同一段滚动位置比像素。
- 上带:滚到 `scrollTop=70`,带区(x355..880, y14..74)**8900/31500 像素变化 = 28.3%**,meanAbs 8.91、maxAbs 192;带下对照区(y90..150)**0 变化**。
- 下带:滚到 `scrollTop=0`(此时倒数第二条跨在带区里),带区(y690..746)**24695/29400 = 84%**,meanAbs 20.01、maxAbs 195.7;对照区(y610..670)也有 3855 像素变化,但 meanAbs 只 **0.77**、maxAbs 7.7 —— 那是 SoftAurora 背景在两张图之间自己飘了一点,量级比带区小 26 倍。报数必须带这条,否则"对照区不为 0"看着像判据坏了。
**默认种子内容在下带只有"翻上去看历史"时才流经**:1280×800 下列表根本不可滚(`scrollHeight - clientHeight = 0`),`--height 600` 压矮后才有 190px 可滚。别拿默认窗口断言"下带没效果"。

**③ 右键窗口控制条**:`npx electron . --winmenu --winmenu-shot .cache/winmenu.png`,一条命令跑完五层。判据(实测):
| 步 | 判据 | 实测 |
|---|---|---|
| 前置断言 | 菜单本来关着 | `idle.open=false` |
| 合成 contextmenu(640,40) | 出现且落在光标位 | `x640 y40 120×44 btns3 pos=fixed region=no-d radius=12px shape=superellipse(2)` |
| 外部 pointerdown / Esc | 两条关闭通路各自生效 | 两次都 `open=false` |
| 越界光标(99999,99999) | clamp 回窗口内 | `x1152 y748`(= 1280−120−8 / 800−44−8) |
| `sendInputEvent` 真实右键(700,36) | 拖拽区不吃右键 | `open=true` 且 `nativeHits=0` |
| 点中"最大化"那颗 | 真的走到主进程 | `maxBefore=false → maxAfter=true`,且菜单自己关掉 |

**两条要如实说明的**:
1. `nativeHits=0` = 主进程 `webContents.on('context-menu')` 对这些事件**一次都不触发**,所以没加"主进程收事件 → IPC 回渲染层"那条转发通路(加了就是死代码)。
2. `sendInputEvent` 是从 web 输入管线注入的,**绕过了 OS 层对 non-client 区的拦截**。它证明"渲染层的 `onContextMenu` 通路与几何/交互全对",但**没有**证明物理右键落在 `-webkit-app-region: drag` 上时窗口管理器不会先把这一下吞掉 —— 那一层要人手点一次才算数。若真被吞,备选是自绘拖拽(把 drag 摘掉,用 `pointerdown` + IPC `setPosition`)或 `win.hookWindowMessage('WM_NCRBUTTONUP')`,都不在本轮范围。
> **2026-10-11 本轮12 更正**:上面这条"要人手点一次才算数"的悬案当天就被用户的手工验收坐实了 —— 物理右键确实被 OS 吞掉(症状就是那条带弹出深色六项系统菜单),所以备选那条(自绘拖拽 + 摘掉 drag)已成为现行实现;本节的右键判据由 `--drag` 接管(判据与实测见第 19 节),`--winmenu` 仍在、主要用来回读布局。本节 ③ 表里那组几何数字与现行实现一致(菜单仍是 120×44 / 3 钮);另有一段插曲值得记:同一位置当时读到过 **115×42**,原因是**入场动画还没落定就去读几何**,复采前加 `sleep(300)` 才稳定 —— 判据读到"差一点点的尺寸"要先怀疑测量时机而不是怀疑代码。

**④ 白水珠(判据要能看见色,否则 0 命中不算证据)**:先把 `Sidebar.tsx` 的 `animationTime` 临时改成 `30000` 把动画冻在飞行中段(**取证后必须改回 450**),再 `npx electron . --gn 2 --gnshot .cache/gn` 连拍静止/飞行/落定三张。
判据(实测):静止→飞行 **917 像素变化**(maxAbs 224,证明"确实有水珠在飞"),这些变化像素里通道差 >40 的**饱和色 = 0 个**;把 `colors={palette.glow}` 加回去做**阳性对照**(`.cache/gncol.*`),同样口径量出 **83 个饱和像素 = 9.5%** ⇒ 度量本身看得见颜色,0 是真阴性。
**坑**:`.cache/nav-tint.mjs` 那套"精确色命中"在两态都是 0(上一轮它给过 441/763,是另一帧时机),所以它**不是**本轮的判据;判据是"变化像素的饱和计数"。另外挂载本身不产生水珠(`GooeyNav.tsx:754` `target<0` 直接 `settle()`),静止帧与飞行帧必须自己证伪"没动画"。

**⑤ 体积**:CSS 22.78 → **23.93 kB**(gzip 5.74),JS 352.33 → **353.24 kB**(gzip 124.33)。增量 = 两条 fade 带 + `.win-menu` 那套 + `.chat-area` 成卡,以及撤掉 `.action-pill.small`。

**⑥ 帧间隔**:`npx electron . --frames 3000` 判据(实测):on p50 **6.9** / p95 **7.0** / max 7.1(432 帧),off 同量级、某轮 max 34.7 —— 与上一轮基线相同;补间只在切换瞬间跑,渐隐带是静态合成层。长帧偶发、两条腿都会中,别拿单样本归因(见第 12 节 ⑦)。

**回退**:`ChatArea.tsx` 头部换回 `isElectron && <>最小/最大/关闭</>` 那五个 `action-pill small` 并删掉 `.win-menu` 与 portal;`app.css` 删 `.chat-area` 的 `border-radius/overflow`、`.message-list` 的负 margin、两条 `.chat-fade`;`Sidebar.tsx` 把 `colors={palette.glow}` 传回去即恢复彩色水珠;`tokens.css` 删 `--chat-veil`。全是视图层,没有运行期开关。

## 15. 输入栏两个圆角异常与「回到最新消息」钮的验证

用户标注:"两个圆角都显示异常" + "这个按钮(输入胶囊右端的 ◯↓)要在消息上划之后才出现,用来快速回到最下面的消息"。

**① 成因(先量清再动手)**:整窗截图缩着看圆角会被重采样抹平,必须 1:1 裁。给 `--shot` 加了 `--crop x,y,w,h`(走 `capturePage(rect)`),裁出输入栏那一条:左端一道弧形亮边、右端绿色圆钮外一圈月牙。
读计算值定位到**单一根因**:`.composer{flex:1}` 本来是给 `.composer-wrap`(行 flex)拿宽用的,而它当时被 BorderGlow 包在 `.border-glow-inner`(**column** flex)里 ⇒ `flex-basis:0%` 顶掉 `height:52`,实测胶囊高 **40**,与内层 `.composer-input`(40)一样高 ⇒ 两层 999px 帽沿同心相咬,两端各多出一道弧;`border-glow-inner` 的 `overflow:auto` 又把胶囊自己的 `--shadow-float` 剪成直边,是第二道亮痕。
> 与第 12 节 Magnet/ClickSpark 那条"高度链断掉把发送钮压成 52×22"是同一族缺陷:**wrapper 换了 flex 方向,`flex:1` 的含义就跟着换**。

**② 修法**:按用户口径整件撤下 BorderGlow(输入栏 hover 只要图标自己的圆底),顺带该路径不再存在;同时删掉 `.composer-glow*` 四条适配规则与 `.composer{position:relative}`。CSS 18.72 kB、JS 349.91 kB(相对撤下前 **−5.21 / −3.33 kB**)。

**③ 判据(打在缺陷可见的那一层)**:
| 判据 | 方法 | 实测 |
|---|---|---|
| 胶囊回到 52 高 | `--scroll` 探针回读 `.composer` 盒 | `h:52`(四挡 1121/1120/960 也都是 52,`glowH/innerH` 同 52) |
| 只剩图标悬浮效果 | 新增 `--hover x,y`(先 `sendInputEvent mouseMove` 再 `capturePage`),停在 📁 上裁 1:1 | 只有那颗 36 圆底变灰,整条胶囊无彩色洗色 |
| 两端帽沿不再相咬 | 同一裁图目视 | 左端干净半圆,绿钮外无月牙 |

**④ 「回到最新消息」钮**:`npx electron . --resize` 一条命令走完"初始 → 压矮 → 上划 → 点掉 → 拉高 → 再压矮上划 → 在显示态下拉高"七步,每步同时读**几何真值**(`covered` = 末条底边是否越过 `.composer-wrap` 顶边)与**DOM 事实**(`dom` = 屏上到底有没有那颗钮),两者必须一致。判据(实测;gap = 输入区顶边 − 末条底边,正=没被盖住):
| 步 | cardH | top/max | gap | covered | dom |
|---|---|---|---|---|---|
| 初始 1280×800(内容放得下) | 780 | 0/0 | **+22** | false | false |
| 压矮到 600 | 580 | 0/190 | **−178** | true | **true** |
| 上划到顶 | 580 | 0/190 | −178 | true | true |
| 点掉它 | 580 | **190/190** | **+12** | false | false |
| 拉高回 800 | 780 | 0/0 | +22 | false | false |
| 再压矮 + 上划 | 580 | 0/190 | −178 | true | true |
| **显示态下拉高**(用户报的第二件事) | 780 | 0/0 | +22 | false | **false** |

- 判据口径:**"末条被输入区盖住"而不是"离底 ≥24px"** —— 读 `.msg-row:last-child` 与 `.composer-wrap` 两个真实盒,列表末尾那 76px padding、消息高度变化、右栏避让改掉的可用宽度都自动算进去,没有要跟的常数。滚到底时 gap = +12(净空)、内容整体放得下时 +22,都是"隐藏"。
- **尺寸变化必须重算**:内容不再溢出时往往连一个 scroll 事件都不派发(没有可滚的量),不挂 `ResizeObserver` 就会赖着不走 —— 表里最后一行就是这一条。观察对象是列表盒,所以窗口缩放、面板开合走同一条路(原来那条"右栏 1121→1120 转浮层"的触发源自本轮11 已随浮层一起删除,第 18 节)。
- 位置:`x=624`、宽 32 ⇒ 中心 640 = `chatCenter=640`(聊天区中线,不是输入栏中线 610 —— 用户明确要"聊天界面中间",且这样右栏展开避让时它不跟着挪)。
- 不得二次移动:探针轮询到"刚出现那一帧"与 600ms 后的落定帧对比 ⇒ `moved(x)=0`,而 `y` 488→482(6px 上浮)。**y 必须同时有差**,否则"moved=0"可能只是首帧读晚了、动画早结束 —— 那时判据是空转的。成因:先前复用 `msg-in`(`from{transform:translateY(8px)} to{transform:none}`)会在播放期间顶掉 `translateX(-50%)`,落定又弹回 ⇒ 就是用户看到的"出现后再往左偏一段";新关键帧 `jump-in` 两帧都自带 `translate(-50%, …)`。
- **别用 `behavior:'smooth'`**:平滑滚动途中会派发 scroll 事件,那一刻末条还被盖着,`syncJump` 会把刚点掉的钮又挂回来(闪一下),落定才消失。直跳只有一次事件,表里"点掉它"那行 `dom:false` 也就是干净的。

**⑤ 布局回归**:`inputW` 基线**已变** —— 撤掉输入胶囊右端那颗 ◯↓(28 + 间隙 6)后,四挡 chatW/pillActionsGap 逐格不变(632/473/786/626、121.4/41.9/92.4/12.4),而 `inputW` 348/189/194/34 → **382/223/228/68**(每挡正好 +34)。第 13 节 ⑥ 那组 inputW 数字自本轮起作废,以本条为准。

**回退**:`ChatArea.tsx` 把 `composerPill` 重新包进 `<BorderGlow className="composer-glow" borderRadius={26} …>` 并在输入胶囊里放回 `<button className="round-btn">`,`app.css` 恢复 `.composer-glow*` 四条与 `.round-btn`、删 `.jump-bottom`/`jump-in`。`bits/BorderGlow/` 一直留着,不 import 就不进产物。

## 16. 快捷短语三态与 HoldButton 的验证与回退

三件事:① 短语区可折叠 + 短按追加进输入框 + 右键就地改名;② 按住 1s 直接发送(组件用附件给的 HoldButton,`holdTime=1000`);③ 整节高度上限 = 右栏 40%,到限后内部滚动。

**① 取源**:`curl -s -o .cache/hb-ts-css.json https://reactbits.dev/r/HoldButton-TS-CSS`,再按 `files[].content` 逐字节写 `src/components/bits/HoldButton/`(用 node 直接从 JSON 落盘,别手抄)。判据(实测):`dependencies: []`、`registryDependencies: []`,落盘 `HoldButton.css 7303 B` / `HoldButton.tsx 9571 B`,与 registry 报的字节数逐字相等 ⇒ **零新增依赖**。上游 JS 变体与 TS 变体 props 一致(含 `onTap`)。

**② 探针 `npx electron . --phrase`**:一条命令跑完六步,每步都记"按下→抬起"的**实测时长** —— onTap(<250ms)、onHold(满 1s)、提前松手三条分支全靠时间差判定,`.click()` 合成事件一条都测不出来。判据(实测):

| 步 | 按住时长 | 结果 |
|---|---|---|
| 短按 | 186ms | `draft="666"`,`msgs` 不变 ⇒ 只追加没发送 |
| 按住 | 1430ms | `msgs 5→6`、`lastMsg="666"`、`data-phase="done"` ⇒ 直发 |
| **提前松手** | **527ms** | `msgsSame && draftSame`、phase 回 `idle` ⇒ 两条分支都没被误触发 |
| 右键 | — | `editing=true`、`editValue="666"`;改名 + Enter 后标签变「666 顶」 |
| 点标题行 | 185ms | `aria-expanded=false`、chevron `matrix(0.019,-0.9998,…)`= −90°、列表消失、网格 550→580 |
| 灌到 41 条 | — | 节高 **302px**(= `max-height:40%` 解析成 302.4 且真绑住),列表 `client 268 < content 370` 内滚,网格仍留 312 |

第三行是**限幅对照**:只测"按住会发送"分不清判据真咬住了还是"任何抬起都发送"。

**③ 探针自己的两个假读数(记下来免得误判)**:
- `labels` 最初读 `.hold-button` 的 `textContent`,量出来是「666已发送666已发送666已发送Press and hold for 1 seconds to confirm」—— 组件把 idle/done 两层标签**各渲染三遍**(本体 + 液面内 + 波峰内),外加一条 `.hold-button__sr` 提示语。要读标签只能选 `.hold-button__idle`。
- `lastMsg` 最初选 `.msg-row:last-child .text`,发「666」之后读成 `null` —— 纯数字被 `classify()` 判成 `bigemoji`,渲染的是 `.bigemoji` 而不是 `.text`。改成量整行 `textContent`。
两条都不是产品缺陷,是判据选错了层。

**④ 40% 的参照系要说清**:`max-height:40%` 的包含块是 `.panel-anim`(= 卡高 780 − 上下内边距 24 = 756)⇒ 上限 302.4px,**按卡高算是 38.8%**。试过换成 `40cqh` + 给 `.right-panel` 加 `container-type: size`:实测 `maxHeight` 解析成 302.4px,**与百分比同一个数**(cqh 量的也是内容盒),于是回退掉尺寸包含(窄窗转浮层时少一层风险)。谁再问"为什么不是 312",答案在这里。

**⑤ 一处刻意的例外**:HoldButton **不进** app.css 文末那条 corner-shape(G2)清单 —— 它的液面与外框共用 `clip-path: inset(0 round var(--hb-radius))`,而 `corner-shape` 不改 `clip-path` 的角形,只把盒体换成超椭圆会让按住时液面边缘与盒角错开。代价是短语 chip 保持 G1 圆角(r=12 时两制差 <1px)。

**⑥ 布局与帧间隔回归**:`--winmenu` 的 layout 读数 chatW 632 / pillActionsGap 121.4 / inputW 382、三栏高 780、inset 10/10/10,与第 15 节新基线逐格相同;`--frames 3000` on 腿 p50 6.9 / p95 7.0 / max 7.1(432 帧)不变 —— HoldButton 的 rAF 只在按住期间跑,静止时每个 chip 只留一个 ResizeObserver。

**回退**:`RightPanel.tsx` 的 `PhraseSection` 换回原来那段 `.chip` + `onClick={() => send(p)}`,`stores/app.ts` 撤掉 `draft/appendDraft/setDraft/updatePhrase/phrasesOpen` 与 `QUICK_PHRASES` 初值(改回 `phrases: ['666']` + 视图层合并),`ChatArea.tsx` 的 `draft` 换回局部 `useState`,`app.css` 删 `.phrase-section/.phrase-input/.section-head.collapsible/.chevron/.head-label/.count` 与 `.phrase-chips .hold-button` 适配段,`tokens.css` 删 `--chip-bg`。`bits/HoldButton/` 留着不 import 就不进产物。

## 17. 会话条目按原型重定、中间卡角的两层成因与验证(2026-10-11)

本轮三件事:① 左栏条目按原型实测改成 pitch 72 / 头像 50;② 中间卡的角"看不见"→ 换深一档 `--chat-veil`;③ 用户又指出弧外多出一块没有转角的软方角 → 撤掉 `.chat-fade` 的 `backdrop-filter`。全是视图层与令牌,无新增依赖。

**① 新增的五条自证探针**(都在 `electron/main.ts`,`npm run build` 后 `npx electron . <旗标>` 跑):

| 旗标 | 读什么 | 关键输出 |
|---|---|---|
| `--rows` | 列表盒 / 条目盒 / 头像在条目里的上下空档,以及三卡圆角与阴影的计算值 | `pitch` `gap` `insetTop/Bottom` `scrollable{client,content,over}` `chatArea{radius,shape,shadow}` |
| `--tabs` [`--tabs-click n`] | 左栏过滤行逐盒几何(nav→stage→list→4 格) | `gapToNavInner` `distinctTops` `fallbackDisplay` |
| `--proto [x0,y0,x1,y1]` | `nativeImage` 解原型 JPEG(BGRA),按行找名称字带 / 头像圆盘 | `bands` `pitch` `edgeRow/edgeCol` 亮度剖面 |
| `--corner` | 中间卡那只角的三态隔离(红底隐子级 / radius 归 0 / 各档 veil、shadow) | 逐态写 `.cache/cor-*.png`,回读计算值 |
| `--cornershot [1,11,12]` | 每个方案在**真实界面**上单独出一张 1:1 角部裁图,拼成带标签对照表 | `.cache/corner-sheet.png` |
| `--leak` | 一次只关一样东西,两只角各出一张裁图 | `.cache/lk-NN-{BL,TL}.png` |

**② 条目间距的判据要打在"用户看得见的那一层"**:`.chat-list` 是 `display:block`、`row-gap:normal`、条目 `margin:0` ⇒ 盒间隙恒为 0,只看这条会得出"已经很紧"的结论。用户眼里的间距 = 头像上下空档,实测 `insetTop/insetBottom` 各 **11**(改前 10),`pitch` 八段全 **72**,头像高 **50**。另加一条 `scrollable`:client 678 = content 678 ⇒ 9 项**根本不滚动**,顺手更正了手册里"首屏 7 项 + 滚动"的旧口径。

**③ 原型量尺(`--proto`)的两个坑**:
- 原型图整图是 **1654×1276**,不是手册早期写的 1280×987(那是窗口区)。按整图折算会把侧栏算成 300(原型实测 389/1264 ≈ 31%)。
- 头像检测不能用"饱和像素":原型里的头像是**暗色照片圆盘**,`sat>70` 只抓到 1 条;换成"非背景"(`lum<200 || sat>70`)才成带。名称字带(暗、连续)是最稳的节奏源。

**④ 角可见性判据(`.cache/cor-arc.mjs`)**:沿已知超椭圆(角点 `CL,CB`、圆心 `(CL+32,CB-32)`、n=4)逐行解析出弧的 x,取弧内 2px 与弧外 2px 之亮度差,报 mean/p10/min。基准 = 右卡同一只角(16.4 / 15.3 / 15.3)。
判据(实测):`--chat-veil` 改前 mean 15.9 / **p10 4.0** / min 2.8 → 改后 **26.5 / 21.9 / 20.6**(撤 blur 后再测 27.9 / 22.3 / 21.8)。
**这个量具的两条边界**(都踩过):① 它只适配 BL/BR 这类角,把 `CB=42` 塞进去当 TL 角会算出一条镜像假曲线;② 描边/内环类方案把边界画在弧"上",±2 采样的 `step` 会假小甚至 p10 掉到 0.4,只能看 ±3 窗口的 `spread` 或目视。

**⑤ 弧外那块方角(`--leak` + `.cache/lk-pts.mjs`)**:判据 = 弧外、外接方格内那块楔形**应当与真底同色**。真底参照另取一态(`.chat-area{visibility:hidden}`)—— 别拿"卡外同排那点"当基准,BL 处它落在侧栏 `--shadow-float` 里(实测 246.6 vs 真底 254),会把干净态误判成有漏。
判据(实测):撤 blur 前 BL 楔形 248,241,245(饱和差 7、比真底暗 11.7)、TL 255,246,228(**饱和差 27**、暗 17.2);撤后两角 254,254,254 / 255,255,255(**饱和差 0**);复采与首采逐位相同。
**归因是三条独立差分**,不是一条:关 `backdrop-filter` / 隐 `.chat-fade` / 给 fade 挂 `border-radius:inherit` 三者都归 0;而 `clip-path:inset(0 round 32px)` **不归 0**(248,241,245)⇒ 祖先圆角和 clip-path 都拦不住 `backdrop-filter` 的那层合成。结论与四条候选见 手册 4.1.2。

**⑥ 两条实测否决的路**(记档防重走):给中间卡补三卡同款 `--shadow-float` → 弧外被压暗到与洗色同调,弧对比 15.9 → **3.8**;把 `--chat-wash` 底部 alpha 提到 .42 → 只到 15.9 / 4.0(弧那几行被 veil 盖住,不吃洗色)。两处都已回退,`tokens.css` 里 `--chat-wash` 保持 0.68/0.42/0.22。

**⑦ 探针自身的两个假动作**:
- `capturePage()` 取的是**已呈现的那一帧**:注入样式后立刻拍,拍到的是改之前的图(实测 `cor-b.png` 一片正常色,而同一时刻 `getComputedStyle` 已读到 `rgb(255,0,0)`)。每条注入后 `sleep(320)` 再拍。
- 覆盖 `--chat-veil` 必须写在**元素**上:`:root{…}` 会被 `:root[data-chat-theme='warm']{…}`(特异度 0,2,0 > 0,1,0)吃掉,当时量出"改与不改逐位相同"的假对照。
- 另外那张探针样式表如果被某一步 `remove()` 掉,后续步骤再 `getElementById(...).textContent` 就是撞 null —— 整条探针会在最后一档抛死。`SHEET()` 改成"取或建"。

**⑧ 回归**:`--winmenu` 布局读数 chatW 632 / pillActionsGap 121.4 / inputW 382、三栏 780、inset 10/10/10 逐格等于基线;`npm run typecheck` 双侧 0;CSS 25.83 kB(gzip 6.25)、JS 356.47 kB(gzip 125.13)不变(本轮全是数值与一条声明的增删)。

**回退**:`app.css` 把 `.chat-item` 高改回 64、`.avatar` 改回 44/19px、`.chat-fade` 补回 `backdrop-filter: blur(10px)`;`tokens.css` 的 `--chat-veil` 改回 warm `rgba(255,240,214,.82)` / mint `rgba(226,246,236,.82)`。探针旗标不碰运行期代码,留着不跑即零成本。

## 18. 窄窗等比收缩与 1248 下限的验证与回退(2026-10-11 本轮11)

用户报的缺陷:"窗口宽度缩小时,最右侧卡片会和中间聊天区域合并在一起",并给三条口径:不重叠、左右等宽、中间区最小宽 = 侧栏的 2 倍。选定方案 A(等比收缩),随后追加"限制缩放的最小宽度 1248"。

**① 诊断先量清再动手**:旧模型把三件事耦合在一起 —— 中间区的 `1fr`(= `minmax(auto,1fr)`,轨道不会缩到 min-content 以下)、`@container (max-width:1100px)` 的浮层分支、以及 `--chat-min` / `--panel-footprint` / `--head-reserve` 三个避让令牌。重叠就出在这三者的接缝上:浮层挡位触发前,右栏已经被压到与中间区相叠。

**② 新增探针 `--cols`**(可选 `--cols-shot <前缀>` 每挡出一张图):主进程按 **11 挡**设窗宽(`cols-done {n:11, widths:[1280,1260,1240,1200,1160,1120,1100,1060,1020,980,960]}`),每挡读一张量表。**1248 不在请求列表里** —— 它是低挡被 `minWidth` 夹回后 `winW` 报出的实际值。字段与本轮实测(2026-10-12 复采):

| 挡(请求→实际) | `sideW` | `chatW` | `rightW` | `chatOverSide` | `overlap` | `sideMinusRight` | `inputW` |
|---|---|---|---|---|---|---|---|
| 1280→1280 | 300 | 632 | 300 | 2.107 | **−14** | **0** | 382 |
| 1260→1260 | 300 | 612 | 300 | 2.040 | −14 | 0 | 362 |
| 1240→**1248** | 300 | 600 | 300 | **2.000** | −14 | 0 | 350 |
| 1200 / 1160 / 1120 / 1100 / 1060 / 1020 / 980 / 960 → **1248** | 300 | 600 | 300 | 2.000 | −14 | 0 | 350 |

判据(全部要在同一份输出里成立):`overlap < 0`(负值 = 还留着 `--gap-side` 那条空隙)、`sideMinusRight = 0`、`chatOverSide ≥ 2`、`overflowX = 0`、`emojiClip = 0`、`composerClip = 0`、`msgClip = 0`、`rightPos = "relative"`。最后一行是"下限真咬住"的证据:请求 960 时 `winW` 报 1248。

**③ 三条抓到的静默失效(每条都改了代码)**:

1. **等宽不能交给 flex 收缩**。flex 的收缩量按**内容盒**基线分配(`border-box` 下先减 padding),右栏有 12+12 横向内边距,于是每挡都比左栏少缩一点:实测先差 **24px**、调一轮后仍差 **5.9px**。改法 = 两侧同源写死 `flex: 0 0 var(--side-w)`。
2. **`overflow-y: auto` 会顺手升起横向滚动条**:`overflow-x: visible` 被升格成 `auto`,表情网格在窄挡出现幽灵横滚条。改 `.emoji-grid { overflow-x: clip }` + 列 `repeat(6|9, minmax(0,1fr))`。**判据必须读 `.emoji-grid` 自己的 `scrollWidth − clientWidth`** —— 读父级得到 0,看着像"没有溢出"。
3. **浮层分支整条删除**,连带 `.window[data-panel]` 不再被 CSS 消费(`App.tsx` 仍写,现在只有 `--cols` 的 `dataPanel` 字段读它)。

**④ 判据纪律**:用户的三条口径是**三条独立不变量**,必须一条一个读数(`overlap` / `sideMinusRight` / `chatOverSide`)。把它们合成一个"布局没坏"的布尔值就回到假绿 —— 等宽量不出铺满、不重叠也量不出等宽。

**⑤ 回归与体积**:`--cols` 11 挡如上;1280 挡与第 13/15 节记录的读数**逐格相同**(chatW 632 / inputW 382),说明这条 clamp 没动默认窗宽下的几何;`npm run typecheck` 双侧 0。

**回退**:要回到旧形态,需要同时改四处 —— `tokens.css` 恢复 `--chat-min: 460px` / `--panel-footprint: calc(300+10+14)` / `--actions-w: 96px` / `--head-reserve`(并删 `--col-side-min`),`app.css` 把 `.window` 的 `--side-w` 声明删掉、`.sidebar`/`.right-panel` 改回 `flex: 0 0 var(--col-side)`、`.chat-area` 改回 `flex: 1`(去掉 `min-width: 0`)、把 `@container (max-width: 1100px)` 那一整块浮层与避让规则补回,**并**把 `electron/main.ts` 的 `minWidth: 1248` 改回 960(否则 1120/960 两挡永远进不去,回退等于没回退)。

## 19. 自绘窗口壳:拖拽 / 三项控制条 / 最小化退场的验证与回退(2026-10-11 本轮12–14)

三件事一轮做,但**必须一起验**:它们共用同一个前提 —— OS 不再参与这条带子。用户口径依次是:① 拖拽带右键那个深色弹窗与界面不协调,只留最小/全屏/关闭三项 → 澄清后选 A 方案"去掉 `-webkit-app-region: drag` 让右键响应 DOM";② bug:双击最大化后**单击**就恢复;③ 疑问:最小化为什么没有 OS 那段缩进任务栏的动画 → 选 B"给一个通用缩小动画,Win10+Win11 效果一致";④ 退场动画帧率太低,优化到 60fps。

**① 探针旗标**(`electron/main.ts`,先 `npm run build` 再 `npx electron . <旗标>`;`--cols` / `--drag` / `--minanim` 跑完各自 `app.quit()`(实测三次调用都 rc=0、无残留进程),`--plain` 是人工对照形态不会自动退出):

| 旗标 | 验什么 | 关键输出字段 |
|---|---|---|
| `--drag` | 右键归属 + 两条带 + 控制条几何与开关 + 拖拽接线 + 位移公式 + 双击 + 单击不恢复 + 阈值 | `appRegion` `bands` `menuMid` `menuSide` `noDragControl` `nativeHitsReal` `dragSeqLog` `posDeltaCursorDrift` `dragMath` `maxAfterDbl` `stillMaxAfterClick` `clickLog` `slop` |
| `--minanim` | 退场动画三腿对照(off / on / bd) | `presented{calls,distinct}` `curve` `raf{p50,p95,max}` `origin` |
| `--plain` | OS 最小化动画的三臂对照(同时开 `frame:false`+不透明 与 `frame:true` 两颗对照窗) | 日志里的 `main-hwnd` / `plain-hwnd`,配合 `.cache/win-styles.ps1` 读 style/exstyle;跑完要自己关窗口 |
| `--cols` | 布局量表(第 18 节) | 同上 |

**② A 组:右键真归 DOM 了吗**(2026-10-12 复采):`appRegion: []`(整棵树 0 处 `-webkit-app-region: drag`)、两条带都在(`sidebar-top 300x56` / `chat-header 632x68`);两处右键各浮出 **120×44 / 3 钮 / 最小化·最大化·关闭 / `wrapPos fixed` / `z 99` / 底 `rgba(255,255,255,.85)` / 半径 12px 走 `superellipse(2)`**;Esc 与带外按下都关(`menuMidEscape: null`、`menuAfterCloseReal: null`);**对照腿** `noDragControl: null` —— 带内 `.no-drag` 上右键不浮出,证明判据不是"任何右键都开菜单";`nativeHitsReal: 0` —— 主进程 `webContents.on('context-menu')` 一次都没触发。

**③ B 组:拖拽只能分两层验**(端到端验不了,原因要如实说):合成指针事件的坐标是 **view 相对**的,而 `screenX = winX + viewX`,窗口一动 `winX` 就变 ⇒ 自指反馈,量不出真实跟手。

- 接线层:`dragSeqLog {start:1, move:3, end:1}` 且 `posDeltaCursorDrift [0,0]` —— 真实光标没动 ⇒ **窗口必须一格不差**。这一条同时排掉两种错:把 view 坐标当屏幕坐标(会瞬移几千 px)、以及"看起来通了其实什么都没发"。
- 公式层:`dragTarget()` 单独导出,用合成数字验四例 —— 静止 (100,200)、右下 (+60,+40)、贴顶夹到 `y=20` 且 `hitTop: true`、左上越界 (0,160)。
- 双击:`maxAfterDbl: true` → 再双击还原。
- **单击不恢复**(修 bug 那一枪):`maxBeforeClick: true` + `stillMaxOnPointerdown: true` + `stillMaxAfterClick: true`,而 `clickLog {start:1, move:0, end:1}` 证明这一枪真开过。阈值另测四挡 `slop {jitter2: false, edge4: true, down6: true, negative: true}` —— 2px 不算起拖、4px 算、负方向同判。
- **元教训**:上一轮我写的断言是 `restoredOnDragStart: true`,那是**把缺陷当规格**。判据里出现"符合 bug 的行为"就一定是错的,这条要写进验收单而不只是修代码。

**④ C 组:OS 动画为什么拿不到(三臂对照)**:同一登录会话下开三颗窗口 —— 本应用(`frame:false` + 透明)、`frame:false` + 不透明、`frame:true` —— 只有**第三臂**有缩进任务栏的动画,故分界在 `frame`,更准确地说是 Chromium 不给 frameless 窗口挂 `WS_SYSMENU`。注册表 `MinAnimate` / `EnableAnimations` 本机均为开(`.cache/anim-flags.ps1`),所以不是系统关了动画。
**被证伪的假设**:`WS_EX_LAYERED`(三颗窗口都没有)。**未分开的混淆项**:`WS_CLIPCHILDREN`(与 `frame` 同时出现,没能单独拆)⇒ 这条只到"相关",不到"因果"。

**⑤ D 组:退场动画与"帧率太低"的归因**(`--minanim` 三腿,2026-10-12 复采):

| 腿 | 注入 | `presented`(去重帧/调用) | `raf` p50/p95/max | scale 采样(20/60/100/140/180ms 档) |
|---|---|---|---|---|
| `off` | `will-change:auto` + 用回入场曲线 `--ease` | 13/16 | 7 / **13** / **20** ms | 24ms **1.000** → 79ms **0.6577** → 100ms 0.6283 → 142ms 0.6066 → 183ms 0.6005 |
| `on` | 出厂态(`--ease-exit` + `will-change`) | **17/19** | 7 / 9 / 12 ms | 26ms 0.9955 → 66ms 0.9428 → 100ms 0.8694 → 142ms 0.7572 → 184ms 0.6229 |
| `bd` | 出厂态再退掉退场期间的 `backdrop-filter` | 15/22 | 7 / 7 / 11 ms | 26ms 0.9955 → 64ms 0.9428 → 106ms 0.8524 → 140ms 0.757 → 182ms 0.623 |

行程比例是把 scale 按全程 `1 → 0.6`(即 0.4)折算的:`off` 腿在 **79ms 就走完 85.6%**(scale 0.6577)、100ms 到 92.9%,而 `on` 腿 66ms 才 14.3%、100ms 到 32.7%、184ms 才 94.3%。⇒ **主因是曲线,不是掉帧**:强减速曲线把绝大部分位移压进头 80ms,后面全是肉眼分不出的尾巴,看着就是"一格一格"。

**这个仪表的 run-to-run 离散要记清**:同一条 `on` 腿在前一轮(2026-10-11 首测)是 **15/18、p95 7 / max 11**,本轮复采是 **17/19、p95 9 / max 12**;`off` 腿两轮是 11/16 与 13/16。`presented` 的绝对值会被 `capturePage` 自己的吞吐抖动带走 ±2 帧,所以**别拿单轮的绝对值当阈值**,要拿的是"同一次运行里三腿的相对次序 + 曲线形状"。曲线形状两轮完全一致(off 永远在 80ms 内走完 85%),这条才是稳的判断依据。

`bd` 这条腿要如实报:它的 `raf`(7/7/11)甚至比 `on`(7/9/12)略好,`presented` 是 15/22 对 17/19 —— 去重帧更少而调用次数更多,**说明这个仪表在 backdrop-filter 这一档已经分不出胜负**(三腿的 `calls` 本身就在 16/19/22 之间抖)。所以保留 `backdrop-filter` 的依据是"**收益未证 + 观感损失确定**",不是"实测无差异"。这条候选到此为止,别再去试第二次。
恢复腿另两条读数:静息 `transform-origin: 640px 400px`,退场期间 **640px 800px**(= 底边中心,判据打在"收拢点"这一层),`after` 回到 `transform: none / opacity 1 / dur 0.18s,0.18s` —— 摘类干净、没有残留。

**⑥ 仪器本身的教训(上一轮我把结论报错了)**:rAF 间隔量的是**主线程 BeginFrame 节奏**,`getComputedStyle().transform` 读的是**按时钟插值的值**,两者都不能证明"呈现了几帧"。能给出呈现帧**下限**的做法是:动画期间循环 `capturePage({x:470,y:300,width:160,height:160})`,对 `img.toBitmap()` 做 sha1 去重。上一轮我拿 rAF 报"最大帧距 8ms、帧率没问题",被用户一句"帧率太低"推翻 —— 不是判据太松,是**打错了层**。

**⑦ 一条真实事故(会弹模态错误框)**:跑取证时一条 `grep -oE '"layout":\{…'` 的正则括号不配平,管道读端先退出 ⇒ 主进程 `console.log` 抛 EPIPE ⇒ Electron 弹出错误对话框挡在用户面前。处置:`electron/main.ts` 入口挂 `process.stdout.on('error', () => {})` 与 `process.stderr.on('error', () => {})`,并且**探针输出一律 `> 文件 2>&1`,不接管道**。

**⑧ 已声明的未覆盖范围**(不是 bug,交付时要说清):`Win+D` 与从任务栏点下去/点回来走 OS 路径,应用拦不到 ⇒ 那两种情况没有动画;放弃 OS 拖拽 ⇒ 放弃 Aero Snap 的**左右分屏吸附**,只有顶边"卡住松手即最大化"自己补了;`prefers-reduced-motion` 下用 `--force-prefers-reduced-motion` 跑真窗口验证走的是兜底计时器那条路(立即最小化、不播、不卡住)。

**⑨ 这套能力已抽成用户级 Skill `electron-window-chrome`**(2026-10-12):`assets/` 5 个可直接拷入任意 Electron+React 工程的示例源文件(通用化成 `.drag-region` / `.no-drag` 类契约 + `--chrome-*` 自带默认值 + 零第三方依赖),`references/` 是 17 条坑位清单与三腿验收手册,`scripts/` 两条:

```
node ~/.qoder-cn/skills/electron-window-chrome/scripts/check-wiring.mjs --root .      # 接线审计,有 FAIL 就 exit 1
node ~/.qoder-cn/skills/electron-window-chrome/scripts/prove-audit.mjs                # 证明上面那条审计"确实会红"
```

实测(2026-10-12):对 ChatAPP 本体 `PASS=27 WARN=0 FAIL=0`(`--verbose` 下 39 条);`prove-audit.mjs` 用 `assets/` 现搭最小工程注入 12 种缺陷 → **12/12 条变异全部按预期翻转**(其中 `will-change` 缺失那条按设计只降级成 warn)。资产本身用本工程 `tsc --strict --noEmit` 编过,`rc=0`。审计脚本自己踩到的两个假绿已修:判据必须跑在**注释抹成等长空格**的文本上(否则"原先这两条是 -webkit-app-region: drag"这句注释会被当成接线),抠 handler 体要用**圆括号配平**而不是花括号(`win.on('restore', () => send(...))` 这种不带花括号的箭头,找下一个 `{` 会跳到别的函数体上去)。

**回退**:① `src/styles/global.css` 把 `.drag-region { -webkit-app-region: drag }` 与 `.no-drag { -webkit-app-region: no-drag }` 两条补回;② 摘掉 `App.tsx` 里的 `<WindowChrome />` 并删该组件;③ `electron/main.ts` 删 `window:drag-start/move/end` 三段与 `win.on('restore')` 的推送,`electron/preload.ts` 删 `dragStart/dragMove/dragEnd/onRestore`;④ `app.css` 删 `.window` 的 `transition` 与 `.window--minimizing/--noanim`、`.win-menu*`,`tokens.css` 删 `--ease-exit` / `--t-minimize`。回退之后**深色系统菜单会回来且条目不可控**(本轮就是要摆脱它),`--drag` 的 `appRegion` 判据会转为非空 —— 这条判据就是留给这个方向的哨兵。
