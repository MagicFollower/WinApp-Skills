---
name: startui4-wpf
description: 用 StartUI4Controls（StartUI4.WPF v3.0.0，net10.0-windows 纯 C# 模板 WPF 组件库）搭工程、改界面与发版本的可复现流程——app/+lib/ 源码自包含骨架（lib 种子本地优先，缺失或不完整时按 manifest 从上游 WinApp-Skills 仓库的 componentSourceCode 自动回源并写回）、UI4Theme 38 令牌与 UI4.Brush.*/UI4.Color.* 资源键契约、开工前必问的三挡决策（明暗策略/两档分发口径/要不要套装与 UI4ThemeScope 局部换肤）、启动顺序（base.OnStartup 之后才 Register/SetTheme）、DWM 标题栏、UI4MessageBox/UI4ColorPicker/UI4NotifyIcon/UI4CodeEditor/UI4GridView 用法、界面自适应当编码期约束、间距刻度与阴影/悬浮外溢余量（组件之间不贴边、投影不被切平）、单文件两档发布（self_contained / no_runtime）与 --selftest 门禁、FileVersion 守门、应用图标默认本地生成（make-icon.ps1 出字母像素多尺寸 ico，不联网不依赖 ImageMagick）、交付 doc/运行与构建（T0Level）.md 八节（每条命令本机跑过贴真实输出，没跑过标未验证）与交付前目录收敛。当需要新建 .NET 10 WPF 桌面应用、要写或改 UI4* 控件的 XAML/C#、切主题后某处颜色不动、{DynamicResource UI4.*} 解析不到、找不到 StartUI4Controls 的类型或属性、组件源码目录是空目录或缺文件、UI4 控件在深色下对比度不对、要打包 WPF 单 exe、要补零基础上手文档或把既有 StartUI4 工程按示例项目结构收敛时使用。不用于 WinForms、UWP/WinUI、.NET Framework 48 版 StartUI4，也不用于不含该库的普通 WPF 页面。
---

# StartUI4.WPF 桌面应用搭建与组件使用

## Overview

StartUI4Controls 是**零 XAML** 的 WPF 组件库：39 个 `UI4*` 控件的模板全由 C# 构建，主题是一张 `UI4ThemeToken → Color` 的 38 令牌表。这带来两个反直觉点，是本 Skill 的主要价值：

1. **不需要挂资源字典就有完整外观**——宿主直接 `<ui:UI4Button/>` 即可；但也因此**没有任何编译期约束**保护资源键与属性名，写错键名不报错、不抛异常，只表现为"这处颜色没跟主题"。
2. **本地赋值就是退订主题**，这是设计。`SetResourceReference` 占的也是本地值槽，宿主写一个字面色就把引用顶掉，之后 `ClearValue` 只能回到库内代码的字面默认色。

所以顺序是：先问定三挡决策（开工前必须问用户），把工程与令牌接通（Step 1–2），再选控件写界面（Step 3–5），把自适应当编码期约束一次到位（Step 7），最后用发布与自测（Step 6）、上手文档（Step 8）、目录收敛（Step 9）、验收清单（Step 10）把产物钉住。

## 何时使用

- 新建 .NET 10 WPF 桌面应用，要用这套组件（含"照示例项目结构再来一个"）。
- 已有工程里要写/改 `UI4*` 控件的 XAML 或 C#，需要准确的属性名、枚举、事件签名。
- 切主题后部分界面没变、`{DynamicResource UI4.Brush.X}` 拿到空、套装键 `Apply` 无反应。
- 自定义主题运行期崩在 `GetColor`（`KeyNotFoundException`）。
- 深色/高对比度下某控件对比度不对（黑字压黑底）。
- 要打包成单 exe（免装运行时 / 依赖共享运行时两档），或要把 `--selftest` 接进发布门禁。
- 托盘图标、原生标题栏染色、代码编辑器、右键菜单这类系统集成面。
- 组件源码目录是空目录或缺文件（`assets/seed/lib`、或既有工程的 `lib/`）——跑 `fetch-source.ps1` 按清单补齐，别手工从上游粘文件。
- 要写交付件 `doc/运行与构建（T0Level）.md`（八节、每条命令贴本机真实输出），或要把工程目录收敛到"删干净仍能一把重建"的最小清单（Step 9）。

## 目录布局（新建工程先定）

```text
<proj>/
├── app/                      应用本体
│   ├── App.xaml  App.xaml.cs          启动序列 + 宿主配色注册（顺序承重，见 Step 2）
│   ├── MainWindow.xaml  .xaml.cs
│   ├── <AppName>.csproj               ProjectReference ../lib/StartUI4Controls.csproj
│   ├── app.manifest                   PerMonitorV2 DPI
│   ├── AppIcon.ico                    ApplicationIcon + Resource 两处都指它（默认由 scripts/make-icon.ps1 本地生成）
│   ├── Models/ ViewModels/ Views/ Services/ Helpers/ Converters/
│   ├── Helpers/Theme.cs               宿主配色单源（常量）
│   ├── Services/SelfTest.cs           --selftest，退出码 = 失败断言数
│   └── 快速启动.txt                    build / run / 两档 publish / selftest 四条命令带判据
├── lib/                      组件源码整份自包含（44 个 .cs + csproj + AssemblyInfo + LICENSE.txt + README.md 组件手册 + 架构审计报告）
├── publish.cmd               单文件 + 自带运行时
└── publish_no_runtime.cmd    单文件 + 框架依赖
```

`lib/` 用**源码工程引用**而不是 dll 或 NuGet：模板改动能就地重编，交付包能独立 clone 重建。`LICENSE.txt` 不能删——csproj 里 `<None Include="./LICENSE.txt" Pack="true">`，缺文件时 `dotnet pack` 报 NU5019 并把整条构建链拖红。

## Step 0 — 先探测，再动手

