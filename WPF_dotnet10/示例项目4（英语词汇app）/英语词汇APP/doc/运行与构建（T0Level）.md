# VocabDesk 零基础上手（T0Level）

> **这份文档的规矩**（填的人读完这段就可以删掉这段）
> 1. 面向"从没碰过这个工程的人"：每条命令都给**判据**（跑完该看到什么、看不到什么算坏）。
> 2. 每条 powershell 命令必须在本机跑过，并把**真实输出**贴进"实测输出"代码块；不贴想象中的输出。
> 3. 跑不了或没跑的行，显式写 `未验证`＋原因（缺工具／缺权限／离线机器），不要留空，也不要照抄模板里的示例数字。
> 4. 模板里带 `【模板】` 标记的都是待填位，交付前必须清零。
> 5. 环境相关数字（耗时、体积、SDK 版本）标清出处与口径（Debug/Release、单机、是否首次运行），别当常量引用。

- 工程根：`【模板】<绝对路径>`
- 本次填写人／日期：`【模板】`
- 实测环境：`【模板】OS 版本 + dotnet SDK 版本`

---

## 0. 前置要求

| 依赖 | 要求 | 判据命令 | 本机实测 |
|---|---|---|---|
| .NET SDK | 10.x（`net10.0-windows`） | `dotnet --list-sdks` | `【模板】` |
| WPF 桌面运行时 | `Microsoft.WindowsDesktop.App` 10.x（框架依赖档必需） | `dotnet --list-runtimes \| Select-String WindowsDesktop` | `【模板】` |
| NuGet 源可达 | lib 唯一第三方依赖 `AvalonEdit 6.3.1.120` 要能 restore | `dotnet build app\VocabDesk.csproj` 首行无 NU 错误 | `【模板】` |
| Windows 版本 | Win10（LTSC/企业版）或 Win11；Win7/8.1 不支持（net10 的 OS 底线） | `systeminfo \| Select-String OS` | `【模板】` |

