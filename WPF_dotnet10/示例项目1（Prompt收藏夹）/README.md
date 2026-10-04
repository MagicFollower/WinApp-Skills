# Prompt 收藏夹（WPF 示例项目）

> 此处原有一张界面截图 `PixPin_2026-10-01_13-11-28.png`，**未随本包交付**（2026-10-04 核对，目录内不存在，见《[问题归因与基线核对（2026-10-04）.md](问题归因与基线核对（2026-10-04）.md)》§四）。

## 构建与打包

`app/` 是应用，`lib/` 是 StartUI4Controls 控件库源码（net10.0-windows）。**2026-10-04 已把 `lib/` 逐文件对齐到本包内的基准《组件源码与组件使用说明/StartUI4Controls》**（44 个 `.cs` + csproj + LICENSE + 组件手册 README + 架构审计报告，忽略行尾 CR 后零差异；此前 `lib/` 落后两处内容、缺一份文档，详见《[问题归因与基线核对（2026-10-04）.md](问题归因与基线核对（2026-10-04）.md)》§二）。应用与库都跑在 **.NET 10** 上（`net48 → net10.0-windows` 的迁移已完成，见下）。

### 开发构建

```
cd app
dotnet build PromptFavorites.csproj
dotnet run --project PromptFavorites.csproj
```

只构建应用不会要求本机装 win-x64 运行时包——打包开关都挂在 `Condition="'$(PublishSingleFile)' == 'true'"` 上。

### 一键打包单 exe：两个脚本，两种产物

| 双击这个 | 产物 | 实测体积 | 目标机要求 |
|---|---|---|---|
| `publish.cmd` | `publish\PromptFavorites_self_contained.exe` | 70,576,670 字节（约 67 MB） | **什么都不用装**，运行时打进包里 |
| `publish_no_runtime.cmd` | `publish_no_runtime\PromptFavorites_no_runtime.exe` | 2,472,148 字节（约 2.4 MB） | 必须已装 .NET 10 **Desktop** Runtime（`dotnet --list-runtimes` 里要有 `Microsoft.WindowsDesktop.App 10.x`） |

两个产物都是单文件。手动执行等价于：

```
dotnet publish app\PromptFavorites.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:ArtifactLabel=self_contained `
  -p:DebugType=embedded -p:GenerateDocumentationFile=false -o publish

dotnet publish app\PromptFavorites.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:ArtifactLabel=no_runtime `
  -p:DebugType=embedded -p:GenerateDocumentationFile=false -o publish_no_runtime
```