```bash
dotnet --list-sdks          # 要有 10.x（实测 10.0.401）
dotnet --list-runtimes | grep WindowsDesktop   # 框架依赖发布要 10.x 在位
```

一、`dotnet build` 会 restore `AvalonEdit 6.3.1.120`（lib 唯一第三方依赖），所以要确认 NuGet 源可达；离线机器上先在本机 `dotnet restore` 一次成功再断网。

二、**组件源码从哪来**（种子目录可能是空的，别一上来就当它存在）。权威来源是
`https://github.com/MagicFollower/WinApp-Skills/tree/main/WPF_dotnet10/componentSourceCode/StartUI4Controls`，
main = commit `5d96442a`，共 48 个文件；`assets/seed/lib` 就是它的逐文件忠实镜像（48/48 blob sha 已实测相同），
`assets/seed/manifest.json` 钉死了这 48 条 `path + blobSha + size` 与内容计数基线（44 个 `.cs` / 38 个令牌 / 235 条 DP 声明）。
`scripts/fetch-source.ps1` 按三挡办事：

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1"              # 默认：本地命中零联网，有缺口才回源并写回种子
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1" -Offline     # 禁网：有缺口就逐名列出后失败（退 4）
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1" -Refresh     # 上游改版：强制重新取源覆盖种子
```

取源通路 A（tar.gz + 系统 tar.exe，只解那个子目录）→ B（同 A 但 URL 前加 `-Mirror` 反代前缀）→ C（git sparse clone）→ D（清单里剩下的个别文件走 raw 单文件）。
**判可达性要用真正下载的 URL 做 GET，别拿 HEAD 的状态码当依据**（这台机器实测过 HEAD 200 而 GET 超时）。退出码：0 成功 / 2 参数或 Skill 目录不完整 / 3 三条通路全败 / 4 `-Offline` 有缺口 / 5 取回后校验不过。

三、种子版本要对得上。`StartUI4Controls.csproj` 里 `Version=3.0.0`（上游 `MagicFollower/StartUI4.WPF` 分支 `dotnet_10` commit `269189e`）。
在既有工程里干活前先看它的 `lib/StartUI4Controls.csproj` 版本号，2.x 与 3.x 的主题机制不同（2.0.0 的 `IThemeAware`/`TrackControl` 在 3.0.0 已整体删除），别把旧写法抄进去。

四、PowerShell 侧两条硬约束（本 Skill 的脚本与任何自写校验脚本都适用）：`.ps1` 里有中文字面量必须存成**带 BOM 的 UTF-8**，否则 PS 5.1 按 ANSI 解析直接语法错（更阴的是 ANSI 解码会把中文字节的下一个换行吞掉，两条语句并成一条）；脚本第一句设 `[Console]::OutputEncoding = UTF8`，否则诊断信息在管道里是乱码。带中文的路径别用 `powershell -Command` 内联传（字节会被码页吃掉），走 `-File` + 参数。

## 开工前必须问用户（三挡各三选一，不要自己拍）

模板把这三个决策钉成了机器可查的口子（`Theme.Policy` 留 `TODO` 时 `--selftest` 直接红、发布脚本挡下），所以先问再写：

1. **明暗策略**：`both`（亮+暗，默认跟随系统并给切换按钮）/ `light-only` / `dark-only`。答案决定要不要注册第二套定义、`App` 里 `SetTheme` 钉哪一档、要不要「跟随系统」按钮。**只做一档也要显式钉住当前档**，别把 `SetTheme` 整段删掉——那样库会按 `AppsUseLightTheme` 与系统高对比度自行解析，用户系统一改，你的"单档"跟着变。
2. **分发口径**：只出免装运行时的单 exe / 只出小的框架依赖包 / 两档都出。两档体积差两个数量级（实测 1.4 MB vs 70 MB），先问清目标机有没有 `Microsoft.WindowsDesktop.App` 10.x。
3. **要不要套装键与局部换肤**：只用内置 `light/dark/highcontrast`，还是要 `UI4ThemePacks` 的 8 套业务档、或 `UI4ThemeScope` 的局部作用域。这决定 `App` 启动序列里 `RegisterAll()` 与 `Apply(套装键)` 的顺序（顺序错时 `Apply` 只返回 `false`、界面纹丝不动）。

答案原样写进 `doc/运行与构建（T0Level）.md`（第 7 节注意事项上方那段"本次填写人／实测环境"），作为**有意决策**记录，不是遗漏。用户不在场且必须继续时：按 `both` + 两档都出 + 只用内置档推进，并在文档里显式标「此三挡未经确认」。

## Step 1 — 一把生成骨架

```bash
powershell -NoProfile -ExecutionPolicy Bypass \
  -File "<skill>/scripts/scaffold.ps1" -Name MyNote -Path "D:\work\MyNote"
```

`-Name` 会同时当 `RootNamespace` / `AssemblyName` / 数据目录字面量，要求字母开头、仅字母数字。产物是上面那棵树 + `app/AppIcon.ico`——**默认本地生成，不联网**：`scripts/make-icon.ps1` 用应用名首字母画一张像素点阵图标（16×16 逻辑网格上的 5×7 点阵字母，底色 `#4F6BE8`、字形 `#F7F9FB`，四角削成圆角），一次写出 16/24/32/48/64/128/256 七档 32bpp 帧（256 帧内嵌 PNG）。
种子完整时全程不联网；种子缺失/不完整时它会先调 `fetch-source.ps1` 回源补齐再建工程（离线机器/CI 上想禁止联网就加 `-Offline`，直连抖动时加 `-Mirror <反代前缀>`）。

