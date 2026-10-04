---
name: startui4-wpf
description: 用 StartUI4Controls（StartUI4.WPF v3.0.0，net10.0-windows 纯 C# 模板 WPF 组件库）搭工程、改界面与发版本的可复现流程——app/+lib/ 源码自包含骨架（lib 种子本地优先，缺失或不完整时按 manifest 回源并写回；种子自 2026-10-04 起是自持基线，-Refresh 默认被挡）、UI4Theme 38 令牌 + UI4.Font.* 三个排印键与 UI4.Brush.*/UI4.Color.* 资源键契约、开工前必问的四挡决策（明暗策略/两档分发口径/要不要套装与 UI4ThemeScope 局部换肤/设置按钮的位置与面板内容）、设置面板通路（应用信息+配色+字体+全局缩放：Typography 单源层级键、ZoomedSize 窗口下限折算、kv1 设置落盘与钳位、Popup 不吃 LayoutTransform 的已知边界）、启动顺序（base.OnStartup 之后才 Register/SetTheme）、DWM 标题栏、UI4MessageBox/UI4ColorPicker/UI4NotifyIcon/UI4CodeEditor/UI4GridView 用法、界面自适应当编码期约束、间距刻度与阴影/悬浮外溢余量（组件之间不贴边、投影不被切平、钉死尺寸的圆钮会被字号撑爆）、单文件两档发布（self_contained / no_runtime）与 --selftest 门禁（含排印与缩放段）、FileVersion 守门、应用图标默认本地生成（make-icon.ps1 出字母像素多尺寸 ico，不联网不依赖 ImageMagick）、交付 doc/运行与构建（T0Level）.md 八节（每条命令本机跑过贴真实输出，没跑过标未验证）与交付前目录收敛。当需要新建 .NET 10 WPF 桌面应用、要写或改 UI4* 控件的 XAML/C#、给应用加设置页/主题切换/字号缩放、切主题或调字号后某处没跟着变、{DynamicResource UI4.*} 解析不到、找不到 StartUI4Controls 的类型或属性、组件源码目录是空目录或缺文件、UI4 控件在深色下对比度不对、要打包 WPF 单 exe、要补零基础上手文档或把既有 StartUI4 工程按示例项目结构收敛时使用。不用于 WinForms、UWP/WinUI、.NET Framework 48 版 StartUI4，也不用于不含该库的普通 WPF 页面。
---

# StartUI4.WPF 桌面应用搭建与组件使用

## Overview

StartUI4Controls 是**零 XAML** 的 WPF 组件库：39 个 `UI4*` 控件的模板全由 C# 构建，主题是一张 `UI4ThemeToken → Color` 的 38 令牌表，外加同一份字典里的 3 个排印键（`UI4.Font.Size.Base` / `Code` / `Family`）。这带来两个反直觉点，是本 Skill 的主要价值：

1. **不需要挂资源字典就有完整外观**——宿主直接 `<ui:UI4Button/>` 即可；但也因此**没有任何编译期约束**保护资源键与属性名，写错键名不报错、不抛异常，只表现为"这处颜色没跟主题"或"字号没跟着调"。
2. **本地赋值就是退订主题**，这是设计。`SetResourceReference` 占的也是本地值槽，宿主写一个字面色就把引用顶掉，之后 `ClearValue` 只能回到库内代码的字面默认色。

所以顺序是：先问定四挡决策（开工前必须问用户，含设置按钮的位置与面板内容），把工程与令牌接通（Step 1–2），再选控件写界面（Step 3–5，设置面板在 Step 4.5），把自适应当编码期约束一次到位（Step 7），最后用发布与自测（Step 6）、上手文档（Step 8）、目录收敛（Step 9）、验收清单（Step 10）把产物钉住。

## 何时使用

- 新建 .NET 10 WPF 桌面应用，要用这套组件（含"照示例项目结构再来一个"）。
- 已有工程里要写/改 `UI4*` 控件的 XAML 或 C#，需要准确的属性名、枚举、事件签名。
- 切主题后部分界面没变、`{DynamicResource UI4.Brush.X}` 拿到空、套装键 `Apply` 无反应。
- 自定义主题运行期崩在 `GetColor`（`KeyNotFoundException`）。
- 深色/高对比度下某控件对比度不对（黑字压黑底）。
- 要打包成单 exe（免装运行时 / 依赖共享运行时两档），或要把 `--selftest` 接进发布门禁。
- 要给应用加**设置面板**：明暗档切换、字体族与字号、全局缩放、"关于/应用信息"，或反过来要把这些能力补进一个已有窗口（Step 4.5 有整条通路与自测）。
- 用户报"字号调大后按钮里的字跑偏 / 设置里恢复默认后下拉框空白 / 缩放后窗口拖不动"这类观感问题（多数不是渲染 bug，而是钉死尺寸、层级抹平、ComboBox 清空列表外值这三条机制）。
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
│   ├── Helpers/Theme.cs               宿主配色单源（常量 + 必须显式决策的 Policy）
│   ├── Helpers/Typography.cs          字号/字体/缩放的单源（默认值、区间、层级换算、发布资源键）
│   ├── Services/SettingsService.cs    设置落盘 %APPDATA%\<App>\settings.json（kv1，坏文件改名留档，永不抛）
│   ├── Services/SettingsCodec.cs      纯函数编解码（自检直接对它出题）
│   ├── Services/ThemeService.cs       配色档：稳定键 → SetTheme/Apply → 展示文案，按 Policy 挡可选项
│   ├── Services/SelfTest.cs           --selftest，退出码 = 失败断言数（配色 + 排印缩放两段）
│   ├── ViewModels/MainViewModel.cs    设置项的可绑定视图（真源仍是 SettingsService）
│   ├── Views/SettingsOverlay.xaml     设置浮层内容（应用信息 / 配色 / 字体 / 全局缩放）
│   ├── Converters/Converters.cs       BoolToVis（浮层显隐）+ ZoomedSize（缩放下的窗口下限）
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

二、**组件源码从哪来**（种子目录可能是空的，别一上来就当它存在）。种子 `assets/seed/lib` 共 48 个文件
（44 `.cs` + csproj + LICENSE.txt + README.md 组件手册 + 架构审计报告），`assets/seed/manifest.json` 钉死这 48 条
`path + blobSha + size` 与内容计数基线（44 个 `.cs` / 38 个令牌 / **237** 条 DP 声明）；blobSha 是 **LF 归一后的 git blob sha**，与 GitHub contents API 同口径。
`scripts/fetch-source.ps1` 按三挡办事：