- 文件名后缀由 `-p:ArtifactLabel=` 驱动（csproj 里拼进 `AssemblyName`），所以两种 exe 不会互相覆盖，也能一眼分辨带没带运行时。数据根和 `%APPDATA%\PromptFavorites` 设置目录用的是字面量，不跟着程序集名变。
- 自包含那次首次打包要联网从 nuget.org 下载 `Microsoft.WindowsDesktop.App.Runtime.win-x64`（约 150 MB）；本机若没装该运行时包，离线状态下这一步会失败。框架依赖那次不需要运行时包。
- `DebugType=embedded` 与 `GenerateDocumentationFile=false` 是**全局属性**，会一并作用到被引用的 `lib` 工程：pdb 内嵌进 dll、不再生成文档 xml，所以输出目录里只剩一个 exe，同时库内异常堆栈仍带行号。
- 自包含单文件里的 WPF 原生库（`PresentationNative_cor3` / `wpfgfx_cor3` / `D3DCompiler_47_cor3` 等）没法从包内直接加载，靠 `IncludeNativeLibrariesForSelfExtract` 在首次启动时解到 `%TEMP%\.net\PromptFavorites_self_contained\`（实测约 7.9 MB），所以第一次启动略慢。框架依赖版**什么都不解**——原生库本来就在共享运行时目录里（实测 `%TEMP%\.net\` 下不会多出目录）。
- 单文件压缩只对自包含合法：`EnableCompressionInSingleFile` 在 csproj 里按 `SelfContained` 分档，框架依赖强开会报 `NETSDK1176`。这也是 2.4 MB 那版没有再压缩的原因。
- 去掉 `UseWindowsForms`**不会让包体变小**：`Microsoft.WindowsDesktop.App.Runtime.win-x64` 是 WPF + WinForms 一体的单体运行时包，实测自包含 exe 里仍含 `System.Windows.Forms.dll` / `System.Drawing.dll` / `Microsoft.VisualBasic.Forms.dll`。这一项的收益是应用不再依赖 WinForms、也不再引入第二条消息泵，与体积无关。

### 自测

```
publish\PromptFavorites_self_contained.exe --selftest
publish_no_runtime\PromptFavorites_no_runtime.exe --selftest
echo $LASTEXITCODE      # 失败断言数，0 = 全通过
```

两个产物实测都是退出码 0。退出码即结论。2026-10-04 起 `--selftest` 是**四段合成**（`Services/SelfTest.cs`），实测 `PASS 断言组=236`：

| 段 | 覆盖 | 抓的是什么 |
|---|---|---|
| `SettingsSelfTest` | kv1 往返（10 个 hostile 值）、5 轮体积不增、旧 JSON 还原、分隔符折叠、非法几何值拒绝、枚举按键名解析与 kv1 往返、自定义顺序编解码不变量、**排印三键（`fontFamilyName`/`baseFontSize`/`zoomPercent`）写读键名一致 + 往返 + 越界钳位 + 非法值不覆盖已生效值** | 当年 16 MB 设置膨胀那类**指数放大**；`themeMode`/`sortMode` 被数字串蒙过；以及 kv1 没有 schema 时**任一侧键名打错一个字母就静默丢设置** |
| `ThemeSelfTest` | 令牌总数仍为 38、三份内置定义逐令牌齐全、明暗两档宿主色表各覆盖 38 个、应用后 `UI4.Color.*`/`UI4.Brush.*` 38×2 + 3 个别名与色表同源、7 项对比度门槛、深浅判据与库同式 | **写错资源键不报错也不抛异常**这条库特性——只有这段能把它抓出来 |
| `DataSelfTest` | frontmatter 已知字段往返、无 frontmatter 兜底、正文中 `---` 不误判、**未知键保留**、含冒号的值不被切坏；仓储在临时目录里的建模块/建条目/覆写不残留旧尾巴/改名撞目标不静默覆盖/跨模块移动/收藏与复制的时间戳口径/写盘不留 `.tmp`；`PromptModule` 的**变更通知** | 磁盘链路与外部编辑器共存的数据安全；以及"属性没有 `PropertyChanged`，任何一次整表重建都会让它看起来是好的"这类只在增量刷新时暴露的缺陷 |
| `DisplaySelfTest` | 库是否发布了 `UI4.Font.*` 三个兜底键、宿主在应用资源根上的覆盖值**换档后仍然有效**、`Typography.Publish` 把每个层级键与每个固定件尺寸键都写进去了（半径必须是 `CornerRadius` 类型，`DynamicResource` 不做类型转换）、字号阶梯在默认基准下逐值等于既有设计（11/12/13/14/16）、层级单调性与下限、**圆钮直径与胶囊高度在默认基准下仍是 28 / 24，且在 12–28 全区间都装得下对应字身（≥ 字身 × 1.3）**、`ClampBase`/`ClampZoom` 边界、`ZoomedSizeConverter` 的折算与工作区上限（参数写错必须回 `UnsetValue`）、主窗口与设置面板两块 XAML 能解析（只构造不 Show） | 排印与缩放这条通路全程都是运行期字符串，错一个字符没有编译错也没有异常，只表现为"改了没反应"；面板类 XAML 的解析错误原本只有"点开设置"那一刻才暴露；固定件尺寸那条对着的是"字体放大后 + 号内容偏到右下" |

任一段抛异常会被记成一条 FAIL（不再让进程带堆栈退出），所以"退出码＝失败断言数"这条契约在任何情况下都成立。

判据非空洞已经实测过五次：删掉夜景色表里的 `OnAccent` 一行 → `FAIL [theme] 夜景宿主色表未覆盖全部 38 个令牌：缺 OnAccent`，退出码 1；浅色档占位符原本对输入底只有 **2.94:1**，卡在 3:1 门槛下被这段抓出来，向正文混的比例从 30% 提到 38% 后到 **3.48:1**；第三批又做了三次——把写侧键名 `baseFontSize` 改成 `baseFontsize` → 退出码 `2`（两条 FAIL 点名键名不一致与"值变成 0"），删掉库里 `res["UI4.Font.Size.Base"] = DefaultFontSizeBase;` 这一行 → 退出码 `2`（`库没有发布 UI4.Font.Size.Base 的兜底默认值，解析到 空`，正是分叉版踩过的那个洞），在 `Typography.SizeOf` 开头插一句 `return baseSize;` 抹平层级 → 退出码 `6`，六条 FAIL 逐个层级点名。三处都按备份逐字节回滚并用 `diff` 确认无输出。

报告同时写 `%APPDATA%\PromptFavorites\selftest.txt`——刻意不写 exe 旁边，因为单文件下 `AppDomain.CurrentDomain.BaseDirectory` 可能指向会被清理的临时解包目录。全绿时报告末尾还带两行 `INFO`，是明暗两档各项对比度的**实测值**（文档里的对比度表就抄这里，免得数字再漂）。

### 标题与图标

窗口标题在 `MainWindow.xaml` 的 `Title`（当前为「收藏夹」）；应用图标是 `app\AppIcon.ico`（实测 372,526 字节 / 7 挡：16/24/32/48/64/128/256，末挡正好收到文件尾），由 `tools\make-icon.ps1` 用 GDI+ 矢量图元手绘生成 —— **该脚本与 `tools\verify-icon.ps1` 都没随本包交付**，所以"改图标设计重跑脚本"这条路当前不可复现，只能整体替换 `app\AppIcon.ico`（图标文件本身在，构建与两处引用都正常）。改法、exe 图标与窗口图标两条链路的区别、以及运行时怎么验，见 [标题与图标设置教程.md](标题与图标设置教程.md)。

### 配色

界面是**明暗双档**，同一套「终端靛」的两个亮度方向：

- **浅色（终端靛）**：底 `#f9fbff`、正文 `#1b2737`、主色 `#4f6be8`、强调 `#0c8ba8`、边框 `#bcc7d6`、选中行 `#e2e5fb`。源色取自 [100-themes](https://github.com/MagicFollower/100-themes) 的 `terminal-blue/day/colors.toml`，按"浅亮、低干扰"重调。
- **夜景（终端靛·夜）**：底 `#0e1622`、面板 `#1c232f`、正文 `#e8eef8`、主色 `#7c93ff`、强调 `#2bb6d4`、边框 `#33415a`、选中行 `#263152`、中性按钮底 `#282f3b`。同一支靛蓝往**亮**走而不是换色相；因为主色变亮了，压在主色上的字改成深墨 `#0b1220`（白字压 `#7c93ff` 只有 2.67:1，深墨压同一色有 6.66:1）。