判据：stdout 首行 `OK`，并回报 `lib/ 48 个文件`、`占位符替换 13 个文件`、图标那一段 `OK AppIcon.ico letter=<首字母> ... frames=7`；走过回源会先打 `SEED 已补齐并写回，下次零联网`。非 0 退出码含义：2 名字非法、3 Skill 目录不完整、4 种子有缺口（`-Offline` 下）或回源后仍不完整、5 目标已有 C# 源码（要 `-Force`）、6 产物缺文件、7 占位符没替换干净、8 图标生成失败（`make-icon.ps1` 的自证没过）；回源自身的失败码（2/3/4/5）在它自己的输出里，全败时会把三条通路的失败原因都打出来。

生成后立刻编译一次，把 restore 与工具链问题在写业务代码前清掉：

```bash
dotnet build "<proj>/app/<AppName>.csproj"
```

**9 条警告是上游 lib 自带的**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），`0 个错误`就是过。别去"修"它们，那会把种子改得跟上游不可比。

手工加控件文件时注意：`Copy-Item -LiteralPath <目录> -Destination <已存在的目录> -Recurse` 会套出一层同名子目录，逐文件按相对路径拷才对。

## Step 2 — 主题接线（最容易静默失效的一段）

`App.xaml.cs` 的骨架已经在模板里，四条约束不要改：

1. `UI4Theme.Register` / `SetTheme` / `ApplyToApplication` **只能在 `base.OnStartup(e)` 之后、且在 `OnStartup` 内**调。库内写回资源字典的第一句是 `Application.Current == null` 就 return，在构造函数或 `Main` 里调**静默不装资源**，症状是宿主 `{DynamicResource UI4.*}` 全空而库内控件照常好看。
2. `UI4ThemePacks.RegisterAll()` 在 `UI4Theme.Apply(套装键)` **之前**。顺序错时 `Apply` 只返回 `false`、不抛异常，界面纹丝不动。
3. 装字典要早于第一个窗口 `Show()`，否则首帧 `DynamicResource` 回落默认色。宿主自己 `Register` 了当前键的定义时，`SetTheme` 已隐式装好；两种都不调 = 只有库内控件有样式。
4. `--selftest` 分支排在 `OnStartup` 最前面（不开窗、不读设置、不挂 UI 异常钩子），退出码即失败断言数。

资源键规则（`theming.md` 有全表）：令牌枚举名**逐字**决定 `UI4.Color.<名>` 与 `UI4.Brush.<名>`，另有 `UI4.Brush.Text` / `UI4.Brush.Border` 两个真别名。写错不报编译错，只表现为不跟随。

宿主配色走单源派生，别散着写：`app/Helpers/Theme.cs` 存常量，`RegisterAppTheme()` 从 `UI4ThemeDefinition.Light()` 起步（**天然覆盖 38 个令牌**）再 `With(...)` 覆盖想改的，最后 `UI4Theme.Register(def)`。`new UI4ThemeDefinition("key")` 只填几个令牌 = 运行期崩在取色上。覆盖"当前正在用的键"会被库就地整体重应用，不需要先切走再切回。

判深浅按底色亮度，别读 `ResolvedMode`（自定义键与 8 套一律报 `Light`）：

```csharp
Color bg = UI4Theme.Current.ColorOf(UI4ThemeToken.Background);
bool isDark = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B < 128;   // 与 UI4WindowTitleBar 同判据
```

**别绑静态属性**：`{Binding Path=(ui:UI4Theme.CurrentMode)}` 会停在初值——库发的是 `StaticPropertyChanged`，WPF 绑静态 CLR 属性找的是同名 `<属性>Changed` 静态事件。要在界面上显示当前主题就订阅 `ThemeChanged`，读 `UI4Theme.ResolvedKey` 与 `UI4ThemePacks.DisplayLabel(...)`。

## Step 3 — 选控件与写 XAML

命名空间固定：`xmlns:ui="clr-namespace:StartUI4Controls;assembly=StartUI4Controls"`。

先看 `controls.md` 的选型表（39 个控件 + 基类 + 一句用途 + 坑）。三个高频误判：

- `UI4TextBlock`/`UI4Panel`/`UI4ListView` **不是**同名原生类的子类（`ContentControl`/`ContentControl`/`ListBox`），靠名字猜基类会写错 Style/Trigger。
- `UI4GridView.ItemWidth` 是**算列数的基准单元**，不是卡片宽度；卡片铺满所在列。
- `UI4Grid.Background` 会被库的 `UpdateBackground()` 直接 `SetValue` 写回渐变，宿主赋的底色在主题切换时丢失。

主窗口骨架（实测可编译可跑）：`ui:UI4Grid` 铺页面渐变 → 内部普通 `Grid` 分行 → 工具条 `ui:UI4Button` + 内容 `ui:UI4Panel`。**不要**自绘标题栏、不要 `WindowStyle=None` + `AllowsTransparency=true`：窗口里出现过 UI4 控件，库就在 `Loaded` 时按 DWM 把原生标题栏染好。完全不含 UI4 控件的窗口要手动 `UI4WindowTitleBar.Apply(win)`，否则它等到下次主题切换才被扫到。

宿主自己的 `TextBlock`/`Border`/`Grid` 与 UI4 控件混用是合法的，只要颜色写 `{DynamicResource UI4.Brush.*}`。代码里现取资源要兜底：`Application.Current.FindResource("UI4.Brush.Surface")` 对缺失键**抛异常**，写 `(Brush)Current.FindResource(...) ?? Brushes.White`。

### 间距与留白（写布局时就定，别等收尾补）

拥挤的界面基本都是这三件事没做：数值不成刻度、阴影没留外溢、悬浮没留余量。规则与判据：