不该进版本控制的：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`、`publish\`、`publish_no_runtime\`、`%APPDATA%\VocabDesk\`（运行期设置与 selftest 报告）。

`lib/` 是组件库源码整份拷贝，**不要改它**——改了就和上游脱钩（理由与恢复办法见第 7 节）。

---

## 1. 项目启动（首次构建）

    dotnet build app\VocabDesk.csproj

判据：末尾 `已成功生成` + `0 个错误`；警告**固定 9 条**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），都来自 `lib/` 上游代码。

- 别去"修"那 9 条：修完 `lib/` 就与上游不可比（第 7 节）。
- 警告数**多于或少于** 9 条 → 先看 `lib/` 是不是被改过或文件不全，再怀疑工具链。
- 增量构建会出现 `0 个警告`（只重编 app、没重编 lib），别把它当"警告消失了"；要数警告就 `dotnet build --no-incremental` 或先 `dotnet clean`。
- `dotnet pack` 缺 `lib/LICENSE.txt` 会报 `NU5019` 并把整条构建链拖红——`lib/` 少文件时的典型表现，恢复办法见 Skill 的 `fetch-source.ps1`。

实测输出（首次构建）：

```
【模板】贴完整输出，至少含 restore 行 + 末尾的成功/警告/错误计数
```

---

## 2. 本地调试

    dotnet run --project app\VocabDesk.csproj

判据：窗口出现，标题为第 4 节里 `Title="…"` 的值。点右上角齿轮开设置浮层（Esc / 点遮罩 / 「完成」都能关）：「配色」里每一档逐个点都要改观感，含**原生标题栏**跟着染；底部状态行的"生效键"从 `light` 变到 `dark` / `paper-grey`；「正文字号」滑杆要带动整窗（含胶囊与圆钮的尺寸），「全局缩放」在 150% / 200% 下内容不裁切、窗口拖不到比折算后的下限更小。**只有局部在变 = 那处写了字面色或字面字号**（本地赋值即退订，属设计）。已知边界：下拉浮层与右键菜单不吃 `LayoutTransform`，缩放档下弹层内容仍按 100% 渲染。

设置落盘在 `%APPDATA%\VocabDesk\settings.json`（kv1 纯文本，可自己开编辑器改，越界值会被钳回区间）；默认值与允许区间只有一处来源 `app\Helpers\Typography.cs`。

无人值守自证（不开窗、退出码 = 失败断言数）：

    app\bin\Debug\net10.0-windows\VocabDesk.exe --selftest
    $LASTEXITCODE
    Get-Content "$env:APPDATA\VocabDesk\selftest.txt"

判据：退出码 `0`；报告逐行 `PASS`。这条比人眼看窗口更硬，因为它同时钉住了令牌数、三份内置定义可取色、宿主配色对比度、`Mix`/`IsDark` 判据、明暗策略已显式决策、8 套预置的资源键齐全，外加排印与缩放一段（库有没有发布 `UI4.Font.*` 兜底值、宿主覆盖换档后还在、字号层级与固定件尺寸、区间钳位、字体候选表首位、缩放转换器边界、以及**主窗口与设置面板两块 XAML 能否构造**）。

- `dotnet run` 会占住终端（GUI 进程不退出），自动化里用 `Start-Process -PassThru` 起 exe、按 pid 读 `MainWindowTitle`，验完 `Stop-Process -Id` **只关自己起的那个 pid**。
- 带空格/中文的路径要用引号包住再传给 `Start-Process -FilePath`，否则报"找不到模块 'C:\Program'"。

实测输出：

```
【模板】dotnet run 的启动耗时 + selftest 退出码 + 报告全文
```

---

## 3. 打包与启动

两个脚本，产物名靠 `-p:ArtifactLabel=` 分档、互不覆盖。**脚本末尾有 `pause`**，交互式双击用；无人值守要喂一行空输入让它过去：

    '' | .\publish_no_runtime.cmd
    '' | .\publish.cmd

| 档位 | 产物 | 目标机要求 | 本机实测 |
|---|---|---|---|
| 框架依赖 | `publish_no_runtime\VocabDesk_no_runtime.exe` | 必须已装 `Microsoft.WindowsDesktop.App` 10.x | `【模板】体积 + 耗时` |
| 自带运行时 | `publish\VocabDesk_self_contained.exe` | 免装 .NET | `【模板】体积 + 耗时；首次跑要多等运行时包下载（约 150 MB）` |

判据（两档都要满足）：

1. 脚本内第 2 步会自己跑 `--selftest`，打出 `selftest exit code: 0`；非 0 时它提示看 `%APPDATA%\VocabDesk\selftest.txt`。
2. 产物目录里**只有那一个 exe**（`DebugType=embedded` 是全局属性，`lib` 也不会留散落 pdb）。
3. 起打包产物并读回窗口标题——**必须用本轮重新打包的 exe**：

       $p = Start-Process -FilePath .\publish\VocabDesk_self_contained.exe -PassThru
       Start-Sleep -Seconds 6
       $p.Refresh(); $p.MainWindowTitle
       Stop-Process -Id $p.Id -Force

   判据：标题等于本轮源码里的 `Title`。拿上一轮的 exe 验本轮改动会假红（实测踩过：改完标题拿旧产物匹配，得出"打包版不出窗"的错误结论）。
4. 自带运行时档**首次启动**要把原生库解到 `%TEMP%\.net\VocabDesk_self_contained\`，会比后续启动慢；记下两次的耗时再下结论。

实测输出：

```
【模板】两档各自的耗时、exe 字节数、selftest exit code、MainWindowTitle 回读值
```

---

## 4. 修改标题

真源只有一处：`app\MainWindow.xaml` 的 `Title="…"`（同时是任务栏与 DWM 标题栏文字）。

    Select-String -Path app\MainWindow.xaml -Pattern 'Title="'

判据：改完重新构建并起进程，`MainWindowTitle` 回读为新值。

- **不要自绘标题栏**：窗口里出现过 UI4 控件，库会在 `Loaded` 时按当前主题把原生标题栏染好（Win10 只认深/浅标志，Win11 才染底色/文字/描边三色，是系统能力差异不是 bug）。`WindowStyle=None` + `AllowsTransparency=true` 会绕过整套染色通路。
- 完全不含 UI4 控件的窗口要手动 `UI4WindowTitleBar.Apply(win)`，否则它等到下次主题切换才被扫到。
- `UI4MessageBox` / `UI4ColorPicker` 是自绘标题区，对它们调 `Apply` 无可见效果。

实测输出：

```
【模板】Select-String 命中行 + 回读到的 MainWindowTitle
```

---

## 5. 修改图标

一个文件三处引用：`app\AppIcon.ico` → csproj 的 `<ApplicationIcon>`（exe 内嵌图标）＋ `<Resource>`（窗口/任务栏图标，`Window.Icon="AppIcon.ico"` 走它）。覆盖文件即可，不用改 csproj。

**默认图标是本地生成的字母像素图标**（不联网、不要 ImageMagick）：`make-icon.ps1` 在 16×16 逻辑网格上画应用名首字母的 5×7 点阵（底色 `#4F6BE8`、字形 `#F7F9FB`、四角削成圆角），一次写全 16/24/32/48/64/128/256 七帧 32bpp ICO。换字母、配色或尺寸集，重跑一次覆盖回去即可：

    powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>\scripts\make-icon.ps1" `
      -Name VocabDesk -Out app\AppIcon.ico -Back "#4F6BE8" -Fore "#F7F9FB"