两档**手动切换**：入口在**设置面板**里（顶部搜索栏右端的齿轮 → 「配色」一节那个按钮，按钮文案写的是要点过去哪一档，当前档由 `themeMode` 单源决定）。档位在关窗后记住（`settings.json` 的 `themeMode` 键，文件名是历史遗留、内容是 kv1 纯文本）。第三批之前它曾是搜索栏上一个独立的「明/夜」按钮，同一状态有两个入口会各存一份观感，现在收敛成面板里一处。**刻意不做"跟随系统"**——`Models/AppThemeMode.cs` 里连 `System` 成员都没有，这样设置文件不可能存进一个没人消费的取值，也就不会去挂 `SystemEvents` 订阅。

对比度门槛由 `--selftest` 量，下表是它今天写进报告的实测值：

| 关系 | 浅色 | 夜景 | 门槛 |
|---|---|---|---|
| 正文 / 底 | 14.56:1 | 15.58:1 | ≥7 |
| 次级文本 / 底 | 5.73:1 | 9.72:1 | ≥4.5 |
| 按钮字 / 主色（填充按钮） | 4.56:1（白字胜出，正文只有 3.31:1） | 6.66:1（深墨胜出，正文只有 2.41:1） | ≥4.5 |
| 按钮字 / 中性按钮底 | 12.13:1 | 11.55:1 | ≥4.5 |
| 正文 / 选中行 | 12.09:1 | 10.96:1 | ≥4.5 |
| 占位符 / 输入底 | 3.48:1 | 3.89:1 | ≥3 |
| 图标 / 底 | 5.73:1 | 7.06:1 | ≥3 |

**改配色只动一处**：`Helpers/Theme.cs` 的源色常量。`Helpers/HostPalette.cs` 把它派生成**每档 38 个令牌**的完整色表（`Tokens(Light)` / `Tokens(Dark)`），`Apply()` 把两档都注册进库再切到当前档；`App.xaml` 里只剩两把语义色刷子（收藏金），其值用 `x:Static` 指向 `Theme.cs`，不再重抄十六进制。曾经的三条并行通路（`{x:Static h:Theme.*}` 常量、手抄的 `App.Brush.*`、令牌 `{DynamicResource UI4.Brush.*}`）已收敛：**XAML 与代码里所有取色都走 `UI4.Color.*` / `UI4.Brush.*` 资源引用**，代码后置的按钮态刷色改用 `SetResourceReference`（`Views/*.xaml.cs` 的 `PaintChip`）。

四条约束与机制（2026-10-04 按对齐后的 v3.0.0 源码复核）：

- **`UI4Button` 的字色不是按亮度阈值挑的**：`lib/UI4Button.cs:224` 的 `ForegroundFor()` 在 `OnAccent` 与正文色之间**取与底色对比度更高的那个**（旧文档写的"Luminance < 0.45 给白字"是早期实现，已不成立）。所以浅色档主色上白字、夜景档主色上深墨，都是它自己选出来的——前提是**宿主不要本地赋 `Foreground`**：本地赋值占的是本地值槽，会把这条自动判据顶掉（`Brushes.White/Black` 那几处就是这么坏的，现已改为 `ClearValue`）。
- **中性按钮（收藏/复制/保存/取消）的底色走 `UI4.Color.OffBackground`**，两档都保持在浅/深两端，不落进"中底配白字"的不可读区。
- **`UI4ListBox` 的选中行不参与主题同步**：`PressedBackground`/`PressedForeground` 是硬编码默认值，所以两个列表上显式绑了 `UI4.Color.RowSelectedBackground` / `UI4.Color.TextForeground`；`TextColor`/`BorderNormalColor`/`PanelBackground`/`HoverBackground` 由库自动跟随，不用管。
- **两档都要注册**：只注册当前档的话，切到另一档时库会回落到它自己的内置定义（夜景就变成库的默认蓝黑了）。

收藏星标的金色是语义色，不跟界面配色走，两档同一个金。顶部 Toast 那枚"反色胶囊"由 `HostPalette.PublishSemanticBrushes` 按当前档派生 `App.Brush.Toast` / `App.Brush.ToastText`（底 = 正文向底混 8%，字取反方向），也不手抄色值。

### 设置

入口是搜索栏右端的齿轮按钮（`Views/SettingsOverlay.xaml`），关闭有三条：面板里的「完成」、点遮罩、按 Esc（开着时遮罩盖住工具栏，齿轮点不到，所以它只负责开）。**开合状态只有 `MainViewModel.IsSettingsOpen` 一处**，按钮上不另存一份；设置在这面关掉时才落盘一次——滑杆每动一格就写盘会把用户同时手改的其它键一起冲掉，也是无谓的磁盘 IO。