1. **刻度只用 4 的倍数**：`4 / 8 / 12 / 16 / 20 / 24 / 32`。`10 / 14 / 18` 这类值的问题不是难看，是**每个组各写各的**，最后相邻间隙全不一样。静态判据（不用构建）：

   ```powershell
   Select-String -Path app\*.xaml -Pattern '(Margin|Padding)="([0-9,\s]*)"' -AllMatches |
     ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[2].Value -split '[,\s]+' } |
     Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } |
     Where-Object { $_ -ne 0 -and ($_ % 4) -ne 0 } | Sort-Object -Unique
   ```

   判据：输出为空。实测在模板改之前它会报 `10 / 14 / 18`，改完（全部并到 12/16/20）报空——所以这条真能抓到东西。`0` 只允许出现在"由父级 `Padding` 统一供间距"的那一侧。

2. **三条下限**：同排兄弟之间 ≥ 8；分组之间（工具条 ↔ 内容卡 ↔ 页脚）≥ 16；内容到窗口边缘 ≥ 16。模板给的是 12 / 16 / 20，照抄这个量级。
3. **阴影要留外溢**：投影需要 `ShadowDepth + ShadowBlurRadius` 的空间——`UI4Panel` 默认 `8 + 5 ≈ 13`，`UI4ListView`/`UI4GridView` 常用 `ShadowDepth=15 + ShadowBlurRadius=12 ≈ 27`。外边距小于它，四边投影就被切平。**不要用 `ClipToBounds=true` 兜**：切平四边正是它的效果，正确处置是把外边距加到 ≥ 那个和。
4. **悬浮放大要留余量**（这条最容易踩，症状是"悬浮没反应"而不是"太挤"）：库里的契约是 `allowed = min(HoverMaxGrow(8), sideSlack + ContentPadding(4) − EdgeReserve(6))`，`sideSlack` 就是 `ItemMargin.Left/.Top`。`ItemMargin` 给 0 → `allowed ≤ 0` → 放大倍率被钳到 1，看起来像控件坏了。给到 ≥ 10 才吃满 8 px 预算。
5. **内边距与对齐**：卡片 `Padding` 用 16–24；同组控件统一宽度或统一 `HorizontalAlignment`，别一个控件一套 margin；长文本给 `TextWrapping` 并保证父容器有宽度约束——`UI4ComboBox` 弹层裁切、卡片标题溢出都出在"给了无限宽容器"（`ScrollViewer` + 无宽度约束的 `StackPanel`）上。

判据（三条都要过）：静态扫描输出为空；起窗口截图上相邻控件之间看得见空白、卡片投影四边完整；鼠标扫过卡片时**确实有放大反馈**（没反馈就是第 4 条）。

## Step 4 — 深色、套装与局部换肤

- 全局切档：`UI4Theme.SetTheme(UI4ThemeMode.Light|Dark|System|HighContrast)`；`System` 会读 `AppsUseLightTheme` 注册表并订阅 `SystemEvents.UserPreferenceChanged`，退出时 `ReleaseSystemFollow()`（模板已挂在 `Exit` 上）。
- 8 套预置（`data-console` `reading` `paper-white` `paper-grey` `form` `oncall` `terminal` `showcase`）：`UI4Theme.Apply(UI4ThemePacks.PaperGrey)`，键是英文稳定契约，中文只用于展示。
- 只换强调色：`UI4Theme.SetAccent(color)`，`AccentDark` 自动按 ×0.85 亮度派生。
- 局部：`ui:UI4ThemeScope.Theme="dark"` 写在任意 `FrameworkElement` 上，整棵子树换档、可嵌套；**空串/纯空白/未注册键 = 撤销作用域，不抛异常**。撤销写 `UI4ThemeScope.SetTheme(el, "")`。
- 持久化默认关闭：`UI4Theme.Persistence = new RegistryThemePersistence()`（`HKCU\Software\StartUI4`）或 `new JsonThemePersistence(path)`，然后 `ApplyPersisted()` + `ThemeChanged += (s,e) => UI4Theme.Save()`。**存的是请求模式**（可能是 `System`），不是解析结果。

改主题后某处没变，按这个顺序查（`theming.md` §三条性质 有成因）：① 那处是不是写了字面色（本地赋值退订，最常见）；② 资源键名拼错（对照 `theming.md` 的 38 个名字逐字比）；③ 该属性是否属于"没接令牌的 20 个颜色 DP"（`controls.md` 有表，深色下要宿主显式接）；④ 是不是那 8 个库内无人消费的令牌（`ListSelected`/`Separator`/`GridLine` 等，改它们不会让任何库内控件变脸）。

## Step 5 — 对话框、托盘与其他系统集成

```csharp
if (UI4MessageBox.Show("确认删除这条记录？", "确认", UI4MessageBoxButtons.OKCancel, owner: this) == true) Delete();
Color? picked = UI4ColorPicker.ShowDialog("选择强调色", UI4Theme.Current.ColorOf(UI4ThemeToken.Accent), this);
if (picked.HasValue) UI4Theme.SetAccent(picked.Value);
```

`Show` 返回 `bool?`：OK=`true`、Cancel=`false`、直接关窗=`null`。判 `== true`，不要判 `if (result)`。两个对话框都是自绘标题区，对它们调 `UI4WindowTitleBar.Apply` 无可见效果。

右键菜单是纯代码组件：`menu.AddItem(new UI4MenuItem(UI4MenuItemType.Copy, text, null, handler)); menu.Attach(host); menu.Open();`

托盘 `UI4NotifyIcon` 必须在 `OnClosing` 里 `Visibility = Visibility.Collapsed; Dispose();`，否则托盘残留"点不动的死图标"（Shell 行为，不是库的 bug）。`UI4CodeEditor` 的语法高亮配色由 XSHD 决定、不跟主题，库只染外壳底与前景。

## Step 6 — 发布两档 + 自测门禁

`publish.cmd` → `<App>_self_contained.exe`（约 60+ MB，目标机免装 .NET；首次要下 150 MB 运行时包）；`publish_no_runtime.cmd` → `<App>_no_runtime.exe`（约 2–3 MB，目标机必须有 `Microsoft.WindowsDesktop.App 10.x`）。两档靠 `-p:ArtifactLabel=` 分产物名、互不覆盖，`DebugType=embedded` 是全局属性所以 `lib` 项目也不会留散落 pdb。