**2026-10-04 起种子是自持基线**（清单 `upstream.baseline.selfHosted = true`）：它比 `fetchedFrom` 那份
（`WinApp-Skills@5d96442` 的 `componentSourceCode/StartUI4Controls`，该目录已在 main 上删除）多 **11 个文件**的有意改动
——6 个控件的字面字号改引用 `UI4.Font.Size.*`、`UI4Theme.WriteTokens` 发布三个排印键兜底值、`UI4ListView` 两个描边 DP、
`UI4NavigationView` 的 `ForEachListBox`、`UI4Panel` 悬浮描边挂令牌、`UI4ListBox` 触发器画刷由快照改绑定、手册新增 §4.2.1。
改动源自两个示例工程的 `lib/`（并集），组件仓库 `StartUI4.WPF@dotnet_10` 那份实测（tip `e15dd51` 的 `UI4Theme.cs`）**还没有**排印键。
所以 `fetch-source.ps1 -Refresh` **默认拒绝（退 2）并解释原因**：按旧上游覆盖会把这条通路静默打回去，
症状是所有 `UI4*` 控件掉到 WPF 裸默认 12 px、无编译错。实测：`-Refresh -AllowUpstreamReset` 指到临时目录跑通后，
sha 校验正好点名拦下那 11 个文件并退 5（通路 A 629,603 B / 3 s 左右）。

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1"              # 默认：本地命中零联网（LOCAL 48/48），有缺口才回源并写回种子
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1" -Offline     # 禁网：有缺口就逐名列出后失败（退 4）
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1" -Refresh     # 默认被挡（退 2）：种子已自持
powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>/scripts/fetch-source.ps1" -SeedDir <临时目录> -Refresh -AllowUpstreamReset   # 只用来验通路可达，别拿它覆盖真种子
```

取源通路 A（tar.gz + 系统 tar.exe，只解那个子目录）→ B（同 A 但 URL 前加 `-Mirror` 反代前缀）→ C（git sparse clone）→ D（清单里剩下的个别文件走 raw 单文件）。
**判可达性要用真正下载的 URL 做 GET，别拿 HEAD 的状态码当依据**（这台机器实测过 HEAD 200 而 GET 超时）。退出码：0 成功 / 2 参数或 Skill 目录不完整（含 `-Refresh` 缺 `-AllowUpstreamReset`）/ 3 三条通路全败 / 4 `-Offline` 有缺口 / 5 取回后校验不过。**缺文件时的手工通路不是 GitHub**：从示例工程的 `lib/` 或既有工程的 `lib/` 整文件拷回种子再重钉清单（见"源码来源与升级"）。

三、种子版本要对得上。`StartUI4Controls.csproj` 里 `Version=3.0.0`（上游 `MagicFollower/StartUI4.WPF` 分支 `dotnet_10` commit `269189e`）。
在既有工程里干活前先看它的 `lib/StartUI4Controls.csproj` 版本号，2.x 与 3.x 的主题机制不同（2.0.0 的 `IThemeAware`/`TrackControl` 在 3.0.0 已整体删除），别把旧写法抄进去。

四、PowerShell 侧两条硬约束（本 Skill 的脚本与任何自写校验脚本都适用）：`.ps1` 里有中文字面量必须存成**带 BOM 的 UTF-8**，否则 PS 5.1 按 ANSI 解析直接语法错（更阴的是 ANSI 解码会把中文字节的下一个换行吞掉，两条语句并成一条）；脚本第一句设 `[Console]::OutputEncoding = UTF8`，否则诊断信息在管道里是乱码。带中文的路径别用 `powershell -Command` 内联传（字节会被码页吃掉），走 `-File` + 参数。

## 开工前必须问用户（四挡，不要自己拍）

模板把前三个决策钉成了机器可查的口子（`Theme.Policy` 留 `TODO` 时 `--selftest` 直接红、发布脚本挡下），第四个决策钉成了**脚手架自带的面板**，所以先问再写。用一次 `AskUserQuestion` 把 1–3 问完，第 4 挡拆成"位置（单选）+ 内容（多选）"两个子问一起给：

1. **明暗策略**：`both`（亮+暗，默认跟随系统并给切换按钮）/ `light-only` / `dark-only`。答案决定要不要注册第二套定义、`App` 里 `SetTheme` 钉哪一档、要不要「跟随系统」按钮。**只做一档也要显式钉住当前档**，别把 `SetTheme` 整段删掉——那样库会按 `AppsUseLightTheme` 与系统高对比度自行解析，用户系统一改，你的"单档"跟着变。
2. **分发口径**：只出免装运行时的单 exe / 只出小的框架依赖包 / 两档都出。两档体积差两个数量级（实测 1.4 MB vs 70 MB），先问清目标机有没有 `Microsoft.WindowsDesktop.App` 10.x。
3. **要不要套装键与局部换肤**：只用内置 `light/dark/highcontrast`，还是要 `UI4ThemePacks` 的 8 套业务档、或 `UI4ThemeScope` 的局部作用域。这决定 `App` 启动序列里 `RegisterAll()` 与 `Apply(套装键)` 的顺序（顺序错时 `Apply` 只返回 `false`、界面纹丝不动）。
4. **设置按钮放哪 + 面板里放哪几节**。脚手架的默认形态是"工具条右端一枚齿轮 → 窗口内浮层，四节全给"，改哪一项都是**删代码而不是补代码**：

   | 位置选项 | 形态 | 代价 |
   | --- | --- | --- |
   | A 工具条/标题行右端齿轮（默认） | 窗口内浮层 + 半透遮罩，关法三条（完成 / 遮罩 / Esc） | 无；`Content="&#xE713;"` + `FontFamily="Segoe MDL2 Assets"`，图标字体不吃用户选的字体族（正是要的语义） |
   | B 左栏或侧边导航底部一项 | 同上，入口挪进 `UI4NavigationView` 的 BottomItems | 折叠面板宽度窄，文字项要能截断；底部项与顶部项的取色分开接 |
   | C 只在菜单 / 右键菜单里给一项 | 不占窗口空间 | 可发现性差，第一次用的人找不到；`UI4ContextMenu` 是纯代码组件，条目要 `AddItem` → `Attach` → `Open` |
   | D 独立设置窗口 | 另开 `Window` | 窗口里若不含 UI4 控件要手动 `UI4WindowTitleBar.Apply`；宿主的 `LayoutTransform` 缩放管不到它（要显式给系数）；遮罩与 Esc 语义得重做一遍。设置项多且有分类导航时才值 |

   | 内容选项（可多选） | 底座里现成吗 | 说明 |
   | --- | --- | --- |
   | 应用信息（版本 / 运行时 / 组件库 / 数据根 / 设置目录） | 现成，全只读 | 排障时"用户报的到底是哪一版"就靠这几行；版本走反射（`Assembly.GetName().Version` 与 csproj `<Version>` 同口径），运行时用 `RuntimeInformation.FrameworkDescription`，**用窗口内文本展示，不要弹模态框**（模态框只留给错误报告） |
   | 配色（明暗档切换） | 现成 | 档位由 `Theme.Policy` 挡决定（见第 1 条）；按钮文案写**目标档**，当前档由设置单源决定 |
   | 字体（族下拉 + 正文字号滑杆） | 现成 | 区间与默认只从 `Helpers/Typography.cs` 来；层级键与固定件尺寸见 Step 4.5 |
   | 全局缩放（50–200%） | 现成 | 挂根内容 `LayoutTransform`；窗口下限要跟着折算，弹层不跟缩放是已知边界 |
   | 其它（语言 / 快捷键 / 数据路径 / 导入导出…） | **不在底座里** | 可以放，但要现接线：新增设置键要同时改 `SettingsService.ToMap`/`ApplyMap` 与 `SettingsSelfTest` 的键名点名断言，否则 kv1 没 schema、键名打错一个字母就是静默丢设置 |

   **「不要设置面板」也是合法答案**——那时至少留一个明暗切换入口（否则暗档直接不可达），并把 `Views/SettingsOverlay.xaml` 与 `MainWindow.xaml` 里的浮层宿主段一起删掉，别只删按钮留着死代码。

答案原样写进 `doc/运行与构建（T0Level）.md`（第 7 节注意事项上方那段"本次填写人／实测环境"），作为**有意决策**记录，不是遗漏。用户不在场且必须继续时：按 `both` + 两档都出 + 只用内置档 + 齿轮入口四节全给推进，并在文档里显式标「此四挡未经确认」。

## Step 1 — 一把生成骨架

```bash
powershell -NoProfile -ExecutionPolicy Bypass \
  -File "<skill>/scripts/scaffold.ps1" -Name MyNote -Path "D:\work\MyNote"
