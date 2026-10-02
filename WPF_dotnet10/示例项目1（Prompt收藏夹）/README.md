![](.\PixPin_2026-10-01_13-11-28.png)

## 构建与打包

`app/` 是应用，`lib/` 是 StartUI4Controls 控件库源码（net10.0-windows，与主仓库 `src/StartUI4Controls` 同一份）。应用与库都跑在 **.NET 10** 上（`net48 → net10.0-windows` 的迁移已完成，见下）。

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

两个产物实测都是退出码 0。退出码即结论（`SettingsSelfTest`，23 组断言，含"连续 5 轮序列化文本长度不增"这条防路径指数膨胀）。报告同时写 `%APPDATA%\PromptFavorites\selftest.txt`——刻意不写 exe 旁边，因为单文件下 `AppDomain.CurrentDomain.BaseDirectory` 可能指向会被清理的临时解包目录。

### 标题与图标

窗口标题在 `MainWindow.xaml` 的 `Title`（当前为「收藏夹」）；应用图标是 `tools\make-icon.ps1` 手绘生成的 `app\AppIcon.ico`（7 挡尺寸）。改法、exe 图标与窗口图标两条链路的区别、以及运行时怎么验，见 [标题与图标设置教程.md](标题与图标设置教程.md)。

### 配色

界面是单一定死的浅色方案「终端靛」：底 `#f9fbff`、正文 `#1b2737`、主色 `#4f6be8`、强调 `#0c8ba8`、边框 `#bcc7d6`、选中行 `#e2e5fb`。源色取自 [100-themes](https://github.com/MagicFollower/100-themes) 的 `terminal-blue/day/colors.toml`，按"浅亮、低干扰"重调：底色亮度 0.964，边框降到 1.65:1 的发丝线，列表选中行用浅底黑字而不是主色块。

| 关系 | 实测对比 |
|---|---|
| 正文 / 底 | 14.56:1 |
| 次级文本 / 底 | 5.73:1 |
| 白字 / 主色（填充按钮） | 4.56:1 |
| 正文 / 中性按钮底 | 10.81:1 |
| 正文 / 选中行 | 12.09:1 |

改配色只动一处：`Helpers/Theme.cs` 的源色常量。`App.RegisterAppTheme()` 据此派生全部 30 个 `UI4ThemeToken`（面板=背景向白提亮 85%、中性按钮底=背景混边框色 62%、行悬浮=背景混主色 6%、次级文本向正文混 18% 以过 AA），覆盖 `light` 键后由 `UI4Theme.SetTheme(Light)` 生效，`lib/` 一行未改。三条约束：

- **主色不能再往淡走**：`UI4Button` 按底色亮度自动挑字色（`Luminance < 0.45` 给白字），而白字要 ≥4.5:1 就得主色亮度 ≤0.183——再淡就掉进"白字配中底"的不可读区。要浅就浅底、线、选中块。
- **中性按钮（收藏/复制/保存/取消）的底必须是浅的**（现在 L≈0.70）。踩过的坑：把它映射成中灰 `muted`（L≈0.37）时库自动配白字，实测只有 2.46:1，几乎读不出字。
- **`UI4ListBox` 的选中行不参与主题同步**：`PressedBackground`/`PressedForeground` 是硬编码默认值，所以两个列表上显式绑了 `UI4.Color.RowSelectedBackground` / `UI4.Color.TextForeground`；`TextColor`/`BorderNormalColor`/`PanelBackground`/`HoverBackground` 由库自动跟随，不用管。

收藏星标的金色是语义色，不跟界面配色走。

### 数据与设置位置

| 内容 | 位置 |
|---|---|
| Prompt 数据根 | `文档\Prompts`（可在界面里换，写回设置） |
| 应用设置 | `%APPDATA%\PromptFavorites\settings.json`（kv1 纯文本，兼容遗留 JSON） |
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
| 控件调用 | 一行未改。用到的 5 个控件（`UI4Button` / `UI4CodeEditor` / `UI4ComboBox` / `UI4ListBox` / `UI4TextBox`）与 `UI4MessageBox.Show`、`UI4Clipboard.TrySetTextAsync` 的签名在 net10 库里原样存在 |
