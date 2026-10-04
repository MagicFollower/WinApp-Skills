# 宿主工程接入契约

从 `samples/Prompt收藏夹（示例项目）` 逐行核出的 app 侧写法。`theming.md` 讲的是库怎么工作，这里讲的是**宿主必须怎么落笔**才不会静默失效。

## 目录约定

```text
<proj>/
├── app/                      应用本体（MVVM 六目录）
│   ├── App.xaml  App.xaml.cs            启动序列 + 宿主配色注册
│   ├── MainWindow.xaml  MainWindow.xaml.cs
│   ├── <AppName>.csproj                 引用 ../lib/StartUI4Controls.csproj
│   ├── app.manifest                       PerMonitorV2 DPI
│   ├── AppIcon.ico                        同时是 ApplicationIcon 与 Resource
│   ├── Models/ ViewModels/ Views/ Services/ Helpers/ Converters/
│   ├── Helpers\Theme.cs                   配色单源 + 必须显式决策的 Policy
│   ├── Helpers\Typography.cs              字号/字体/缩放的单源（默认值、区间、层级换算）
│   ├── Services\SettingsService.cs        设置落盘（%APPDATA%\<App>\settings.json，kv1）
│   ├── Services\SettingsCodec.cs          纯函数编解码（自检直接对它出题）
│   ├── Services\ThemeService.cs           配色档：稳定键 → SetTheme/Apply → 展示文案
│   ├── Views\SettingsOverlay.xaml         设置浮层内容（应用信息/配色/字体/缩放）
│   ├── Converters\Converters.cs           BoolToVis + ZoomedSize（缩放下的窗口下限）
│   └── 快速启动.txt                        build / run / publish / --selftest 四条命令带判据
├── lib/                      组件源码整份自包含拷贝（48 个文件 = 44 个 .cs + csproj + AssemblyInfo + LICENSE.txt + README.md 组件手册 + 架构审计报告）
├── publish.cmd               单文件 + 自带运行时
├── publish_no_runtime.cmd    单文件 + 框架依赖
└── <设计说明>.md  README.md  <图标教程>.md
```

`lib/` 用**源码工程引用**（`ProjectReference`）而不是 dll 引用，也不是 NuGet：模板改动要能就地重编，且交付包必须能独立 clone 重建。`LICENSE.txt` 不能删——csproj 里 `<None Include="./LICENSE.txt" Pack="true" .../>`，缺文件时 `dotnet pack` 报 NU5019 并把整条构建链拖红。

`lib/README.md` 就是组件手册（与 `references/component-manual.md` 同一份），随源码进工程，接手的人不用另外找文档。这份 `lib/` 由 `scripts/fetch-source.ps1` 保证完整：**本地命中就零联网**，缺文件才按 `assets/seed/manifest.json` 回源并写回。种子自 2026-10-04 起是**自持基线**（清单里的 `upstream.baseline`），比清单 `fetchedFrom` 那份上游目录多 11 个文件的改动（排印三键 + 四处控件修复），所以 `-Refresh` 默认被挡、要 `-AllowUpstreamReset` 才放行；工程里的 `lib/` 少文件时先跑脚本，别手抄。

## app/ 的 csproj 关键行

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<LangVersion>latest</LangVersion>
<Nullable>disable</Nullable>           <!-- 与 lib 同口径，否则逐行可比性没了 -->
<ImplicitUsings>disable</ImplicitUsings>
<ApplicationManifest>app.manifest</ApplicationManifest>
<ApplicationIcon>AppIcon.ico</ApplicationIcon>
```

```xml
<ItemGroup>
  <Resource Include="AppIcon.ico" />   <!-- Window Icon="AppIcon.ico" 是 pack URI，必须嵌进程序集 -->
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="../lib/StartUI4Controls.csproj" />
</ItemGroup>
```

发布开关要包在条件里，别让 `dotnet build` 也去解析运行时包：

```xml
<PropertyGroup Condition="'$(PublishSingleFile)' == 'true'">
  <DebugType>embedded</DebugType>                       <!-- pdb 内嵌，产物目录不留散落文件又保住行号 -->
  <DebugSymbols>false</DebugSymbols>
  <EnableCompressionInSingleFile Condition="'$(SelfContained)' == 'true'">true</EnableCompressionInSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <SatelliteResourceLanguages>zh-Hans;en</SatelliteResourceLanguages>