面板四节：**应用信息**（应用版本 / 运行时 / 组件库 / 数据根目录 / 设置目录，全部只读；前三项由反射取，`AppVersion` 就是 csproj 的 `<Version>1.0.0</Version>`，`RuntimeVersion` 显示 `RuntimeInformation.FrameworkDescription`）、**配色**、**字体**、**全局缩放**。后两节的默认值与区间只从 `Helpers/Typography.cs` 来（XAML 里用 `x:Static` 引 `MinBaseSize`/`MaxBaseSize`/`MinZoomPercent`/`MaxZoomPercent`），「恢复默认」两个按钮只调 `ResetTypography()` / `ResetZoom()`，view 的代码后置里不写第二份常量。

排印通路是这一节的核心，也是与 `-增加了设置` 那个分叉版分歧最大的地方：

- **库里新增 3 个排印键**，由 `UI4Theme.WriteTokens` 随每份主题字典一起发布：`UI4.Font.Size.Base`（`15`）、`UI4.Font.Size.Code`（`14`）、`UI4.Font.Family`（`Segoe UI`）。有兜底值这条很要紧——库内控件模板引用的就是这些键，宿主没发布覆盖值时它们必须解析得到，否则所有 `UI4*` 控件静默掉到 WPF 裸默认 12 px。
- **6 处引用点**（本包对 `lib/` 的唯一代码改动）：`UI4Button.cs:137`（样式 Setter）、`UI4TextBox.cs:161`、`UI4ComboBox.cs:193`、`UI4ListBox.cs:239`、`UI4PasswordBox.cs:237`、`UI4CodeEditor.cs:37`。上游那份是字面常量。
- **宿主覆盖写在应用资源根**（`Typography.Publish`），一次写全 7 个键：`UI4.Font.Size.Base`/`Code` + `App.Font.Size.Caption`/`Small`/`Medium`/`Lead`/`Icon`。换主题**不会**把覆盖值冲掉——库的值住在 `Application.Resources.MergedDictionaries` 里那份共享字典，宿主写的是 `Application.Resources` 的**自有项**，同一层自有项优先于 MergedDictionaries。这条是 `DisplaySelfTest` 里的实测断言（插个 20 再切档，解析到的仍是 20），不是从文档推的。
- **字号是层级不是档位**：基准 15 时阶梯 = 胶囊 11 / 标签 12 / 次要 13 / 正文 15 / 提示与代码 14 / 图标 16，逐值等于改成资源键之前的字面数字（`DisplaySelfTest` 钉住"这次替换是平移、不是改设计"）。分叉版把每个 `FontSize` 都绑成同一个 `UI4.Font.Size.Base`，那是把层级抹平——所以它在默认档下会把 11 px 的胶囊和 16 px 的标题变成同一个尺寸。
- **界面里已经没有字面字号**：`MainWindow.xaml` 与 `Views/*.xaml` 的 `FontSize` 全是资源引用；正文编辑区显式解绑，交回库自己的 `UI4.Font.Size.Code` 引用。**固定件尺寸同理**——圆钮直径/胶囊高度与它们的圆角半径也走资源键（`App.Size.RoundButton` / `App.Radius.RoundButton` / `App.Size.Chip` / `App.Radius.Chip`，由 `Typography` 按基准派生），因为钉死 28×28 又留着库默认 `Padding=10,0,10,0` 的按钮，在字号一大之后会把内容挤成"偏到右下"（第四批修的正是这个，见文末）。
- **区间钳位**：字号 12–28（下限 12，再小中文正文不可读）、缩放 50–200。设置文件是用户能自己开编辑器改的，`500`、`-1`、`abc`、`NaN` 都进过断言：越界钳到边界，NaN/Infinity 回默认，解析不了的**不覆盖**已生效值。

**全局缩放**挂在根内容的 `LayoutTransform`（`ScaleTransform`，绑定 `ZoomFactor`）上，搜索栏、三栏、Toast、设置浮层一起缩放。选 `LayoutTransform` 而不是 `RenderTransform`：它先把可用尺寸除以系数交给子树再变换，所以放大后**不会裁切**，只是窗里能看见的设计像素变少——代价是窗口的"最小尺寸"要跟着长，于是 `MinWidth`/`MinHeight` 用 `ZoomedSizeConverter` 按 `900×系数` / `500×系数` 折算并钳到工作区（不然 200% 档下窗口下限会超过屏幕）。

一条已知边界留给使用者判断：**WPF 的 Popup 住在自己的可视根里，不吃祖先的 `LayoutTransform`**，所以非 100% 档下 `UI4ComboBox` 的下拉列表与 `UI4ContextMenu` 的右键菜单内容仍按 100% 渲染（位置对、字号不跟着放大）。这是 WPF 的机制而非本项目的疏忽，没有做补偿；观感是否可接受请按 150%/200% 各看一眼再定。



### 数据与设置位置

| 内容 | 位置 |
|---|---|
| Prompt 数据根 | `文档\Prompts`（可在界面里换，写回设置） |
| 应用设置 | `%APPDATA%\PromptFavorites\settings.json`（kv1 纯文本，兼容遗留 JSON；含 `themeMode` 明暗档与 `fontFamilyName`/`baseFontSize`/`zoomPercent` 三个排印键） |
| 自测报告 | `%APPDATA%\PromptFavorites\selftest.txt` |

三者都与 exe 放在哪里无关，所以单文件 exe 可以任意拷贝、任意目录运行。

### net48 → net10 迁移改了什么