两个开关分档的原因（写在自己的发布脚本里要照抄）：`EnableCompressionInSingleFile` 只对自带运行时合法，框架依赖发布会 `NETSDK1176`；WPF 的 `PresentationNative_*` / `wpfgfx_*` / `D3DCompiler_47_*` 没法从 bundle 直接 `LoadLibrary`，要靠 `IncludeNativeLibrariesForSelfExtract` 运行期解到 `%TEMP%\.net\<程序集名>\`。

脚本第 2 步就是门禁：`start "" /wait "%EXE%" --selftest`，退出码非 0 就报警告并指向 `%APPDATA%\<App>\selftest.txt`。模板自带的 `SelfTest.cs` 钉住了七件容易回退的事：令牌总数仍是 38、三份内置定义逐令牌可取色、宿主单源色的对比度门槛、`Mix`/`IsDark` 的判据一致、**`Theme.Policy` 已显式决策（留 `TODO` 就红）**、**8 套预置逐套装字典后 38 个令牌的 `UI4.Color.*`/`UI4.Brush.*` 与三个别名键齐全**、**字典里的底色与 `Dark()` 定义同源**。后两条是"切主题某处不跟随"的机器版判据——键名写错在运行期不报错，只有这条断言会红。改配色后先跑它再跑界面。

## Step 7 — 界面自适应是编码期约束

写第一行 XAML 之前就要定下来，别等收尾补救。每条都给可查判据。

- **下限尺寸**：`MinWidth`/`MinHeight`（模板给 720×480）。判据：`Get-Process` 起来后把窗口拖到极限或直接 `SetWindowPos` 到 100×100，客户区仍不低于下限（WPF 由 `MinWidth` 保证，不需要代码兜）。
- **分档而不是写死**：多栏布局按 `ActualWidth` 分档（`VisualStateManager` 或 `SizeChanged` 里切 `Grid` 列数），不要在 XAML 里钉死像素宽。判据：窗口收到窄档时列数真的降下来。`UI4GridView` 自带按宽度算列数（`ComputedColumns`），别在它外面再算一遍。
- **DPI**：`app.manifest` 的 `PerMonitorV2` 声明别删——net10 的 WPF 仍按清单取 DPI 感知级别。判据：150%/200% 缩放下文字不发虚、窗口不糊。
- **字体**：族用系统栈（从 `SystemFonts.MessageFontFamily` 起步，通常是 `Segoe UI Variable Text` / `Segoe UI`），字号别硬编码到 12 px 以下；要跟系统字号走就用 `SystemFonts`/`DynamicResource`，不要手写常数覆盖全局。判据：把系统字号调到 125% 后正文可见且不溢出按钮。
- **窗口几何持久化要回正**：存过 `Left/Top/Width/Height` 的工程，恢复时用 `SystemParameters.WorkArea` 判定是否仍在某块屏内，不在就回正中。判据：把窗口挪到 -24000,-24000 存下、拔掉外接屏再起，窗口必须可见（这条最容易在换显示器后炸）。
- **标题与图标各只有一个真源**：标题 = `MainWindow.xaml` 的 `Title`（别自绘标题栏）；图标 = `app/AppIcon.ico`，csproj 的 `<ApplicationIcon>` 与 `<Resource>` 两处都指它，`Window.Icon` 也指同一个文件。判据见 Step 10 第 6/7 条。
- **图标默认本地生成**（不联网、不要 ImageMagick）：换字母或配色只需重跑生成器，它自带自证（目录条目连续、末帧落在文件尾、GDI+ 回取 16/32 两档验圆角透明与取色）：

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/make-icon.ps1" \
    -Name MyNote -Out "D:\work\MyNote\app\AppIcon.ico" -Back "#4F6BE8" -Fore "#F7F9FB"
  ```

  `-Glyph` 可显式指定首字母（只支持 A–Z 点阵），`-Sizes` 换尺寸集。帧表越大 exe 越大，本机实测：单帧 4,286 B → 七帧 105,414 B 的 ico，让框架依赖单文件包从 `1,393,834 B` 涨到 `1,696,938 B`（未压缩档里 ico 会同时进 apphost 与 WPF 资源两处），自带运行时档只涨 `+105,567 B`（整包压缩过）。只要日常观感就 `-Sizes 16,32,48,256`。要改用外部图（例如 `selfh.st/icons` / `github.com/selfhst/icons` 的 `ico/` 现成多尺寸 ico）就**直接覆盖** `app/AppIcon.ico`，同时把来源 URL、图标 ref、许可（那份仓库是 CC-BY-4.0，要署名并注明修改）记进 `doc/` 第 5 节——署名不能只写在聊天里。
- **间距与外溢余量见 Step 3「间距与留白」**：刻度只用 4 的倍数，同排 ≥ 8、分组之间 ≥ 16、内容到窗口边缘 ≥ 16；卡片外边距 ≥ `ShadowDepth + ShadowBlurRadius`（≈13~27），`ItemMargin` 左右 ≥ 10 才留得住悬浮放大的预算。窗口收窄时先让**分档**改列数，不要靠压掉 margin 来腾地方——margin 被压成 0 的那一刻，投影切边与"悬浮没反应"就会同时出现。

## Step 8 — `doc/运行与构建（T0Level）.md`（新工程必交）

`scaffold.ps1` 会把模板落到 `<proj>\doc\运行与构建（T0Level）.md`，脚本结尾会打印还剩多少处 `【模板】`。**构建与验收通过后必须逐条填**，这是给"从没碰过这个工程的人"的上手文档：