</PropertyGroup>
```

`EnableCompressionInSingleFile` 只对自带运行时合法，框架依赖发布会 `NETSDK1176`。WPF 的 `PresentationNative_*` / `wpfgfx_*` / `D3DCompiler_47_*` 没法从 bundle 直接 `LoadLibrary`，所以自带运行时要 `IncludeNativeLibrariesForSelfExtract`，运行期解到 `%TEMP%\.net\<程序集名>\`。

产物命名用一个 label 参数分档、互不覆盖（动 `AssemblyName`，apphost 必须与配对的托管 dll 同名）：

```xml
<PropertyGroup Condition="'$(ArtifactLabel)' != ''">
  <AssemblyName><AppName>_$(ArtifactLabel)</AssemblyName>
</PropertyGroup>
```

数据根与设置目录用**字面量**应用名，不跟着 `AssemblyName` 变，否则两档产物会各写各的配置。

## app.manifest（DPI，逐字）

```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
  </windowsSettings>
</application>
```

net10 的 WPF 仍按清单取 DPI 感知级别。示例工程**没有** `longPathAware`，它与 `RootPathResolver.MaxRootLength = 240` 是配对决定——加了长路径支持就要同步放宽那个常量。

## App.xaml.cs 启动序列（顺序是承重的）

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);                                     // ① 必须最先

    if (e.Args != null && Array.IndexOf(e.Args, "--selftest") >= 0)
    {
        Shutdown(SettingsSelfTest.Run());                   // ② 自测分支排在最前：不开窗、不读设置、不挂异常钩子
        return;
    }

    DispatcherUnhandledException += OnDispatcherUnhandledException;   // ③

    Settings = new SettingsService();                                 // ④ 构造不碰磁盘
    Settings.Load();                                                  //    读设置（永不抛：坏文件改名留档后回默认值）

    RegisterAppTheme();                                     // ⑤ UI4Theme.Register(宿主定义)
    UI4ThemePacks.RegisterAll();                            // ⑥ 套装键要早于 Apply
    ThemeService.Apply(Settings.ThemeKey);                  // ⑦ 内部就是 SetTheme(模式) 或 Apply(套装键)
    ApplyDisplaySettings();                                 // ⑧ Typography.Publish(字体族 + 基准字号 + 层级键)

    // ⑨ 数据准备失败也要让窗口出现
    try { var w = new MainWindow(); MainWindow = w; w.Show(); }
    catch (Exception ex) { TryReport("创建主窗口", ex); Shutdown(-2); return; }
}
```

五条硬约束：

1. **`Register`/`SetTheme`/`ApplyToApplication` 只能在 `base.OnStartup(e)` 之后调**。库内 `WriteToApplicationResources()` 第一句是 `Application.Current == null ? return`，在构造函数或 `Main` 里调**静默不装资源**，症状是宿主 `{DynamicResource UI4.*}` 全空。
2. **`UI4ThemePacks.RegisterAll()` 在 `UI4Theme.Apply(套装键)` 之前**，否则 `Apply` 认不出键返回 `false`（不抛异常）。内置 `light`/`dark`/`highcontrast` 由静态构造自带，不注册也能用。
3. **`ApplyToApplication()` 在第一个窗口 `Show()` 之前**，否则首帧的 `DynamicResource` 查不到键、回落到控件默认色。宿主自己 `Register` 了含当前键的定义时，`SetTheme` 已隐式装好字典；两种都不调 = 只有库内控件有样式、宿主样式全裸。
4. **排印覆盖写在 `Application.Resources` 的自有项上**（`Typography.Publish` 干的就是这件事），要在装字典之后：库的兜底值住在 `MergedDictionaries` 里那份共享字典，宿主覆盖写在自有项，同一层自有项优先——所以切主题冲不掉覆盖值。写反两层（把键塞进共享字典内部）就会被下次重写顶掉。
5. **启动不变量：窗口必须出现**。数据初始化失败只 `TryReport` 继续走；连窗口都创建不了才 `Shutdown(-2)`，绝不留「进程存活但无窗口」。延迟工作用 `Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, …)`。