| 项 | 变化 |
|---|---|
| `PromptFavorites.csproj` | `net48 → net10.0-windows`；`LangVersion 7.3 → latest`；去掉 `UseWindowsForms`；新增 `ApplicationManifest` 与条件化单文件发布属性组 |
| `app.manifest` | 新增，`PerMonitorV2` DPI 感知（与 `StartUI4Demo` 同款） |
| 目录选择器 | `System.Windows.Forms.FolderBrowserDialog` → WPF 原生 `Microsoft.Win32.OpenFolderDialog`（两处：`App.PromptForRootPath`、`MainWindow.SelectNewFolder`）。这是本次唯一的用户可见变化：换成 Vista 风格的 IFileDialog |
| `SettingsSelfTest` | 自测报告改落 `%APPDATA%`，退出码逻辑未动 |
| `RootPathResolver.MaxRootLength` | **取值仍是 240**，只更新了注释：net10 运行时本身已不受 260 限制，但这是既有校验规则，放开要连同清单里的 `longPathAware` 一起决策 |
| 控件调用 | 一行未改。迁移时清点的是 5 个控件（`UI4Button` / `UI4CodeEditor` / `UI4ComboBox` / `UI4ListBox` / `UI4TextBox`）与 `UI4MessageBox.Show`、`UI4Clipboard.TrySetTextAsync`，签名在 net10 库里原样存在。**2026-10-04 复核：应用实际用到 7 个 UI4 控件**，上面那行漏了 `UI4ContextMenu`（`Views/*.xaml.cs` 三处右键菜单）与 `UI4MessageBox`（含 `UI4MessageBoxButtons`），这两者的签名同样原样存在 |

## 基线与口径修订记录（2026-10-04）

本轮只做两件事：**把 `lib/` 对齐到本包内的组件源码基准**，**把文档里滞后的口径改成实测值**。`app/` 的代码与交互一行未动。归因过程与"问题+答案"归档在《[问题归因与基线核对（2026-10-04）.md](问题归因与基线核对（2026-10-04）.md)》。

### 做了什么

| 动作 | 内容 | 判据（本机实测） |
|---|---|---|
| `lib/UI4ThemePacks.cs` 覆盖 | 取基准版：`PaperWhite`/`PaperGrey` 两段调色值按 100-themes 的 polaroid/tundra 重调 + 注释。两边公开成员签名完全一致，无 API 变化 | 归一 LF 后 sha1 `0cd61f7c984e78bbf1e09f7721e3e7fa45809cf3` 与基准相同 |
| `lib/README.md` 覆盖 | 旧文件其实是**619 行的架构审计报告**，不是手册；换成基准那份 2128 行《StartUI4Controls 组件手册》 | sha1 `c66f3529c717e031ebd2e09a3353313278051b65` 相同 |
| 补 `lib/架构审计报告-3.0.0主题机制评审.md` | 基准里有、`lib/` 里缺 | sha1 `60e735d3d4270a3705bb8406987b51d1fbefdd9f` 相同 |

对齐后 `diff -rq --strip-trailing-cr lib/ ../组件源码与组件使用说明/StartUI4Controls` 无输出（除 `lib/bin`、`lib/obj` 这两个构建中间目录）；`dotnet build app/PromptFavorites.csproj --no-incremental` → `0 个错误`、`9 个警告`（全部是上游 lib 的 `CS0414`×5 + `CS1574`×4）、4.91 s；`app/bin/Debug/net10.0-windows/PromptFavorites.exe --selftest` → 退出码 `0`，报告时间戳 2026-10-04 14:18:25、`PASS 断言组=29`。

### 一处需要你知道的连带影响

`publish/PromptFavorites_self_contained.exe`（2026-10-02 17:06，70,593,607 字节）是**对齐前的 `lib/` 打出来的**。行为等价有实测依据：本次唯一的代码差异只在 `UI4ThemePacks` 的两段套装调色值，而应用从不消费套装（`app/` 里与主题相关的调用只有 `App.xaml.cs:122` 的 `UI4Theme.Register(def)` 与 `:32` 的 `SetTheme(UI4ThemeMode.Light)`，没有 `UI4ThemePacks.RegisterAll` / `Apply` / `SetAccent` / `UI4ThemeScope`）。要严格按当前源码重打，就跑 `publish_no_runtime.cmd`（小、快）或 `publish.cmd`；重打前先删空 `publish*/`，别拿旧产物验本轮改动。

### 本文被纠正的旧口径

| 位置 | 旧说法 | 实测 |
|---|---|---|
| 配色节 | 派生全部 **30** 个 `UI4ThemeToken` | 库有 **38** 个，`RegisterAppTheme()` 显式覆盖 **27** 个，其余 11 个继承库内置 `Light()` |
| 自测节 | **23** 组断言 | `PASS 断言组=29`，且组数是 `hostile.Length*2+9` 现算的，不是常数 |
| 迁移表 | 用到 **5** 个控件 | 7 个（补 `UI4ContextMenu`、`UI4MessageBox`） |
| 标题与图标节 | 图标由 `tools\make-icon.ps1` 生成 | 脚本与 `tools\verify-icon.ps1` **都未随包交付**；`app\AppIcon.ico` 本身完好（7 挡、末帧收到文件尾） |
| 首行 | 内嵌截图 `PixPin_2026-10-01_13-11-28.png` | 文件不在包内，已改为显式标注 |
| 开头 | `lib/` 与主仓库 `src/StartUI4Controls` 同一份 | 曾经落后两处；现已对齐到包内基准，基准身份见归因文档 §一 |