```

`-Name` 会同时当 `RootNamespace` / `AssemblyName` / 数据目录字面量，要求字母开头、仅字母数字。产物是上面那棵树 + `app/AppIcon.ico`——**默认本地生成，不联网**：`scripts/make-icon.ps1` 用应用名首字母画一张像素点阵图标（16×16 逻辑网格上的 5×7 点阵字母，底色 `#4F6BE8`、字形 `#F7F9FB`，四角削成圆角），一次写出 16/24/32/48/64/128/256 七档 32bpp 帧（256 帧内嵌 PNG）。
种子完整时全程不联网；种子缺失/不完整时它会先调 `fetch-source.ps1` 回源补齐再建工程（离线机器/CI 上想禁止联网就加 `-Offline`，直连抖动时加 `-Mirror <反代前缀>`）。

判据：stdout 首行 `OK`，并回报 `lib/ 48 个文件`、`占位符替换 22 个文件`、图标那一段 `OK AppIcon.ico letter=<首字母> ... frames=7`、`还差 19 处【模板】`；走过回源会先打 `SEED 已补齐并写回，下次零联网`。产物清单由脚本自己点名（含 `Helpers\Typography.cs`、`Services\SettingsService.cs`/`SettingsCodec.cs`/`ThemeService.cs`、`ViewModels\MainViewModel.cs`、`Views\SettingsOverlay.xaml(.cs)`、`Converters\Converters.cs`），缺一项退 6。非 0 退出码含义：2 名字非法、3 Skill 目录不完整、4 种子有缺口（`-Offline` 下）或回源后仍不完整、5 目标已有 C# 源码（要 `-Force`）、6 产物缺文件、7 占位符没替换干净、8 图标生成失败（`make-icon.ps1` 的自证没过）；回源自身的失败码（2/3/4/5）在它自己的输出里，全败时会把三条通路的失败原因都打出来。

生成后立刻编译一次，把 restore 与工具链问题在写业务代码前清掉：

```bash
dotnet build "<proj>/app/<AppName>.csproj"
```

**9 条警告是 lib 自带的**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），`0 个错误`就是过。别去"修"它们——改 `lib/` 要同时重钉种子清单，否则 `fetch-source.ps1` 的 sha 校验会红（见"源码来源与升级"）。

手工加控件文件时注意：`Copy-Item -LiteralPath <目录> -Destination <已存在的目录> -Recurse` 会套出一层同名子目录，逐文件按相对路径拷才对。

## Step 2 — 主题接线（最容易静默失效的一段）

`App.xaml.cs` 的骨架已经在模板里，四条约束不要改：

1. `UI4Theme.Register` / `SetTheme` / `ApplyToApplication` **只能在 `base.OnStartup(e)` 之后、且在 `OnStartup` 内**调。库内写回资源字典的第一句是 `Application.Current == null` 就 return，在构造函数或 `Main` 里调**静默不装资源**，症状是宿主 `{DynamicResource UI4.*}` 全空而库内控件照常好看。
2. `UI4ThemePacks.RegisterAll()` 在 `UI4Theme.Apply(套装键)` **之前**。顺序错时 `Apply` 只返回 `false`、不抛异常，界面纹丝不动。
3. 装字典要早于第一个窗口 `Show()`，否则首帧 `DynamicResource` 回落默认色。宿主自己 `Register` 了当前键的定义时，`SetTheme` 已隐式装好；两种都不调 = 只有库内控件有样式。
4. `--selftest` 分支排在 `OnStartup` 最前面（不开窗、不读设置、不挂 UI 异常钩子），退出码即失败断言数。

资源键规则（`theming.md` 有全表）：令牌枚举名**逐字**决定 `UI4.Color.<名>` 与 `UI4.Brush.<名>`，另有 `UI4.Brush.Text` / `UI4.Brush.Border` 两个真别名，以及 3 个不从令牌派生的排印键 `UI4.Font.Size.Base` / `UI4.Font.Size.Code` / `UI4.Font.Family`（由 `UI4Theme.WriteTokens` 随每份字典发布，库里默认 15 / 14 / Segoe UI）。写错不报编译错，只表现为不跟随。

排印键有**两层**，写错层就白改：库的默认值住在 `Application.Resources.MergedDictionaries` 里那份共享字典，宿主的覆盖要写在 `Application.Resources` 的**自有项**上——同一层自有项先于 MergedDictionaries 命中，所以切主题冲不掉覆盖；反过来把键塞进共享字典内部（例如拿 `UI4Theme.SharedResourcesFor(key)` 返回的那个实例去写），下次重写就没了。细则与自测见 Step 4.5。

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
6. **钉死尺寸的图标钮与胶囊要随字号长**（做了 Step 4.5 的字号调节后，这条从"观感"升级成"缺陷"）：`UI4Button` 的样式默认 `Padding=10,0,10,0`，宿主给 `Width=28 Height=28` 时内容区只剩 **8 px**，图标字号一档就装不下——居中的 `ContentPresenter` 被 arrange 成 8 px 后文字从左上角起画，看起来是"内容偏到右下"。**加 `HorizontalContentAlignment` 无效**（它本来就是 `Center`），正确处置是给 `Padding="0"` 并让尺寸随字号派生（`App.Size.RoundButton` / `App.Size.Chip`），半径同理（`App.Radius.*`，且必须是 `CornerRadius` 类型的资源——`DynamicResource` 不做类型转换，挂 double 到 `CornerRadius` 上会在运行期炸）。判据：`--selftest` 的"默认基准下圆钮 28 / 胶囊 24"与"12–28 全区间 ≥ 字身 × 1.3"两条。

判据（三条都要过）：静态扫描输出为空；起窗口截图上相邻控件之间看得见空白、卡片投影四边完整；鼠标扫过卡片时**确实有放大反馈**（没反馈就是第 4 条）。