`App.xaml` 配 `ShutdownMode="OnMainWindowClose"`，`MainWindow` 在 `Show()` 前手动赋值（托盘类应用改 `OnExplicitShutdown` 并自己管退出）。

## 宿主配色单源 → 库令牌

`Helpers/Theme.cs` 是一份 `static class` 的 `Color` / 冻结画刷常量，**宿主唯一色源**；`RegisterAppTheme()` 从它派生库令牌：

```csharp
var def = UI4ThemeDefinition.Light();      // 从内置工厂起步 = 38 个令牌天然齐全
def.Key = "light";                          // 覆盖当前键：库就地整体重应用，不用先切走再切回
def.With(UI4ThemeToken.Background, Theme.Background)
   .With(UI4ThemeToken.Accent, Theme.Accent)
   // …宿主想改的几个语义令牌
UI4Theme.Register(def);
```

`Mix(a, b, w)` 这类按权重混色的辅助要自己写（逐通道 `a + (b-a)*w`，保留 `a.A`）；调对比度时用它把不达标的色推向正文色，例如次级文字向正文混 18% 把 4.49:1 推到 5.7:1。

对比度陷阱：`UI4Button` 按底色亮度自动挑字色（**L<0.45 给白字**）。中性按钮的底如果做成中灰，白字只有 2.4:1；把中性底调浅（例：向 `Muted` 混 0.62）它会自动换成正文色，实测 10.8:1。

## 消费令牌的两种写法与各自代价

| 写法 | 跟主题？ | 用途 |
|---|---|---|
| `{DynamicResource UI4.Brush.Surface}` / `UI4.Color.BorderNormal` | ✅ | 宿主样式接令牌的首选 |
| `{x:Static h:Theme.Accent}`、自建 `App.Brush.*` 常量 | ❌ 编译期定死 | 固定配色方案（示例工程故意用单档浅色） |
| 给 UI4 控件属性写死字面值 | ❌ 顶掉 `SetResourceReference`，`ClearValue` 只能回到库内字面默认色 | 只改一处观感时用 |

别名键 `UI4.Brush.Text` / `UI4.Brush.Border` / `UI4.Brush.Accent` 是库里真写进字典的，可以直接用；其余必须写全令牌名（`UI4.Brush.TextForeground`，不是 `UI4.Brush.Text.Foreground`）。

命令式取色（代码里构造画刷、判深浅）：

```csharp
Color bg = UI4Theme.Current.ColorOf(UI4ThemeToken.Background);
bool isDark = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B < 128;
```

**别绑静态属性**：`{Binding Path=(ui:UI4Theme.CurrentMode)}` 会停在初值不再刷新——库发的是 `StaticPropertyChanged`，而 WPF 绑静态 CLR 属性找的是同名 `<属性>Changed` 静态事件。要在界面上显示当前主题就订阅 `ThemeChanged`，读 `UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey)`。

代码里现取资源要兜底：`Application.Current.FindResource("UI4.Brush.Surface")` 对**缺失键抛异常**（不是返回 null），所以 `(Brush)Current.FindResource(...) ?? Brushes.White`。这类 `DialogHelper` 在 `Application.Current` 为 null 时（自测分支、单元钩子）不能进。

## 对话框与托盘的宿主侧约束

```csharp
if (UI4MessageBox.Show("确认删除这条记录？", "确认", UI4MessageBoxButtons.OKCancel, owner: this) == true) Delete();
Color? picked = UI4ColorPicker.ShowDialog("选择强调色", UI4Theme.Current.ColorOf(UI4ThemeToken.Accent), this);
if (picked.HasValue) UI4Theme.SetAccent(picked.Value);
```

`Show` 返回 `bool?`：OK=`true`、Cancel=`false`、直接关窗=`null`。判 `== true`，不要判 `if (result)`。

托盘在 `OnClosing` 里必须 `Tray.Visibility = Visibility.Collapsed; Tray.Dispose();`（`Dispose` 幂等，`Application.Exit` 也兜一次），否则托盘残留点不动的死图标。

## 自测契约（`--selftest`）