## 本轮之后又做了什么（2026-10-04 第二批）

钉完基线之后接着做了一轮实现改动，顺序是**先加判据、再改实现**（判据先行才会出现"先红后绿"的证据，而不是改完没人能验）：

| 项 | 内容 | 实测 |
|---|---|---|
| 判据扩面 | `--selftest` 从"设置专项"三段合成到设置 + 主题 + 数据 | `PASS 断言组=111`，两档产物退出码都是 0；`dotnet build --no-incremental` → `0 个错误 / 9 个警告`（上游自带） |
| 判据自证 | 故意删掉夜景色表的一个令牌 | `FAIL [theme] 夜景宿主色表未覆盖全部 38 个令牌：缺 OnAccent`，退出码 1；已按备份逐字节回滚（`diff` 无输出） |
| 抓到的真问题 | 浅色档占位符对输入底 2.94:1，低于 3:1 门槛 | 向正文混 30% → 38%，现在 3.48:1 |
| 写盘原子化 | `File.WriteAllText` 截断写 → 同目录临时文件 + `File.Replace`/`Move`；改名与跨模块移动先判目标存在，冲突抛 `IOException` | `DataSelfTest` 里"覆写更短正文不残留旧尾巴""撞目标不静默覆盖""写后不留 `.tmp`"三条断言通过 |
| frontmatter 保真 | 未知字段读时收下、写时原样带回；`SaveEntry` 先从磁盘继承再落盘 | 之前"每次保存静默删掉外部工具加的字段"的三条断言由红转绿 |
| 明暗双档 | `HostPalette` 每档显式给满 38 个令牌（补齐原先静默继承库默认的 11 个），新增「终端靛·夜」 | 两档 7 项对比度门槛全过，数值由自测的 `INFO` 行输出 |
| 色源收敛 | XAML 与代码后置的取色统一走 `UI4.Color.*` / `UI4.Brush.*` 资源引用；`App.Brush.*` 只留两把手抄语义色；删掉零引用的 `InverseBool`/`SortModeMatch` | 代码里 `Brushes.White/Black` 只剩注释里的说明文字；`{x:Static h:Theme.*}` 站点清零 |
| 明暗切换 | 顶部右端「明/夜」按钮 + `themeMode` 持久化；不做跟随系统（枚举里就没有 `System` 成员） | `themeMode` 的 `Dark` 还原、数字串与 `System` 拒绝都进了断言 |

一处旧机制描述被推翻：README 与 `App.xaml.cs` 的注释都写过"`UI4Button` 按底色亮度自动挑字色（`Luminance < 0.45` 给白字）"。对齐后的 v3.0.0 源码里它是**在 `OnAccent` 与正文色之间取与底色对比度更高者**（`lib/UI4Button.cs:224`），文档已按源码改。

## 第三批：两个分支整合（2026-10-04）

另有一份加了设置功能的分支（`Prompt收藏夹（示例项目）-增加了设置`，核对期间它被并回 `- 副本` 工作区，最终形态以那份为准）。这一批不是"把它接过来"，而是**逐条判定取还是弃**——取的都是它确实解决了主线没有的问题的地方，弃的都是它把主线已修好的东西改回去或引入新风险的地方。完整判定表与证据在《[问题归因与基线核对（2026-10-04）.md](问题归因与基线核对（2026-10-04）.md)》§八，这里只记结论。

| 取 | 弃（理由） |
|---|---|
| 设置浮层这套交互（齿轮入口 + 遮罩 + 完成，`Views/SettingsOverlay.xaml`） | 独立「明/夜」按钮被它删掉、面板里又没有切换 → 暗色档直接不可达。现在切换在面板内，入口仍是一处 |
| 排印与缩放这条通路（`UI4.Font.*` 键 + `LayoutTransform` 全局缩放） | 把所有 `FontSize` 拉平成一个基准 → 抹掉 11/12/13/14/16 层级；层级改由 `Typography.SizeOf` 派生 |
| 面板里的只读应用信息（版本 / 运行时 / 组件库 / 两个目录） | `IOException` 三次 `Thread.Sleep(50/100/200)` 重试放在 UI 线程的 `Load` 上 → 卡死界面且掩盖真因；主线走的是启动期 `Quarantine` 隔离 |
| `PromptModule` 实现 `INotifyPropertyChanged`（主线 `RefreshModuleCounts` 就地改 `EntryCount` 一直没生效，被整表重建掩盖） | `PromptItem.ExtraFields` 那套"载入时快照、保存时写回"的未知键机制 → 外部工具在载入之后的改动会被快照冲掉；主线保存前从磁盘再继承一次 |
| `IPromptService.CreateModule` + 去掉 `ModuleListViewModel` 里那两个 downcast（`((PromptService)_service)` 与就地 `new FileSystemRepository`） | Toast 底色写成 `#E632323D`、星标写成 `#FFFFB800` 的字面色 → 手抄十六进制就是"这处不跟主题"的成因；`DialogHelper` 从资源引用退回本地赋值同理 |
| 搜索 300 ms 防抖、`GridSplitter` 4→6 px 命中区、csproj 显式 `<Version>` | 字号下限 8 → 拉到 12（与 `Typography.MinBaseSize` 同源）；字体默认值写在 view 代码后置 → 收进 `ResetTypography()` |

