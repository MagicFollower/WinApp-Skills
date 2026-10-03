# MemoTask 零基础上手（T0Level）

这是一个「备忘录 + 待办」的 WPF 桌面应用：左侧 `UI4NavigationView` 三个入口（备忘录 / 待办 / 设置），右侧是对应页面；设置页含应用信息与基本配置。组件库是 StartUI4Controls v3.0.0，以 `lib/` 整份源码自包含方式引用。

- 工程根：`C:\Users\webtu\Desktop\WPF桌面APP开发\aaa`
- 本次填写人／日期：Qoder（本轮交付方）／ 2026-10-03
- 实测环境：Windows 11 专业工作站版 `10.0.26300.0`、`.NET SDK 10.0.401`、`Microsoft.WindowsDesktop.App 10.0.12`
- 数据目录：`%APPDATA%\MemoTask\`（`settings.json`、`notes.json`、`todos.json`、`backups\`、`selftest.txt`、`error.log`）

### 本次的三挡决策（有意决策，不是遗漏）

| 决策 | 取值 | 落在代码哪里 |
|---|---|---|
| 明暗策略 | `both`（亮 + 暗，默认跟随系统） | `app\Helpers\Theme.cs` 的 `Theme.Policy = "both"`；`App.xaml.cs` 调 `ThemeService.RegisterDefinitions()` 注册亮/暗两份定义；设置页给「跟随系统 / 亮色 / 暗色 / 高对比度」四挡 |
| 分发口径 | 两档都出 | `publish.cmd`（自带运行时）与 `publish_no_runtime.cmd`（框架依赖），产物名靠 `-p:ArtifactLabel=` 分开 |
| 套装键与局部换肤 | 只用内置档 + 强调色 | **`App.xaml.cs` 里没有 `UI4ThemePacks.RegisterAll()`**（不发放 8 套业务档），也不用 `UI4ThemeScope` 局部换肤；换肤入口是设置页强调色（走 `UI4Theme.SetAccent`） |

第 3 挡与 Skill 模板不同，所以模板里那句"四个按钮（亮 / 暗 / 跟随系统 / 灰纸套装）"的判据在第 2 节按本应用改写了：**本应用没有套装按钮，切档在设置页的下拉里**。

---

## 0. 前置要求

| 依赖 | 要求 | 判据命令 | 本机实测 |
|---|---|---|---|
| .NET SDK | 10.x（`net10.0-windows`） | `dotnet --list-sdks` | `10.0.401 [C:\Program Files\dotnet\sdk]` |
| WPF 桌面运行时 | `Microsoft.WindowsDesktop.App` 10.x（框架依赖档必需） | `dotnet --list-runtimes \| Select-String WindowsDesktop` | `Microsoft.WindowsDesktop.App 10.0.12 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]` |
| NuGet 源可达 | `lib` 唯一第三方依赖 `AvalonEdit 6.3.1.120` 要能 restore | `dotnet build app\MemoTask.csproj` 首段无 `NU` 错误 | 本机包缓存已热；冷 restore 见第 1 节。**离线机器要先在线 restore 一次** |
| Windows 版本 | Win10（LTSC/企业版）或 Win11；Win7/8.1 不支持（net10 的 OS 底线） | `[System.Environment]::OSVersion.VersionString` | `Microsoft Windows NT 10.0.26300.0` |

不该进版本控制的：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`、`publish\`、`publish_no_runtime\`、`%APPDATA%\MemoTask\`（运行期数据与自测报告）。

`lib/` 是上游组件源码的逐文件镜像，**不要改它**——改了就和上游脱钩（理由与恢复办法见第 7 节）。

---

## 1. 项目启动（首次构建）

    dotnet build app\MemoTask.csproj

判据：末尾 `已成功生成` + `0 个错误`；警告**固定 9 条**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），全部来自 `lib/` 上游代码。

- 别去"修"那 9 条：修完 `lib/` 就与上游不可比（第 7 节）。
- 警告数**多于或少于** 9 条 → 先看 `lib/` 是不是被改过或文件不全，再怀疑工具链。
- 增量构建会出现 `0 个警告`（只重编 app、没重编 lib），别把它当"警告消失了"；要数警告就 `dotnet build --no-incremental`，或先 `dotnet clean`。
- `dotnet pack` 缺 `lib/LICENSE.txt` 会报 `NU5019` 并把整条构建链拖红——`lib/` 少文件时的典型表现，恢复办法见 Skill 的 `fetch-source.ps1`。

实测输出（删掉 `app\bin`、`app\obj`、`lib\bin`、`lib\obj` 之后的真·首次构建）：

```
  正在确定要还原的项目…
  已还原 C:\Users\webtu\Desktop\WPF桌面APP开发\aaa\lib\StartUI4Controls.csproj (用时 269 毫秒)。
  已还原 C:\Users\webtu\Desktop\WPF桌面APP开发\aaa\app\MemoTask.csproj (用时 269 毫秒)。
  ...（9 条 warning，路径全在 ...\aaa\lib\ 下）...
  StartUI4Controls -> ...\aaa\lib\bin\Debug\net10.0-windows\StartUI4Controls.dll
  MemoTask -> ...\aaa\app\bin\Debug\net10.0-windows\MemoTask.dll