## Step 4 — 深色、套装与局部换肤

- 全局切档：`UI4Theme.SetTheme(UI4ThemeMode.Light|Dark|System|HighContrast)`；`System` 会读 `AppsUseLightTheme` 注册表并订阅 `SystemEvents.UserPreferenceChanged`，退出时 `ReleaseSystemFollow()`（模板已挂在 `Exit` 上）。**要让用户自己挑档**就别在窗口上摆一排按钮，走 Step 4.5 的设置面板（入口一处、状态一处、落盘一处）。
- 8 套预置（`data-console` `reading` `paper-white` `paper-grey` `form` `oncall` `terminal` `showcase`）：`UI4Theme.Apply(UI4ThemePacks.PaperGrey)`，键是英文稳定契约，中文只用于展示。
- 只换强调色：`UI4Theme.SetAccent(color)`，`AccentDark` 自动按 ×0.85 亮度派生。
- 局部：`ui:UI4ThemeScope.Theme="dark"` 写在任意 `FrameworkElement` 上，整棵子树换档、可嵌套；**空串/纯空白/未注册键 = 撤销作用域，不抛异常**。撤销写 `UI4ThemeScope.SetTheme(el, "")`。
- 持久化默认关闭：`UI4Theme.Persistence = new RegistryThemePersistence()`（`HKCU\Software\StartUI4`）或 `new JsonThemePersistence(path)`，然后 `ApplyPersisted()` + `ThemeChanged += (s,e) => UI4Theme.Save()`。**存的是请求模式**（可能是 `System`），不是解析结果。

改主题后某处没变，按这个顺序查（`theming.md` §三条性质 有成因）：① 那处是不是写了字面色（本地赋值退订，最常见）；② 资源键名拼错（对照 `theming.md` 的 38 个名字逐字比）；③ 该属性是否属于"没接令牌的 19 个颜色 DP"（`controls.md` 有表，深色下要宿主显式接）；④ 是不是那 7 个库内无人消费的令牌（`Separator`/`GridLine`/`HeaderBackground` 等，改它们不会让任何库内控件变脸。`ListSelected` 自 2026-10-04 起**有**消费者了——`UI4ListView.SelectedBorderBrush` 挂的就是它）；⑤ 字号不动而同颜色不动，则是另一条通路：那处写的是字面 `FontSize`，或那个控件把字号钉成了本地值（库里 6 处读排印键，其余是字面值，清单见 `controls.md`「字号与字体族」）。

## Step 4.5 — 设置面板：应用信息 / 配色 / 字体 / 全局缩放

这是"用户能自己改的东西"的收敛点，开工前第 4 挡问的就是它的位置与内容。**入口只留一处**：同一状态在窗口上再放一个同义按钮，就会出现两份观感（哪份是真相要靠猜），切换按钮的文案因此写**目标档**、当前档由设置单源决定。脚手架已自带整套底座（`scaffold.ps1` 生成即可用，改内容是删代码不是补代码）：

| 文件 | 职责 |
| --- | --- |
| `Helpers/Typography.cs` | 默认值、允许区间、层级换算、把键发布到资源根——**唯一的常量出处** |
| `Services/SettingsService.cs` + `SettingsCodec.cs` | kv1 纯文本落盘 `%APPDATA%\<App>\settings.json`；`Load`/`Save` 永不抛（坏文件改名留档），读回一律钳 |
| `Services/ThemeService.cs` | 稳定键（`light`/`dark`/`system`/套装键）→ `SetTheme` 或 `Apply` → 展示文案；可选项按 `Theme.Policy` 挡 |
| `ViewModels/MainViewModel.cs` | 设置项的可绑定视图（真源仍是 `SettingsService`，这里只写"落设定 + 立刻生效 + 通知界面"） |
| `Views/SettingsOverlay.xaml(.cs)` | 面板内容四节；代码后置只有把点击转给 VM 的胶水 |
| `MainWindow.xaml` | 齿轮入口 + 遮罩 + `LayoutTransform` 缩放 + Esc |
| `Converters/Converters.cs` | `BoolToVis`（浮层显隐）与 `ZoomedSize`（缩放下的窗口下限） |
| `Services/SelfTest.cs` | 设置段 + 排印缩放段（下面"判据"里那几条红样例都出自它） |

**排印通路分五层，缺一条就有一处静默症状**：

1. 库兜底：`UI4Theme.WriteTokens` 随每份主题字典发布 `UI4.Font.Size.Base=15` / `Code=14` / `Family=Segoe UI`。**这层不能少**——控件模板引用的就是这些键，没人发布时所有 `UI4*` 控件静默掉到 WPF 裸默认 **12 px**，无编译错无异常。
2. 宿主覆盖：写在 `Application.Resources` 的**自有项**上（`Typography.Publish` 干这事，`App.ApplyDisplaySettings()` 在设置改动后再调一次）。这一层换档不会被冲掉（Step 2 的两层规则）。
3. 层级键：`App.Font.Size.Caption/Small/Medium/Lead/Icon` 由 `Typography.SizeOf(role, base)` 按固定差值派生（基准 15 时 = 11/12/13/14/16，各带下限）。**不要**把所有 `FontSize` 都绑成同一个基准——那会把层级抹平，11 px 的胶囊和 16 px 的标题变成同一个尺寸。
4. 固定件尺寸：`App.Size.RoundButton` / `App.Size.Chip` 与对应半径 `App.Radius.*`（半径必须是 `CornerRadius` 类型的资源）。见 Step 3 第 6 条。
5. 界面里不写字面字号：判据是 `grep -n 'FontSize="[0-9]' app/**/*.xaml` 输出为空（`MainWindow.xaml` 与 `Views/*.xaml`）。

**字号与缩放是两件事，别互相顶替**：

- 字号只带动**读排印键的那些东西**：库里恰好 6 处引用点（`UI4Button`/`UI4TextBox`/`UI4ComboBox`/`UI4ListBox`/`UI4PasswordBox` 读 `Base`，`UI4CodeEditor` 读 `Code`）。`UI4CheckBox.cs:165`、`UI4ListView.cs:256`、`UI4GridView.cs:231`、`UI4Menu`（13）等把字号写成了本地字面值，**既不跟键也不跟 `Window.FontSize` 继承**；要它们跟着就显式赋 `FontSize="{DynamicResource UI4.Font.Size.Base}"`（赋了即退订，之后不再跟键）。宿主自己的 `TextBlock` 一律绑层级键。
- 缩放是整棵子树：`<Grid.LayoutTransform><ScaleTransform ScaleX="{Binding ZoomFactor}" …>` 挂在根内容上。三条代价：① `LayoutTransform` 先把可用尺寸除以系数再交给子树，所以放大**不裁切**，只是窗里能看见的设计像素变少；② 窗口下限必须跟着长，否则"已经是最小尺寸"的窗口装不下变宽的内容——`MinWidth`/`MinHeight` 走 `ZoomedSize` 按 `设计值 × 系数` 折算并**钳到工作区**（不然 200% 档下下限会超过屏幕）；③ **WPF 的 `Popup` 住在自己的可视根里，不吃祖先 `LayoutTransform`**——`UI4ComboBox` 下拉与 `UI4ContextMenu` 右键菜单在 150%/200% 下仍按 100% 渲染（位置对、字不跟着大）。这是 WPF 机制不是库缺陷，默认**不补偿**，但要把这条写进交付文档的"已知边界"让用户自己判断观感。