`lib/` 的排印通路改动这次**回灌了包内基准**（`组件源码与组件使用说明/StartUI4Controls`），两棵树现在逐文件相同；相对 Skill 那份上游镜像种子，本包的偏离恰好是 8 个文件（6 个控件 + `UI4Theme.cs` + `lib/README.md` 新增 §4.2.1），其余 40 个文件只差行尾（`diff -rq --strip-trailing-cr` 无输出）。

第三批验收（本机 2026-10-04 16:00 前后）：`dotnet build --no-incremental` → `0 个错误 / 9 个警告`（上游 `CS0414`×5 + `CS1574`×4）；`--selftest` Debug 版与两档打包产物都是退出码 `0`、`PASS 断言组=179`；`publish\PromptFavorites_self_contained.exe` 70,607,724 字节、`publish_no_runtime\PromptFavorites_no_runtime.exe` 2,535,636 字节，各目录只有一个 exe，两者 `FileVersion` 都是 `1.0.0.0`（与新增的 `<Version>1.0.0</Version>` 一致）；打包版按自己起的 pid 读到 `MainWindowTitle` = 「收藏夹」，收掉后无残留进程。

**留给使用者的三挡目测**（判据证不住观感）：① 面板内切到夜景后整窗与原生标题栏跟随、重启仍是夜；② 字号 12 与 28 两端下三栏不出现截断；③ 150% / 200% 档下下拉与右键菜单的"弹层不跟着放大"这条已知边界能否接受。

## 第四批：截图反馈的两处观感修正（2026-10-04）

用户截图指出两件事：① 两栏默认宽度与头部按钮的内缩不一致；② 字体放大后「+」按钮的内容显示偏移。

| 现象 | 真因（可核对的位置） | 处置 |
|---|---|---|
| 两栏头部的「+」到列边距离不一样（一个 12、一个 8），中栏的「★ 收藏」看起来是通栏条 | 左栏头部 `Padding="12,10,12,6"`、中栏 `Padding="8,8,8,4"`；收藏胶囊是 `DockPanel` 的末子，`LastChildFill` 默认 `true` 把它拉满整行 | 两处统一成 `Padding="12,8"`；两个 `DockPanel` 显式 `LastChildFill="False"`，标题与收藏胶囊都按内容收宽；列宽 200 / 280 → **220 / 300**（`MainWindow.xaml`） |
| 字号放大后「+」的字跑到右下、还被切掉一点 | 圆钮钉死 `Width="28" Height="28"`，而 `UI4Button` 的样式默认 `Padding=10,0,10,0`（`lib/UI4Button.cs:135`）→ 内容区只剩 **8 px 宽**；字号 16 时「+」的字身就已超过 8 px，居中的 `ContentPresenter`（`:160-161`）被 arrange 成 8 px 后文字从左上角起画。**不是没居中**，加 `HorizontalContentAlignment` 无效 | 这两个圆钮改成 `Padding="0"` + 尺寸随字号长：`Width/Height={DynamicResource App.Size.RoundButton}`、`CornerRadius={DynamicResource App.Radius.RoundButton}`，值由 `Typography.RoundButtonSize(base)` 派生（基准 15 时仍是 28，默认观感不变） |
| 同一族缺陷的其余站点（不修就会在下一档字号上再报一次） | 排序胶囊 `Height="24" CornerRadius="12"`、收藏胶囊 `Height="28" CornerRadius="14"`、详情区 `[收起]` 与两个「恢复默认」按钮，全是钉死高度 | 一并换成 `App.Size.Chip` / `App.Radius.Chip`（基准 15 时仍是 24）与 `App.Size.RoundButton`；半径发布成 `CornerRadius` 对象而不是 `double`——`DynamicResource` 不做类型转换，挂 double 会在运行期炸 |

判据：`DisplaySelfTest` 新增一组断言，钉住"默认基准下圆钮仍是 28、胶囊仍是 24"（这条修复是平移不是改设计），以及 12–28 全区间内两者都 ≥ 对应字身 × 1.3。负向自证：把 `RoundButtonSize` 改回恒返回 28 → 退出码 `8`，从基准 21 起逐档报 `FAIL [display] 基准 21 时圆钮 28 px 装不下 22 px 的图标字身`；按备份回滚后 `diff` 无输出。

第四批验收（本机 2026-10-04 16:16 前后）：`--selftest` Debug 与两档重打包产物都是退出码 `0`、`PASS 断言组=236`；报告新增一行 `INFO 固定件尺寸：基准=15 圆钮=28 胶囊=24；基准=28 圆钮=44 胶囊=36`；`publish\PromptFavorites_self_contained.exe` 70,608,701 字节、`publish_no_runtime\PromptFavorites_no_runtime.exe` 2,539,732 字节，`FileVersion` 仍是 `1.0.0.0`；打包版按自己起的 pid 读到标题「收藏夹」，收掉后无残留进程。

**这一批要请你自己目测的**（判据证不住观感，且宽度数字是口味）：① 220 / 300 下两栏头部的「+」是否齐、收藏胶囊收成内容宽是否顺眼；② 字号 20 / 28 两档下「+」与排序胶囊的字是否还在正中；③ 详情区 `[收起]` 与设置面板两个「恢复默认」按钮跟着长高后可接受否。