约定：**退出码 = 失败断言数**，0 为全通过；报告写 `%APPDATA%\<AppName>\selftest.txt`，写不进去回落 `AppDomain.CurrentDomain.BaseDirectory\selftest.txt`，且**报告写失败不得改变退出码**。自测分支不开窗、不读用户设置、不挂 UI 线程异常钩子——这样它才能在 CI 与打包脚本里当门禁用。

模板自带的断言分两段。配色与令牌段（令牌数、三份内置定义逐令牌可取色、对比度门槛、`Mix`/`IsDark`、`Policy` 已决策、8 套预置资源键齐全、字典与定义同源）抓的是"键名写错运行期不报错"。排印与缩放段抓的是同一族风险的另一面：库有没有发布 `UI4.Font.*` 兜底值、宿主覆盖值换档后还在不在、层级与固定件尺寸（默认基准下必须仍是 28 / 24）、`ClampBase`/`ClampZoom` 边界、字体候选表首位、`ZoomedSizeConverter` 参数写错要回 `UnsetValue`、以及**主窗口与设置面板两块 XAML 能构造**（只 `new` 不 `Show`，BAML 与绑定表达式在这一步就解析）。最后那条把"点开设置才发现面板炸了"提前到门禁里。

任一段抛异常都要记成一条 FAIL 而不是让进程带堆栈退出（退出码契约在任何情况下都得成立），报告全绿时补 `INFO` 行写实测数值（字号阶梯、固定件尺寸），文档里的数字抄它、不要手抄。

发布脚本里紧跟一步：`start "" /wait "%EXE%" --selftest` 然后 `if not "!ST!"=="0" ( echo WARNING … )`。

## 标题栏与窗口外观

不要自绘标题栏、不要 `WindowStyle=None` + `AllowsTransparency=true`。窗口用原生 chrome + `Icon="AppIcon.ico"`，标题栏染色由库在 DWM 侧自动完成（窗口里出现过 UI4 控件就够）。**完全不含 UI4 控件的窗口**（例如纯原生内容的关于窗）要手动 `UI4WindowTitleBar.Apply(win)`，否则它等到下次主题切换才被扫到。

窗口几何恢复：`Width/Height/MinWidth/MinHeight` 存设置，恢复时把 `Left/Top` 对 `SystemParameters.VirtualScreen*` 做夹取（防插拔副屏后窗口跑到屏外），`Maximized` 状态在 `SourceInitialized` 里还原。

图标三处配齐、各有各的通路：`<ApplicationIcon>`（烤进 apphost 的 `RT_GROUP_ICON`，管 exe 文件图标）、`<Resource Include>`（管 `Window.Icon` 的 pack URI）、`UI4NotifyIcon.IconSource`（管托盘）。`Window.Icon` 不设时 WPF 会回落到 exe 图标——两处都配不是补齐，而是不把窗口外观挂在回落行为上。

图标文件本身默认由 `scripts/make-icon.ps1` **本地生成**（不联网）：16×16 逻辑网格上画应用名首字母的 5×7 点阵，格点按整数倍放大成方块（像素风，无抗锯齿），四角各削 2 格成圆角，底色 `#4F6BE8` / 字形 `#F7F9FB`（与 `Theme.Accent`、`Theme.LightBackground` 同值，改配色时两处一起想）。帧表固定 16/24/32/48/64/128/256：小于 256 的帧是 32bpp BGRA DIB（`biHeight = 2*size`，AND 掩码按 `alpha==0` 置 1，让只认掩码的旧渲染器也当透明），256 帧是内嵌 PNG。写完它自己回读一遍：目录条目必须连续无缝、末帧长度正好落到文件尾、`ExtractAssociatedIcon` 语义下 16/32 两档圆角 `alpha=0`——所以"半截 ico"不会静默交付。

外部图（例如 `selfhst/icons` 的 `ico/` 现成多尺寸 ico，CDN 直链 `https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<ref>.ico`）走"直接覆盖 `app/AppIcon.ico`"这条路即可，csproj 不用动；那份仓库是 CC-BY-4.0，**用了要在 `doc/` 第 5 节记来源、ref、许可与是否改过**。判可达性要真 GET 拿字节数（HEAD 200 证不住下载）。