**状态与落盘的四条规矩**：

1. 开合状态只有一处（`IsSettingsOpen`），入口按钮上不另存；关法三条（完成 / 点遮罩 / Esc），开着时遮罩盖住齿轮所以齿轮只负责开。
2. **只在关闭时落盘**。滑杆每动一格就 `Save()` 会把用户同时手改的其它键一起冲掉，也是无谓的磁盘 IO；`Save` 内部再比一次"序列化文本没变就不写"。
3. 默认值与区间只从 `Typography` 来：XAML 用 `x:Static` 引 `MinBaseSize`/`MaxZoomPercent`，「恢复默认」按钮只调 VM 的 `ResetTypography()` / `ResetZoom()`，代码后置里不写第二份常量（写了必然漂）。
4. 从文件读回的一切都要钳（12–28 / 50–200，`NaN`/`Infinity` 回默认，解析不了的**不覆盖**已生效值），枚举一律按**名**解析并拒绝数字串（`Enum.TryParse` 会把 `"1"` 静默变成某一档）。kv1 没有 schema：**新增设置键要同时改 `ToMap` / `ApplyMap` 与自检里的键名点名清单**，否则打错一个字母就是静默丢设置。

**字体下拉的两条硬约束**：候选表由纯函数生成且**出厂字体栈固定第 0 位**（`Fonts.SystemFontFamilies` 里没有那个复合串，而 ComboBox 挂了 `ItemsSource` 后把 `SelectedItem` 设成列表外的值会被**静默清空**——"恢复默认后下拉框不跟着变"就是这么来的）；候选项用 `string` 不用 `FontFamily`（相等判定精确，不依赖它对方括号复合族的 `Equals` 口径），选中出厂项时设置里存空串，让"没设置过"与"设成出厂值"是同一个事实。

**判据**：`--selftest` 全绿（模板实测 `67 PASS`、退出码 `0`）。这三条红样例是实测过的，别怀疑断言是空的：删掉库里 `res["UI4.Font.Size.Base"] = DefaultFontSizeBase;` → 退 `2`，`FAIL 库发布 UI4.Font.Size.Base 兜底值 [解析到 空]` + `FAIL 撤掉宿主覆盖后回到库默认`；把写侧键名 `baseFontSize` 改成 `baseFontsize` → 退 `2`，`FAIL kv1 往返后九项设置不变` + `FAIL kv1 写侧键名齐全 [缺 baseFontSize]`；在 `Typography.SizeOf` 开头 `return baseSize;` 抹平层级 → 退 `5`，五条 `FAIL 层级 X 在基准 15 下是 15` 逐个点名。

**只能目测的三件**（判据证不住观感，交付时要显式列给用户）：① 字号拉到 28 与缩放到 200% 时最窄一档布局不出现截断；② 非 100% 档下"弹层不跟着放大"能否接受；③ 重启后仍是上次选的档（`settings.json` 里那五项）。

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

脚本第 2 步就是门禁：`start "" /wait "%EXE%" --selftest`，退出码非 0 就报警告并指向 `%APPDATA%\<App>\selftest.txt`。模板自带的 `SelfTest.cs` 分三段、共 67 条断言（实测 `67 PASS` / 退出码 `0`）：

- **配色与令牌**七件容易回退的事：令牌总数仍是 38、三份内置定义逐令牌可取色、宿主单源色的对比度门槛、`Mix`/`IsDark` 的判据一致、**`Theme.Policy` 已显式决策（留 `TODO` 就红）**、**8 套预置逐套装字典后 38 个令牌的 `UI4.Color.*`/`UI4.Brush.*` 与三个别名键齐全**、**字典里的底色与 `Dark()` 定义同源**。后两条是"切主题某处不跟随"的机器版判据——键名写错在运行期不报错，只有这条断言会红。
- **设置通路（kv1）**：九项各给互不相同的哨兵值做往返（任何一侧键名打错就有一项回到默认值被抓）、写侧键名点名清单、含 `=`/反斜杠/中文/首尾空格/空串的取值往返、含 CR/LF 的值必须被拒（否则撑破行格式）、越界钳位、非法值不覆盖已生效值、`null`/空 map 不清现值。
- **排印与缩放**：库里发布没发布 `UI4.Font.*` 兜底值、宿主覆盖值换档后还在不在、`Publish` 写全每个层级键与固定件尺寸键（半径必须是 `CornerRadius` 类型）、字号阶梯在默认基准下逐值等于既有设计且单调、12–28 全区间圆钮与胶囊装得下字身、`ClampBase`/`ClampZoom` 边界、字体候选表首位、`ZoomedSizeConverter` 折算与工作区钳制（参数写错必须回 `UnsetValue`）、明暗策略与可选档自洽、**主窗口与设置面板两块 XAML 能构造**（只 `new` 不 `Show`，把"点开设置才炸"提前到门禁）。

改配色或改字号后先跑它再跑界面。任一段抛异常都要降级成一条 FAIL（退出码契约在任何情况下都得成立），全绿时报告尾带 `INFO` 行写实测阶梯与尺寸——文档里的数字抄它，别手抄。

## Step 7 — 界面自适应是编码期约束

写第一行 XAML 之前就要定下来，别等收尾补救。每条都给可查判据。

- **下限尺寸**：`MinWidth`/`MinHeight`（模板给 720×480）。判据：`Get-Process` 起来后把窗口拖到极限或直接 `SetWindowPos` 到 100×100，客户区仍不低于下限（WPF 由 `MinWidth` 保证，不需要代码兜）。**做了全局缩放就没有这个固定数**：下限按 `设计值 × 系数` 折算并钳到工作区（模板用 `ZoomedSize` 转换器），否则 200% 档下窗口下限会超过屏幕。
- **分档而不是写死**：多栏布局按 `ActualWidth` 分档（`VisualStateManager` 或 `SizeChanged` 里切 `Grid` 列数），不要在 XAML 里钉死像素宽。判据：窗口收到窄档时列数真的降下来。`UI4GridView` 自带按宽度算列数（`ComputedColumns`），别在它外面再算一遍。
- **DPI**：`app.manifest` 的 `PerMonitorV2` 声明别删——net10 的 WPF 仍按清单取 DPI 感知级别。判据：150%/200% 缩放下文字不发虚、窗口不糊。
- **字体**：出厂栈带中文回退（模板用 `Segoe UI, Microsoft YaHei UI, sans-serif`，从 `SystemFonts.MessageFontFamily` 起步也行），**字号不写字面值**——绑 Step 4.5 的层级键（`UI4.Font.Size.Base` / `App.Font.Size.*`），默认值与区间只从 `Helpers/Typography.cs` 来。判据两条：`grep -n 'FontSize="[0-9]' app/*.xaml app/Views/*.xaml` 输出为空；`--selftest` 的排印段全绿（含"默认基准下阶梯等于既有设计"）。
- **全局缩放**：`LayoutTransform` 挂根内容 + `ZoomedSize` 折算窗口下限（细节与三条代价见 Step 4.5）。判据：150% 档下不出现裁切、窗口拖不到比折算下限更小；弹层不跟缩放是**已知边界**，要写进交付文档而不是当场辩解。
- **窗口几何持久化要回正**：存过 `Left/Top/Width/Height` 的工程，恢复时用 `SystemParameters.VirtualScreen*` 判定是否仍在屏内（模板 `RestoreGeometry` 那一段），不在就不恢复、回 `CenterScreen`；最大化时存 `RestoreBounds` 而不是屏幕尺寸。判据：把窗口挪到 -24000,-24000 存下、拔掉外接屏再起，窗口必须可见（这条最容易在换显示器后炸）。
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

