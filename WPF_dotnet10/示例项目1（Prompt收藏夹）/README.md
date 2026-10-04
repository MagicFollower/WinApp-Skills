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

两个产物实测都是退出码 0。退出码即结论。2026-10-04 起 `--selftest` 是**三段合成**（`Services/SelfTest.cs`），实测 `PASS 断言组=111`：

| 段 | 覆盖 | 抓的是什么 |
|---|---|---|
| `SettingsSelfTest` | kv1 往返（10 个 hostile 值）、5 轮体积不增、旧 JSON 还原、分隔符折叠、非法几何值拒绝、枚举按键名解析与 kv1 往返、自定义顺序编解码不变量 | 当年 16 MB 设置膨胀那类**指数放大**；以及 `themeMode`/`sortMode` 被数字串蒙过 |
| `ThemeSelfTest` | 令牌总数仍为 38、三份内置定义逐令牌齐全、明暗两档宿主色表各覆盖 38 个、应用后 `UI4.Color.*`/`UI4.Brush.*` 38×2 + 3 个别名与色表同源、7 项对比度门槛、深浅判据与库同式 | **写错资源键不报错也不抛异常**这条库特性——只有这段能把它抓出来 |
| `DataSelfTest` | frontmatter 已知字段往返、无 frontmatter 兜底、正文中 `---` 不误判、**未知键保留**、含冒号的值不被切坏；仓储在临时目录里的建模块/建条目/覆写不残留旧尾巴/改名撞目标不静默覆盖/跨模块移动/收藏与复制的时间戳口径/写盘不留 `.tmp` | 磁盘链路与外部编辑器共存的数据安全，这一段以前**完全没有判据** |

任一段抛异常会被记成一条 FAIL（不再让进程带堆栈退出），所以"退出码＝失败断言数"这条契约在任何情况下都成立。

判据非空洞已经实测过两次：删掉夜景色表里的 `OnAccent` 一行 → `FAIL [theme] 夜景宿主色表未覆盖全部 38 个令牌：缺 OnAccent`，退出码 1；浅色档占位符原本对输入底只有 **2.94:1**，卡在 3:1 门槛下被这段抓出来，向正文混的比例从 30% 提到 38% 后到 **3.48:1**。

报告同时写 `%APPDATA%\PromptFavorites\selftest.txt`——刻意不写 exe 旁边，因为单文件下 `AppDomain.CurrentDomain.BaseDirectory` 可能指向会被清理的临时解包目录。全绿时报告末尾还带两行 `INFO`，是明暗两档各项对比度的**实测值**（文档里的对比度表就抄这里，免得数字再漂）。

### 标题与图标

窗口标题在 `MainWindow.xaml` 的 `Title`（当前为「收藏夹」）；应用图标是 `app\AppIcon.ico`（实测 372,526 字节 / 7 挡：16/24/32/48/64/128/256，末挡正好收到文件尾），由 `tools\make-icon.ps1` 用 GDI+ 矢量图元手绘生成 —— **该脚本与 `tools\verify-icon.ps1` 都没随本包交付**，所以"改图标设计重跑脚本"这条路当前不可复现，只能整体替换 `app\AppIcon.ico`（图标文件本身在，构建与两处引用都正常）。改法、exe 图标与窗口图标两条链路的区别、以及运行时怎么验，见 [标题与图标设置教程.md](标题与图标设置教程.md)。

### 配色

界面是**明暗双档**，同一套「终端靛」的两个亮度方向：

- **浅色（终端靛）**：底 `#f9fbff`、正文 `#1b2737`、主色 `#4f6be8`、强调 `#0c8ba8`、边框 `#bcc7d6`、选中行 `#e2e5fb`。源色取自 [100-themes](https://github.com/MagicFollower/100-themes) 的 `terminal-blue/day/colors.toml`，按"浅亮、低干扰"重调。
- **夜景（终端靛·夜）**：底 `#0e1622`、面板 `#1c232f`、正文 `#e8eef8`、主色 `#7c93ff`、强调 `#2bb6d4`、边框 `#33415a`、选中行 `#263152`、中性按钮底 `#282f3b`。同一支靛蓝往**亮**走而不是换色相；因为主色变亮了，压在主色上的字改成深墨 `#0b1220`（白字压 `#7c93ff` 只有 2.67:1，深墨压同一色有 6.66:1）。

两档**手动切换**：顶部搜索栏右端那个 36×36 的「明 / 夜」按钮，档位在关窗后记住（`settings.json` 的 `themeMode` 键）。**刻意不做"跟随系统"**——`Models/AppThemeMode.cs` 里连 `System` 成员都没有，这样 `settings.json` 不可能存进一个没人消费的取值，也就不会去挂 `SystemEvents` 订阅。

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

### 数据与设置位置

| 内容 | 位置 |
|---|---|
| Prompt 数据根 | `文档\Prompts`（可在界面里换，写回设置） |
| 应用设置 | `%APPDATA%\PromptFavorites\settings.json`（kv1 纯文本，兼容遗留 JSON；含 `themeMode` 明暗档） |
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