判据三条，缺一不可：

1. 生成器自己的输出末尾是 `OK AppIcon.ico letter=<首字母> … frames=7`。它内部已断言"目录条目连续无缝 + 末帧正好落到文件尾 + 每帧字节数等于算式值"，任一条不过就抛错退出，不会留下半截 ico。
2. 同一段里的 `probe 16 -> …` / `probe 32 -> …` 两行要报 `corner.alpha=0`——证明圆角透明能活着过 `<ApplicationIcon>` 这条烤进 exe 的通路。
3. 构建后取回内嵌位图逐像素比：

       dotnet build app\VocabDesk.csproj
       Add-Type -AssemblyName System.Drawing
       $ic = [System.Drawing.Icon]::ExtractAssociatedIcon("app\bin\Debug\net10.0-windows\VocabDesk.exe")
       $bmp = $ic.ToBitmap()
       $bmp.Width; $bmp.Height
       $bmp.GetPixel(0, 0).A     # 圆角，应为 0
       $bmp.GetPixel(2, 16)      # 左边缘，应为底色
       $bmp.GetPixel(16, 16)     # 中心，字形色或底色取决于字母

实测教训——换掉 `AppIcon.ico` 后文件字节数完全可以一样（4286 B 对 4286 B，同尺寸同深度），体积判据会漏判；像素点命中才算过。

- 要改用外部图（例如 `selfh.st/icons` / `github.com/selfhst/icons` 的 `ico/` 现成多尺寸 ico）就直接覆盖 `app\AppIcon.ico`，并把来源 URL、图标 ref、许可与"是否改过"记进本节——那份仓库是 **CC-BY-4.0，要署名**。取源用 CDN 直链 `https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<ref>.ico`；判可达性要真 GET 拿到字节数，别拿 HEAD 的状态码当依据。
- `ExtractAssociatedIcon` 只回 32×32；要验其它尺寸得读 ico 的帧表（`make-icon.ps1` 的自证读的就是帧表）。
- 装了 ImageMagick 也可以用 `magick 源.png -define icon:auto-resize=256,64,48,32,16 app\AppIcon.ico`；没装就显式标 `未验证` + 原因（本机 `Get-Command magick` 为空），别照抄成已验证。
- 图标文件缺失时构建会失败：先删掉 `<ApplicationIcon>` 与 `<Resource>` 两行，或补一个文件。