模板里已经写好判据的点：9 条 lib 警告（非增量口径）、`--selftest` 退出码 = 失败断言数（三段、实测 67 条 PASS，报告尾那行 `INFO` 就是字号阶梯与固定件尺寸的实测值——文档里要抄数字就抄它，别手抄）、两档产物各自只有 1 个 exe、`FileVersion` 守门、图标要取回内嵌位图逐像素比（体积相同完全正常，4286 B 对 4286 B 实测过）、以及"拿上一轮产物验本轮改动 = 假红"。第 2 节的窗口验收判据已经写成"齿轮 → 面板 → 每档改观感 + 字号带动整窗 + 缩放不裁切"，第 7 节注意事项里要把 Step 4.5 的**已知边界**（弹层不跟缩放）与第 4 挡的四项决策一并写上。第 5 节的图标命令写 `<skill>/scripts/make-icon.ps1`（本地生成、自带自证，输出那七行 `frame …` 就是实测输出）；`magick -define icon:auto-resize=…` 只在装了 ImageMagick 的机器上才写，没装就保持 `未验证` 并给原因，别照抄成已验证。

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
| 字号调大后圆钮里的字跑到右下、还被切一点 | 钉死 `Width=28 Height=28` × 库默认 `Padding=10,0,10,0` → 内容区只剩 8 px，字身装不下（**不是没居中**，加 `HorizontalContentAlignment` 无效） | `Padding="0"` + 尺寸随字号派生（`App.Size.RoundButton`/`App.Size.Chip`），见 Step 3 第 6 条 |
| 设置里点"恢复默认字体"后下拉框变空白 | ComboBox 挂了 `ItemsSource` 之后把 `SelectedItem` 设成列表外的值会被**静默清空**（出厂字体栈是复合串，`SystemFontFamilies` 里没有） | 候选表把出厂项固定第 0 位（`Typography.BuildFamilyChoices`），存空串表示出厂值 |
| 整窗字突然变小（像掉到 12 px）、没报错也没异常 | 库里 `UI4.Font.*` 的兜底值没发布（`-Refresh` 打过旧上游，或 `WriteTokens` 那三行被动过），或宿主把覆盖写进了共享字典**内部**被下次重写顶掉 | 跑 `--selftest` 排印段（会点名"库没有发布 UI4.Font.Size.Base 的兜底默认值"）；覆盖一律写 `Application.Resources` 自有项 |
| 200% 缩放下窗口拖不动 / 边框跑到屏外 | 窗口下限没跟着缩放系数长，或反过来下限超过工作区 | `MinWidth`/`MinHeight` 走 `ZoomedSize`（`设计值 × 系数` 并钳到工作区） |
| 缩放档下下拉列表和右键菜单的字不跟着大 | WPF 的 `Popup` 住在自己的可视根，**不吃祖先 `LayoutTransform`**（机制，不是疏忽） | 默认记成"已知边界"写进交付文档；要一致渲染得给弹层单独乘系数，属另做决策 |
| 某一栏设置改了重启就丢 | kv1 没有 schema：写侧与读侧键名不一致（`baseFontSize` vs `baseFontsize`）就静默丢 | 跑 `--selftest` 设置段（九项哨兵往返 + 写侧键名点名），新增键要三处同步改 |
| 设置面板点开没反应 / 只在点开时才崩 | 浮层的 `Visibility` 绑到了不存在的数据源，或面板 XAML 里 `x:Static`/转换器写错（这类只在构造那一刻炸） | `--selftest` 排印段末尾两条"主窗口 / 设置面板 XAML 可解析"已经把这一步提前到门禁 |

## Step 10 — 验收清单

窄改动只跑覆盖该面的那一挡；新建工程、改主题通路或改排印通路要跑到第 12 条。UIA 挡证不住颜色与字号，这些结论用截图或 `--selftest` 的 `INFO` 行。

1. `fetch-source.ps1` → `LOCAL 48/48 未联网` 或 `VERIFY ok 48/48`（种子完整才继续）。
2. `dotnet build app\<App>.csproj --no-incremental` → `0 个错误`，警告恰好是上游那 9 条（`CS0414`×5 + `CS1574`×4）。
3. `<App>.exe --selftest` → 退出码 0，`%APPDATA%\<App>\selftest.txt` 全 `PASS`（模板实测 67 条）。三段各自的判据见 Step 6：配色与令牌（含**明暗策略已显式决策**、**预置套装 × 38 令牌资源键齐全**、**字典与定义同源**）、设置通路（哨兵往返 / 键名点名 / 钳位）、排印与缩放（库兜底键 / 覆盖换档仍在 / 层级 / 固定件尺寸 / 转换器边界 / **两块 XAML 能构造**）。
4. 先删空 `publish\` 与 `publish_no_runtime\`，再跑两档 → 产物目录各自**只有一个 exe**，脚本内 `selftest exit code: 0`。
5. 守门：`(Get-Item <exe>).VersionInfo.FileVersion` 等于 csproj 的 `<Version>`（`0.1.0` → `0.1.0.0`）。不等就是在交旧构建。
6. 起**本轮**产物、按自己起的 pid 读 `MainWindowTitle` = 源码里的 `Title`；设置面板里每一档都改观感、原生 `TextBlock` 那行也要变（它证明宿主 `{DynamicResource}` 通路接通）、底部生效键从 `light` 变到 `dark`/`paper-grey`；收尾 `Stop-Process -Id`，残留 `Get-Process -Name <App>*` 数为 0。
7. 图标：`[System.Drawing.Icon]::ExtractAssociatedIcon(<exe>)` 取回内嵌位图，按图样的特征取色点逐像素命中（字节数与旧图标相同是正常的，体积不是判据）；生成式图标还要看 `make-icon.ps1` 自己那两条自证——目录帧数与 `frames=7` 对上、16/32 两档 `corner.alpha=0`（圆角透明没被 `<ApplicationIcon>` 吃掉）。
8. 标题栏底色/文字色随深浅变（Win10 只认深/浅标志，Win11 才染三色，属系统能力差异）。
9. 按 Step 9 收敛目录后，两条判据都过（删完中间物仍能一把重建、已打包 exe 不依赖它们）。
10. `doc/运行与构建（T0Level）.md` 八节齐全、`【模板】` 残留为 0、每条命令都贴了真实输出、未验证项都有原因（Step 8）。
11. 间距：Step 3 的静态扫描**输出为空**（`Margin/Padding` 全在 4 的倍数上），截图上相邻控件看得见留白、卡片投影四边完整、鼠标扫过卡片有放大反馈（没反馈＝`ItemMargin` 贴边，见故障速查）。
12. 设置面板（做了第 4 挡才要跑）：齿轮开得起来，完成 / 遮罩 / Esc 三种关法都关得掉；四节齐全且**改了就立即生效**（配色整窗连标题栏、字号带动层级与圆钮尺寸、缩放不裁切）；关掉面板后 `%APPDATA%\<App>\settings.json` 里看得见的值就是刚选的那套，重启仍是它；把字号拉到 28、缩放拉到 200% 看一遍截断与窗口下限，再回到默认。判据：`FontSize="[0-9]` 静态扫描为空 + 排印段全绿，其余目测（Step 4.5 末）。
13. 种子没被改动过：`fetch-source.ps1` 打 `LOCAL 48/48 种子完整，未联网`，且 `git status` 里 `assets/seed/lib` 干净。改过 `lib/` 就要走"源码来源与升级"的第 2 步重钉清单，否则下一个工程会被 sha 校验挡在退 5。