## 第五批：同一屏的三条反馈（2026-10-04）

第四批改完用户又截了一张（`PixPin_2026-10-04_16-19-32.png`，放大字号下），三条：

| 反馈 | 量到的事实 | 处置 |
|---|---|---|
| ①「+」还是没居中 | 逐像素读那张截图：圆钮 28×30、墨迹 8×8，水平 dx=−0.5 px（**已经居中了**），但垂直 dy=**+1.5~2 px**。这不是槽位问题（`Padding="0"` 之后槽位是满的），是 `+` 这个字符在 Segoe UI 里的墨迹本来就低于 em 框中心，字号越大越明显 | 两个圆钮改用 `Segoe MDL2 Assets` 的 `Add` 字形（`\uE710`，与工具栏文件夹按钮同源）——图标字形按光学居中设计。改后**抓自己启的实例逐像素复核**（125% DPI）：左栏圆心 (200.5, 106)、竖笔 x=200..202、横笔 y=105..106；中栏圆心 (504, 106)、竖笔 504..505、横笔 105..106 → **两向都在 ±0.5 设备像素内** |
| ② 收藏按钮要与第二列所有按钮的宽度之和对齐 | 上一批我把它从"通栏"改成了按内容收宽，结果它比下面的胶囊块窄一截。而 DockPanel 里 stretch 也只能填到「+」的左边，比胶囊块短一个按钮宽 | 头部改成 `Grid`（`*` + `Auto` 两列）：收藏胶囊与 `WrapPanel` 同处第 0 列 → **两者宽度严格相等**（结构保证，不靠算），「+」在第 1 列 |
| ③ 恢复默认字体后下拉框不跟着变 | WPF 的 `ComboBox` 挂了 `ItemsSource` 之后，把 `SelectedItem` 设成一个**不在列表里**的值会被静默清空——出厂字体栈是复合串，`Fonts.SystemFontFamilies` 里没有它 | 候选表改为字符串列表，**出厂字体栈固定第 0 位**（`Typography.BuildFamilyChoices`，纯函数）；`SelectedFontFamily` 由 `FontFamily` 改成 `string`，选中出厂项时设置里存空串；存的字体名若已被系统卸载，回落到出厂项而不是留空白 |

判据：`DisplaySelfTest` 新增 `CheckFamilyChoices`——出厂项在首位、去重、丢空白、其余按序，以及"真实系统字体表首位必须是出厂项"（实测 93 项）。断言组 `236 → 240`。

第五批验收（本机 2026-10-04 16:33 前后）：`--selftest` Debug 与两档重打包产物退出码都是 `0`、`PASS 断言组=240`；报告新增 `INFO 字体候选表：93 项，首位=Segoe UI, Microsoft YaHei UI, sans-serif`；`publish\PromptFavorites_self_contained.exe` 70,609,882 字节、`publish_no_runtime\PromptFavorites_no_runtime.exe` 2,543,828 字节。

**仍请你自己目测**：① 放大字号（20 / 28）下两个「+」是否还在正中；② 收藏胶囊与下面那排胶囊的左右边缘是否严格齐；③ 设置里点「恢复默认字体与字号」后下拉框是否显示出厂字体栈那一项（列表第 0 项），以及换成别的字体后界面是否立即跟着变。

## 第六批：收藏胶囊等宽的两轮修正 + 去掉栏标题加粗（2026-10-04）

| 反馈 | 处置 | 实测 |
|---|---|---|
| 「第二列默认宽度对齐 OK，但列宽拉伸后仍要跟随拉伸的宽度」 | 头部结构换成 `Grid(* + Auto)` 里放一个 `StackPanel`：收藏胶囊（`Stretch`）与排序 `WrapPanel` 同在这个面板里 → 面板宽 = 排序胶囊**最宽一行的宽度**，两行始终等宽；列被拖宽时 `WrapPanel` 可用宽度变大、换行减少、最宽一行变宽，收藏胶囊跟着一起变宽。「+」在第 1 列，仍钉在列的最右边 | 默认 300 列：胶囊 264..496，四个灰胶囊 + 右边距到 495 → **差 1 px**；把列临时改成 420 重测：第五个「自定义」回到第一行，胶囊块 265..544，收藏胶囊 267..545 → 仍然等宽且随列变宽（实验后已回滚到 300，`grep` 确认列定义是 240 / Auto / 300 / Auto / `*`） |
| 「第一二栏的标题现在有加粗吗？有的话移除加粗」 | 两处 `FontWeight="SemiBold"` 删掉：左栏的「模块」标题、中栏列表项的条目名（这两栏里唯一的加粗点）。设置面板的分节标题不在"第一二栏"里，未动 | `grep FontWeight Views/ModuleListView.xaml Views/EntryListView.xaml` 无输出 |

两次走过的弯路都记在《问题归因与基线核对》§十一，别再试：`Auto` 列装 `WrapPanel` 会拿到近乎无限的可用宽度（5 个胶囊排成一行、整列撑到 306 px、「+」被挤出可视区）；`MinWidth` 绑另一行的 `ActualWidth` 有一拍布局滞后（实测收藏胶囊停在 438 而胶囊块到 462）。

第六批验收：`--selftest` Debug 与两档重打包产物退出码都是 `0`、`PASS 断言组=240`；`publish\` 70,609,933 字节、`publish_no_runtime\` 2,543,828 字节，各只有一个 exe。