八个小节缺一不可 —— **0 前置要求 / 1 项目启动（首次构建）/ 2 本地调试 / 3 打包与启动 / 4 修改标题 / 5 修改图标 / 6 发布 / 分发 / 7 注意事项**。

三条硬规矩：

1. 每条 powershell 命令都要在本机跑过，并把**真实输出**原样贴进该节的"实测输出"代码块。
2. 没跑或跑不了的行，显式写 `未验证` ＋ 原因（缺工具/缺权限/离线机器），并在第 7 节末尾的"未验证项汇总"里列全。
3. 不许出现想象中的命令与想象中的输出；交付前 `【模板】` 残留数必须为 0（`Select-String -Path doc\运行与构建（T0Level）.md -Pattern '【模板】' -SimpleMatch`）。

模板里已经写好判据的点：9 条上游警告（非增量口径）、`--selftest` 退出码 = 失败断言数、两档产物各自只有 1 个 exe、`FileVersion` 守门、图标要取回内嵌位图逐像素比（体积相同完全正常，4286 B 对 4286 B 实测过）、以及"拿上一轮产物验本轮改动 = 假红"。第 5 节的图标命令写 `<skill>/scripts/make-icon.ps1`（本地生成、自带自证，输出那七行 `frame …` 就是实测输出）；`magick -define icon:auto-resize=…` 只在装了 ImageMagick 的机器上才写，没装就保持 `未验证` 并给原因，别照抄成已验证。

## Step 9 — 交付前把目录收敛到最小清单