## 实测基准（单机口径，换机器请重测）

2026-10-03 本机（Windows 11 26100/26300 系、SDK `10.0.401`、`Microsoft.WindowsDesktop.App 10.0.12`，运行时包已在 NuGet 缓存里）：

| 项 | 数值 | 口径 |
| --- | --- | --- |
| `dotnet build`（全新工程） | 约 6.6 s | Debug，含 lib 首次编译 |
| `dotnet run` 到窗口出现 | 2,985 ms | Debug，进程内首帧 |
| `publish_no_runtime.cmd` | 3 s，`T0Probe_no_runtime.exe` 1,431,204 B | Release 单文件框架依赖 |
| `publish.cmd` | 9 s，`T0Probe_self_contained.exe` 70,288,063 B | Release 单文件自带运行时；**首次**跑还要下载运行时包（脚本自己提示约 150 MB），耗时不在此列 |
| 打包版起窗口 | 约 5 s 内出窗 | 已解包过的缓存态；首次要解到 `%TEMP%\.net\` |
| `--selftest` | 退出码 0 | Debug 与两档产物都跑过；报告行数见下（模板工程 68 行，示例项目1 是四段合成 `PASS 断言组=240`） |

2026-10-04 本机（同一台，SDK 与运行时同上；这轮改了 `lib/` 并给模板装了设置底座）：

| 项 | 数值 | 口径 |
| --- | --- | --- |
| `fetch-source.ps1`（默认挡） | `LOCAL 48/48 种子完整，未联网` | 种子自持基线，零请求 |
| `fetch-source.ps1 -Refresh` | `FAIL -Refresh 被挡…` 退 `2` | 缺 `-AllowUpstreamReset` 时的默认行为 |
| 同上加 `-AllowUpstreamReset` 指临时目录 | 通路 A `629,603 B` → 拷 `47/48` + raw 补 `12` → `FAIL 取回后校验不过，11 处` 退 `5` | 证两件事：通路可达，且旧上游确实与自持基线差那 11 个文件 |
| `scaffold.ps1` 新工程 | `占位符替换 22 个文件`、`还差 19 处【模板】`、图标 `frames=7` 104,448 B | `-Offline` 全程零联网 |
| 新工程 `dotnet build --no-incremental` | `0 个错误 / 9 个警告`，2.1–2.6 s | Debug；9 条仍是 lib 自带 |
| 新工程 `--selftest` | 退出码 `0`，`67 PASS` + 1 行 `INFO` | `Policy` 填 `both` 与 `light-only` 两种都跑过（后者同样 67 PASS） |
| 同样工程 `Policy = "TODO"` | 退出码 `1`，唯一一条 FAIL 是"明暗策略已显式决策" | 说明门禁只挡该挡住的，排印段不误红 |
| 示例项目1（含设置面板） | `--selftest` 退 `0`、`PASS 断言组=240`，build 2.4–3.8 s | 四段合成：设置 / 主题 / 数据 / 排印缩放 |

三条负向自证（区分性实验，证断言不是空的，见 Step 4.5 判据）：删库里排印兜底值 → 退 `2`；kv1 写侧键名打错 → 退 `2`；抹平字号层级 → 退 `5` 五条点名。三处都在临时探针工程里做，逐字节还原后复验 `0 错误 / 9 警告 / 67 PASS`。

引用这些数字时必须带上"哪一档、是否首次、Debug/Release"，否则会出现拿框架依赖小包比自带运行时大包的量级错误。

## Resources

- `references/component-manual.md` — 组件手册逐字（2165 行，v3.0.0；与 `assets/seed/lib/README.md` 同一份内容，blob sha `792f5ed8`）。**属性名、默认值、枚举成员、事件签名以它为准**，别凭记忆写。手册开头有一行指向仓库根 README 的 `../../README.md` 链接，那是上游仓库级文档，在工程内不可达（种子按原样供，不改写它的链接）。排印契约在 **§4.2.1**（三个键 + 谁在用 + 两层覆盖 + 本包相对上游的 4 处非排印改动），`UI4ListView` 的两个新描边 DP 在 **§3.7**。按需 grep，别整份读：

  ```bash
  grep -n '^## \|^### ' references/component-manual.md   # 全目录（§一 事实 / §二 清单 / §3.1-3.11 详解 / §四 主题 / §五 服务 / §六 配方 / §七 扩展 / §八 附录）
  sed -n '2039,2089p' references/component-manual.md     # §8.1 枚举全清单 / §8.2 事件全清单 / §8.3 不建议用的公开成员
  grep -n 'UI4PasswordBox' references/component-manual.md | head
  ```

- `references/controls.md` — 39 控件选型表、与原生类的继承关系差异、**不接主题的 19 个颜色 DP**、字号与字体族那 6 处引用点、逐条实测过的限制（含 `Popup` 不吃 `LayoutTransform`、`UI4ListBox` 触发器画刷走绑定）。
- `references/theming.md` — 38 令牌与资源键规则、**排印键 `UI4.Font.*` 与两层覆盖**、`UI4Theme`/`UI4ThemeScope`/`UI4ThemePacks` API、8 套预置键与场景、系统跟随与持久化、DWM 标题栏四条通路。
- `references/host-integration.md` — app 侧契约：csproj 双侧关键行、`app.manifest` DPI 逐字、**启动序列九步五条硬约束（含排印覆盖要在装字典之后）**、宿主配色单源→令牌的写法、设置底座六个文件的职责、自测契约（配色 / 设置 / 排印三段各钉什么）、图标三处通路。
- `assets/seed/lib/` — 组件源码整份自包含，**48 个文件** = 44 个 `.cs`（含 `Internal/` 6 个）+ `StartUI4Controls.csproj` + `LICENSE.txt` + `README.md`（组件手册，随工程进 `lib/`）+ `架构审计报告-3.0.0主题机制评审.md`（主题通路的实测数据）。**自 2026-10-04 起它是自持基线**：源自两个示例工程 `lib/` 的并集，比清单 `fetchedFrom` 那份上游目录（commit `5d96442a` 的 `componentSourceCode/StartUI4Controls`，main 上已删）多 11 个文件的有意改动。改它必须同步两个示例工程 + 重钉清单 + 回灌 `references/`（见"源码来源与升级"）。
- `assets/seed/manifest.json` — 上述 48 条 `path + blobSha + size` 与内容计数基线（`.cs` 44 / 令牌 38 / **DP 237**），回源与校验都以它为准；`upstream.baseline` 段记着自持基线的来源、偏离清单与放行条件。
- `assets/templates/app/`、`assets/templates/root/` — 工程模板，`__APPNAME__` 为占位符（`.md` 也参与替换）。配色侧：`Helpers/Theme.cs`（单源 + **必须显式决策的 `Policy`**）。设置侧（Step 4.5）：`Helpers/Typography.cs`、`Services/SettingsService.cs` + `SettingsCodec.cs`、`Services/ThemeService.cs`、`ViewModels/MainViewModel.cs`、`Views/SettingsOverlay.xaml(.cs)`、`Converters/Converters.cs`、`MainWindow.xaml` 的齿轮 + 遮罩 + `LayoutTransform` + Esc。门禁侧：`Services/SelfTest.cs`（三段、实测 67 条）。app csproj（`<Version>0.1.0</Version>` 是 FileVersion 守门的基准）、`root/doc/运行与构建（T0Level）.md`（Step 8 的八节骨架，随 scaffold 落进工程，`【模板】` 标记为待填位）。
- `scripts/scaffold.ps1` — Step 1 的生成器（已实测：空种子 → 自动回源 → 生成 → `dotnet build` 0 错误 9 条 lib 警告 → `--selftest` 退 0 共 67 条 PASS；种子完整时加 `-Offline` 零联网。产物点名清单含设置底座那 8 个文件，缺一项退 6）。
- `scripts/make-icon.ps1` — 应用图标生成器（Step 7）。`-Name` 取首字母、`-Glyph` 显式指定，`-Back`/`-Fore` 换色，`-Sizes` 换尺寸集；输出 16/24/32/48/64/128/256 七帧 32bpp ICO（<256 走 DIB + AND 掩码，256 走内嵌 PNG），写完自己回读目录与 16/32 两档像素做自证。已实测：脚手架产出的工程 `dotnet build` 后 `ExtractAssociatedIcon` 取回 32×32，圆角 `alpha=0`、字形与底色取色命中。
- `scripts/fetch-source.ps1` — 种子的取源器（三挡 + 四条通路 + blob sha 校验，已实测 A 通路 629,603 B 取齐 48 个文件、第二次跑命中本地不发请求）。`-Refresh` 默认被自持基线闸门挡下（退 2），`-AllowUpstreamReset` 才放行。

## 源码来源与升级

**2026-10-04 起方向反过来了**：Skill 里这份 `assets/seed/lib` 就是基线本身，而它的改动来源是仓库内两个示例工程的 `lib/`（并集）。清单 `fetchedFrom` 那份上游目录（`WinApp-Skills@5d96442` 的 `componentSourceCode/StartUI4Controls`）已在 main 上删除，且组件仓库 `StartUI4.WPF@dotnet_10`（实测 tip `e15dd51`）也还没有排印键——所以 `fetch-source.ps1 -Refresh` 默认被挡（退 2），别再把它当"上游改版后用这挡刷新"用。分三种情况：

1. **工程里改了 `lib/`**（最常见，含示例工程与用户自己的工程）：把改动**回灌进种子**，然后一条链走完——
   ```text
   改动的 .cs / README.md → assets/seed/lib（种子存 LF，显式转换：perl -pe 's/\n/\r\n/g' 反向用 sed 's/\r$//'）
   → make-manifest.ps1（重钉 blob sha 与 expected 计数，它会报"多少个文件、令牌几个、DP 几条"）
   → fetch-source.ps1（默认挡，应打 LOCAL 48/48）
   → 同步 references/：component-manual.md ← lib/README.md 逐字；controls.md/theming.md 里被改动的表（令牌数、DP 数、套装键、不接主题的 DP 清单）
   → 反向回灌两个示例工程的 lib/，用 diff -rq --strip-trailing-cr 确认与种子零偏离
   → 两个工程各 dotnet build --no-incremental（0 错误 / 9 警告）+ --selftest（退 0）
   ```
   偏离要**记账**：改了什么、为什么、涉及几个文件，写进清单的 `upstream.baseline`（`divergentFiles` / `divergentList`）与工程侧的归因文档，别让下一轮靠版本号猜——同一份 `3.0.0` 可以挂两套源码。
2. **上游组件仓库真改版了**：先逐文件比"上游 vs 种子"，决定每一条并到哪边（弃的要有可复现的理由），再 `make-manifest.ps1 -Repo MagicFollower/StartUI4.WPF -Branch dotnet_10 -Commit <40 位> -PathPrefix src/StartUI4Controls` 把 `fetchedFrom` 重钉到那份**存在**的目录，并把 `upstream.baseline` 的偏离清单改小（并光了就把 `selfHosted` 去掉，闸门自动放行）。
3. **离线机器或三条通路全败**：从任一已知良好的工程的 `lib/` 整份拷进种子（48 个文件全要，含 `Internal/` 与两份文档），再走第 1 条的"重钉之后"那几步。

三条口径别踩：① `blobSha` 是 **LF 归一后**的 git blob sha，磁盘存 CRLF 还是 LF 都不影响它，但**行尾转换脚本会把"零偏离"变成假偏离**——`sed 's/$/\r/'` 会在末尾没有换行的文件尾多塞一个裸 CR（实测过），要用 `perl -pe 's/\n/\r\n/g'`；② `scaffold.ps1` 的 `required` 清单点名了 `README.md` 与《架构审计报告-3.0.0主题机制评审.md》，文件数判据取自 `manifest.expected.totalFiles`——**改了文件集却不重钉清单，新工程会被完整性检查挡在退 4，内容不符则退 5**；③ 三个内容计数（`.cs` / 令牌 / DP 声明）与清单不符只 `WARN` 不拦，但它就是"该重钉了"的信号（本轮 235 → 237 是 `UI4ListView` 加了两个描边 DP）。

最后重跑 Step 1 → Step 7 全链（含一次 `dotnet build`、一次 `--selftest`，以及一次"种子完整时 `-Offline` 零联网"的确认）。