实测输出：

```
【模板】make-icon.ps1 的 probe/frame/OK 行 + ExtractAssociatedIcon 回读的 alpha 与取色
```

---

## 6. 发布 / 分发

发版流程（顺序不能换）：

1. 改 `app\VocabDesk.csproj` 的 `<Version>`（当前 `0.1.0`）。
2. 重新打包要交付的那一档。
3. **守门比对**：产物的 `FileVersion` 必须等于刚写的版本，否则 `publish\` 里坐的是旧构建。

       (Get-Item .\publish\VocabDesk_self_contained.exe).VersionInfo.FileVersion

   判据：`0.1.0.0`（`<Version>0.1.0</Version>` 派生）。**先删空 `publish\` 与 `publish_no_runtime\` 再打包**，否则上一轮 exe 会蒙过这一步。
4. 拷贝清单：单 exe 就是全部（框架依赖档另需目标机有 Desktop Runtime 10.x）。
5. 每个交付包落地后再跑一次 `--selftest`，退出码 0 才算发出去：

       & .\publish\VocabDesk_self_contained.exe --selftest; $LASTEXITCODE

- 校验和（有分发目录才需要）：`Get-FileHash .\publish\VocabDesk_self_contained.exe -Algorithm SHA256`，把 `.sha256` 与 exe 同目录放。
- 归档命名带上档位（`_self_contained` / `_no_runtime`），两档体积差两个数量级（`【模板】实测两档字节数`），别说"这个包 1.4 MB"却不标是哪档。
- 目标机排查顺序：`dotnet --list-runtimes` 有没有 WindowsDesktop 10.x → 是不是被杀软拦在首次解包 → `%APPDATA%\VocabDesk\selftest.txt` 有没有红。

实测输出：

```
【模板】FileVersion 比对结果 + 两档 exe 体积 + 交付后 selftest 退出码
```

---

## 7. 注意事项

- **`lib/` 是上游逐文件镜像**（`MagicFollower/WinApp-Skills` 的 `WPF_dotnet10/componentSourceCode/StartUI4Controls`）。少文件或改过文件导致构建异常时，跑 Skill 的 `fetch-source.ps1` 补齐（本地优先、缺口才回源、按 blob sha 校验），别手工去上游粘文件。上游改版后刷新顺序：`-Refresh` → `make-manifest.ps1` → 再 `fetch-source.ps1` 看 `VERIFY ok`。
- **本地赋值就是退订主题**，这是设计不是 bug。宿主写一个字面色（`Foreground="#333"`）就把 `SetResourceReference` 顶掉了，之后 `ClearValue` 只能回到库内代码的字面默认色。要跟主题走就写 `{DynamicResource UI4.Brush.*}`；代码里现取资源要兜底，`Application.Current.FindResource` 对缺失键**抛异常**。
- **主题接线的四条硬顺序**：`UI4Theme.Register/SetTheme` 只能在 `base.OnStartup(e)` 之后、且在 `OnStartup` 内（`Application.Current == null` 时库静默不装字典，症状是宿主 `{DynamicResource}` 全空而库内控件照常好看）；`UI4ThemePacks.RegisterAll()` 在 `Apply(套装键)` 之前（顺序错时 `Apply` 只返回 `false`，不抛异常）；装字典要早于第一个窗口 `Show()`；`--selftest` 分支排在 `OnStartup` 最前面。
- **明暗策略是显式决策**（`app\Helpers\Theme.cs` 的 `Theme.Policy`，取值 `both` / `light-only` / `dark-only`）。留 `TODO` 时 `--selftest` 会红并挡住发布；只做一档也要显式钉住当前档，别把 `SetTheme` 整段删掉。这条是有意决策，要写进本节上方的"实测环境"里，不是遗漏。
- **界面自适应当编码期约束**，不是收尾补丁：`MinWidth/MinHeight` 定下限（模板 720×480）；多栏布局按 `ActualWidth` 分档而不是写死宽度；`app.manifest` 的 PerMonitorV2 声明别删（net10 的 WPF 仍按清单取 DPI 级别）；字体族走系统栈（`Segoe UI Variable Text, Segoe UI`）别硬编码像素字号到不可读；窗口尺寸/位置若持久化，恢复时要做越界回正（拔外接屏后窗口跑回屏幕外的坐标）。
- **间距刻度与外溢余量**：`Margin/Padding` 只用 4 的倍数（4/8/12/16/20/24/32）——同排兄弟 ≥ 8、分组之间 ≥ 16、内容到窗口边缘 ≥ 16；卡片外边距要 ≥ `ShadowDepth + ShadowBlurRadius`（`UI4Panel` 默认 8+5≈13，列表/网格卡片常用 15+12≈27），否则投影四边被切平；`UI4ListView`/`UI4GridView` 的 `ItemMargin` 左右 ≥ 10，不然悬浮放大被 `EdgeReserve=6` 吃光、看起来像控件坏了。别用 `ClipToBounds` 兜溢出，切边正是它的效果。静态自查：

       Select-String -Path app\*.xaml -Pattern '(Margin|Padding)="([0-9,\s]*)"' -AllMatches |
         ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[2].Value -split '[,\s]+' } |
         Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } |
         Where-Object { $_ -ne 0 -and ($_ % 4) -ne 0 } | Sort-Object -Unique

  判据：输出为空。实测输出：`【模板】`
- **别绑静态属性**：`{Binding Path=(ui:UI4Theme.CurrentMode)}` 会停在初值——库发的是 `StaticPropertyChanged`，WPF 普通绑定要找同名 `<属性>Changed` 静态事件。要在界面上显示当前主题就订阅 `UI4Theme.ThemeChanged`，读 `UI4Theme.ResolvedKey` 与 `UI4ThemePacks.DisplayLabel(...)`。
- **`UI4Grid.Background` 会被库覆写**：主题切换时 `UpdateBackground()` 直接 `SetValue` 写回渐变，宿主赋的底色会丢。要固定底色就用普通 `Grid` 或 `UI4Panel`。
- **托盘 `UI4NotifyIcon` 必须在 `OnClosing` 里 `Visibility = Collapsed; Dispose();`**，否则托盘残留点不动的死图标（Shell 行为）。它的 `MenuActivation` 是从没被读过的死属性，菜单只跟随全局主题。
- **`UI4PasswordBox.Password` 绑定要显式 `Mode=TwoWay`**；`UI4ListView/UI4GridView` 的悬浮放大是按像素预算反算钳过的，越出父容器不是 bug 而是 `HoverScale` 太大被钳住的表象。
- **`UI4CodeEditor` 的语法高亮不跟主题**（AvalonEdit 由 XSHD 决定），库只染外壳底色与前景。
- **PowerShell 侧**：`.ps1` 里有中文字面量必须存成带 BOM 的 UTF-8（PS 5.1 按 ANSI 解码会把中文字节的下一个换行吞掉，两条语句并成一条，报错位置完全看不懂）；脚本第一句设 `[Console]::OutputEncoding = UTF8`；带中文的路径别用 `powershell -Command` 内联，走 `-File` + 参数。
- **验证陷阱**：拿上一轮产物验本轮改动（第 3 节判据 3）、`关键命令接管道`（`build | grep` 的退出码是 grep 的）、增量构建的 `0 个警告`（第 1 节）——这三条都会把"没通过"报成"通过"或反之。
- 未验证项汇总：`【模板】逐条列出本文标了未验证的行与原因`。