已成功生成。
    9 个警告
    0 个错误

已用时间 00:00:03.34
```

警告构成（本机逐条数出来的）：`CS0414` ×5（`UI4ProgressRing` 的 `_isLoaded`/`_isStartupAnimationRunning`/`_isAnimating` + `UI4ScrollViewer` 的 `_isAnimatingVertical`/`_isAnimatingHorizontal`）、`CS1574` ×4（`UI4ComboBox` 的 `HoverBorderColor`/`FocusBorderColor`、`UI4MultiLanguage` 的 `SetLanguage(string)`、`UI4Panel` 的 `Title`）。

> 口径提醒：`9 个警告` 是 MSBuild 汇总行的数；同一批警告在日志正文里会**打印两遍**（编译期一遍、汇总区一遍），所以直接 `grep -c "warning CS"` 本机得到 18。数警告以汇总行为准。

---

## 2. 本地调试

    dotnet run --project app\MemoTask.csproj

判据：窗口出现，标题 `备忘待办 MemoTask`；左侧三个入口都能切页；**页脚那行**（格式 `MemoTask · 当前：备忘录 · 备忘 N · 待办 M · 主题 …`）里的主题说明随切档变、条数随增删变。

`dotnet run` 会占住终端（GUI 进程不退出），无人值守就用仓库根的 `verify-window.ps1`（起进程 → 轮询主窗口 → 回读标题 → **只关自己起的那个 pid**）：

    .\verify-window.ps1 -Exe app\bin\Debug\net10.0-windows\MemoTask.exe

实测输出：

```
pid=24520
title=备忘待办 MemoTask
first_window_ms=920
residual=0
```

`dotnet run` 到出窗（含 msbuild 判定与冷进程启动）本机两次实测 `4902 ms`（第一次，含构建）与 `2895 ms`（构建已新）。**别拿这两个数和上面的 920 ms 比**——一个是"构建 + 起进程"，一个是"起进程到出窗"。

### 界面判据（本应用版，替换模板里那句"四个按钮"）

1. 设置页 → 外观 → **明暗模式**下拉，逐条选「跟随系统 / 亮色 / 暗色 / 高对比度」：整页底色、卡片、导航栏、原生标题栏都要一起变，页脚那句 `主题 …` 也要跟着变（它读 `UI4Theme.ResolvedKey`，是订阅 `UI4Theme.ThemeChanged` 后手动重发的，不是绑静态属性）。
2. **强调色**：设置页点任一色块，强调色系控件（主按钮、选中态、进度条）跟着变；「恢复主题默认」清掉覆盖后回到当前档自带的 Accent。
3. 只有局部在变 = 那处写了字面色（本地赋值即退订主题，属设计，见第 7 节）。
4. 快捷键：`Ctrl+1/2/3` 切页、`Ctrl+N` 新建备忘、`Ctrl+F` 聚焦搜索、`Ctrl+S` 立即落盘、`Ctrl+D` 勾选/取消选中待办、`Esc` 关详情面板/取消编辑。

### 无人值守自证（不开窗、退出码 = 失败断言数）

    app\bin\Debug\net10.0-windows\MemoTask.exe --selftest
    $LASTEXITCODE
    Get-Content "$env:APPDATA\MemoTask\selftest.txt"

判据：退出码 `0`；报告逐行 `PASS`（本机 **28 条**）。这条比人眼看窗口更硬，因为它钉住了令牌数、三份内置定义逐令牌可取色、宿主配色对比度门槛、`Mix`/`IsDark` 判据、明暗策略已显式决策、中文日期与标签解析、多屏坐标判定、8 套预置的资源键齐全、宿主 XAML 实际用到的键齐全、以及设置整份能写能读回。

实测输出（Debug 产物与两档打包产物都跑过，内容一致）：

```
PASS  令牌总数为 38  [实际 38]
PASS  light 全令牌可取色
PASS  dark 全令牌可取色
PASS  highcontrast 全令牌可取色
PASS  亮档正文/底色对比度 ≥ 4.5  [15.77:1]
PASS  亮档强调色对白对比度 ≥ 3.0  [4.56:1]
PASS  暗档正文/底色对比度 ≥ 4.5  [13.74:1]
PASS  危险色对亮底/暗底均 ≥ 3.0  [亮 3.71 / 暗 4.18]
PASS  警示色对亮底/暗底均 ≥ 3.0  [亮 4.02 / 暗 3.86]
PASS  Mix(w<=0) 返回源色
PASS  Mix(w>=1) 返回目标色
PASS  IsDark 与标题栏同判据
PASS  暗档底色确实判为暗
PASS  HEX 往返一致  [#4F6BE8]
PASS  HEX 拒绝脏输入
PASS  明暗策略已显式决策（Theme.Policy）  [当前 "both"，要求 both / light-only / dark-only]
PASS  今天/明天/后天解析
PASS  相对写法 +3 / 3天后 / -2 天前
PASS  绝对写法 2026-10-20 / 10-20 / 10/20
PASS  非法日期被拒
PASS  逾期文案带天数  [逾期 2 天]
PASS  标签分隔与去重  [工作|灵感|读书]
PASS  屏内坐标判为可见
PASS  离屏坐标判为不可见
PASS  预置套装 × 38 令牌资源键齐全
PASS  字典底色与 Dark() 定义同源  [字典 #FF202026 vs 定义 #FF202026]
PASS  宿主 XAML 用到的资源键齐全
PASS  设置往返含未落位坐标
```

- 自动化里别用 `dotnet run` 占终端；**也别做"点鼠标 + 敲键盘"的脚本**——本机搜狗输入法候选窗会吃掉输入，误点还会落到用户桌面（本轮踩过，已停掉这类自动化）。要验交互就改设置（`%APPDATA%\MemoTask\settings.json` 的 `StartPage` / `ThemeMode` / `Window`）再起进程看结果。
- 带空格/中文的路径要用引号包住再传给 `Start-Process -FilePath`，否则报"找不到模块 'C:\Program'"。
- `--selftest` 不开窗、不读设置，只写 `selftest.txt`（写不进去回落程序目录，且写报告失败不改退出码）。

---

## 3. 打包与启动

两个脚本，产物名靠 `-p:ArtifactLabel=` 分档、互不覆盖。**脚本末尾有 `pause`**，交互式双击用；无人值守要喂一行空输入让它过去（本文所有耗时都是这样测的，含脚本自身收尾开销）：

    '' | .\publish_no_runtime.cmd
    '' | .\publish.cmd

| 档位 | 产物 | 目标机要求 | 本机实测 |
|---|---|---|---|
| 框架依赖 | `publish_no_runtime\MemoTask_no_runtime.exe` | 必须已装 `Microsoft.WindowsDesktop.App` 10.x | 脚本全程 `4,358 ms`，exe `1,696,938 B`（≈1.6 MB）；起窗 `909 ms` |
| 自带运行时 | `publish\MemoTask_self_contained.exe` | 免装 .NET | 脚本全程 `10,427 ms`，exe `70,298,512 B`（≈67 MB）；起窗 `1,039 ms` |

（耗时是"喂一行空输入让 `pause` 过去、整条脚本跑完"的口径，含 msbuild 与打包收尾；换图标那一轮前后各测过一次，同一档相差在 ±2 s 内。）

> 运行时包（约 150 MB）已在 NuGet 缓存里，所以上面两档耗时**不是首次拉包的耗时**；干净机器上 `publish.cmd` 第一次要额外等那段下载，本机测不出（见第 7 节未验证项）。

判据（两档都要满足）＋本机实测：

1. 脚本内第 2 步会自己跑 `--selftest`，打出 `selftest exit code: 0`；非 0 时脚本提示看 `%APPDATA%\MemoTask\selftest.txt`。实测两档都是 `selftest exit code: 0`。
2. 产物目录里**只有那一个 exe**（`DebugType=embedded` 是全局属性，`lib` 也不会留散落 pdb）。实测两档目录内文件数都是 `1`。
3. 起打包产物并读回窗口标题——**必须用本轮重新打包的 exe**（用仓库根的 `verify-window.ps1`，它只关自己起的 pid）：

       .\verify-window.ps1 -Exe publish\MemoTask_self_contained.exe

   实测输出：

   ```
   pid=20124
   title=备忘待办 MemoTask
   first_window_ms=1039
   residual=0
   ```

   判据：标题等于本轮源码里的 `Title`。拿上一轮的 exe 验本轮改动会假红（实测踩过：改完标题拿旧产物匹配，得出"打包版不出窗"的错误结论）。
4. 自带运行时档**首次启动**要把原生库解到 `%TEMP%\.net\MemoTask_self_contained\<hash>\`。本机做了对照（在上一轮 bundle 上，走的是同一份解包代码路径）：把该目录整体移走再起 = 冷解包 `1,121 ms`；同一 bundle 已解过 = `1,072 ms`，本轮缓存态 `1,039 ms`。差约 50 ms，**可忽略**。（源码或图标改动后 bundle hash 变，才会换一个新 `<hash>` 目录重解；本机解出来的缓存目录 7.83 MB，旧 `<hash>` 目录不会自动回收，要省盘自己删。）

---

## 4. 修改标题

真源只有一处：`app\MainWindow.xaml` 的 `Title="…"`（同时是任务栏与 DWM 标题栏文字）。

    Select-String -Path app\MainWindow.xaml -Pattern 'Title="'

判据：改完重新构建并起进程，`MainWindowTitle` 回读为新值。

实测输出：

```
app\MainWindow.xaml:6:        Title="备忘待办 MemoTask" Width="1080" Height="700"

# .\verify-window.ps1 -Exe publish_no_runtime\MemoTask_no_runtime.exe
pid=34004
title=备忘待办 MemoTask
first_window_ms=925
residual=0
```

- **不要自绘标题栏**：窗口里出现过 UI4 控件，库就会在 `Loaded` 时按当前主题把原生标题栏染好（Win10 只认深/浅标志，Win11 才染底色/文字/描边三色，是系统能力差异不是 bug）。`WindowStyle=None` + `AllowsTransparency=true` 会绕过整套染色通路。
- 完全不含 UI4 控件的窗口要手动 `UI4WindowTitleBar.Apply(win)`，否则它等到下次主题切换才被扫到。
- `UI4MessageBox` / `UI4ColorPicker` 是自绘标题区，对它们调 `Apply` 无可见效果。
- 标题与数据目录是两条线：`%APPDATA%\MemoTask\` 的字面量在 `app\Services\AppPaths.cs`，改 `Title` 不动它，改 `ArtifactLabel` 也不动它——只有 exe 文件名跟着 `ArtifactLabel` 变。

---

## 5. 修改图标

一个文件三处引用：`app\AppIcon.ico` → csproj 的 `<ApplicationIcon>`（exe 内嵌图标）＋ `<Resource>`（窗口/任务栏图标，`Window.Icon="AppIcon.ico"` 走它）。覆盖文件即可，不用改 csproj。

当前图标是**本地生成的字母像素图标**（不联网、不要 ImageMagick）：16×16 逻辑网格上画 `M` 的 5×7 点阵，格点放大成整数倍方块（无抗锯齿），四角各削 2 格成圆角，底色 `#4F6BE8`（= `Theme.Accent`）、字形 `#F7F9FB`（= `Theme.LightBackground`）。换字母/配色/尺寸集就重跑 Skill 的生成器覆盖回去：

    powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>\scripts\make-icon.ps1" `
      -Name MemoTask -Out app\AppIcon.ico -Back "#4F6BE8" -Fore "#F7F9FB"

判据三条：① 生成器末尾 `OK AppIcon.ico letter=M … frames=7`（它内部断言目录连续、末帧正好落到文件尾、每帧字节数等于算式值，不过就抛错）；② 同段 `probe 16/32` 两行 `corner.alpha=0`（圆角透明活着过了 `<ApplicationIcon>`）；③ 构建后用仓库根的比对脚本整幅逐像素比：

    dotnet build app\MemoTask.csproj
    .\icon-check.ps1 -Exe publish\MemoTask_self_contained.exe

实测输出（本轮，`icon-check.ps1` 已改成整幅比对 + 读帧表）：

```
ico=AppIcon.ico  bytes=105414  type=1  frames=7
  frame 16x16  DIB  bpp=32  1128 B  @ 118
  frame 24x24  DIB  bpp=32  2440 B  @ 1246
  frame 32x32  DIB  bpp=32  4264 B  @ 3686
  frame 48x48  DIB  bpp=32  9640 B  @ 7950
  frame 64x64  DIB  bpp=32  16936 B  @ 17590
  frame 128x128 DIB bpp=32  67624 B  @ 34526
  frame 256x256 PNG bpp=32  3264 B  @ 102150
exe-icon=32x32  ico@32=32x32
DIFF-PIXELS  0 / 1024
exe corner(0,0).alpha=0  center=FFF7F9FB
PASS 整幅一致（1024 像素全等）
```

结论：1024 个像素全等、圆角 `alpha=0`、中心取到字形色 `#F7F9FB`（`M` 的中缝是笔画）。Debug 产物与两档打包产物都跑过同一条。

- **体积/字节数不是判据**：旧图标是单帧 4,286 B，新的是七帧 105,414 B，而"字节数一样"也完全可能出现在两张不同的图上——只有整幅逐像素比算过。
- 帧表越大 exe 越大，实测：换成七帧 105,414 B 的 ico 后，框架依赖单文件包从 `1,393,834 B` 涨到 `1,696,938 B`（+303,104 B，未压缩档里 ico 会同时进 apphost 与 WPF 资源两处），自带运行时档从 `70,192,945 B` 涨到 `70,298,512 B`（+105,567 B，整包压缩过所以只多一份）。嫌大就 `-Sizes 16,32,48,256` 收窄。
- `ExtractAssociatedIcon` 只回 32×32；其它尺寸要读 ico 帧表（上面那段就是 `icon-check.ps1` 读出来的）。
- 装了 ImageMagick 也可以用 `magick 源.png -define icon:auto-resize=256,64,48,32,16 app\AppIcon.ico` —— **未验证**：本机 `Get-Command magick` 为空，没装。本工程的图标不依赖它。
- 要用外部图（例如 `selfh.st/icons` / `github.com/selfhst/icons` 的 `ico/` 现成多尺寸 ico，CDN 直链 `https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<ref>.ico`）就直接覆盖 `app\AppIcon.ico`，并把来源 URL、图标 ref、许可与是否改过记在本节——那份仓库是 **CC-BY-4.0，要署名**。判可达性要真 GET 拿字节数，别拿 HEAD 状态码当依据。
- 图标文件缺失时构建会失败：先删掉 `<ApplicationIcon>` 与 `<Resource>` 两行，或补一个文件。

---

## 6. 发布 / 分发

发版流程（顺序不能换）：

1. 改 `app\MemoTask.csproj` 的 `<Version>`（当前 `0.1.0`）。
2. **先删空 `publish\` 与 `publish_no_runtime\` 再打包**，否则上一轮 exe 会蒙过第 4 步。
3. 重新打包要交付的那一档（第 3 节两条命令）。
4. **守门比对**：产物的 `FileVersion` 必须等于刚写的版本，否则 `publish\` 里坐的是旧构建。

       Get-Item .\publish\MemoTask_self_contained.exe, .\publish_no_runtime\MemoTask_no_runtime.exe |
         Select-Object Name, Length, LastWriteTime, @{n='FileVersion';e={$_.VersionInfo.FileVersion}}

   实测输出：

   ```
   Name                           Length LastWriteTime        FileVersion
   ----                           ------ -------------        -----------
   MemoTask_self_contained.exe  70298512 2026/10/3 11:45:37   0.1.0.0
   MemoTask_no_runtime.exe       1696938 2026/10/3 11:45:05   0.1.0.0
   ```

   判据：`0.1.0.0`（由 `<Version>0.1.0</Version>` 派生）。**不等就是在交旧构建。**
5. 拷贝清单：单 exe 就是全部（框架依赖档另需目标机有 Desktop Runtime 10.x）。
6. 每个交付包落地后再跑一次 `--selftest`，退出码 0 才算发出去：

       & .\publish\MemoTask_self_contained.exe --selftest; $LASTEXITCODE

   实测：两档各跑一次，退出码都是 `0`，报告 28 行全 `PASS`（全文见第 2 节）。
7. 校验和（有分发目录就顺手给）：

       Get-FileHash .\publish\MemoTask_self_contained.exe, .\publish_no_runtime\MemoTask_no_runtime.exe -Algorithm SHA256

   实测（本轮产物）：

   ```
   MemoTask_self_contained.exe  E153E42599E31F154FCFD535E8AB353F2D74E21D11249575BEC27C5056322903
   MemoTask_no_runtime.exe      019386799E06C3E7359E8341387920F21404931D99F15CBBD1E170A19C349481
   ```

- 归档命名带上档位（`_self_contained` / `_no_runtime`），两档体积差两个数量级（本机 `1,696,938 B` 对 `70,298,512 B`），别说"这个包 1.4 MB"却不标是哪档。
- **字节数一样 ≠ 同一个产物**：修完 `JsonStore` 那一轮重打包，`MemoTask_no_runtime.exe` 前后都是 `1,393,834 B`，但 SHA256 从 `C893986F…` 变成 `B1229C6F…`。判"是不是本轮产物"要用 SHA256、`LastWriteTime` 或断言条数，别用体积（与第 5 节图标那条同源）。
- 目标机排查顺序：`dotnet --list-runtimes` 有没有 WindowsDesktop 10.x → 是不是被杀软拦在首次解包 → `%APPDATA%\MemoTask\selftest.txt` 有没有红 → `%APPDATA%\MemoTask\error.log`（宿主侧未处理异常落这里）。

---

## 7. 注意事项

### 上游与恢复

- **`lib/` 是上游逐文件镜像**（`MagicFollower/WinApp-Skills` 的 `WPF_dotnet10/componentSourceCode/StartUI4Controls`）。少文件或改过文件导致构建异常时，跑 Skill 的 `fetch-source.ps1` 补齐（本地优先、缺口才回源、按 blob sha 校验），别手工去上游粘文件。上游改版后刷新顺序：`-Refresh` → `make-manifest.ps1` → 再 `fetch-source.ps1` 看 `VERIFY ok`。

### 主题通路

- **本地赋值就是退订主题**，这是设计不是 bug。宿主写一个字面色（`Foreground="#333"`）就把 `SetResourceReference` 顶掉了，之后 `ClearValue` 只能回到库内代码的字面默认色。要跟主题走就写 `{DynamicResource UI4.Brush.*}`；代码里现取资源要兜底——`Application.Current.FindResource` 对缺失键**抛异常**，本应用一律用 `TryFindResource`。
- **主题接线的四条硬顺序**（`app\App.xaml.cs` 已按此排好，改动时别打乱）：`UI4Theme.Register/SetTheme` 只能在 `base.OnStartup(e)` 之后、且在 `OnStartup` 内（`Application.Current == null` 时库静默不装字典，症状是宿主 `{DynamicResource}` 全空而库内控件照常好看）；`UI4ThemePacks.RegisterAll()` 必须在 `Apply(套装键)` 之前——**本应用不发放套装，所以压根没调 `RegisterAll()`**，别看见某处 `Apply` 返回 `false` 就往顺序上找；装字典要早于第一个窗口 `Show()`；`--selftest` 分支排在 `OnStartup` 最前面。
- **明暗策略是显式决策**（`app\Helpers\Theme.cs` 的 `Theme.Policy`，取值 `both` / `light-only` / `dark-only`）。留 `TODO` 时 `--selftest` 会红并挡住发布；只做一档也要显式钉住当前档，别把 `SetTheme` 整段删掉（那样库会按 `AppsUseLightTheme` 与系统高对比度自行解析）。本应用取 `both`。
- **别绑静态属性**：`{Binding Path=(ui:UI4Theme.CurrentMode)}` 会停在初值——库发的是 `StaticPropertyChanged`，WPF 普通绑定要找同名 `<属性>Changed` 静态事件。页脚那句「主题 …」就是订阅 `UI4Theme.ThemeChanged` 后手动重发 `Footer` 的（见 `MainViewModel` 构造函数）。
- **`UI4Grid.Background` 会被库覆写**：主题切换时 `UpdateBackground()` 直接 `SetValue` 写回渐变，宿主赋的底色会丢。要固定底色就用普通 `Grid` 或 `UI4Panel`（本应用主窗口用 `UI4Grid` 铺页面渐变，卡片一律 `UI4Panel`）。
- **38 个令牌里没有状态色**（没有 `Danger` / `Warning`），所以删除按钮、逾期/优先级文字色用的是 `app\Helpers\Theme.cs` 里的固定色（`{x:Static h:Theme.Danger}` 等）；这两条色对亮/暗两档底色的对比度由 `--selftest` 钉住（≥3.0）。写 `UI4.Color.Danger` 这种不存在的键**不报编译错**，只表现为画不出东西（本轮实测踩过一次）。
- **`UI4NavigationView` 的三个坑**：`ItemFontSize` 这个 DP 的 `OwnerType` 登记在 `UI4NavigationView` 上，XAML 挂不上，只能 `Nav.SetValue(UI4NavigationViewItem.ItemFontSizeProperty, 12.0)`（库默认 10 px，低于可读下限）；它是 `ItemsControl`，右侧内容靠 `SelectedItem.Content`，页面 Key 用 `UI4NavigationViewItem.Tag`（`notes`/`todos`/`settings`），程序化改 `SelectedItem` 不会触发 `SelectionChanged` 的常规绑定期望，所以 `MainWindow` 用 `DependencyPropertyDescriptor.FromName("SelectedItem", …).AddValueChanged` 才接得住；`ItemText/ItemIcon/…` 那五个颜色 DP 没接令牌（手册 §4.11），本应用在 `MainWindow.xaml` 里逐个写了 `{DynamicResource UI4.Brush.*}` 才跟主题。
- **默认 TwoWay 的绑定点上要写 `Mode=OneWay`**，否则窗口创建期直接抛"无法对只读属性进行 TwoWay 绑定"（本轮实测：`Run.Text` 绑 `StartedText` 让窗口起不来，异常落 `%APPDATA%\MemoTask\error.log`）。同理，`ComboBox` 的 `SelectedValue` 回写要挡 `null`（`ItemsSource` 重建时会把 `null` 写回，把用户选的标签筛选悄悄清掉）；选项对象得覆写 `ToString()` 返回中文 Label，否则闭合态显示类型名。
- **`UI4PasswordBox.Password` 绑定要显式 `Mode=TwoWay`**；`UI4ListView/UI4GridView` 的悬浮放大是按像素预算反算钳过的，越出父容器不是 bug 而是 `HoverScale` 太大被钳住的表象。本应用备忘列表用 `UI4ListView` + `ItemMargin="24"`，正好吃满 8 px 放大预算。
- **`UI4CodeEditor` 的语法高亮不跟主题**（AvalonEdit 由 XSHD 决定），本应用未使用该控件。
- **本应用没有托盘图标**（`UI4NotifyIcon` 未使用）。若将来加，必须在 `OnClosing` 里 `Visibility = Collapsed; Dispose();`，否则托盘残留点不动的死图标（Shell 行为，不是库的 bug）；它的 `MenuActivation` 是从没被读过的死属性。

### 自适应与间距

- **界面自适应当编码期约束**，不是收尾补丁：`MinWidth/MinHeight` 定下限（本应用 720×480）；多栏布局按 `ActualWidth` 分档而不是写死宽度（备忘录页在内容区宽 ≥ 760 时"列表 + 编辑器"并排，窄档改单栏并用「返回列表」切换，见 `NotesViewModel.ShowList/ShowDetail` 与 `MainWindow.UpdatePanes`）；`app.manifest` 的 PerMonitorV2 声明别删（net10 的 WPF 仍按清单取 DPI 级别）；字体族走系统栈（`Segoe UI Variable Text, Segoe UI`），正文不小于 12；窗口尺寸/位置持久化，恢复时做越界回正（`Helpers\Monitors.cs` 用 `MonitorFromPoint` 判三点，判不过就 `ClampToNearestMonitor`；DIP 与物理像素别混在一个单位里比）。
- **间距刻度与外溢余量**：`Margin/Padding` 只用 4 的倍数（4/8/12/16/20/24/32）——同排兄弟 ≥ 8、分组之间 ≥ 16、内容到窗口边缘 ≥ 16；卡片外边距要 ≥ `ShadowDepth + ShadowBlurRadius`（本应用列表卡片 `Margin="24"` 对 `ShadowDepth=12 + ShadowBlurRadius=12 = 24`；`UI4Panel` 默认是 8+5≈13）；`UI4ListView`/`UI4GridView` 的 `ItemMargin` 左右 ≥ 10，不然悬浮放大被 `EdgeReserve=6` 吃光、看起来像控件坏了。别用 `ClipToBounds` 兜溢出，切边正是它的效果。静态自查用仓库根脚本（含 `Views\` 与 `InnerPadding`）：

       .\scan-spacing.ps1

   实测输出（判据：`off_scale_count=0`）：

   ```
   scanned_files=5
     App.xaml
     MainWindow.xaml
     NotesView.xaml
     SettingsView.xaml
     TodosView.xaml
   off_scale_values=[]
   off_scale_count=0
   ```

### 本应用踩过并修掉的两个坑（改同类代码前先看这两条）

1. **`settings.json` 会静默写不出去**（首轮运行必现）。`AppSettings.Window.Left/Top` 默认是 `double.NaN`（含义 = 还没落过位），而 `System.Text.Json` 默认序列化 `NaN` **抛 `ArgumentException`**：

       .NET number values such as positive and negative infinity cannot be written as valid JSON.
       To make it work ... consider specifying 'JsonNumberHandling.AllowNamedFloatingPointLiterals'

   `JsonStore.Save` 把异常吞成 `return false`，于是"改了主题/起始页/自动保存间隔，重启全没了"。修法一行：`JsonStore.Build()` 里加 `NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals`（读写共用同一份 options，所以 `"NaN"` 也能原样读回来）。
   对照实验（本机实测）：去掉那行 → `--selftest` 退 `1`，报告出现 `FAIL  设置往返含未落位坐标`；加回 → 退 `0`、28 行全 `PASS`。该断言用临时文件走真实写盘—读盘，并把 `AutoSaveMs` 设成 `4321` 当标记（读盘任何一步失败都会回落默认 `800`，那条就红），不会被"看上去还是 NaN"糊过去。
2. **本机截屏通路在硬件加速态取不到帧**。症状：窗口标题栏在、`ContentRendered` 正常触发、底色确实取到了深色，但 `CopyFromScreen`、`PrintWindow`、整屏截图**全是白的**；把进程切到软件渲染（`RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly`）后截图立刻正常。判读：这是本机图形/截屏栈的问题，不是应用画错了。
   落成的产品能力：设置页 → 外观 → **渲染档位**（`WPF 默认（硬件加速）` / `软件渲染`，存 `settings.json` 的 `RenderMode`，只在建窗口前生效所以**改完要重启**）。代码默认 `auto`（可移植，不该被本机的毛病拖着走）；本机 `%APPDATA%\MemoTask\settings.json` 现值已设为 `software`。
   **由此得一条验收纪律：颜色类结论要么人眼看窗口，要么先把 `RenderMode` 设成 `software` 再截图；硬件加速态下的白屏截图不算证据。**

### PowerShell 侧

- `.ps1` 里有中文字面量必须存成**带 BOM 的 UTF-8**（PS 5.1 按 ANSI 解码会把中文字节的下一个换行吞掉，两条语句并成一条，报错位置完全看不懂）。本轮实测踩过：一个无 BOM 脚本里的 `Set-Location "…\WPF桌面APP开发\aaa"` 报"找不到路径"，回显里中文已变乱码。仓库根三个脚本（`icon-check.ps1`、`scan-spacing.ps1`、`verify-window.ps1`）都已存为 **BOM + CRLF**。
- 脚本第一句设 `[Console]::OutputEncoding = UTF8`，否则诊断信息在管道里是乱码。
- 带中文的路径别用 `powershell -Command` 内联，走 `-File` + 参数。
- 脚本里别写 `exit`（交互式会话里会被 PowerShell 包装并吞掉真实退出码）。
- `Tee-Object` 在 PS 5.1 只认 `-FilePath`；写成 `-Path`/`-LiteralPath` 会让整条采集链失败，而你可能只看到脚本末尾那句 `DONE`。

### 验证陷阱（这四类都会把"没通过"报成"通过"或反之）

- 拿上一轮产物验本轮改动（第 3 节判据 3；第 6 节"字节数一样"是它的孪生兄弟）。
- 关键命令接管道：`dotnet build | Select-String …` 之后的 `$LASTEXITCODE` 是管道末段的，不是 build 的。要先落文件再判。
- 增量构建的 `0 个警告`（第 1 节）。
- `--selftest` 退出码 `0` **不能证明你跑的是本轮产物**（旧产物也是 0，只是少一条断言）——配合报告行数（本机 28）或 `LastWriteTime`/SHA256 一起判。

### 交付目录清单（收敛口径）

留：`app\`（含 `快速启动.txt`）、`lib\`（源码 + `LICENSE.txt` + `README.md` 组件手册 + 审计报告，都是源不是中间物）、`doc\`、根上 `publish.cmd` / `publish_no_runtime.cmd` / `icon-check.ps1` / `scan-spacing.ps1` / `verify-window.ps1`。
删：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`、验收产生的截图/日志、非本轮的 `publish*\*.exe`。
两条判据（本机都过）：删掉 `bin\`/`obj\` 后 `publish\MemoTask_self_contained.exe --selftest` 照样退 `0`（单文件包自含全部依赖）；`dotnet build app\MemoTask.csproj` 从零产出、两档 publish 再跑全绿，警告仍是上游那 9 条。

### 未验证项汇总

| 项 | 在哪节 | 原因 |
|---|---|---|
| `magick 源.png -define icon:auto-resize=…` 生成多尺寸 ico（§5 里是**可选**路径，本工程图标由 `make-icon.ps1` 本地生成，不依赖它） | §5 | 本机 `Get-Command magick` 为空，没装 ImageMagick |
| 干净机器上 `publish.cmd` **首次**拉运行时包（约 150 MB）的耗时 | §3 | 本机 NuGet 缓存已有 `Microsoft.WindowsDesktop.App` 10.0.12 运行时包，测不出那段下载 |
| 未装 Desktop Runtime 10.x 的机器上跑框架依赖档的表现 | §3 / §6 | 本机装了，无法原地复现 |
| Win10（LTSC/企业版）上的标题栏三色与 DPI 表现 | §4 | 本机只有 Win11 `10.0.26300.0`；库在 Win10 只认深/浅标志 |
| 真·多显示器（外接屏插拔）下的窗口回正 | §7 | 本机单屏；`Monitors` 的判定只用 `--selftest` 的屏内/离屏两组坐标验过 |