删：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`（`dotnet build` 能重建的中间物）、`publish\` 与 `publish_no_runtime\` 里非本轮的 exe、验收产生的 `*.log` / 截图 / 临时分发目录、Skill 侧的 `assets\seed\.fetch-tmp\`。

留：`app\`、`lib\`（源码与 `LICENSE.txt`、`README.md`、审计报告都是源，不是中间物）、`doc\`、根上的 `publish.cmd` / `publish_no_runtime.cmd`、`快速启动.txt`。

两条判据：

1. 已打好的包不依赖中间目录——删掉 `bin\`/`obj\` 后 `publish\__APPNAME___self_contained.exe --selftest` 照样退 0（单文件包自含全部依赖）。
2. 删完能一把重建：`dotnet build app\<App>.csproj` 从零产出、两档 publish 再跑一次全绿，警告仍是上游那 9 条。

自查口径：`Get-ChildItem` 结果里除 `bin/obj/publish*` 外，每一项都应能归进"代码 / 文档 / 脚本 / 配置 / 产物"五类之一，归不进去的就是残留。

## 故障速查

| 症状 | 真因 | 处置 |
| --- | --- | --- |
| 切主题后某处颜色不动 | 那处写了字面色（本地赋值即退订，属设计） | 改 `{DynamicResource UI4.Brush.*}`；见 Step 2 |
| 宿主 `{DynamicResource UI4.*}` 全空，库内控件却正常 | `Register/SetTheme` 在 `base.OnStartup` 之前或之外调了，`Application.Current == null` 时库静默不装字典 | 挪到 `OnStartup` 内、`base.OnStartup(e)` 之后 |
| `UI4Theme.Apply(套装键)` 返回 false 且界面纹丝不动 | `UI4ThemePacks.RegisterAll()` 没在 `Apply` 之前调 | 按 Step 2 顺序；`Apply` 不抛异常，只回 false |
| 页脚"当前主题"停在初值不再刷新 | 绑静态 CLR 属性需要同名 `<Prop>Changed` 静态事件，库发的是 `StaticPropertyChanged` | 订阅 `UI4Theme.ThemeChanged`，读 `ResolvedKey` + `DisplayLabel` |
| 自定义主题运行期崩在 `KeyNotFoundException` | `new UI4ThemeDefinition("key")` 只填了几个令牌 | 从 `Light()` 起步（天然覆盖 38 个令牌）再 `With(...)` |
| 改了标题/图标，产物没变 | **拿上一轮的 exe 验本轮改动**（实测踩过：改完 Title 用旧包判"打包版不出窗"） | 先删空 `publish*`，重新打包，再按 pid 读 `MainWindowTitle` |
| `dotnet build` 报 0 个警告，以为警告没了 | 增量只重编 app，没重编 lib | `dotnet build --no-incremental` 或先 `dotnet clean` |
| `dotnet pack` 报 `NU5019` | `lib/LICENSE.txt` 缺失（csproj 里 `<None Pack="true">` 指它） | 跑 `fetch-source.ps1` 补种子，别删 csproj 那行 |
| 构建说找不到 `AvalonEdit` | NuGet 源不可达（lib 唯一第三方依赖） | 先在线 restore 一次；离线机预置包缓存 |
| 双击单 exe 秒退/无窗口 | 首次启动要解原生库到 `%TEMP%\.net\<App>_self_contained\`，或杀软拦解包；也可能真崩了 | 看事件日志与 `%APPDATA%\<App>\selftest.txt`；别先怀疑打包 |
| 托盘留下点不动的死图标 | `UI4NotifyIcon` 没在 `OnClosing` 里 `Collapsed` + `Dispose()` | 补上（Shell 行为，不是库的 bug） |
| 悬浮放大越出父容器 | `HoverScale` 太大、按像素预算被钳过，或宿主在 `ScrollViewer+StackPanel` 里给了无限宽 | 见组件手册 §3.7 的钳制公式，别加 `ClipToBounds` 兜底 |
| 悬浮上去"没反应"，放大倍率像坏了 | `ItemMargin` 贴边 0，`sideSlack + 4 − 6 ≤ 0` 把放大预算整个吃掉（契约见 Step 3 第 4 条） | `UI4ListView`/`UI4GridView` 的 `ItemMargin` 左右给 ≥ 10 |
| 卡片四边投影被切平 | 外边距 < `ShadowDepth + ShadowBlurRadius`（`UI4Panel` 默认 ≈13，卡片常用 ≈27） | 把外边距加到 ≥ 该和；`ClipToBounds` 是切边的成因不是解法 |
| 界面看着挤、各段间隙不一致 | `Margin/Padding` 用了 10/14/18 这类不成刻度的值 | 并到 4 的倍数（4/8/12/16/20/24/32），跑 Step 3 的静态扫描到输出为空 |
| `UI4Grid` 里宿主设的底色换肤后丢失 | 库的 `UpdateBackground()` 在主题切换时直接 `SetValue` 覆写 | 固定底色用普通 `Grid`/`UI4Panel` |
| PS 脚本里中文注释后一行莫名语法错 | PS 5.1 把无 BOM 的 UTF-8 按 ANSI 解码，中文字节吃掉下一个换行 | `.ps1` 存带 BOM 的 UTF-8，或保持 ASCII-only |
| 关键命令退出码看着是 0 但其实失败 | `build \| grep` 的退出码是 grep 的 | 先落文件再判，管道别接在验证步骤上 |
| Skill 的 tar.gz 通路 A 白跑一次 | `tar.exe` 被 Git Bash 的 GNU tar 抢走，它把 `E:\path` 当远程主机 | 脚本已锁 `$env:SystemRoot\System32\tar.exe`；手工跑时也用全路径 |
| 回源后校验说 `架构审计报告…md 缺`，但文件明明在 | bsdtar 把非 ASCII 文件名解成乱码名（`鏋舵瀯…`） | 脚本按清单点名拷 + raw 单文件补；手工时别整目录递归拷 |

## Step 10 — 验收清单

窄改动只跑覆盖该面的那一挡；新建工程或改主题通路要跑到第 7 条。UIA 挡证不住颜色，颜色类结论用截图。

1. `fetch-source.ps1` → `LOCAL 48/48 未联网` 或 `VERIFY ok 48/48`（种子完整才继续）。
2. `dotnet build app\<App>.csproj --no-incremental` → `0 个错误`，警告恰好是上游那 9 条（`CS0414`×5 + `CS1574`×4）。
3. `<App>.exe --selftest` → 退出码 0，`%APPDATA%\<App>\selftest.txt` 全 `PASS`。断言已含：令牌总数 38、三份内置定义逐令牌可取色、宿主对比度门槛、`Mix`/`IsDark` 判据、**明暗策略已显式决策**、**预置套装 × 38 令牌资源键齐全**、**字典与定义同源**。
4. 先删空 `publish\` 与 `publish_no_runtime\`，再跑两档 → 产物目录各自**只有一个 exe**，脚本内 `selftest exit code: 0`。
5. 守门：`(Get-Item <exe>).VersionInfo.FileVersion` 等于 csproj 的 `<Version>`（`0.1.0` → `0.1.0.0`）。不等就是在交旧构建。
6. 起**本轮**产物、按自己起的 pid 读 `MainWindowTitle` = 源码里的 `Title`，四个主题按钮都改观感、原生 `TextBlock` 那行也要变（它证明宿主 `{DynamicResource}` 通路接通）、右下生效键从 `light` 变到 `dark`/`paper-grey`；收尾 `Stop-Process -Id`，残留 `Get-Process -Name <App>*` 数为 0。
7. 图标：`[System.Drawing.Icon]::ExtractAssociatedIcon(<exe>)` 取回内嵌位图，按图样的特征取色点逐像素命中（字节数与旧图标相同是正常的，体积不是判据）；生成式图标还要看 `make-icon.ps1` 自己那两条自证——目录帧数与 `frames=7` 对上、16/32 两档 `corner.alpha=0`（圆角透明没被 `<ApplicationIcon>` 吃掉）。
8. 标题栏底色/文字色随深浅变（Win10 只认深/浅标志，Win11 才染三色，属系统能力差异）。
9. 按 Step 9 收敛目录后，两条判据都过（删完中间物仍能一把重建、已打包 exe 不依赖它们）。
10. `doc/运行与构建（T0Level）.md` 八节齐全、`【模板】` 残留为 0、每条命令都贴了真实输出、未验证项都有原因（Step 8）。
11. 间距：Step 3 的静态扫描**输出为空**（`Margin/Padding` 全在 4 的倍数上），截图上相邻控件看得见留白、卡片投影四边完整、鼠标扫过卡片有放大反馈（没反馈＝`ItemMargin` 贴边，见故障速查）。

## 实测基准（单机口径，换机器请重测）

2026-10-03 本机（Windows 11 26100/26300 系、SDK `10.0.401`、`Microsoft.WindowsDesktop.App 10.0.12`，运行时包已在 NuGet 缓存里）：

| 项 | 数值 | 口径 |
| --- | --- | --- |
| `dotnet build`（全新工程） | 约 6.6 s | Debug，含 lib 首次编译 |
| `dotnet run` 到窗口出现 | 2,985 ms | Debug，进程内首帧 |
| `publish_no_runtime.cmd` | 3 s，`T0Probe_no_runtime.exe` 1,431,204 B | Release 单文件框架依赖 |
| `publish.cmd` | 9 s，`T0Probe_self_contained.exe` 70,288,063 B | Release 单文件自带运行时；**首次**跑还要下载运行时包（脚本自己提示约 150 MB），耗时不在此列 |
| 打包版起窗口 | 约 5 s 内出窗 | 已解包过的缓存态；首次要解到 `%TEMP%\.net\` |
| `--selftest` | 退出码 0，报告 6 行起 | Debug 与两档产物都跑过 |

引用这些数字时必须带上"哪一档、是否首次、Debug/Release"，否则会出现拿框架依赖小包比自带运行时大包的量级错误。

## Resources

- `references/component-manual.md` — 组件手册逐字（2128 行，v3.0.0；与 `assets/seed/lib/README.md` 同一份内容，blob sha `bf9da41c`）。**属性名、默认值、枚举成员、事件签名以它为准**，别凭记忆写。手册开头有一行指向仓库根 README 的 `../../README.md` 链接，那是上游仓库级文档，在工程内不可达（种子是上游忠实镜像，不改写它的链接）。按需 grep，别整份读：

  ```bash
  grep -n '^## \|^### ' references/component-manual.md   # 全目录（§一 事实 / §二 清单 / §3.1-3.11 详解 / §四 主题 / §五 服务 / §六 配方 / §七 扩展 / §八 附录）
  sed -n '2039,2089p' references/component-manual.md     # §8.1 枚举全清单 / §8.2 事件全清单 / §8.3 不建议用的公开成员
  grep -n 'UI4PasswordBox' references/component-manual.md | head
  ```

- `references/controls.md` — 39 控件选型表、与原生类的继承关系差异、不接主题的 20 个颜色 DP、逐条实测过的限制。
- `references/theming.md` — 38 令牌与资源键规则、`UI4Theme`/`UI4ThemeScope`/`UI4ThemePacks` API、8 套预置键与场景、系统跟随与持久化、DWM 标题栏四条通路。
- `references/host-integration.md` — app 侧契约：csproj 双侧关键行、`app.manifest` DPI 逐字、启动序列四条硬约束、宿主配色单源→令牌的写法、自测契约、图标三处通路。
- `assets/seed/lib/` — 组件源码整份自包含，**48 个文件** = 44 个 `.cs`（含 `Internal/` 6 个）+ `StartUI4Controls.csproj` + `LICENSE.txt` + `README.md`（组件手册，随工程进 `lib/`）+ `架构审计报告-3.0.0主题机制评审.md`（主题通路的实测数据）。逐文件等于上游 `WinApp-Skills` 的 `componentSourceCode/StartUI4Controls`（commit `5d96442a`），也就是 `StartUI4.WPF` 仓库 `dotnet_10` commit `269189e`。
- `assets/seed/manifest.json` — 上述 48 条 `path + blobSha + size` 与内容计数基线，回源与校验都以它为准；上游改版后要重新生成（见"源码来源与升级"）。
- `assets/templates/app/`、`assets/templates/root/` — 工程模板，`__APPNAME__` 为占位符（`.md` 也参与替换）。含 `Helpers/Theme.cs`（配色单源 + **必须显式决策的 `Policy`**）、`Services/SelfTest.cs`（8 组断言，含资源键完整性与字典同源）、app csproj（`<Version>0.1.0</Version>` 是 FileVersion 守门的基准）、`root/doc/运行与构建（T0Level）.md`（Step 8 的八节骨架，随 scaffold 落进工程，`【模板】` 标记为待填位）。
- `scripts/scaffold.ps1` — Step 1 的生成器（已实测：空种子 → 自动回源 → 生成 → `dotnet build` 0 错误 9 条上游警告 → `--selftest` 退 0；种子完整时加 `-Offline` 零联网）。
- `scripts/make-icon.ps1` — 应用图标生成器（Step 7）。`-Name` 取首字母、`-Glyph` 显式指定，`-Back`/`-Fore` 换色，`-Sizes` 换尺寸集；输出 16/24/32/48/64/128/256 七帧 32bpp ICO（<256 走 DIB + AND 掩码，256 走内嵌 PNG），写完自己回读目录与 16/32 两档像素做自证。已实测：脚手架产出的工程 `dotnet build` 后 `ExtractAssociatedIcon` 取回 32×32，圆角 `alpha=0`、字形与底色取色命中。
- `scripts/fetch-source.ps1` — 种子的取源器（三挡 + 四条通路 + blob sha 校验，已实测 A 通路 3.2 s 取齐 48 个文件、第二次跑命中本地不发请求）。

## 源码来源与升级

种子的真相源是上游仓库里那个目录，不是 Skill 里这份拷贝——Skill 这份是它的逐文件镜像（blob sha 全等）＋一份钉死清单。上游改版时分三种情况：

1. **文件集没变，内容变了**（最常见）：`fetch-source.ps1 -Refresh` 强制重新取源并写回种子，校验按 blob sha 说话。若三个内容计数（`.cs` / 令牌 / DP 声明）与清单基线不符，脚本会打 `WARN` 但不拦截——这时接着走第 2 步把基线重钉。
2. **文件增删或要重钉基线**：`make-manifest.ps1` 按 `assets/seed/lib` 目录里**实际存在**的文件重算 `path + blobSha + size + expected` 并覆盖 `manifest.json`（`fetchedFrom` 段原样继承；上游换了仓库/目录用 `-Repo/-Branch/-Commit/-PathPrefix` 覆盖）。顺序是：先 `-Refresh`（它会在校验步骤把与旧清单的差异列出来）→ 再 `make-manifest.ps1` → 再跑一次 `fetch-source.ps1` 确认 `VERIFY ok`。
3. **离线机器或 GitHub 三条通路全败**：从 `StartUI4.WPF` 仓库 `dotnet_10` 拉 `src/StartUI4Controls` **整份**替换 `assets/seed/lib`——48 个文件全要，含 `Internal/` 与两份文档（`README.md`、`架构审计报告-*.md`），然后走第 2 步重钉清单。

之后同步 `references/`：`component-manual.md` ← 新的 `lib/README.md`（逐字，别自己缩写），`controls.md`/`theming.md` 里被改动的表（令牌数、套装键、不接令牌的 DP）。
`scaffold.ps1` 的 `required` 清单里点名了 `README.md` 与《架构审计报告-3.0.0主题机制评审.md》，文件数判据取自 `manifest.expected.totalFiles`——**改了文件集却不重钉清单，新工程会被完整性检查挡在退 4**。最后重跑 Step 1 → Step 7 全链（含一次 `dotnet build` 与一次空种子回源）。
