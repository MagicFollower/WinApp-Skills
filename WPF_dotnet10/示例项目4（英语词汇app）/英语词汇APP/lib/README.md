# StartUI4Controls 组件手册

本目录（`src/StartUI4Controls`）就是组件库本体，**这份 README 是组件库自身的权威参考**：
组件清单、每个组件的属性/事件/方法/用法、主题系统、静态服务与扩展约定。所有表格逐条对着
`*.cs` 源码核对过，源码与本文件冲突时以源码为准，并在 [§8.4 校对记录](#84-校对记录与根-readme-的口径差异) 里登记。

仓库级内容不在这里，去 [`../../README.md`](../../README.md)：

- 环境要求、源码构建、`dotnet pack` 与 NuGet 引用
- Demo 工程（`samples/StartUI4Demo`）的分页结构与命令行参数
- net48 → net10 迁移记录、一键回归、发布产物清单（哪些文件运行必需）
- 与上游 net6 版的差异、常见问题（`常见问题清单/`）、AI Agent 作业手册（`Agent手册/`）

## 目录

- [一、程序集事实与本手册的口径](#一程序集事实与本手册的口径)
  - [1.1 公开 API 统计](#11-公开-api-统计)
  - [1.2 文件地图](#12-文件地图)
  - [1.3 三分钟接入](#13-三分钟接入)
- [二、组件清单](#二组件清单)
- [三、组件详解](#三组件详解)
  - [3.1 基础交互：UI4Button / UI4CheckBox / UI4Radio / UI4Switch](#31-基础交互)
  - [3.2 文本与输入：UI4TextBox / UI4PasswordBox / UI4TextBlock / UI4FlipTextBlock](#32-文本与输入)
  - [3.3 下拉与取色：UI4ComboBox / UI4ColorPicker](#33-下拉与取色)
  - [3.4 进度指示：UI4ProgressBar / UI4ProgressRing](#34-进度指示)
  - [3.5 滑块：UI4Slider / UI4CircleSlider](#35-滑块)
  - [3.6 容器与布局：UI4Panel / UI4Grid / UI4ScrollViewer](#36-容器与布局)
  - [3.7 列表与卡片：UI4ListBox / UI4ListView / UI4GridView](#37-列表与卡片)
  - [3.8 页签与导航：UI4Pivot / UI4Tab / UI4NavigationView](#38-页签与导航)
  - [3.9 菜单与对话框：UI4Menu / UI4ContextMenu / UI4MessageBox](#39-菜单与对话框)
  - [3.10 代码编辑器：UI4CodeEditor](#310-代码编辑器)
  - [3.11 系统集成：UI4NotifyIcon / UI4WindowTitleBar](#311-系统集成)
- [四、主题系统](#四主题系统)
  - [4.1 数据模型：38 个令牌与三份内置定义](#41-数据模型38-个令牌与三份内置定义)
  - [4.2 生效通路：一份共享字典 + 资源引用](#42-生效通路一份共享字典--资源引用)
  - [4.3 UI4Theme API 一览](#43-ui4theme-api-一览)
  - [4.4 内置三套的完整取值](#44-内置三套的完整取值)
  - [4.5 预置套装 UI4ThemePacks（8 套）](#45-预置套装-ui4themepacks8-套)
  - [4.6 局部作用域 UI4ThemeScope](#46-局部作用域-ui4themescope)
  - [4.7 强调色、自定义主题与注册时机](#47-强调色自定义主题与注册时机)
  - [4.8 系统跟随与高对比度](#48-系统跟随与高对比度)
  - [4.9 主题持久化](#49-主题持久化)
  - [4.10 窗口标题栏跟随主题](#410-窗口标题栏跟随主题)
  - [4.11 主题盲区与已知限制](#411-主题盲区与已知限制)
- [五、静态服务](#五静态服务)
  - [5.1 UI4Clipboard](#51-ui4clipboard)
  - [5.2 UI4MultiLanguage](#52-ui4multilanguage)
- [六、用法配方](#六用法配方)
- [七、扩展约定](#七扩展约定)
- [八、附录](#八附录)
  - [8.1 枚举全清单](#81-枚举全清单)
  - [8.2 事件全清单](#82-事件全清单)
  - [8.3 不打算给你用的公开成员](#83-不打算给你用的公开成员)
  - [8.4 校对记录（与根 README 的口径差异）](#84-校对记录与根-readme-的口径差异)

---

## 一、程序集事实与本手册的口径

| 项 | 值 | 核对方式 |
|---|---|---|
| 程序集 / 根命名空间 | `StartUI4Controls` / `StartUI4Controls` | `StartUI4Controls.csproj` |
| NuGet 包 id | `StartUI4.WPF` | `PackageId` |
| 版本 | 3.0.0（`Version` / `AssemblyVersion` / `FileVersion` 三者一致） | `StartUI4Controls.csproj` |
| 目标框架 | `net10.0-windows` + `UseWPF=true` | 同上 |
| 语言级别 | `LangVersion=latest`，`Nullable=disable`，`ImplicitUsings=disable` | 同上 |
| 第三方依赖 | 仅 `AvalonEdit 6.3.1.120`（只有 `UI4CodeEditor` 用） | 同上 |
| 主题资源位置 | `ThemeInfo(ResourceDictionaryLocation.None, SourceAssembly)` | `AssemblyInfo.cs` |
| XAML 文件数 | **0**：工程内没有任何 `.xaml` / `Themes/generic.xaml` | `find . -name '*.xaml'` |
| 模板构建方式 | 全部由 C# 构建（`ControlTemplate` + `FrameworkElementFactory`，滚动条样式用 `XamlReader` 解析字符串） | 各控件 `Build*Style()` |
| 打包 | `GeneratePackageOnBuild=false`，需要包时显式 `dotnet pack` | csproj 注释说明了原因（NU5019 会拖红整条构建链） |

> **没有 generic.xaml 意味着什么**：宿主引用 dll 后直接写 `<ui:UI4Button/>` 就有完整外观，
> 不需要往 `Application.Resources` 里挂资源字典；代价是模板改动必须重编 dll。

### 1.1 公开 API 统计

| 类别 | 数量 | 说明 |
|---|---|---|
| 注册的依赖属性 | **237** | `DependencyProperty.Register` 235 个 + `RegisterAttached` 2 个（`UI4ThemeScope.Theme`、`UI4WindowTitleBar.Enabled`） |
| 依赖属性别名 | 1 | `UI4CheckBox.BoxCornerRadiusProperty = CornerRadiusProperty`，两个名字指向同一个 DP |
| 公开 CLR 属性包装 | 与上表一一对应 | 另有 `UI4GridView.ComputedColumns`（只读）、`UI4ContextMenu`/`UI4MenuItem`/`UI4TrayMenuItem` 的普通属性、`UI4Theme.Persistence` 等 |
| 公开事件 | **11** | 实例事件 7 个 + `UI4Theme` 静态事件 4 个，见 [§8.2](#82-事件全清单) |
| 公开枚举 | **7** | `UI4ThemeMode`、`UI4ThemeToken`(38 成员)、`ListStyleType`、`UI4MenuItemType`、`UI4MessageBoxButtons`、`PopupActivationMode`、`UI4LanguageKey` |
| 公开类型总数 | **62** | 44 个普通 `class` + 3 个 `sealed class` + 7 个 `static class` + 1 个 `interface`（`IThemePersistence`）+ 7 个 `enum`；其余类型一律 `internal`，见 [§8.3](#83-不打算给你用的公开成员) |

各控件的属性数加起来正好等于 234，这张表因此可以当自检单用：新增一个 DP 却没记进本手册，总数就对不上。

### 1.2 文件地图

| 文件 | 公开类型 | 一句话 |
|---|---|---|
| `UI4Button.cs` | `UI4Button` | 渐变/圆角/悬浮按钮，前景在 `OnAccent` 与正文色间取对比度更高者 |
| `UI4CheckBox.cs` | `UI4CheckBox` | 勾选框，块尺寸/圆角/填充/描边可调 |
| `UI4Radio.cs` | `UI4Radio` | 单选按钮，属性面与 `UI4CheckBox` 对齐 |
| `UI4Switch.cs` | `UI4Switch` | 滑动开关，`IsOn` 双向绑定 + `Toggled` |
| `UI4TextBox.cs` | `UI4TextBox`、3 个转换器 | 输入框：占位符、清除按钮、聚焦描边、原生剪贴板 |
| `UI4PasswordBox.cs` | `UI4PasswordBox` | 密码框：明暗文切换、自定义掩码、可绑定 `Password` |
| `UI4TextBlock.cs` | `UI4TextBlock` | 文本块：面板圆角、阴影、渐变字（走 `Foreground`） |
| `UI4FlipTextBlock.cs` | `UI4FlipTextBlock` | 数字翻牌动画文本 |
| `UI4ComboBox.cs` | `UI4ComboBox` | 下拉框：聚焦渐变描边、弹层宽度自适应、超长项省略号 + ToolTip |
| `UI4ColorPicker.cs` | `UI4ColorPicker` | HSV 取色对话框（`Window`），带静态 `ShowDialog` |
| `UI4ProgressBar.cs` | `UI4ProgressBar` | 渐变进度条，支持不确定往返模式 |
| `UI4ProgressRing.cs` | `UI4ProgressRing` | 环形进度：确定弧 / 旋转弧 / 中心数值 |
| `UI4Slider.cs` | `UI4Slider` | 线性滑块，带数值行 |
| `UI4CircleSlider.cs` | `UI4CircleSlider` | 环形滑块，拖拽改值 |
| `UI4Panel.cs` | `UI4Panel` | 阴影 + 悬浮缩放容器 |
| `UI4Grid.cs` | `UI4Grid` | 默认铺页面渐变的 `Grid` |
| `UI4ScrollViewer.cs` | `UI4ScrollViewer` | 美化滚动条 + 滚轮平滑滚动 |
| `UI4ListBox.cs` | `UI4ListBox`、`ListStyleType` | 普通 / 圆点 / 编号三种列表样式 |
| `UI4ListView.cs` | `UI4ListView` | 卡片式列表（单列，悬浮放大不越界） |
| `UI4GridView.cs` | `UI4GridView` | 网格卡片（按宽度自适应列数） |
| `UI4Pivot.cs` | `UI4Pivot`、`UI4PivotItem` | 滑动切换页签 |
| `UI4TabControl.cs` | `UI4Tab`、`UI4TabItem`、`TabCloseRoutedEventArgs` | 浏览器风格标签页 |
| `UI4NavigationView.cs` | `UI4NavigationView` 及 Item / BottomItem | 侧边导航 + 底部固定项 + 内容区 |
| `UI4Menu.cs` | `UI4Menu`、`UI4MenuElementItem`、`UI4MenuSeparatorElement` | 菜单栏：文字图标、KeyTip、分隔符 |
| `UI4ContextMenu.cs` | `UI4ContextMenu`、`UI4MenuItem`、`UI4MenuItemType`、`UI4MenuIcons`、`GeometryHelper` | 代码构造的右键菜单与内置图标 |
| `UI4MessageBox.cs` | `UI4MessageBox`、`UI4MessageBoxButtons` | 消息对话框（`Window`），静态 `Show` |
| `UI4NotifyIcon.cs` | `UI4NotifyIcon`、`UI4TrayMenuItem`、`PopupActivationMode` | 系统托盘（纯 P/Invoke，不依赖 WinForms） |
| `UI4CodeEditor.cs` | `UI4CodeEditor` | AvalonEdit 封装：C# 高亮、行号、内置右键菜单 |
| `UI4Clipboard.cs` | `UI4Clipboard` | 原生 Win32 剪贴板读写 |
| `UI4MultiLanguage.cs` | `UI4MultiLanguage`、`UI4LanguageKey` | 库内静态文案，8 套语言 |
| `UI4Theme.cs` | `UI4Theme`、`UI4ThemeMode` | 主题引擎：模式、资源桥、强调色、持久化 |
| `UI4ThemeToken.cs` | `UI4ThemeToken` | 38 个颜色令牌 |
| `UI4ThemeDefinition.cs` | `UI4ThemeDefinition` | 一份主题的令牌取值 + 内置 light/dark/highcontrast |
| `UI4ThemePacks.cs` | `UI4ThemePacks` | 8 套预置业务主题 |
| `UI4ThemeScope.cs` | `UI4ThemeScope` | 局部 / 每窗口主题（附加属性） |
| `UI4ThemePersistence.cs` | `IThemePersistence`、`RegistryThemePersistence`、`JsonThemePersistence` | 主题模式持久化后端 |
| `UI4WindowTitleBar.cs` | `UI4WindowTitleBar` | DWM 让系统标题栏跟随主题 |
| `Internal/*.cs` | 全 `internal` | 滚动条资源、剪贴板接管、窗口动画/拖拽缩放、`ThemeSync` |

### 1.3 三分钟接入

```xml
<!-- 1) 引入命名空间（源码工程与 NuGet 包都是这个 uri） -->
xmlns:ui="clr-namespace:StartUI4Controls;assembly=StartUI4Controls"
```

```xml
<!-- 2) 直接写控件：不需要额外的资源字典 -->
<ui:UI4Button Content="你好，StartUI4" Width="200" Height="40" />
```

```csharp
// 3) App 启动时接上主题（顺序见 §6.1；不写这句也能用，默认就是 light）
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    UI4ThemePacks.RegisterAll();      // 注册 8 套预置主题
    UI4Theme.ApplyToApplication();    // 把令牌写进 Application.Resources，宿主可用 {DynamicResource}
    UI4Theme.SetTheme(UI4ThemeMode.System);
}
```

```xml
<!-- 4) 宿主自己的样式消费同一批令牌，切主题即自动跟随 -->
<TextBlock Foreground="{DynamicResource UI4.Brush.TextForeground}" />
```

---

## 二、组件清单

| 分组 | 类型 | 基类 | 属性数 | 说明 |
|---|---|---|---|---|
| 基础交互 | `UI4Button` | `Button` | 6 | 渐变按钮；禁用态换模板，前景取「与底色对比度更高」的那个 |
| 基础交互 | `UI4CheckBox` | `CheckBox` | 6(+1 别名) | 勾选框 |
| 基础交互 | `UI4Radio` | `RadioButton` | 6 | 单选按钮 |
| 基础交互 | `UI4Switch` | `Control` | 7 | 滑动开关，`IsOn` 默认双向绑定 |
| 文本输入 | `UI4TextBox` | `TextBox` | 10 | 占位符 / 清除按钮 / 聚焦描边 |
| 文本输入 | `UI4PasswordBox` | `TextBox` | 13 | 密码框，可绑定 `Password`、自定义掩码 |
| 文本输入 | `UI4TextBlock` | `ContentControl` | 10 | 面板文本，渐变字走 `Foreground` |
| 文本输入 | `UI4FlipTextBlock` | `ContentControl` | 11 | 翻牌数字动画 |
| 选择 | `UI4ComboBox` | `ComboBox` | 8 | 弹层宽度自适应的下拉框 |
| 对话框 | `UI4ColorPicker` | `Window` | — | HSV 取色，静态 `ShowDialog` |
| 对话框 | `UI4MessageBox` | `Window` | — | 消息框，`OK` / `OKCancel` |
| 进度 | `UI4ProgressBar` | `Control` | 8 | 渐变进度条 |
| 进度 | `UI4ProgressRing` | `ContentControl` | 13 | 环形进度 |
| 滑块 | `UI4Slider` | `Slider` | 6 | 线性滑块 |
| 滑块 | `UI4CircleSlider` | `ContentControl` | 10 | 环形滑块 |
| 容器 | `UI4Panel` | `ContentControl` | 10 | 阴影 + 悬浮缩放卡片 |
| 容器 | `UI4Grid` | `Grid` | 2 | 默认页面渐变底 |
| 容器 | `UI4ScrollViewer` | `ScrollViewer` | 1 | 美化滚动条 + 平滑滚动 |
| 列表 | `UI4ListBox` | `ListBox` | 13 | 普通 / 圆点 / 编号；`IsMenuMode` 供菜单类组件复用 |
| 列表 | `UI4ListView` | `ListBox` | 17 | 卡片列表 |
| 列表 | `UI4GridView` | `ListBox` | 15 | 自适应列数网格卡片 |
| 导航 | `UI4Pivot` / `UI4PivotItem` | `Selector` / `HeaderedContentControl` | 10 / 1 | 滑动页签 |
| 导航 | `UI4Tab` / `UI4TabItem` | `Selector` / `HeaderedContentControl` | 11 / 6 | 浏览器风格标签 |
| 导航 | `UI4NavigationView` / `…Item` / `…BottomItem` | `ItemsControl` / `ContentControl` | 13 / 4 | 侧边导航 + 内容区 |
| 菜单 | `UI4Menu` / `UI4MenuElementItem` / `UI4MenuSeparatorElement` | `Menu` / `MenuItem` / `Separator` | 6 / 5 / 1 | 菜单栏 |
| 菜单 | `UI4ContextMenu` | —（纯代码组件） | 5 个普通属性 | 右键菜单，内置 7 种标准条目 |
| 编辑器 | `UI4CodeEditor` | AvalonEdit `TextEditor` | — | C# 高亮 + 内置右键菜单 |
| 系统集成 | `UI4NotifyIcon` | `FrameworkElement`、`IDisposable` | 6 | 托盘图标 + 自定义菜单 |
| 系统集成 | `UI4WindowTitleBar` | —（附加属性 + 静态方法） | 1 附加 | 标题栏随 DWM 染色 |
| 主题 | `UI4Theme` | —（静态服务） | — | 模式 / 令牌资源桥 / 强调色 / 持久化 |
| 主题 | `UI4ThemeScope` | —（附加属性） | 1 附加 | 子树局部换肤 |
| 主题 | `UI4ThemeDefinition` | `sealed class` | — | 一份主题的令牌取值 |
| 主题 | `UI4ThemePacks` | —（静态类） | — | 8 套预置业务主题 |
| 主题 | `UI4ThemeToken` / `UI4ThemeMode` | 枚举 | 38 / 4 | 令牌键与模式 |
| 主题 | `IThemePersistence` + 2 个实现 | 接口 | — | HKCU 注册表 / JSON 文件 |
| 服务 | `UI4Clipboard` | —（静态类） | — | 原生 Win32 剪贴板 |
| 服务 | `UI4MultiLanguage` | —（静态类） | — | 8 套静态文案 |

---

## 三、组件详解

**表格约定**

- 颜色与画刷默认值一律写 `#RRGGBB` / `#AARRGGBB`；`Thickness` 写 `l,t,r,b`。
- **跟随令牌**列写的是构造函数里 `SetResourceReference` 挂上的资源键（`UI4.Color.X` / `UI4.Brush.X`）。
  挂了令牌的属性在宿主**未显式赋值**时随主题变；一旦在 XAML/代码里赋过值，就停止跟随（本地值优先，这是预期行为，不是缺陷）。
- **继承属性**只列常用项：控件继承基类的一切属性与事件（`Content`、`Width`、`Margin`、`FontSize`、
  `Foreground`、`IsEnabled`、`Click`……），本手册不重复基类文档。

### 3.1 基础交互

#### UI4Button（`UI4Button.cs`，基类 `Button`）

渐变按钮。常态用 `GradientStart → GradientEnd` 的水平渐变（`0,0.5 → 1,0.5`），悬浮与禁用各自换一整套模板。

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 圆角半径 |
| `GradientStart` | `Color` | `#0078D4` | `UI4.Color.Accent` | 渐变起始色 |
| `GradientEnd` | `Color` | `#9333EA` | `UI4.Color.AccentEnd` | 渐变结束色 |
| `HoverBackground` | `Brush` | `#0066B5` | `UI4.Brush.AccentDark` | 悬浮背景 |
| `HoverBorderBrush` | `Brush` | `null` | — | 悬浮描边；`null` = 沿用 `BorderBrush` |
| `HoverForeground` | `Brush` | `White` | `UI4.Brush.OnAccent` | 悬浮前景 |

样式默认值（不是 DP，宿主可直接覆盖）：`MinHeight=30`、`Padding=10,0,10,0`、`FontSize=15`、`FontWeight=SemiBold`、`Cursor=Hand`、`BorderThickness=0`。

**前景色怎么定的**：`Foreground` 的样式默认值是 `ForegroundFor(Blend(GradientStart, GradientEnd))`——
在 `UI4.Brush.OnAccent` 与 `UI4.Brush.TextForeground` 之间**取与底色对比度更高的那个**（WCAG 2.x 比值），
而不是按亮度阈值判深浅。原因写在源码注释里：高对比度主题的强调色是亮黄（相对亮度 ≈0.93），
阈值判法会把它当浅底，而该主题的正文色本就是给黑底准备的白字，于是白字压黄底（≈1.07:1，几乎不可读）。
这只是样式 Setter 的默认值，**宿主本地显式设置的 `Foreground` 仍然优先**。

**禁用态（`IsEnabled=False` 或绑定 `Command` 且 `CanExecute` 为 false）**：整块换成禁用模板，
背景固定为 `DynamicResource UI4.Brush.OffBackground`、前景 `UI4.Brush.TextMuted`、`BorderThickness=0`。

- 为什么换 `Template` 而不是设 `Background`/`GradientStart`：宿主在 XAML 里本地赋过渐变值后，
  样式 Setter 永远压不过本地值——这正是"复制按钮禁用了却还是蓝色"的成因。
- 为什么用 `OffBackground` 而不是 `BorderNormal`：高对比度主题下 `BorderNormal` 是纯白，会和白字叠成不可读；
  `OffBackground` 在三套内置主题里都是中性灰阶（HC 为深灰）。

事件与属性全部继承 `Button`（`Click`、`Command`、`CommandParameter`、`Content`……）。

```xml
<ui:UI4Button Content="确定" Width="100" Height="30" />
<ui:UI4Button Content="渐变按钮" GradientStart="#FF0024FF" GradientEnd="#FFB400FF"
              Width="150" Height="40" CornerRadius="20" />
<ui:UI4Button Content="跟随主题" />   <!-- 三个令牌引用在构造函数里挂好，切主题自动变 -->
<ui:UI4Button Content="本地值不参与主题" GradientStart="#FF101010" GradientEnd="#FF202020" />
```

#### UI4CheckBox（`UI4CheckBox.cs`，基类 `CheckBox`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 勾选块圆角 |
| `CheckBackground` | `Color` | `#0066B5` | `UI4.Color.CheckBackground` | 勾选态填充色 |
| `BorderNormalColor` | `Color` | `#B4B4C8` | `UI4.Color.BorderNormal` | 未勾选边框色 |
| `BoxSize` | `double` | `18` | — | 勾选块边长 |
| `TextColor` | `Color` | `#1E1E1E` | `UI4.Color.TextForeground` | 文字颜色 |
| `TextMargin` | `Thickness` | `8,0,0,0` | — | 文字与勾选块的间距 |

- `BoxCornerRadius` 是 `CornerRadius` 的**别名**（`BoxCornerRadiusProperty = CornerRadiusProperty`，同一个 DP），
  写哪个都行，只为兼容既有 XAML。
- 未勾选框色与悬浮框色由两个 **private** DP 挂在 `UI4.Color.CheckBoxUnchecked` / `UI4.Color.HoverBorderColorLight` 上，
  跟随主题但没有公开设置入口；要改这两处颜色请用主题令牌，或在 `UI4ThemeScope` 里换主题。
- `IsChecked`、`IsThreeState`、`Checked` / `Unchecked` / `Indeterminate` 继承自 `CheckBox`。

```xml
<ui:UI4CheckBox Content="同意服务条款" IsChecked="True" Margin="10" />
<ui:UI4CheckBox Content="大号" CheckBackground="Green" BorderNormalColor="DarkGreen"
                BoxSize="24" BoxCornerRadius="6" />
```

#### UI4Radio（`UI4Radio.cs`，基类 `RadioButton`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CheckBackground` | `Color` | `#0066B5` | `UI4.Color.CheckBackground` | 选中态圆环颜色 |
| `BorderNormalColor` | `Color` | `#B4B4C8` | **`UI4.Color.BorderSecondary`** | 未选中边框色 |
| `DotColor` | `Color` | `White` | — | 选中圆点颜色 |
| `BoxSize` | `double` | `18` | — | 圆圈直径 |
| `TextColor` | `Color` | `Black` | `UI4.Color.TextForeground` | 文字颜色 |
| `TextMargin` | `Thickness` | `8,0,0,0` | — | 文字间距 |

> 与 `UI4CheckBox` 的唯一令牌差异：单选框的常态边框挂 `BorderSecondary`，复选框挂 `BorderNormal`。
> 内置 light 下两者分别是 `#B4B4C8` / `#C8C8DC`，所以默认外观就比复选框的框深一档。
> `TextColor` 的字面默认值也是 `Black`（复选框是 `#1E1E1E`），实际渲染由令牌覆盖。

```xml
<ui:UI4Radio Content="选项 1" IsChecked="True" GroupName="G1" Margin="5" />
<ui:UI4Radio Content="选项 2" GroupName="G1" Margin="5" />
```

#### UI4Switch（`UI4Switch.cs`，基类 `Control`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `IsOn` | `bool` | `false` | — | 开关状态，**注册为 `BindsTwoWayByDefault`** |
| `GradientStart` | `Color` | `#0078D4` | `UI4.Color.Accent` | 开启态渐变起始色 |
| `GradientEnd` | `Color` | `#0078D4` | `UI4.Color.Accent` | 开启态渐变结束色（默认与起始同色 = 纯色） |
| `OffBackground` | `Color` | `#C8C8D2` | `UI4.Color.OffBackground` | 关闭态底色 |
| `ThumbColor` | `Color` | `White` | — | 滑块颜色 |
| `SwitchWidth` | `double` | `50` | — | 轨道宽 |
| `SwitchHeight` | `double` | `28` | — | 轨道高（滑块直径 = `SwitchHeight - 6`，轨道圆角 = `SwitchHeight / 2`） |

| 事件 | 签名 | 说明 |
|---|---|---|
| `Toggled` | `RoutedEventHandler`，`RoutingStrategy.Bubble` | 状态变化时触发（含代码赋值） |

- 视觉树是代码搭的：`Grid[ 轨道 Border（居中）, 滑块 Ellipse（TranslateTransform 位移）]`。
  控件被横向拉宽时轨道仍按 `SwitchWidth × SwitchHeight` 居中摆放，**不会被拉变形**。
- 交互只有一个入口：`OnMouseLeftButtonUp` 切换 `IsOn`（`Cursor=Hand`）。**没有键盘切换**，
  需要空格/回车切开关的宿主自己挂 `KeyDown`。
- 在 XAML 里写 `IsOn="True"` 会在初始化阶段就触发一次 `Toggled`，处理器要对尚未构造完的成员判空。
- `Unloaded` 时会摘掉 `SizeChanged` / `Loaded` / `Unloaded` 自身订阅，反复装卸不泄漏。

```xml
<ui:UI4Switch IsOn="{Binding WifiEnabled}" Toggled="Switch_Toggled" />

<ui:UI4Switch IsOn="True" SwitchWidth="60" SwitchHeight="32"
              GradientStart="Green" GradientEnd="DarkGreen" OffBackground="LightGray" />
```

```csharp
private void Switch_Toggled(object sender, RoutedEventArgs e)
{
    var sw = (UI4Switch)sender;
    StatusText.Text = sw.IsOn ? "已开启" : "已关闭";
}
```

---

### 3.2 文本与输入

#### UI4TextBox（`UI4TextBox.cs`，基类 `TextBox`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 圆角 |
| `BorderNormalColor` | `Color` | `#C8C8DC` | `UI4.Color.BorderNormal` | 常态边框色 |
| `HoverBorderColor` | `Color` | `#0078D4` | `UI4.Color.BorderHover` | 悬浮边框色 |
| `FocusBorderColor` | `Color` | `#0066B5` | `UI4.Color.BorderFocus` | 聚焦边框色 |
| `EditBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 编辑区背景 |
| `TextColor` | `Color` | `#1E1E1E` | `UI4.Color.TextForeground` | 文字颜色 |
| `InnerPadding` | `Thickness` | `12,5,32,5` | — | 内容内边距；右侧 32 是给按钮留的位 |
| `ShowClearButton` | `bool` | `false` | — | 是否显示清除按钮 |
| `PlaceholderText` | `string` | `""` | — | 占位符文本 |
| `PlaceholderForeground` | `Brush` | `LightGray` | `UI4.Brush.Placeholder` | 占位符颜色 |

行为契约：

- 占位符在有文本时自动隐藏；清除按钮只在 `ShowClearButton=True` **且** 有文本时出现。
  清除按钮/图标的颜色由两个 private DP 挂 `UI4.Color.Icon` / `UI4.Color.IconHover`。
- 剪贴板走原生通道：`Internal.ClipboardCommandTakeover.Install(this)` 在**隧道阶段**吃掉
  `Ctrl+C` / `Ctrl+X` / `Ctrl+V`，改由 [`UI4Clipboard`](#51-ui4clipboard) 读写，WPF 的 OLE 通道不会被触发。
  这样截图工具、剪贴板历史程序持有全局剪贴板锁时，UI 线程不会卡在秒级重试或 `CLIPBRD_E_CANT_OPEN` 上。
- 右键菜单是库内的 [`UI4ContextMenu`](#39-菜单与对话框)（宽 170，条目：撤销 / 剪切 / 复制 / 粘贴 / 删除 / 全选），
  构造后把 `ContextMenu` 置 `null` 屏蔽 WPF 默认菜单，再 `Attach(this)`。
- 多行：继承 `AcceptsReturn`、`TextWrapping`、`VerticalScrollBarVisibility`、`MaxLength`、`IsReadOnly`、`Text` 等。

```xml
<ui:UI4TextBox Width="260" Text="可编辑文本" />
<ui:UI4TextBox Width="300" PlaceholderText="请输入用户名..." />
<ui:UI4TextBox Width="260" Text="带清除按钮" ShowClearButton="True" />

<ui:UI4TextBox Width="450" Height="100" AcceptsReturn="True" TextWrapping="Wrap"
               VerticalScrollBarVisibility="Auto" ShowClearButton="True" Text="多行文本" />
```

#### UI4PasswordBox（`UI4PasswordBox.cs`，基类 `TextBox`）

密码框基于 `TextBox` 自建掩码（不是 `System.Windows.Controls.PasswordBox`），因此掩码字符可自定义、
`Password` 可以绑定。

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 圆角 |
| `BorderNormalColor` | `Color` | `#C8C8DC` | `UI4.Color.BorderNormal` | 常态边框色 |
| `HoverBorderColor` | `Color` | `#0078D4` | `UI4.Color.BorderHover` | 悬浮边框色 |
| `FocusBorderColor` | `Color` | `#0066B5` | `UI4.Color.BorderFocus` | 聚焦边框色 |
| `EditBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 编辑区背景 |
| `TextColor` | `Color` | `#1E1E1E` | `UI4.Color.TextForeground` | 文字颜色 |
| `InnerPadding` | `Thickness` | `12,5,32,5` | — | 内容内边距（右侧给切换按钮留位） |
| `ShowPasswordButton` | `bool` | `true` | — | 是否显示明文/密文切换按钮 |
| `PlaceholderText` | `string` | `""` | — | 占位符 |
| `PlaceholderForeground` | `Brush` | `LightGray` | `UI4.Brush.Placeholder` | 占位符颜色 |
| `Password` | `string` | `""` | — | 密码明文；**普通 `PropertyMetadata`，绑定要写 `Mode=TwoWay`** |
| `PasswordChar` | `char` | `●` | — | 掩码字符 |
| `IsPasswordMode` | `bool` | `true` | — | 当前是否密文显示 |

| 方法 | 说明 |
|---|---|
| `ClearPassword()` | 清空密码 |

行为与**安全注意**：

- `Password` 以普通 `string` 存放在内存里，可能被内存转储或调试器读到；对安全要求高的场景请改用 WPF 原生
  `System.Windows.Controls.PasswordBox`（它用不安全内存块存储）。这是源码 XML 注释里明确写的取舍，不是实现瑕疵。
- 掩码切换按钮的图标色挂 `UI4.Color.Icon` / `UI4.Color.IconHover`；**切到明文态时按钮前景改用 `UI4.Brush.Accent`**，
  用颜色提醒当前正在明文显示。
- 输入过滤：`PreviewTextInput` + `PreviewKeyDown`；`ApplicationCommands.Cut/Copy/Paste` 由 `CommandBinding`
  接管（`OnPasteCanExecute` 恒为 true 并 `Handled`），粘贴逻辑与 [§5.1 `UI4Clipboard`](#51-ui4clipboard) 同一条通道。
- `Password` 不是 `BindsTwoWayByDefault`（与 `UI4Switch.IsOn` 不同），绑定必须显式 `Mode=TwoWay`。

```xml
<ui:UI4PasswordBox PlaceholderText="请输入密码" Width="260" Height="36" />
<ui:UI4PasswordBox PasswordChar="*" ShowPasswordButton="True" Width="260" />
<ui:UI4PasswordBox Password="{Binding UserPassword, Mode=TwoWay}" Width="260" />
```

#### UI4TextBlock（`UI4TextBlock.cs`，基类 `ContentControl`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `Text` | `string` | `""` | — | 文本；`Text` 为空时回落到 `Content`（内部 `TextOrContentConverter` 多路绑定） |
| `CornerRadius` | `CornerRadius` | `6` | — | 背景面板圆角 |
| `PanelBackground` | `Brush` | `Transparent` | — | 背景面板色 |
| `HorizontalContentAlign` | `HorizontalAlignment` | `Left` | — | 内容水平对齐 |
| `VerticalContentAlign` | `VerticalAlignment` | `Center` | — | 内容垂直对齐 |
| `TextWrapping` | `TextWrapping` | `NoWrap` | — | 换行方式 |
| `ShadowDepth` | `double` | `8` | — | 阴影偏移 |
| `ShadowBlurRadius` | `double` | `5` | — | 阴影模糊半径 |
| `ShadowOpacity` | `double` | `0` | — | 阴影不透明度（`0` = 无阴影） |
| `ShadowColor` | `Color` | `Black` | `UI4.Color.Shadow` | 阴影颜色 |

被 `OverrideMetadata` 改了默认值的**继承属性**（库默认值，父级显式赋值仍会覆盖）：
`Padding = 8,6,8,6`、`FontSize = 15`、`FontWeight = Normal`；`Foreground` 挂 `UI4.Brush.TextForeground`。

- **渐变字**：给 `Foreground` 传 `LinearGradientBrush`。库里的 `GradientStart` / `GradientEnd` 死属性
  已经删掉了（`BuildTextStyle` 从未消费它们，设了不生效）。
- 模板里承载文字的是一个内部 `TextBox`（只用于渲染），因此文字可选中、右键有**复制 / 全选**两项菜单。
- `Content` 与 `Text` 二选一即可，两个都写时 `Text` 优先。

```xml
<ui:UI4TextBlock Text="普通文本" FontSize="24" />
<ui:UI4TextBlock Content="带阴影的文本" FontSize="26"
                 ShadowDepth="8" ShadowOpacity="0.6" ShadowBlurRadius="10" />

<ui:UI4TextBlock Text="渐变文本" FontSize="32" FontWeight="SemiBold">
    <ui:UI4TextBlock.Foreground>
        <LinearGradientBrush StartPoint="0,0.5" EndPoint="1,0.5">
            <GradientStop Color="#FF2762EB" />
            <GradientStop Color="#FFAD00FF" Offset="1" />
        </LinearGradientBrush>
    </ui:UI4TextBlock.Foreground>
</ui:UI4TextBlock>
```

#### UI4FlipTextBlock（`UI4FlipTextBlock.cs`，基类 `ContentControl`）

数字翻牌：`Text` 变化时逐位以卡片翻转动画过渡，适合计数器、时钟、指标看板。

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `Text` | `string` | `"0"` | — | 显示文本；赋值即触发动画 |
| `FlipRate` | `double` | `0.3` | — | 翻转速率（每个字符段的秒数） |
| `CardBackground` | `Color` | `White` | `UI4.Color.Surface` | 卡片背景 |
| `CardForeground` | `Color` | `Black` | `UI4.Color.TextForeground` | 卡片文字色 |
| `CardBorderBrush` | `Color` | `Gray` | `UI4.Color.BorderNormal` | 卡片边框色 |
| `CardCornerRadius` | `CornerRadius` | `12` | — | 卡片圆角 |
| `CardBorderThickness` | `Thickness` | `1` | — | 卡片边框厚度 |
| `ShadowColor` | `Color` | `Black` | — | 阴影颜色（不跟随令牌） |
| `CardShadowDepth` | `double` | `10` | — | 阴影偏移 |
| `CardShadowBlurRadius` | `double` | `15` | — | 阴影模糊 |
| `CardShadowOpacity` | `double` | `0.1` | — | 阴影不透明度 |

```xml
<ui:UI4FlipTextBlock x:Name="FlipText" Text="42" FontSize="64" />
<ui:UI4FlipTextBlock Text="7" FontSize="48" CardBackground="DarkGreen"
                     CardForeground="Gold" FlipRate="0.5" />
```

```csharp
FlipText.Text = new Random().Next(0, 100).ToString();   // 赋值即触发翻转
```

> **翻牌中缝是硬编码色**：上下两半之间那条分隔线写死 `#33000000`，不跟随任何令牌，
> 暗色与高对比度下都偏淡。`UI4ThemeToken.Separator` 的注释虽写着「菜单分隔、翻牌中缝」，
> 但目前库里没有任何地方消费它——见 [§4.11 主题盲区](#411-主题盲区与已知限制)。

### 3.3 下拉与取色

#### UI4ComboBox（`UI4ComboBox.cs`，基类 `ComboBox`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 控件圆角 |
| `BorderNormalColor` | `Color` | `#C8C8DC` | `UI4.Color.BorderNormal` | 常态边框色 |
| `FocusGradientStart` | `Color` | `#0078D4` | `UI4.Color.Accent` | 聚焦渐变描边起始色 |
| `FocusGradientEnd` | `Color` | `#9333EA` | `UI4.Color.AccentEnd` | 聚焦渐变描边结束色 |
| `EditBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 闭合态编辑区背景 |
| `TextColor` | `Color` | `#1E1E1E` | `UI4.Color.TextForeground` | 文字颜色 |
| `InnerPadding` | `Thickness` | `12,4,30,4` | — | 内边距（右侧 30 给下拉箭头留位） |
| `DropCornerRadius` | `CornerRadius` | `6` | — | 下拉面板圆角 |

行为契约（模板里都写死了，宿主不用配）：

- **弹层至少和控件一样宽**：`Popup.MinWidth` 绑 `TemplatedParent.ActualWidth`，内容更宽时自然撑开；
  `Popup` 用 `Placement=Bottom` + `AllowsTransparency=true` + `PopupAnimation.Fade`。
- **选中项超长不溢出**：闭合态文本 `TextTrimming=CharacterEllipsis`，并给字符串项自动挂同名 ToolTip
  （内部 `StringSelectionConverter`，非公开类型）。
- **项容器样式内置**：项高 36、`Padding=10,0,0,0`，悬浮底 `DynamicResource UI4.Brush.HoverOverlay`、
  选中底 `UI4.Brush.SelectedOverlay`，光标 Hand。换 `ItemContainerStyle` 会覆盖这套。
- 下拉滚动时滚动条淡入、停止后淡出（挂 `PreviewMouseWheel` / `ScrollChanged`）。
- `IsEditable=True` 时切换为内嵌编辑框，样式同步应用。

> **一个当前实现的副作用**：样式把 `MinWidth` 自绑到自身 `ActualWidth`（`UI4ComboBox.cs:212`）。
> 于是控件被长项撑宽后不会自动缩回。要固定宽度就显式给 `Width`；要允许收缩就自己覆盖 `MinWidth`。

```xml
<ui:UI4ComboBox Width="300" Height="36" SelectedIndex="0">
    <ComboBoxItem>选项 1</ComboBoxItem>
    <ComboBoxItem>选项 2</ComboBoxItem>
</ui:UI4ComboBox>

<!-- 数据绑定 -->
<ui:UI4ComboBox Width="220" ItemsSource="{Binding Providers}"
                DisplayMemberPath="Name" SelectedValuePath="Id"
                SelectedValue="{Binding ProviderId, Mode=TwoWay}" />
```

#### UI4ColorPicker（`UI4ColorPicker.cs`，基类 `Window`）

HSV 取色对话框：二维色图 + 色相条 + HEX / ARGB 输入 + RGB/HSV 模式切换。没有依赖属性，接口全在构造与方法上。

| 成员 | 签名 | 说明 |
|---|---|---|
| 构造函数 | `UI4ColorPicker(string title = null, Color? defaultColor = null)` | 建实例；`title` 缺省用 `UI4MultiLanguage` 的 `ColorPicker` 文案 |
| `ShowDialog` | `static Color? ShowDialog(string title = null, Color? defaultColor = null, Window owner = null)` | 模态取色；**取消返回 `null`** |
| `Show` | `bool? Show(Window owner = null)` | 实例方式显示，返回是否确认 |
| `SelectedColor` | `Color`（只读） | 确认后的颜色 |

- 对话框可八向拖边框改尺寸（`Internal.WindowResizeBehavior`）。
- 主题：窗口前景挂 `UI4.Brush.TextForeground`，主容器背景挂 `UI4.Brush.Surface`，色图外框挂 `UI4.Brush.BorderNormal`。
- 标题与按钮文字跟随 `UI4MultiLanguage` 当前语言。

```csharp
Color? picked = UI4ColorPicker.ShowDialog("选择强调色",
    UI4Theme.Current.ColorOf(UI4ThemeToken.Accent), this);
if (picked.HasValue) UI4Theme.SetAccent(picked.Value);

var picker = new UI4ColorPicker("选择颜色", Colors.Green);
if (picker.Show(this) == true) Apply(picker.SelectedColor);
```

---

### 3.4 进度指示

#### UI4ProgressBar（`UI4ProgressBar.cs`，基类 `Control`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `5` | — | 圆角 |
| `GradientStart` | `Color` | `#0096E6` | `UI4.Color.ProgressStart` | 渐变起始色 |
| `GradientEnd` | `Color` | `#0078D4` | `UI4.Color.Accent` | 渐变结束色 |
| `TrackBackground` | `Color` | `#0A000000` | `UI4.Color.TrackBackground` | 轨道底色 |
| `IsIndeterminate` | `bool` | `false` | — | 不确定模式（往返动画） |
| `Minimum` / `Maximum` | `double` | `0` / `100` | — | 取值范围 |
| `Value` | `double` | `0` | — | 当前值 |

- **本地设置 `Background` 会覆盖渐变**：取值顺序是「`Background` 有本地值且非 null → 用它；否则用
  `GradientStart → GradientEnd` 的渐变」。所以 `Background="Red"` 得到纯色进度条，是设计如此。
- `IsIndeterminate=True` 时不读 `Value`，显示往返指示块。
- `Minimum` / `Maximum` 变化只重算比例，不重置 `Value`。

```xml
<ui:UI4ProgressBar Value="50" Maximum="100" Width="200" Height="6" />
<ui:UI4ProgressBar IsIndeterminate="True" Width="200" Height="6" />
<ui:UI4ProgressBar Value="50" Width="200" Background="Red" />   <!-- 纯色，覆盖渐变 -->
```

#### UI4ProgressRing（`UI4ProgressRing.cs`，基类 `ContentControl`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `IsActive` | `bool` | `true` | — | 是否显示（false 时整个环收起） |
| `IsIndeterminate` | `bool` | `true` | — | 不确定模式：旋转弧 |
| `Minimum` / `Maximum` | `double` | `0` / `100` | — | 取值范围 |
| `Value` | `double` | `0` | — | 当前值（确定模式） |
| `AnimatedValue` | `double` | `0` | — | 动画驱动值，**库内使用**，宿主只读不写 |
| `RingBackground` | `Brush` | `#0A000000` | `UI4.Brush.TrackBackground` | 环底色 |
| `RingForeground` | `Brush` | `#0078D4` | `UI4.Brush.Accent` | 进度环颜色，可传渐变画笔 |
| `RingThickness` | `double` | `6` | — | 环宽 |
| `ShowValueText` | `bool` | `true` | — | 中心是否显示数值 |
| `ValueFontSize` | `double` | `30` | — | 中心数值字号 |
| `EnableStartupAnimation` | `bool` | `true` | — | 加载时从 0 转到当前值 |
| `StartupAnimationDuration` | `double` | `0.5` | — | 启动动画秒数 |

```xml
<ui:UI4ProgressRing IsActive="True" IsIndeterminate="True" Width="80" Height="80" />

<ui:UI4ProgressRing IsIndeterminate="False" Value="75" Maximum="100"
                    RingThickness="10" ShowValueText="True" ValueFontSize="24"
                    Width="120" Height="120" />
```

---

### 3.5 滑块

#### UI4Slider（`UI4Slider.cs`，基类 `Slider`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 轨道圆角 |
| `GradientStart` | `Color` | `#0078D4` | `UI4.Color.Accent` | 已选段渐变起点 |
| `GradientEnd` | `Color` | `#0078D4` | `UI4.Color.Accent` | 已选段渐变终点（默认同色 = 纯色） |
| `TrackBackground` | `Color` | `White` | `UI4.Color.Surface` | 未选段底色 |
| `ThumbSize` | `double` | `16` | — | 滑块直径 |
| `IsValueVisible` | `bool` | `true` | — | 是否显示数值行 |

- `IsValueVisible=True` 时在轨道下方排三个 `TextBlock`：最小值 / 当前值 / 最大值，`StringFormat=F0`
  （**取整显示**，要小数请关掉数值行自己显示），前景继承控件 `Foreground`。
- 拖动换算：`ΔValue = HorizontalChange / (ActualWidth - ThumbSize) × (Maximum - Minimum)`，并钳在区间内。
- `Minimum` / `Maximum` / `Value` / `TickFrequency` 等继承自 `Slider`。

```xml
<ui:UI4Slider Value="50" Maximum="100" Width="200" Height="20" />
<ui:UI4Slider Value="50" Maximum="200" Width="200" IsValueVisible="False"
              GradientStart="#FFFFF900" GradientEnd="Red" />
```

#### UI4CircleSlider（`UI4CircleSlider.cs`，基类 `ContentControl`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `Value` | `double` | `0` | — | 当前值 |
| `Minimum` / `Maximum` | `double` | `0` / `100` | — | 取值范围 |
| `SmallChange` | `double` | `1` | — | 步进 |
| `RingThickness` | `double` | `8` | — | 环宽 |
| `RingForeground` | `Brush` | `#0078D4` | `UI4.Brush.Accent` | 进度环颜色（可渐变） |
| `RingBackground` | `Brush` | `#0A000000` | `UI4.Brush.TrackBackground` | 环底色 |
| `ShowValueText` | `bool` | `false` | — | 中心是否显示数值 |
| `ValueFontSize` | `double` | `30` | — | 中心数值字号 |
| `AnimationDuration` | `double` | `0.5` | — | 值变化动画秒数 |

- 交互：`OnMouseLeftButtonDown` 记拖拽并开始 `CaptureMouse()`，`OnMouseMove` 期间按**指针相对圆心的角度**
  反算 `Value`；抬起鼠标结束。所以拖动跨 12 点方向时按角度连续取值，不做整圈累加。
- 没有对外事件；要跟值走就绑 `Value`（`SetResourceReference` 之外它就是普通 DP）。

```xml
<ui:UI4CircleSlider Value="60" ShowValueText="True" />

<ui:UI4CircleSlider Width="200" Height="200" RingThickness="12" Maximum="360"
                    Value="60" ShowValueText="True" ValueFontSize="60" />
```

### 3.6 容器与布局

#### UI4Panel（`UI4Panel.cs`，基类 `ContentControl`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `12` | — | 圆角 |
| `BorderColor` | `Color` | `#3C788CC8` | — | 常态描边色（`Color` 型，专为驱动 `ColorAnimation`） |
| `HoverBorderBrush` | `SolidColorBrush` | `#46788CC8` | — | 悬浮描边色 |
| `ShadowDepth` | `double` | `0` | — | 阴影偏移 |
| `ShadowBlurRadius` | `double` | `15` | — | 阴影模糊 |
| `ShadowOpacity` | `double` | `0.1` | — | 阴影不透明度 |
| `ShadowColor` | `Color` | `Black` | `UI4.Color.Shadow` | 阴影颜色 |
| `ContentPadding` | `Thickness` | `0` | — | 内容内边距 |
| `HoverAnimationDuration` | `Duration` | `200ms` | — | 悬浮动画时长 |
| `HoverScale` | `double` | `1.005` | — | 悬浮缩放倍率 |

模板结构（决定了两个常见疑问）：

```
Grid PART_Grid                    ← ScaleTransform 挂这里（悬浮缩放整块一起放大）
 ├─ Border PART_ShadowBorder      ← DropShadowEffect 只挂这里，Background 也来自这里
 ├─ Border PART_InnerBorder       ← 背景 Transparent，承载 BorderBrush/BorderThickness + ColorAnimation
 │    └─ ContentPresenter
```

- **缩放时文字为什么不发虚**：`DropShadowEffect` 只作用于 `PART_ShadowBorder`，内容层不吃这个位图效果，
  所以整体放大仍是矢量重绘。（不是"内容不缩放"——内容确实跟着 `PART_Grid` 一起放大。）
- 要 Brush 型描边请用继承来的 `BorderBrush`；`BorderColor` 是给悬浮颜色动画用的 `Color`，两者不要混。
- 面板底色来自继承的 `Background`，构造函数里挂 `UI4.Brush.Background`。

```xml
<ui:UI4Panel Width="300" Height="200">
    <TextBlock Text="面板内容" HorizontalAlignment="Center" VerticalAlignment="Center" />
</ui:UI4Panel>

<ui:UI4Panel Width="300" Height="200" ShadowDepth="15" ShadowOpacity="0.6"
             HoverScale="1.05" BorderColor="#3C788CC8" ContentPadding="16" />
```

#### UI4Grid（`UI4Grid.cs`，基类 `Grid`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `GradientStart` | `Color` | `White` | `UI4.Color.BackgroundGradientStart` | 纵向渐变起点（`0.5,0`） |
| `GradientEnd` | `Color` | `White` | `UI4.Color.BackgroundGradientEnd` | 纵向渐变终点（`0.5,1`） |

除默认背景外与 `Grid` 完全一致（行列定义、`Span`、共享尺寸照旧）。

> **设 `Background` 不会永久生效**：`UpdateBackground()` 是直接 `SetValue(BackgroundProperty, 渐变刷)`，
> 在构造与每次 `GradientStart`/`GradientEnd` 变化（含主题切换）时执行。宿主写 `Background="White"` 能盖住当前这一帧，
> 但下一次换主题就被写回渐变。**页面底色请用主题令牌**（`UI4Theme.SetAccent` / 自定义主题 / `UI4ThemeScope`），
> 或者干脆用普通 `Grid` + `{DynamicResource UI4.Brush.Background}`。

#### UI4ScrollViewer（`UI4ScrollViewer.cs`，基类 `ScrollViewer`）

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `IsSmoothScrollEnabled` | `bool` | `true` | 滚轮走平滑动画而不是瞬移 |

| 方法 | 说明 |
|---|---|
| `SmoothScrollToVerticalOffset(double offset)` | 平滑滚到指定纵向偏移 |
| `SmoothScrollToHorizontalOffset(double offset)` | 平滑滚到指定横向偏移 |

滚动条样式（细条、圆角滑块、滚动时淡入静止后淡出）来自 `Internal/ScrollBarResources.cs` 里的 XAML 字符串，
其中滑块色挂 `{DynamicResource UI4.Brush.ScrollBarThumb}`、外框挂 `UI4.Brush.BorderWeak`——所以美化滚动条本身也跟随主题。
`UI4ListBox` / `UI4ListView` / `UI4GridView` / `UI4ComboBox` / `UI4CodeEditor` 内部复用同一份样式。

---

### 3.7 列表与卡片

三个列表控件都派生自 `ListBox`，所以 `ItemsSource`、`ItemTemplate`、`SelectedItem`、
`SelectionChanged`、`SelectionMode` 等一律继承可用。

#### UI4ListBox（`UI4ListBox.cs`，基类 `ListBox`）

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `CornerRadius` | `CornerRadius` | `6` | — | 面板圆角 |
| `BorderNormalColor` | `Color` | `#2563EB` | `UI4.Color.BorderNormal` | 面板边框色（默认值仅在宿主赋值前有意义） |
| `PanelBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 面板背景 |
| `TextColor` | `Color` | `Black` | `UI4.Color.TextForeground` | 文字颜色 |
| `ItemPadding` | `Thickness` | `12,8,12,8` | — | 项内边距 |
| `ItemCornerRadius` | `CornerRadius` | `6` | — | 项圆角 |
| `HoverBackground` | `Color` | `#0AF5FFFF` | `UI4.Color.HoverOverlay` | 项悬浮背景 |
| `HoverForeground` | `Color` | `#DC000000` | — | 项悬浮文字色（**未挂令牌**） |
| `PressedBackground` | `Color` | `#2563EB` | — | 项按下背景（**未挂令牌**） |
| `PressedForeground` | `Color` | `#FFFFFF` | — | 项按下文字色 |
| `ListStyleType` | `ListStyleType` | `None` | — | `None` / `Disc` / `Number` |
| `NumberCircleBackground` | `Brush` | `#2563EB`（冻结画刷） | — | 编号圆底（**未挂令牌**） |
| `IsMenuMode` | `bool` | `false` | — | 菜单模式：模板按「宽度受视口约束」重测，让长文字触发 `TextTrimming`（`UI4ContextMenu` / `UI4NotifyIcon` 内部置 `true`，见 §3.9、§3.11）；变更即重建 Style |

- `ListStyleType`：`None` 普通列表；`Disc` 前面加圆点；`Number` 前面加编号圆角标，
  序号由内部 `IndexPlusOneConverter` 把 `AlternationIndex` 转成 1 起的数字（`AlternationCount` 被设为 `int.MaxValue`）。
- 编号角标里的**数字颜色**在 `Dispatcher.BeginInvoke` 中重绘，`UI4ThemeScope` 局部作用域下这一处可能取到全局色（已知遗留，见 §4.11）。
- `RefreshTheme()`：按当前主题重建 `Style`。颜色已由资源引用驱动，这个方法只为「没进可视树、收不到 `Loaded`」的
  场景（如 Popup 预构建内容）保留，兼容旧调用方。
- 三个"未挂令牌"的属性在深色 / 高对比度下不会自动变，见 [§4.11](#411-主题盲区与已知限制) 的自救写法。
- **项的水平对齐无条件是 `Stretch`**（`UI4ListBox.cs:301` 的 `ListBoxItem` 样式 Setter、`:387` 的内容
  `ContentPresenter`）：以前两处都写 `Left`，项按内容自适应宽度，于是长文字永远"够宽"、`TextTrimming` 不触发。
  改 `Stretch` 后项铺满可视宽度，普通列表的项背景与描边也会整行贯通——有意为之，不是回归。
- `IsMenuMode`（DP 在 `UI4ListBox.cs:212`）只管一件事：`HorizontalScrollBarVisibility` 在 `true` 时取
  `Disabled`、`false` 时保持 `Auto`（`:288`）。为什么值得为它单开一个 DP——`Auto` 的 ScrollViewer 拿「无限宽」
  去测量内容，项容器就按期望宽度排布，菜单里的长文字截不了；`Disabled` 把宽度约束交回视口，省略号才会出现。
  默认 `false`，所以宿主直接用的普通列表照旧有横向滚动条；变更会重建 Style。

```xml
<ui:UI4ListBox Width="200" Height="220">
    <ListBoxItem>Item 1</ListBoxItem>
    <ListBoxItem>Item 2</ListBoxItem>
</ui:UI4ListBox>

<ui:UI4ListBox ListStyleType="Disc" Width="200" Height="220" />
<ui:UI4ListBox ListStyleType="Number" NumberCircleBackground="Green" Width="200" Height="220" />

<!-- 按下态自己接令牌，避免深色下黑字压黑底 -->
<ui:UI4ListBox PressedBackground="{DynamicResource UI4.Color.RowSelectedBackground}"
               PressedForeground="{DynamicResource UI4.Color.TextForeground}" />
```

#### UI4ListView（`UI4ListView.cs`，基类 `ListBox`）

卡片式列表：每项是带阴影的圆角卡片，默认单列铺满、悬浮轻微放大。

| 属性 | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `ItemWidth` / `ItemHeight` | `double` | `NaN` | — | 项尺寸，`NaN` = 自适应 |
| `ItemCornerRadius` | `CornerRadius` | `12` | — | 卡片圆角 |
| `ItemBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 卡片背景 |
| `ItemBorderBrush` | `Color` | `#3C788CC8` | `UI4.Color.PanelBorder` | 卡片边框色（静止态） |
| `ItemBorderThickness` | `Thickness` | `1` | — | 边框厚度 |
| `HoverBorderBrush` | `Color` | `#FF0078D4` | `UI4.Color.BorderHover` | 鼠标经过时的卡片描边色（构造函数挂令牌，宿主本地赋值即退订） |
| `SelectedBorderBrush` | `Color` | `#FF2563EB` | `UI4.Color.ListSelected` | 选中卡片的描边色。与 `HoverBorderBrush` 分开给，否则鼠标一压就分不清选的是哪张 |
| `ItemPadding` | `Thickness` | `0` | — | 卡片内边距 |
| `ItemMargin` | `Thickness` | `10` | — | 卡片外边距，**同时是悬浮放大的可用余量** |
| `HoverScale` | `double` | `1.01` | — | 悬浮缩放倍率（上限） |
| `HoverMaxGrow` | `double` | `8` | — | 每边最多外扩像素 |
| `HoverAnimationDuration` | `Duration` | `200ms` | — | 悬浮动画时长（`CubicEase EaseOut`） |
| `ShadowColor` | `Color` | `#23000000` | `UI4.Color.Shadow` | 阴影颜色 |
| `ShadowBlurRadius` | `double` | `12` | — | 阴影模糊 |
| `ShadowDepth` | `double` | `0` | — | 阴影偏移 |
| `ShadowOpacity` | `double` | `0` | — | 阴影不透明度 |

**悬浮放大不越界的契约**（源码常量：`ContentPadding = 4`、`EdgeReserve = 6`）：

```
allowed = min(HoverMaxGrow, sideSlack + ContentPadding − EdgeReserve)   // sideSlack = ItemMargin.Left / .Top
生效倍率 = 1.0                                     当 allowed ≤ 0
        = min(HoverScale, 1 + 2·allowed / 槽尺寸)   否则
```

于是：窄卡片（`ItemWidth=230`、`HoverScale=1.06`）按设计者的倍率走；没给 `ItemWidth` 而铺满一行的宽项自动收敛，
每边外扩恒定在若干像素，**窗口多宽都不会越界**。缩放节点挂在容器本体（`PrepareContainerForItemOverride`）而不是模板里，
两个原因：模板 `SetValue` 的对象被所有容器共享（悬浮一项会连带其它项）；挂本体才能被 UIA 的 `ListItem.BoundingRectangle` 量到。

```xml
<ui:UI4ListView ItemsSource="{Binding Cards}" ShadowDepth="15" ShadowOpacity="0.2"
                HoverScale="1.01" SelectionChanged="Cards_SelectionChanged">
    <ui:UI4ListView.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="20">
                <ui:UI4TextBlock Text="{Binding Title}" FontSize="18" Margin="0,0,0,6" />
                <ui:UI4TextBlock Text="{Binding Description}" TextWrapping="Wrap" />
            </StackPanel>
        </DataTemplate>
    </ui:UI4ListView.ItemTemplate>
</ui:UI4ListView>
```

#### UI4GridView（`UI4GridView.cs`，基类 `ListBox`）

属性面与 `UI4ListView` 同构——同名同类型的那 15 个 DP：
`ItemWidth` `ItemHeight` `ItemCornerRadius` `ItemBackground` `ItemBorderBrush` `ItemBorderThickness`
`ItemPadding` `ItemMargin` `HoverAnimationDuration` `ShadowColor` `ShadowBlurRadius` `ShadowDepth`
`ShadowOpacity` `HoverScale` `HoverMaxGrow`；跟随的令牌也一一对应（`ItemBackground`→`Surface`、
`ItemBorderBrush`→`PanelBorder`、`ShadowColor`→`Shadow`）。**但不含 `UI4ListView` 的 `HoverBorderBrush` /
`SelectedBorderBrush`**（那 2 个只加在 `UI4ListView` 上，网格卡片仍是静止与悬浮同一支 `ItemBorderBrush`），
所以两类的 DP 数是 15 对 17。差别只在**尺寸语义**与**列数**：

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ItemWidth` | `double` | `300` | **基准单元宽度，只用来算列数**；卡片实际宽度由所在列决定 |
| `ItemHeight` | `double` | `220` | 卡片高度 |
| `ComputedColumns` | `int`（只读，`{ get; private set; }`） | `1` | 当前算出的列数 |

列数算法（`ComputeColumns()`，逐行对源码核对）：

```
若 ItemWidth 为 NaN 或 ≤ 0            → 1 列
available = ActualWidth − SystemParameters.VerticalScrollBarWidth − 2 × ContentPadding(4)
若 available ≤ 0                      → 1 列
unit = ItemWidth + ItemMargin.Left + ItemMargin.Right
columns = max(1, round(available / unit, AwayFromZero))     // 用 round 不用 floor
若 Items.Count > 0                    → columns = min(columns, Items.Count)
```

- 为什么 `round`：可用宽度只够 2.9 个单元时排 2 列，卡片会被撑胖约 45%。
- 为什么封顶到项数：`UniformGrid` 按列数等分宽度，列数多于项数时右侧整列空着，"铺满所在列"反而更糟。
  代价是项数少时卡片很宽（4 张卡片、`ItemWidth=300` 在全屏下每张会被拉到约 578 px）。嫌胖就加项。
- 列数在样式重建时当场重算（`OnStyleUpdate` 内补 `UpdateColumns()`），所以运行期改 `ItemWidth` 不必先缩窗口才生效。
- 悬浮放大的不越界契约与 `UI4ListView` 完全相同。
- **内部 ScrollViewer 的横向滚动固定 `Disabled`**（`UI4GridView.cs:400`，与同族 `UI4ListView.cs:353` 一致）。
  写成 `Auto` 时 ScrollViewer 会用「无限宽」测量内容，`UniformGrid` 于是按子项的期望宽度分列而不是按视口分列：
  卡片撑出视口就冒横向滚动条，列宽还随文案长短抖动——与本控件「`ItemWidth` 只算列数、卡片铺满所在列」的契约直接冲突。
  横向因此没有可滚的内容，窄的方向靠 `ComputeColumns()` 减列。

```xml
<ui:UI4GridView ItemsSource="{Binding Cards}" ItemWidth="250" ItemHeight="200"
                ShadowDepth="15" ShadowOpacity="0.3" HoverScale="1.1">
    <ui:UI4GridView.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="20">
                <Ellipse Width="36" Height="36" Fill="{Binding IconColor}" />
                <ui:UI4TextBlock Text="{Binding Title}" Margin="0,8,0,3" />
                <ui:UI4TextBlock Text="{Binding Description}" TextWrapping="Wrap" />
            </StackPanel>
        </DataTemplate>
    </ui:UI4GridView.ItemTemplate>
</ui:UI4GridView>
```

### 3.8 页签与导航

#### UI4Pivot / UI4PivotItem（`UI4Pivot.cs`，基类 `Selector` / `HeaderedContentControl`）

| 属性（`UI4Pivot`） | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `SelectedItemForeground` | `Color` | `#0078D4` | `UI4.Color.Accent` | 选中项标题色 |
| `ItemFontSize` | `double` | `20` | — | 项字号 |
| `SelectedFontSize` | `double` | `25` | — | 选中项字号（放大） |
| `BrandFontSize` | `double` | `22` | — | 品牌文字字号 |
| `ItemFontWeight` | `FontWeight` | `Normal` | — | 项字重 |
| `BrandFontWeight` | `FontWeight` | `SemiBold` | — | 品牌文字字重 |
| `ItemForeground` | `Color` | `#DC000000` | `UI4.Color.TextForeground` | 项颜色 |
| `ItemHoverForeground` | `Color` | `#DC000000` | `UI4.Color.TextForeground` | 项悬浮颜色 |
| `ItemPadding` | `Thickness` | `10,8,10,8` | — | 项内边距 |
| `ItemMargin` | `Thickness` | `5,0,5,0` | — | 项外边距 |

| 属性（`UI4PivotItem`） | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `IsBrand` | `bool` | `false` | 是否当作品牌标题（**挂在 Item 上，不在 `UI4Pivot`**） |

- 切换页时内容区做**平移 + 淡入淡出**：淡出 120 ms、滑动 180 ms，入场方向按新旧索引关系决定，
  所以往左点和往右点的内容是相向滑动的。
- `SelectedIndex` / `SelectedItem` / `SelectionChanged` 继承自 `Selector`。

```xml
<ui:UI4Pivot ItemFontSize="18" SelectedIndex="0" SelectionChanged="Pivot_SelectionChanged">
    <ui:UI4PivotItem Header="StartUI4" IsBrand="True">
        <TextBlock Text="品牌位，通常放标题" />
    </ui:UI4PivotItem>
    <ui:UI4PivotItem Header="首页"><TextBlock Text="首页内容" /></ui:UI4PivotItem>
    <ui:UI4PivotItem Header="设置"><TextBlock Text="设置内容" /></ui:UI4PivotItem>
</ui:UI4Pivot>
```

#### UI4Tab / UI4TabItem（`UI4TabControl.cs`，基类 `Selector` / `HeaderedContentControl`）

| 属性（`UI4Tab`） | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `HeaderBackground` | `Color` | `#0A000000` | `UI4.Color.TrackBackground` | 标签栏背景 |
| `TabBackground` | `Color` | `Transparent` | — | 标签背景 |
| `TabSelectedBackground` | `Color` | `White` | `UI4.Color.Surface` | 选中标签背景 |
| `TabHoverBackground` | `Color` | `#1E000000` | `UI4.Color.HoverOverlay` | 标签悬浮背景 |
| `TabForeground` | `Color` | `#C8000000` | `UI4.Color.TextMuted` | 标签文字色 |
| `TabSelectedForeground` | `Color` | `#FF000000` | `UI4.Color.TextForeground` | 选中标签文字色 |
| `CloseButtonColor` | `Color` | `#96000000` | `UI4.Color.TextMuted` | 关闭按钮颜色 |
| `TabFontSize` | `double` | `13` | — | 标签字号 |
| `TabPadding` | `Thickness` | `12,8,8,8` | — | 标签内边距 |
| `ShowAddButton` | `bool` | `true` | — | 是否显示「＋」 |
| `AddButtonColor` | `Color` | `#96000000` | `UI4.Color.TextMuted` | 「＋」颜色 |

| 属性（`UI4TabItem`） | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `TextIcon` | `string` | `null` | 文字图标（如 Segoe MDL2 码位 `&#xE80F;`） |
| `TextIconFontFamily` | `FontFamily` | `Segoe MDL2 Assets` | 文字图标字体 |
| `ImageSource` | `ImageSource` | `null` | 图片图标（与 `TextIcon` 二者取一显示） |
| `IconSize` | `double` | `16` | 图标尺寸 |
| `IsClosable` | `bool` | `true` | 是否显示关闭按钮 |
| `IsBrand` | `bool` | `false` | 是否当作品牌区（挂在 Item 上） |

| 事件 | 挂在 | 签名 | 说明 |
|---|---|---|---|
| `AddTab` | `UI4Tab` | `RoutedEventHandler`（Bubble） | 点「＋」 |
| `CloseTab` | `UI4Tab` | `TabCloseRoutedEventHandler`（Bubble） | 请求关闭；`e.TabItem` 是被点的标签，**不置 `e.Handled` 时控件自行把它从 `Items` 移除** |
| `CloseTab` | `UI4TabItem` | `RoutedEventHandler`（Bubble） | 同一按钮在标签项上的原始事件，冒泡可见 |

`TabCloseRoutedEventArgs` 只有一个公开成员 `UI4TabItem TabItem { get; }`。

```xml
<ui:UI4Tab x:Name="MyTab" AddTab="MyTab_AddTab" CloseTab="MyTab_CloseTab">
    <ui:UI4TabItem Header="主页" TextIcon="&#xE80F;">
        <TextBlock Text="主页内容" Margin="16" />
    </ui:UI4TabItem>
    <ui:UI4TabItem Header="设置" TextIcon="&#xE713;" IsClosable="False">
        <TextBlock Text="设置内容" Margin="16" />
    </ui:UI4TabItem>
</ui:UI4Tab>
```

```csharp
private void MyTab_AddTab(object sender, RoutedEventArgs e)
{
    var item = new UI4TabItem
    {
        Header = "新标签",
        TextIcon = "\uE723",
        Content = new TextBlock { Text = "动态标签内容", Margin = new Thickness(12) }
    };
    MyTab.Items.Add(item);
    MyTab.SelectedItem = item;
}

private void MyTab_CloseTab(object sender, TabCloseRoutedEventArgs e)
{
    if (e.TabItem.Header as string == "设置") { e.Handled = true; return; }  // 拦住不让关
    StatusText.Text = "已关闭：" + e.TabItem.Header;                          // 不置 Handled，控件自己移除
}
```

#### UI4NavigationView 及 Item / BottomItem（`UI4NavigationView.cs`，基类 `ItemsControl` / `ContentControl`）

| 属性（`UI4NavigationView`） | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `Header` | `string` | `null` | — | 左栏顶部标题（默认双向绑定） |
| `LeftPanelBackground` | `Brush` | `#0A000000` | `UI4.Brush.Surface` | 左栏背景 |
| `LeftPanelWidth` | `double` | `NaN` | — | 左栏宽度，`NaN` = 自适应（默认双向绑定 + `AffectsMeasure`） |
| `ItemFontSize` | `double` | `10` | — | 项字号（`AffectsMeasure`）；DP 登记在 `UI4NavigationView` 自身（`UI4NavigationView.cs:348`），XAML 里可直接写 `ItemFontSize="12"` |
| `ItemBackground` | `Brush` | `Transparent` | — | 项背景 |
| `ItemForeground` | `Brush` | `Black` | — | 项文字色 |
| `ItemHoverColor` | `Color` | `#0A000000` | — | 项悬浮底色 |
| `ItemHoverForeground` | `Color` | `Black` | — | 项悬浮文字色 |
| `ItemPressedBackground` | `Color` | `White` | — | 项按下底色 |
| `ItemPressedForeground` | `Color` | `Black` | — | 项按下文字色 |
| `SelectedItemBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 选中项背景 |
| `SelectionIndicatorBrush` | `Brush` | `#0078D4` | `UI4.Brush.Accent` | 选中指示条颜色 |
| `SelectedItem` | `UI4NavigationViewItem` | `null` | — | 当前选中项（默认双向绑定） |
| `RegularItems` | `ObservableCollection<UI4NavigationViewItem>` | — | — | **只读视图**：普通项 |
| `BottomItems` | `ObservableCollection<UI4NavigationViewBottomItem>` | — | — | **只读视图**：底部固定项 |

| 属性（`UI4NavigationViewItem`，`…BottomItem` 继承之） | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Header` | `string` | `null` | 项标题 |
| `ImageSource` | `ImageSource` | `null` | 图片图标 |
| `TextIcon` | `string` | `null` | 文字图标 |
| `TextIconFontFamily` | `FontFamily` | `null` | 文字图标字体 |

结构与契约（逐条对源码）：

- 模板 = 左栏（`PART_LeftPanel` 里一个 `PART_ListBox` + 一个 `PART_BottomListBox` + 两根选中指示条）
  与右栏 `PART_ContentPresenter`，**右栏内容绑 `SelectedItem.Content`**——所以页内容就写在被选中的项里。
- 两个列表都是库内的 `UI4ListBox`，因此导航项自动拿到列表的美化滚动条与主题引用。
- 项的分流在 `OnItemsChanged` 里做：`UI4NavigationViewBottomItem` → `BottomItems`，其余
  `UI4NavigationViewItem` → `RegularItems`。**增删请改 `Items`**（直接往 `RegularItems` 加不会进左栏，
  下一次 `Items` 变化还会被整表清空重建）。
- 指示条用 `TranslateTransform` 跟着选中项移动，颜色 = `SelectionIndicatorBrush`。
- **项容器尺寸随字号长**：`ListBoxItem` 样式的 `Width` 绑到 `LeftPanelWidth`、高度只留 `MinHeight=70`
  （`UI4NavigationView.cs:803`、`:808`），项内部「图标 + 标题」那块用 `MinWidth`/`MinHeight=60`（`:476`、`:477`），
  标题不再有 `MaxWidth=76` 上限（裁剪交回 `TextTrimming` 与宿主设的 `LeftPanelWidth`）。这三处原先是钉死的
  70×70 / 60×60 / 76，`ItemFontSize` 一大就把标签挤成一个字，而且外层 `LeftPanelWidth` 给多宽都没用——容器自己就是 70。
  宿主没设 `LeftPanelWidth`（`NaN`）时项宽回退成按内容自适应。
- **UIA 子树可读**：`OnCreateAutomationPeer()` 返回 `FrameworkElementAutomationPeer`（`:680`）。本控件模板里
  没有 `ItemsPresenter`（项由内部两个 `UI4ListBox` 重新承载），默认的 `ItemsControlAutomationPeer` 只按「自己的
  项容器」枚举子节点、一个也找不到，并且它会顶掉默认的可视子枚举——症状是读屏 / 自动化在窗口里枚举不到任何左栏
  导航项，右栏整块内容也一起从 UIA 树上消失。
- 整体 `Background` 挂 `UI4.Brush.Background`、`Foreground` 挂 `UI4.Brush.TextForeground`。

```xml
<ui:UI4NavigationView Header="导航视图" LeftPanelWidth="220">
    <ui:UI4NavigationViewItem TextIcon="&#xE104;" Header="代码">
        <TextBlock Text="代码页内容" Margin="16" />
    </ui:UI4NavigationViewItem>
    <ui:UI4NavigationViewItem TextIcon="&#xE8A5;" Header="收藏">
        <TextBlock Text="收藏页内容" Margin="16" />
    </ui:UI4NavigationViewItem>
    <ui:UI4NavigationViewBottomItem TextIcon="&#xE713;" Header="设置">
        <TextBlock Text="设置页内容" Margin="16" />
    </ui:UI4NavigationViewBottomItem>
</ui:UI4NavigationView>
```

---

### 3.9 菜单与对话框

#### UI4Menu / UI4MenuElementItem / UI4MenuSeparatorElement（`UI4Menu.cs`）

| 属性（`UI4Menu`） | 类型 | 默认值 | 跟随令牌 | 说明 |
|---|---|---|---|---|
| `BarBackground` | `Brush` | `#F8F8F8` | `UI4.Brush.MenuBackground` | 菜单栏背景 |
| `ItemHoverBrush` | `Brush` | `#14000000` | `UI4.Brush.HoverOverlay` | 项悬浮背景 |
| `PopupCornerRadius` | `CornerRadius` | `6` | — | 下拉面板圆角 |
| `TextForeground` | `Brush` | `#141414` | `UI4.Brush.TextForeground` | 文字颜色 |
| `PopupBackground` | `Brush` | `White` | `UI4.Brush.Surface` | 下拉面板背景 |
| `KeyTipForeground` | `Brush` | `#666666` | `UI4.Brush.Icon` | KeyTip 提示文字色 |

| 属性（`UI4MenuElementItem`，基类 `MenuItem`） | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `TextIcon` | `string` | `""` | 文字图标（占位在 Header 左侧） |
| `IconFontFamily` | `FontFamily` | `Segoe UI Symbol` | 图标字体 |
| `IconFontSize` | `double` | `14` | 图标字号 |
| `IconForeground` | `Brush` | `null` | 图标颜色（null = 用文字色） |
| `KeyTip` | `string` | `""` | 键提示文本，如 `(F)` |

| 属性（`UI4MenuSeparatorElement`，基类 `Separator`） | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `SeparatorColor` | `Brush` | `#DCDCDC` | 分隔线颜色（**未挂令牌**，见 §4.11） |

`KeyTip` 只是**显示**快捷键提示文本，库不实现 Alt 键导航；要真的按键打开菜单得宿主自己接 `AccessText`（`_`）或按键监听。

```xml
<ui:UI4Menu>
    <ui:UI4MenuElementItem Header="文件" KeyTip="(F)">
        <ui:UI4MenuElementItem Header="新建" TextIcon="&#xE710;"
                               IconFontFamily="Segoe MDL2 Assets" IconFontSize="14"
                               Click="New_Click" />
        <ui:UI4MenuSeparatorElement />
        <ui:UI4MenuElementItem Header="退出" TextIcon="&#xE7E8;" Click="Exit_Click" />
    </ui:UI4MenuElementItem>
</ui:UI4Menu>
```

#### UI4ContextMenu（`UI4ContextMenu.cs`，纯代码组件，无基类）

菜单本体是一个 `Popup` + 内部 `UI4ListBox`：`Popup` 用 `Placement=MousePoint`、`StaysOpen=false`、
`AllowsTransparency=true`，打开时才把 `PlacementTarget` 设成绑定目标——所以它不需要在窗口可视化树里占任何位置就能弹
（托盘菜单用的就是同一套思路）。

菜单条目的宽度契约（`UI4ContextMenu.cs:345`、`:352`、`:386-387`、`:398-405`）：内部 `UI4ListBox` 置
`IsMenuMode = true`，每条 item 的 `Grid` 宽度按 `Width − 8 − 4 − ItemPadding.Left − ItemPadding.Right` 现算
（8 = ScrollViewer 内边距 `4,4,4,4`，4 = `ListBoxItem` 描边外扩 2×2），标题写
`TextTrimming=CharacterEllipsis` 并配 `ToolTip = item.Text`，`Popup` 与包住列表的 `Border` 取同一个 `Width`
且 `Border.ClipToBounds=true`。一句话：**长条目在菜单右缘收成省略号、可悬浮看全文，而不是把 Popup 撑宽或让内容溢出边界**。
`Width` 与 `ItemPadding` 是在 `Attach()` 里被读一次的（`BuildMenu()` 由 `Attach` 调用，`:324`），
之后再改这两个值不会重排已经建好的条目——`Open()` 只刷新各项的可用态透明度；要换宽度得 `Detach()` 后重新 `Attach()`。

| 成员 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Width` | `double`（普通属性） | `180` | 菜单宽度 |
| `ItemPadding` | `Thickness`（普通属性） | `12,8,12,8` | 项内边距 |
| `BorderColor` | `Color`（属性 + 已赋值标记） | — | 边框色；**没赋过值时由内部列表的资源引用跟随主题** |
| `Background` | `Brush` | — | 菜单背景；同上 |
| `HoverBackground` | `Color` | — | 项悬停色；同上 |
| `IsOpen` | `bool`（只读） | — | 是否已弹出 |
| `AddItem(UI4MenuItem item)` | — | — | 添加自定义项 |
| `AddItem(UI4MenuItemType type, Action command, Func<bool> canExecute = null)` | — | — | 按内置类型添加，**自动取图标与多语言文本** |
| `Attach(UIElement target)` / `Detach()` | — | — | 绑定 / 解绑右键目标 |
| `Open()` / `Close()` | — | — | 手动开合 |

配套公开类型：

| 类型 | 成员 |
|---|---|
| `UI4MenuItem` | 构造 `(UI4MenuItemType type, string text, ImageSource icon, Action command, Func<bool> canExecute = null)`；属性 `Type` / `Text` / `Icon` / `Command` / `CanExecute` |
| `UI4MenuItemType` | 枚举：`Undo` `Redo` `Cut` `Copy` `Paste` `Delete` `SelectAll` |
| `UI4MenuIcons` | 静态属性：`Undo` `Redo` `Cut` `Copy` `Paste` `Delete` `SelectAll`（`ImageSource`），`GetIcon(UI4MenuItemType)` |
| `GeometryHelper` | 静态扩展 `GetOutlinedGeometry(this RectangleGeometry)`，图标描边化用 |

```csharp
var menu = new UI4ContextMenu { Width = 190 };                 // 不给颜色 = 跟随主题
menu.AddItem(new UI4MenuItem(UI4MenuItemType.Copy, "复制文本", UI4MenuIcons.Copy, () => DoCopy()));
menu.AddItem(UI4MenuItemType.SelectAll, () => DoSelectAll());
menu.Attach(myControl);                                          // 在 myControl 上右键弹出
```

> `UI4CodeEditor`、`UI4TextBox`、`UI4PasswordBox`、`UI4TextBlock` 内部用的就是它，所以这几个控件的右键菜单长得一样、
> 也都跟主题。想局部换肤：给承载窗口设 `UI4ThemeScope`，菜单会跟着查到的令牌走；
> 但**托盘菜单例外**——它不在任何窗口的可视化树上，只跟全局主题。

#### UI4MessageBox（`UI4MessageBox.cs`，基类 `Window`）

| 枚举 `UI4MessageBoxButtons` | 值 |
|---|---|
| `OK` | 仅确定 |
| `OKCancel` | 确定 / 取消 |

| 成员 | 签名 |
|---|---|
| 构造函数 | `UI4MessageBox(string title, string content, UI4MessageBoxButtons buttonMode = OK)` |
| 静态方法 | `static bool? Show(string content, string title = null, UI4MessageBoxButtons buttons = OK, double width = 460, Window owner = null)` |

返回值语义：`true` = 确定，`false` = 取消，`null` = 直接关闭窗口。

- `title` 为 `null` 时取 `UI4MultiLanguage.Get(UI4LanguageKey.Notice)`，中文实际文案是**「提示」**
  （`UI4MessageBox.cs:257` 的 `<param>` 注释写的是「注意」，以字符串表为准）；按钮文字同样跟随当前语言。
- `width` 默认常量 `DefaultWidth = 460`。
- `owner` 给了就 `CenterOwner`，没给则 `CenterScreen`。
- **没有系统标题栏**：`WindowStyle = None` + `AllowsTransparency = true`，标题区是内容里画的圆角卡片，
  可八向拖边框改尺寸（`Internal.WindowResizeBehavior`）。所以 `UI4WindowTitleBar` 对它没有任何可见作用。
- 主题接线：主容器背景挂 `UI4.Brush.Surface`，标题/正文挂 `UI4.Brush.TextForeground`，图标文字挂 `UI4.Brush.Icon`。

```csharp
bool? r = UI4MessageBox.Show("这是内容。");                       // 标题「注意」，仅确定
bool? r2 = UI4MessageBox.Show("确认执行该操作吗？", "请确认",
        UI4MessageBoxButtons.OKCancel, owner: this);
if (r2 == true) { /* 执行 */ }
```

---

### 3.10 代码编辑器

#### UI4CodeEditor（`UI4CodeEditor.cs`，基类 `ICSharpCode.AvalonEdit.TextEditor`）

构造函数里做了这些事，宿主无需再配：

| 项 | 设定 |
|---|---|
| 语法高亮 | `HighlightingManager.Instance.GetDefinition("C#")` |
| 行号 / 自动换行 | `ShowLineNumbers = true`、`WordWrap = true` |
| 字体 | `Consolas`，`FontSize = 14` |
| 编辑选项 | `ConvertTabsToSpaces = true`、`IndentationSize = 4`、`EnableRectangularSelection = false` |
| 剪贴板 | `ClipboardCommandTakeover.Install(editor)`——Ctrl+C/X/V 走 [`UI4Clipboard`](#51-ui4clipboard) |
| 右键菜单 | `UI4ContextMenu`（宽 200）：撤销 / 重做 / 剪切 / 复制 / 粘贴 / 删除 / 全选，各项带 `CanExecute` |
| 主题 | `Background` 挂 `UI4.Brush.Surface`、`Foreground` 挂 `UI4.Brush.TextForeground`；`Loaded` 时把美化 `ScrollViewer` 样式套给内部滚动区 |

> **语法高亮的配色不跟主题**：AvalonEdit 的颜色由内嵌 XSHD 定义决定，不走 WPF 资源体系，
> 库只能改编辑器外壳底色/前景。这是已知限制（§4.11），深色主题下关键字配色需要宿主自己换
> `SyntaxHighlighting` 定义（`HighlightingManager.Instance.GetDefinition("...")` 或加载自定义 xshd）。

```xml
<ui:UI4CodeEditor x:Name="CodeEditor" Height="220" />
```

```csharp
CodeEditor.Text = "public class Sample { }";
CodeEditor.SyntaxHighlighting =
    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("XML");
```

---

### 3.11 系统集成

#### UI4NotifyIcon（`UI4NotifyIcon.cs`，基类 `FrameworkElement`，实现 `IDisposable`）

纯 P/Invoke 的托盘图标（`Shell_NotifyIcon` + 自建消息窗口），**不依赖 WinForms**。

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MenuActivation` | `PopupActivationMode` | 构造时被设为 `None` | **当前实现不读这个值**（见下方提示） |
| `IconSource` | `ImageSource` | `null` | 托盘图标；解析失败回退 `SystemIcons.Application` |
| `ToolTipText` | `string` | `""` | 悬浮提示；**超过 127 字符会被截断**（Shell 限制） |
| `MenuWidth` | `double` | `160` | 右键菜单宽度 |
| `MenuItemPadding` | `Thickness` | `12,8,12,8` | 菜单项内边距 |
| `MenuCornerRadius` | `CornerRadius` | `6` | 菜单圆角 |

| 事件 | 类型 | 说明 |
|---|---|---|
| `TrayLeftMouseUp` | `RoutedEventHandler`（Bubble） | 托盘左键抬起 |
| `TrayRightMouseDown` | `RoutedEventHandler`（Bubble） | 托盘右键按下（库内部也订阅了它用来弹菜单） |
| `TrayMouseDoubleClick` | `RoutedEventHandler`（Bubble） | 双击 |

| 方法 | 说明 |
|---|---|
| `AddItem(UI4TrayMenuItem item)` | 添加自定义菜单项 |
| `AddItem(UI4MenuItemType type, Action command, Func<bool> canExecute = null)` | 按内置类型添加（自动取图标与多语言文本） |
| `ClearMenuItems()` / `OpenMenu()` / `CloseMenu()` | 菜单操作 |
| `Dispose()` | 摘除托盘图标；`Application.Exit` 时库会自动调用一次 |

| 配套公开类型 | 说明 |
|---|---|
| `UI4TrayMenuItem` | 两个构造：`(type, text, ImageSource icon, command, canExecute)` 与 `(type, text, string iconText, command, canExecute)`；属性 `Type` / `Text` / `Icon` / `IconText` / `Command` / `CanExecute` |
| `PopupActivationMode` | 枚举 `None` `LeftClick` `RightClick` `DoubleClick` `LeftOrRightClick` `All` |

生命周期与已知行为（逐条对源码）：

- **`Visibility` 就是开关**：`VisibilityProperty.OverrideMetadata(...)`，`Visible` → `CreateTrayIcon()`（`NIM_ADD` + `NIM_SETVERSION`），
  其它 → `RemoveTrayIcon()`（`NIM_DELETE`）。XAML 里常写 `Visibility="Collapsed"`，代码里需要时才置 `Visible`。
- 菜单是一个 `Popup`（`Placement=MousePoint`、`StaysOpen=false`、`AllowsTransparency=true`、`Slide` 动画），
  子内容是内部 `UI4ListBox`；另有 100 ms 的 `DispatcherTimer` 检测鼠标移出后关闭。
- **托盘菜单的条目宽度契约同 §3.9**：内部 `UI4ListBox` 置 `IsMenuMode = true`（`UI4NotifyIcon.cs:278`），
  行 `Grid` 宽度按 `MenuWidth − 8 − 4 − MenuItemPadding.Left − MenuItemPadding.Right` 现算（`:308`），
  标题 `TextTrimming=CharacterEllipsis` + `ToolTip = item.Text`（`:354`、`:355`），列表外再包一层
  `Border{ClipToBounds=true}`（`:282-286`），`Popup` 与 `Border` 同宽。
  时机差别要记着：行内容每次 `OpenMenu()` 都重建（`RebuildAllRows()`，`:233`），而 `Popup`/`Border`/列表三者
  的宽度只在**构造期**的 `BuildTrayPopup()` 读一次 `MenuWidth`（`:94`）——运行期改 `MenuWidth` 只改得到行宽，改不到弹层宽。
- **`MenuActivation` 目前是死属性**：构造函数把它设成 `None`，但全文件没有任何地方读它；右键弹菜单是靠
  构造时订阅的 `TrayRightMouseDown += OnTrayRightClick`。内部处理器先跑（`e.Handled = true`），
  宿主的同名处理器仍会收到，但菜单已经弹出——要自己接管就立刻 `CloseMenu()`。`PopupActivationMode` 枚举同理。
- **托盘菜单只跟全局主题**：托盘不在任何窗口的可视化树上，`UI4ThemeScope` 的祖先链查不到，
  所以给窗口设局部作用域不会影响托盘菜单。原先的三个 DP（`MenuBorderColor` / `MenuBackground` / `MenuHoverBg`）
  已删除——它们既没有 `PropertyChangedCallback`（运行期改不生效），又拿不到作用域令牌。
- Windows 10/11 默认把新托盘图标收进溢出区，需在任务栏设置里拖出，这与库无关。

```xml
<ui:UI4NotifyIcon x:Name="TrayIcon" Visibility="Collapsed" ToolTipText="My App"
                  IconSource="/Assets/app.ico"
                  TrayLeftMouseUp="TrayIcon_TrayLeftMouseUp" />
```

```csharp
TrayIcon.AddItem(UI4MenuItemType.Copy, () => CopySomething(), () => canCopy);
TrayIcon.AddItem(new UI4TrayMenuItem(UI4MenuItemType.Delete, "清空回收站", "🗑", () => Empty(), () => true));
TrayIcon.Visibility = Visibility.Visible;              // 注册托盘图标

// 关闭窗口：先隐藏再释放，否则托盘会留一个死图标直到鼠标划过
protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
{
    TrayIcon.Visibility = Visibility.Collapsed;
    TrayIcon.Dispose();
    base.OnClosing(e);
}
```

#### UI4WindowTitleBar（`UI4WindowTitleBar.cs`，静态类 + 附加属性）

让 **DWM 绘制的系统标题栏**跟随主题。完整机制、通路时序与限制见 [§4.10](#410-窗口标题栏跟随主题)。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Enabled`（`EnabledProperty` / `GetEnabled` / `SetEnabled`） | 附加 `bool`，默认 `true` | 置 `false` 让某个窗口豁免；改动即时生效（关闭时交还系统默认色） |
| `Apply(Window)` | `bool Apply(Window window)` | 按该窗口的**有效主题**（含作用域）立即染色；窗口可见、未豁免且系统接受时返回 `true` |
| `ApplyOpenWindows()` | `void` | 重染本进程全部已打开窗口 |
| `SupportsCaptionColors` | `bool`（只读） | 系统是否允许自定义标题栏底色/文字/边框（Win11 起为 `true`） |
| `ToColorRef(Color)` | `int` | `Color → COLORREF (0x00BBGGRR)`，宿主自调 `dwmapi` 时用来对齐口径 |

```xml
<!-- 截图/投屏窗口要保持系统原样 -->
<Window ui:UI4WindowTitleBar.Enabled="False" ... />
```

---

## 四、主题系统

### 4.1 数据模型：38 个令牌与三份内置定义

主题的全部数据就是一张 `UI4ThemeToken → Color` 的表。

| 类型 | 位置 | 角色 |
|---|---|---|
| `UI4ThemeToken`（枚举，38 成员） | `UI4ThemeToken.cs` | 令牌键。键名同时决定资源键 `UI4.Color.<键>` 与 `UI4.Brush.<键>` |
| `UI4ThemeMode`（枚举） | `UI4Theme.cs` | `Light` / `Dark` / `System` / `HighContrast` |
| `UI4ThemeDefinition`（`sealed`） | `UI4ThemeDefinition.cs` | 一份主题的令牌取值；`Key` 是大小写不敏感的标识 |
| `UI4Theme`（静态服务） | `UI4Theme.cs` | 当前模式、已注册定义表、共享资源字典、持久化 |
| `UI4ThemeScope`（附加属性） | `UI4ThemeScope.cs` | 子树局部主题 |
| `UI4ThemePacks`（静态类） | `UI4ThemePacks.cs` | 8 套预置业务主题 |
| `IThemePersistence` + 2 实现 | `UI4ThemePersistence.cs` | 模式持久化后端 |

`UI4ThemeDefinition` 的公开面很小，自定义主题全靠它：

| 成员 | 签名 | 说明 |
|---|---|---|
| 构造函数 | `UI4ThemeDefinition(string key)` | 空定义：只设键，令牌自己填 |
| `Key` | `string { get; set; }` | 主题标识，`light` / `dark` / `highcontrast` / 自定义 |
| `With` | `UI4ThemeDefinition With(UI4ThemeToken, Color)` | 设一个令牌，返回自身可链式 |
| `Has` | `bool Has(UI4ThemeToken)` | 该令牌是否已定义 |
| `GetColor` | `Color GetColor(UI4ThemeToken)` | 取值；**未定义时抛 `KeyNotFoundException`** |
| `Clone` | `UI4ThemeDefinition Clone()` | 深拷贝，`Key` 变成 `<原键>.clone` |
| `Light` / `Dark` / `HighContrast` | `static UI4ThemeDefinition` | 三份内置定义，每次调用新建 |

> `GetColor` 抛异常这一点决定了：**自定义主题必须覆盖全部 38 个令牌**，漏一个就在那次取色上崩。
> 所以派生自定义主题的正确姿势是 `UI4ThemeDefinition.Light().Clone()` 再改想改的令牌
> （或直接改 `XxxDefinition()` 工厂），不要 `new UI4ThemeDefinition("my")` 只填三个令牌就注册。

令牌按用途分组（值见 [§4.4](#44-内置三套的完整取值) / [§4.5](#45-预置套装-ui4themepacks8-套)）：

| 组 | 令牌 |
|---|---|
| 强调色 | `Accent` `AccentDark` `AccentEnd` `OnAccent` |
| 文字 | `TextForeground` `TextSecondary` `TextMuted` `Placeholder` |
| 面 | `Background` `Surface` `MenuBackground` `OffBackground` `TrackBackground` `BackgroundGradientStart` `BackgroundGradientEnd` |
| 边与分隔 | `BorderNormal` `BorderSecondary` `BorderHover` `BorderFocus` `BorderWeak` `PanelBorder` `Separator` `GridLine` |
| 状态叠加 | `HoverOverlay` `SelectedOverlay` `RowHoverBackground` `RowSelectedBackground` `ListSelected` |
| 控件专用 | `CheckBackground` `CheckBoxUnchecked` `HoverBorderColorLight` `ProgressStart` `HeaderBackground` `HeaderForeground` `Icon` `IconHover` `Shadow` `ScrollBarThumb` |

### 4.2 生效通路：一份共享字典 + 资源引用

一个主题键对应**一份共享 `ResourceDictionary`**（`UI4Theme.SharedResourcesFor(key)`），里面写满
38×2 个键（`UI4.Color.X` 与 `UI4.Brush.X`，Brush 是冻结画刷）外加 3 个常用别名（下表）与 3 个排印默认值（[§4.2.1](#421-排印键-ui4font)）：

| 别名 | 指向 |
|---|---|
| `UI4.Brush.Text` | `TextForeground` 的画刷 |
| `UI4.Brush.Border` | `BorderNormal` 的画刷 |
| `UI4.Brush.Accent` | `Accent` 的画刷（这个不是别名，就是 `Accent` 本体） |

```
UI4Theme.SetTheme / Apply / SetAccent / Register
   └─ 重写这份共享字典（同一实例，原地改）
        ├─ Application.Resources.MergedDictionaries 挂着它 → 宿主 {DynamicResource} 跟随
        ├─ 库内控件构造函数 SetResourceReference(Dp, "UI4.Color.X") → 查同一份字典，跟随
        └─ UI4ThemeScope 把同键的同一实例插进子树 Resources.MergedDictionaries 末位 → 局部跟随
```

实测口径（对 `src/StartUI4Controls` 全量 grep，含 `Internal/`）：

| 声明式接线形式 | 数量 | 说明 |
|---|---|---|
| `SetResourceReference(...)` 调用点 | **95 处 / 25 个文件** | 89 处挂在控件自身 DP（含继承来的 `Background` / `Foreground`），6 处挂在控件内部构造的元素上（`UI4ColorPicker`、`UI4MessageBox`） |
| 代码模板里的 `DynamicResourceExtension` | 12 处 | 触发器里染色，如 `UI4Button` 禁用态、`UI4ComboBox` 项悬浮/选中 |
| XAML 字符串里的 `{DynamicResource UI4.…}` | 8 处 | 滚动条资源（`Internal/ScrollBarResources.cs` 与 ListView/GridView 内联副本） |
| 被引用的去重资源键 | 47 个 | 38 令牌的 Color/Brush 变体 + 3 别名中真正被用到的部分 |

三条必须知道的性质：

1. **切主题不需要逐控件刷新**。控件不实现任何主题接口（2.0.0 的 `IThemeAware` / `TrackControl` 已整体删除），
   `ThemeChanged` 事件现在只有两个命令式消费者：DWM 标题栏染色与 AvalonEdit 相关刷新。
2. **本地赋值 = 该属性退订主题**，这是设计而不是缺陷。`SetResourceReference` 占的也是本地值槽，
   宿主写 `GradientStart="#090909"` 会把引用顶掉；此后 `ClearValue` 回落到**代码里的字面默认色而不是主题色**。
   只改一处观感就照这个语义写，想整棵子树换观感请用 `UI4ThemeScope`。
3. **换字典不留空窗**。`WriteToApplicationResources()` 是**原位替换**上一次挂上的那份（先 `merged[index] = dict` 再摘多余的），
   不是 `Remove` + `Add`——后者会在中间留一个引用解析不到的窗口，实测 `dark → highcontrast` 期间控件会短暂读到硬编码默认色。
   库还维护 `_installedResources` 账本，摘除只认账本：`Register` 会清缓存，靠缓存去找旧字典就会把已挂上的那份永远留在树里。

### 4.2.1 排印键 `UI4.Font.*`

字号与字体族走的是同一份共享字典，但键不是从令牌派生的——它们是 3 个固定键，由 `WriteTokens` 在
每次重写字典时一并发布（`UI4Theme.DefaultFontSizeBase` / `DefaultFontSizeCode` / `DefaultFontFamily`）：

| 键 | 类型 | 库内默认 | 谁在用 |
|---|---|---|---|
| `UI4.Font.Size.Base` | `double` | `15` | `UI4Button`（样式 Setter，`UI4Button.cs:137`）、`UI4TextBox.cs:161`、`UI4ComboBox.cs:193`、`UI4ListBox.cs:251`、`UI4PasswordBox.cs:237` |
| `UI4.Font.Size.Code` | `double` | `14` | `UI4CodeEditor.cs:37` |
| `UI4.Font.Family` | `FontFamily` | `Segoe UI` | 宿主可直接绑给窗口/文本元素；库内控件不引用它（沿用 WPF 继承） |

三条口径：

1. **这 3 个键永远存在**（在每份主题字典里都有库内默认值），所以宿主引用它们不会因为"没挂字典"拿到 `DependencyUnsetValue`；
   想改观感就在**应用资源根**上覆盖同名键（后写者胜），或给某个控件实例直接赋本地值（照 §4.2 第 2 条 = 退订）。
2. **宿主覆盖不会被主题切换冲掉**：主题值住在 `Application.Resources.MergedDictionaries` 里的那份共享字典，
   而宿主直接写的是 `Application.Resources` 自身的键——按 WPF 的查找顺序，同一层的自有项优先于 MergedDictionaries，
   所以覆盖值是稳定生效的（这条是实测结论，见 `ThemeSelfTest` 的 `字体覆盖值在换档后仍解析到宿主值`）。
   反过来，宿主若把键写进那份共享字典**内部**（例如 `UI4Theme.SharedResourcesFor` 返回的实例），就会被下次重写顶掉。
3. **排印通路的本地改动只有上表那 6 处引用点**，上游 `src/StartUI4Controls` 里字号是字面常量（`FontSize = 15d` / `14`）。
   上表 §4.2 的"实测口径"统计的是上游那份树，不含这 6 处。
4. **本包相对上游另有 7 处非排印改动**（都在 `lib/` 里，与字号无关）：`UI4ListView` 新增 `HoverBorderBrush` /
   `SelectedBorderBrush` 两个 `Color` DP（§3.7 的属性表已收，DP 数 15 → 17）；`UI4NavigationView` 把五处
   背景/前景回调里的 `if (_listBox != null)` 换成 `ForEachListBox`（`:207,240,263,286,309`），
   让折叠面板与菜单里的每个列表都吃到同一份色；
   `UI4Panel` 的 `HoverBorderBrush` 改挂 `UI4.Brush.BorderHover` 令牌；`UI4ListBox` 悬浮/选中触发器里的
   `Foreground` 由 `SolidColorBrush` 快照改成绑定到宿主的 `HoverForeground` / `PressedForeground`——
   使用方（`UI4NavigationView`）把 `ItemContainerStyle` 赋成本地值之后再重建 Style 也覆盖不回来，
   快照会永久冻在首次取值那一刻，表现为"换了主题列表文字色不动"。
   另 3 处（2026-10-04 同日第二轮，取自示例项目1/2 与示例项目3 的 `lib/`）：`UI4ListBox` 新增 `IsMenuMode`
   DP，并把 `ListBoxItem` 样式与内容 `ContentPresenter` 的水平对齐由 `Left` 改 `Stretch`（§3.7），
   `UI4ContextMenu` / `UI4NotifyIcon` 据此置 `IsMenuMode=true`、按 `Width` 现算行 `Grid` 宽并给长文字加
   `TextTrimming` + `ToolTip`（§3.9、§3.11）；`UI4GridView` 内部 ScrollViewer 的横向滚动由 `Auto` 改
   `Disabled`，与同族 `UI4ListView` 一致（§3.7）；`UI4NavigationView` 把 `ItemFontSize` 的 DP 从
   `UI4NavigationViewItem` 挪回本控件（挂在 Item 上时 `<ui:UI4NavigationView ItemFontSize="…">` 编译期报
   MC3072「属性不存在」）、项容器尺寸改随字号长、并改用 `FrameworkElementAutomationPeer` 让左栏项与右栏内容
   回到 UIA 树上（§3.8）。

### 4.3 UI4Theme API 一览

| 成员 | 签名 | 说明 |
|---|---|---|
| `Current` | `UI4Theme`（只读） | 当前主题实例；首次访问自动初始化为 `light`。取色用 `Current.ColorOf(令牌)` |
| `ColorOf` / `BrushOf` | `Color` / `SolidColorBrush` | 实例方法：取令牌色 / 取冻结画刷 |
| `CurrentMode` | `UI4ThemeMode`（只读） | 最近一次 `SetTheme` **请求**的模式，可能是 `System` |
| `ResolvedMode` | `UI4ThemeMode`（只读） | 解析后的实际模式；自定义键一律报 `Light`（见 §4.11） |
| `ResolvedKey` | `string`（只读） | 当前生效的主题键（`light` / `dark` / `highcontrast` / 套装键 / 自定义键） |
| `ThemeKeys` | `IEnumerable<string>` | 已注册的键集合 |
| `SetTheme` | `void SetTheme(UI4ThemeMode)` | 按模式切；`System` 会开启系统跟随并解析真实键 |
| `Apply` | `bool Apply(string definitionKey)` | 按**键**切（套装与自定义主题走这里）；未知键返回 `false` 且不改状态 |
| `Register` | `void Register(UI4ThemeDefinition)` | 注册自定义主题（同名覆盖）；`null` 抛 `ArgumentNullException` |
| `SetAccent` | `void SetAccent(Color)` | 只覆盖强调色，自动派生 `AccentDark`（×0.85 亮度） |
| `ApplyToApplication` | `void` | 把当前主题的共享字典挂进 `Application.Resources`（幂等） |
| `FollowSystemHighContrast` | `static bool`（默认 `false`） | `System` 模式下是否优先跟随系统高对比度 |
| `ReleaseSystemFollow` | `void` | 显式停止系统跟随（如退出时） |
| `Persistence` | `static IThemePersistence`（默认 `null`） | 持久化后端；`null` = 不持久化 |
| `Save` / `ApplyPersisted` | `void` / `bool` | 写入 / 读取并应用当前请求模式 |
| `ThemeChanged` | `static event EventHandler` | 主题重写后异步广播（`Dispatcher.BeginInvoke`，`Input` 优先级） |
| `StaticPropertyChanged` | `static event PropertyChangedEventHandler` | `CurrentMode` / `ResolvedMode` / `ResolvedKey` 变化通知，供 XAML 绑定 |
| `ThemeSaved` / `ThemeLoading` | `static event EventHandler<UI4ThemeMode>` | `Save` 之后 / `ApplyPersisted` 应用之前 |

`UI4ThemeDefinition` 的三个内置工厂 + `UI4ThemePacks` 的 8 个工厂都在 [§4.4](#44-内置三套的完整取值)、[§4.5](#45-预置套装-ui4themepacks8-套)。

### 4.4 内置三套的完整取值

38 个令牌 × 3 套内置主题。`#AARRGGBB` 的前两位是 alpha（叠加色用它表达半透明）。
下表由 `UI4ThemeDefinition.Light()` / `Dark()` / `HighContrast()` 的实参直接生成，改色板后再重新生成即可对齐。

| 令牌 `UI4ThemeToken` | `light` | `dark` | `highcontrast` |
|---|---|---|---|
| `Accent` | #0078D4 | #0099FF | #FFFF00 |
| `AccentDark` | #0066B5 | #0078D4 | #DDDD00 |
| `AccentEnd` | #9333EA | #6428C8 | #FFFF00 |
| `TextForeground` | #1E1E1E | #E6E6E6 | #FFFFFF |
| `TextSecondary` | #000000 | #CCCCCC | #FFFFFF |
| `Background` | #FFFFFF | #202026 | #000000 |
| `Surface` | #FFFFFF | #282830 | #000000 |
| `BorderNormal` | #C8C8DC | #3C3C4B | #FFFFFF |
| `BorderSecondary` | #B4B4C8 | #36364A | #FFFFFF |
| `BorderHover` | #0078D4 | #0099FF | #FFFF00 |
| `BorderFocus` | #0066B5 | #0078D4 | #FFFF00 |
| `Placeholder` | #D3D3D3 | #808080 | #CCCCCC |
| `HoverOverlay` | #14000000 | #14FFFFFF | #33FFFF00 |
| `SelectedOverlay` | #0A000000 | #0AFFFFFF | #4DFFFF00 |
| `TrackBackground` | #0A000000 | #14FFFFFF | #33FFFFFF |
| `CheckBackground` | #0066B5 | #008CD2 | #00008B |
| `Icon` | #78788C | #A0A0B4 | #FFFFFF |
| `IconHover` | #3C3C50 | #C8C8DC | #FFFF00 |
| `PanelBorder` | #3C788CC8 | #3C6478B4 | #FFFFFF |
| `OffBackground` | #C8C8D2 | #3C3C46 | #3A3A3A |
| `MenuBackground` | #F8F8F8 | #2D2D32 | #000000 |
| `ListSelected` | #2563EB | #3B7BFF | #00008B |
| `HeaderBackground` | #F5F5F5 | #2A2A30 | #101010 |
| `HeaderForeground` | #1E1E1E | #E6E6E6 | #FFFFFF |
| `RowHoverBackground` | #F0F0F5 | #2E2E38 | #33FFFFFF |
| `RowSelectedBackground` | #D3D3D3 | #3A3A48 | #00008B |
| `GridLine` | #E6E6EB | #3A3A45 | #FFFFFF |
| `ProgressStart` | #0096E6 | #00AAFF | #FFFF00 |
| `CheckBoxUnchecked` | #D3D3D3 | #505058 | #000000 |
| `HoverBorderColorLight` | #8C8CAA | #606078 | #FFFF00 |
| `OnAccent` | #FFFFFF | #FFFFFF | #000000 |
| `Shadow` | #000000 | #000000 | #000000 |
| `ScrollBarThumb` | #50000000 | #66FFFFFF | #FFFFFF |
| `Separator` | #DCDCDC | #404048 | #FFFFFF |
| `BorderWeak` | #1A000000 | #33FFFFFF | #FFFFFF |
| `TextMuted` | #C8000000 | #99FFFFFF | #FFFFFF |
| `BackgroundGradientStart` | #E1ECF5 | #26262E | #000000 |
| `BackgroundGradientEnd` | #FFFFFF | #202026 | #000000 |

高对比度这套的三个非常规取值值得单独记：

- `Accent` / `AccentEnd` 是**纯黄** `#FFFF00`，所以 `OnAccent` 必须是**黑** `#000000`（黄底黑字），
  这也是 `UI4Button` 前景改用「对比度取高」而不是亮度阈值判的原因。
- `CheckBackground` / `ListSelected` / `RowSelectedBackground` 用**深蓝** `#00008B`，
  以保证白色对勾与白字在选中底上仍可读。
- `BorderNormal` / `BorderSecondary` / `GridLine` / `Separator` / `BorderWeak` / `TextMuted` / `Icon` 全部拉成纯白，
  弱层级在这种主题里靠「有框 / 没框」而不是靠灰阶深浅区分。

### 4.5 预置套装 UI4ThemePacks（8 套）

内置三套是「通用底」，业务页往往要更贴场景的色板。`UI4ThemePacks` 在内置定义之上派生 8 套
**手调、覆盖全部 38 个令牌**的套装（亮 5 + 暗 3）。

```csharp
UI4ThemePacks.RegisterAll();                  // 启动时一次，可重复调用
UI4Theme.Apply(UI4ThemePacks.DataConsole);    // 全局应用；键是英文稳定契约
UI4Theme.Register(UI4ThemePacks.DefinitionFor(UI4ThemePacks.PaperGrey));  // 只注册一套（想改令牌再注册）

string cn   = UI4ThemePacks.DisplayName(UI4Theme.ResolvedKey);   // 「数据台」
string shade = UI4ThemePacks.ShadeName(UI4Theme.ResolvedKey);    // 「亮」
string row  = UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey);  // 「数据台（亮） · data-console」
foreach (string k in UI4ThemePacks.Keys) { /* 8 个套装键，按亮 5 + 暗 3 排列 */ }
```

| 常量 | 键（稳定契约） | 展示名 | 深浅 | 基线 | 场景 |
|---|---|---|---|---|---|
| `UI4ThemePacks.DataConsole` | `data-console` | 数据台 | 亮 | `Light()` | 表格/列表密集的后台：面板比底白一档、网格线可辨，正文近黑拿高对比 |
| `UI4ThemePacks.Reading` | `reading` | 阅读 | 亮 | `Light()` | 长文与文档：暖纸底、低饱和，强调色只用于链接与焦点 |
| `UI4ThemePacks.PaperWhite` | `paper-white` | 纸白 | 亮 | `Light()` | 暖白纸质亮档（参考 100-themes `polaroid/day`），紫粉强调 |
| `UI4ThemePacks.PaperGrey` | `paper-grey` | 灰纸 | 亮 | `Light()` | 冷灰纸质档（参考 `tundra/day`），与纸白同亮度只差色温 |
| `UI4ThemePacks.Form` | `form` | 录入 | 亮 | `Light()` | 表单与设置页：字段边界清晰、焦点环醒目 |
| `UI4ThemePacks.OnCall` | `oncall` | 值守 | 暗 | `Dark()` | 夜间长时监控大屏：压暗纯白、琥珀强调，不与业务红/绿告警色抢位 |
| `UI4ThemePacks.Terminal` | `terminal` | 终端 | 暗 | `Dark()` | 日志与代码：冷青强调，层次主要靠边框而非底色台阶 |
| `UI4ThemePacks.Showcase` | `showcase` | 展示 | 暗 | `Dark()` | 投屏看板与媒体页：深靛底配紫→品红渐变，远距离可读 |

契约：

- **键是英文且不会改**（写进 XAML、配置与代码），中文与深浅标记只用于展示。
  `DisplayName` / `ShadeName` 对未登记的键（含宿主自定义主题）返回原键名或空串，排错时看到的仍是真键名。
- **深浅标记只加在套装上**：内置三套不加（`light` / `dark` / `highcontrast` 的中文名自带深浅义，
  宿主自定义键的深浅也不由键名承诺）。
- 套装可以直接写进 `UI4ThemeScope.Theme="paper-grey"` 做局部换肤（[§4.6](#46-局部作用域-ui4themescope)）。
- 改色板只改 `UI4ThemePacks.cs` 里对应的 `XxxDefinition()` 工厂，每套的取值理由写在方法注释里。

亮档 5 套（38 个令牌全覆盖，未覆盖的会继承基线，但这里没有「未覆盖」——每套都写满 38 个）：

| 令牌 `UI4ThemeToken` | `data-console` | `reading` | `paper-white` | `paper-grey` | `form` |
|---|---|---|---|---|---|
| `Accent` | #1D5FA8 | #1F6F6B | #A1568C | #7A5F9C | #0B7285 |
| `AccentDark` | #17497F | #175754 | #7E3F6B | #5B4480 | #085C6B |
| `AccentEnd` | #2466A3 | #22736F | #8D4A79 | #6C5293 | #157A8C |
| `TextForeground` | #16202B | #23211D | #2E241D | #1C292D | #10202A |
| `TextSecondary` | #3D4A5C | #4A463F | #4F463E | #3E4A4E | #33495A |
| `Background` | #F5F7FA | #FBF9F4 | #F7F2EF | #EFF5F7 | #F7F9FB |
| `Surface` | #FFFFFF | #FFFDFA | #FCFAF8 | #FAFCFD | #FFFFFF |
| `BorderNormal` | #CBD5E1 | #D9D2C4 | #DEDAD6 | #D6DCDE | #B9C6D2 |
| `BorderSecondary` | #B6C2D0 | #C6BEAE | #C9C0BA | #C2C9CC | #94A6B5 |
| `BorderHover` | #1D5FA8 | #1F6F6B | #A1568C | #7A5F9C | #0B7285 |
| `BorderFocus` | #17497F | #175754 | #7E3F6B | #5B4480 | #085C6B |
| `Placeholder` | #7A8A9C | #8A847A | #8E8279 | #7A8689 | #778C9B |
| `HoverOverlay` | #0E0B1F33 | #0F3B2F1E | #0F3B2F2A | #0F33403F | #0F0B7285 |
| `SelectedOverlay` | #261D5FA8 | #261F6F6B | #26A1568C | #267A5F9C | #260B7285 |
| `TrackBackground` | #E2E8F0 | #EAE4D8 | #E4DEDA | #DFE6E9 | #DDE7EE |
| `CheckBackground` | #1D5FA8 | #1F6F6B | #A1568C | #7A5F9C | #0B7285 |
| `Icon` | #64748B | #6E685F | #6B615A | #5C686C | #5E7484 |
| `IconHover` | #1D5FA8 | #1F6F6B | #A1568C | #7A5F9C | #0B7285 |
| `PanelBorder` | #DCE3EC | #E4DED1 | #E4DEDA | #DAE1E4 | #C9D6E2 |
| `OffBackground` | #E9EEF4 | #EFEAE0 | #EDE8E4 | #E8EEF0 | #EAF0F4 |
| `MenuBackground` | #FFFFFF | #FFFDF8 | #FBF8F6 | #FBFDFE | #FFFFFF |
| `ListSelected` | #1D5FA8 | #1F6F6B | #E4D0D9 | #D8D7E6 | #0B7285 |
| `HeaderBackground` | #F1F5F9 | #F5F1E8 | #EBE7E3 | #E3E9EB | #EEF4F8 |
| `HeaderForeground` | #16202B | #23211D | #1A120C | #0B1619 | #10202A |
| `RowHoverBackground` | #EEF4FB | #F5F1E8 | #F1ECE8 | #E7EEF0 | #EAF5F8 |
| `RowSelectedBackground` | #DCEAF9 | #EAE3D3 | #E4D0D9 | #D8D7E6 | #D6EEF4 |
| `GridLine` | #E6EBF2 | #EDE8DE | #EDE7E3 | #E6ECEE | #E3EBF1 |
| `ProgressStart` | #1D5FA8 | #2C8A85 | #B0699B | #8A72B0 | #1B93A8 |
| `CheckBoxUnchecked` | #CBD5E1 | #C6BEAE | #C9C0BA | #C2C9CC | #B9C6D2 |
| `HoverBorderColorLight` | #93B4D8 | #A9C9C6 | #D9BFD0 | #BCC3DE | #8FC6D4 |
| `OnAccent` | #FFFFFF | #FFFFFF | #FFFFFF | #FFFFFF | #FFFFFF |
| `Shadow` | #2D1A2433 | #2D4A463F | #2D4A4039 | #2D45525A | #2D10202A |
| `ScrollBarThumb` | #8CB6C2D0 | #8CC9C2B2 | #8CC9BFB9 | #8CB6BDC0 | #8CAFC2CE |
| `Separator` | #E2E8F0 | #E7E1D5 | #E4DEDA | #DFE6E9 | #E3EBF1 |
| `BorderWeak` | #EDF1F6 | #EFEADF | #EFE9E5 | #E8EEF0 | #EFF4F8 |
| `TextMuted` | #64748B | #6E685F | #6B615A | #5C686C | #5E7484 |
| `BackgroundGradientStart` | #F8FAFC | #FDFBF7 | #F9F5F2 | #F2F7F9 | #F7FAFC |
| `BackgroundGradientEnd` | #FFFFFF | #FFFFFF | #F2EBE6 | #E9F1F4 | #FFFFFF |

暗档 3 套：

| 令牌 `UI4ThemeToken` | `oncall` | `terminal` | `showcase` |
|---|---|---|---|
| `Accent` | #E0A458 | #3FB6D3 | #6B4BE8 |
| `AccentDark` | #B9863F | #2E8CA6 | #5738C9 |
| `AccentEnd` | #F0C07A | #6FD3E8 | #B8439A |
| `TextForeground` | #D8DEE6 | #D6E4F0 | #EEF0FA |
| `TextSecondary` | #AEB8C4 | #A7BACD | #C4C9E8 |
| `Background` | #0E1116 | #0B1220 | #171A2E |
| `Surface` | #161B22 | #111A2B | #222645 |
| `BorderNormal` | #333D48 | #263650 | #3E4576 |
| `BorderSecondary` | #404B57 | #334561 | #4C5490 |
| `BorderHover` | #E0A458 | #3FB6D3 | #6B4BE8 |
| `BorderFocus` | #F0C07A | #6FD3E8 | #B8439A |
| `Placeholder` | #747F8C | #6E8297 | #868DB8 |
| `HoverOverlay` | #14FFFFFF | #14FFFFFF | #1AFFFFFF |
| `SelectedOverlay` | #2EE0A458 | #2E3FB6D3 | #336B4BE8 |
| `TrackBackground` | #262F3A | #1B2942 | #2C3159 |
| `CheckBackground` | #E0A458 | #3FB6D3 | #6B4BE8 |
| `Icon` | #8C97A4 | #879CB2 | #9FA6CC |
| `IconHover` | #E0A458 | #3FB6D3 | #B8439A |
| `PanelBorder` | #2A323C | #1F2C44 | #343A63 |
| `OffBackground` | #1A2028 | #141D2E | #232741 |
| `MenuBackground` | #161B22 | #111A2B | #222645 |
| `ListSelected` | #2F3E4E | #24466B | #4C5490 |
| `HeaderBackground` | #12171D | #0E1727 | #1B1E36 |
| `HeaderForeground` | #D8DEE6 | #D6E4F0 | #EEF0FA |
| `RowHoverBackground` | #1B222B | #16233A | #2A2F55 |
| `RowSelectedBackground` | #24303C | #1D3350 | #35407A |
| `GridLine` | #1E252D | #17233A | #2A2F55 |
| `ProgressStart` | #F0C07A | #6FD3E8 | #B8439A |
| `CheckBoxUnchecked` | #3A4550 | #2C3E5A | #444B7C |
| `HoverBorderColorLight` | #4A5866 | #3E5675 | #5A6398 |
| `OnAccent` | #101418 | #06131F | #FFFFFF |
| `Shadow` | #73000000 | #80000814 | #8C05070F |
| `ScrollBarThumb` | #B33A4550 | #B32C3E5A | #B3444B7C |
| `Separator` | #222A33 | #1B2942 | #30365F |
| `BorderWeak` | #1A212A | #131E31 | #1F2340 |
| `TextMuted` | #8C97A4 | #879CB2 | #9FA6CC |
| `BackgroundGradientStart` | #0E1116 | #0B1220 | #171A2E |
| `BackgroundGradientEnd` | #161B22 | #111A2B | #222645 |

### 4.6 局部作用域 UI4ThemeScope

在任意 `FrameworkElement`（整窗、`UserControl`、一张卡片）上声明主题键，**整棵子树**改用该主题，与全局互不干扰。

```xml
<Border ui:UI4ThemeScope.Theme="dark">
    <StackPanel TextElement.Foreground="{DynamicResource UI4.Brush.Text}">
        <ui:UI4Button Content="深色按钮" />
        <ui:UI4TextBox Text="深色输入框" />
    </StackPanel>
</Border>
```

```csharp
UI4ThemeScope.SetTheme(myWindow, "highcontrast");   // 整窗另一套主题
UI4ThemeScope.SetTheme(card, "");                   // 撤销：子树回到全局主题
string key = UI4ThemeScope.GetTheme(card);          // 只读该元素自己声明的键（不含祖先的作用域）
```

| 成员 | 签名 | 说明 |
|---|---|---|
| `Theme`（`ThemeProperty`，读写走 `SetTheme` / `GetTheme`） | 附加 `string`，默认 `null` | 作用域键，大小写不敏感 |
| `SetTheme` / `GetTheme` | `void` / `string` | 读写；`element == null` 抛 `ArgumentNullException` |

生效只有一条通路（`OnThemePropertyChanged`）：把该键的**共享令牌字典**（与全局同一实例）插进元素自身
`Resources.MergedDictionaries` 的末位。于是宿主 `{DynamicResource}` 与库内 `SetResourceReference` 的控件都自动跟随，
**控件本身不需要实现任何主题接口**。由此得到四条性质：

- **空串、纯空白与未注册键 = 撤销作用域**，不抛异常——XAML 里写错键名不会让宿主崩，只是回到全局主题。
- **可嵌套**：子树内再声明一个键就是内层作用域。控件解析有效主题时走 `ResolveKey`，先沿逻辑/模板父链
  （能穿过 `Popup` 与控件模板），再退回视觉父链，取最近的一个已注册键。
- **元素自身 `Resources` 里的同名直接键优先于作用域字典**（标准 WPF 资源就近语义）。
- 作用域根若是整个 `Window`，变更与撤销时库会顺带 `UI4WindowTitleBar.Apply(该窗口)`——
  标题栏是非客户区，资源引用够不着，只能重染。

因为字典实例是共享的，`SetAccent` 原地写一次，全局与**所有同键作用域**同时跟随，而异键作用域不受影响。
`Register` 覆盖一个键时会作废旧实例，库再通过 `ReplaceSharedDictionary` / `RebindIfScoped`
把仍挂着旧字典的作用域就地换指向新实例——否则那些作用域会永远显示旧主题（撤销时还会去摘一个已经不在树里的字典）。

### 4.7 强调色、自定义主题与注册时机

```csharp
// 只换强调色：AccentDark 自动按 0.85 亮度派生，共享字典原地重写
UI4Theme.SetAccent(Color.FromRgb(0xE6, 0x78, 0x14));

// 完整自定义主题：克隆内置定义（38 个令牌齐全），再改想改的
UI4ThemeDefinition ocean = UI4ThemeDefinition.Dark().Clone();  // Key = "dark.clone"
ocean.Key = "ocean";
ocean.With(UI4ThemeToken.Accent, Color.FromRgb(0x00, 0x96, 0xAA))
     .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x00, 0x5A, 0x82))
     .With(UI4ThemeToken.Background, Color.FromRgb(0xEC, 0xF8, 0xFA));
UI4Theme.Register(ocean);
UI4Theme.Apply("ocean");        // 也可以写进 UI4ThemeScope.Theme="ocean"

foreach (string k in UI4Theme.ThemeKeys) { /* light / dark / highcontrast / 8 套 / ocean */ }
```

`Register` 的三条分支（`UI4Theme.cs:161-181`，逐行对源码）：

| 情况 | 行为 |
|---|---|
| 注册的键**正是当前生效键** | 清 `_instances` 与 `_sharedResources`，把 `_current` 置空后 `ApplyResolved(_resolvedKey, …)` 整体重来：重建实例、原位换入新字典、触发 `ThemeChanged`（标题栏随之重染） |
| 注册的键**不是当前键，但有作用域在用** | `UI4ThemeScope.RebindIfScoped(key)` 促使该键的字典重建并就地重指向 |
| 其它 | 只登记定义，等 `Apply` / 作用域声明时再生成字典 |

> **旧告诫已失效**：2.0.0/3.0.0 早期文档写过「别对当前正在用的键重复 `Register`，否则引用式控件用新令牌、
> `UI4Theme.Current` 驱动的命令式路径停在旧令牌」。现在这条已被上面第一行分支消掉了——
> 覆盖当前键就是就地整体重来一遍，不需要先切到别的键再切回来。恢复内置配色的最短写法：
>
> ```csharp
> UI4Theme.Register(UI4ThemeDefinition.Light());   // 覆盖当前键 → 库内立即重应用
> ```

### 4.8 系统跟随与高对比度

```csharp
UI4Theme.SetTheme(UI4ThemeMode.System);
UI4ThemeMode real = UI4Theme.ResolvedMode;   // 解析出的 Light / Dark / HighContrast

UI4Theme.FollowSystemHighContrast = true;    // 默认 false；System 模式下优先跟随系统高对比度
```

解析顺序（`ResolveSystemKey()`）：

1. `FollowSystemHighContrast == true` **且** `SystemParameters.HighContrast == true` → `highcontrast`；
2. 否则读注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`：
   `0` → `dark`，其它或缺失 → `light`（异常吞掉，按 `light`）。

事件侧：进入 `System` 模式时订阅 `SystemEvents.UserPreferenceChanged`，只响应
`General` / `Color` / `Accessibility` 三类，并在 `Application.Current.Dispatcher` 上编组后再重解析；
切到非 `System` 模式或调 `ReleaseSystemFollow()` 时退订。读注册表包在 `try` 里，失败一律按亮色。

`UI4ThemeMode.HighContrast` 与 `UI4Theme.Apply("highcontrast")` 等价。高对比度这套的取值理由见
[§4.4](#44-内置三套的完整取值)末尾（黄底必须黑字、选中底用深蓝保证白字可读）。

### 4.9 主题持久化

默认**不持久化**（`UI4Theme.Persistence == null`）。要存就赋一个后端：

| 类型 | 存哪里 | 细节 |
|---|---|---|
| `IThemePersistence`（接口） | — | `void Save(UI4ThemeMode)` / `UI4ThemeMode? Load()` |
| `RegistryThemePersistence` | `HKCU\Software\StartUI4`，值名 `ThemeMode`，`DWORD` = 枚举整数值 | 默认无参构造用这个子键；`new RegistryThemePersistence(subKey)` 可换子键。`Load` 校验 `Enum.IsDefined`，异常吞掉返回 `null` |
| `JsonThemePersistence` | 指定路径的极简 JSON：`{"mode":"Dark"}` | 构造时给路径（通常 `%APPDATA%` 或程序目录）；`Save` 会建目录；`Load` 按枚举名子串匹配，无第三方依赖 |

```csharp
UI4Theme.Persistence = new RegistryThemePersistence();     // 或 new JsonThemePersistence(path)
UI4Theme.ApplyPersisted();                                  // 启动时恢复（无记录返回 false）

UI4Theme.ThemeChanged += (s, e) => UI4Theme.Save();         // 切换即落盘
```

`Save()` 在没有后端时只触发 `ThemeSaved`；`ApplyPersisted()` 读到值时先触发 `ThemeLoading` 再 `SetTheme`。
**注意持久化的是"请求模式"（可能是 `System`），不是解析结果**——恢复后 `ResolvedMode` 仍按当时系统状态算。

### 4.10 窗口标题栏跟随主题

标题栏属于**非客户区**，由 DWM（Desktop Window Manager，Vista 起的桌面合成组件）绘制，
WPF 的属性、`DynamicResource`、控件模板全都够不着它。程序只能通过 `dwmapi!DwmSetWindowAttribute` 表达意图：

| DWM 属性 | 编号 | 染什么 | 可用性 |
|---|---|---|---|
| `DWMWA_USE_IMMERSIVE_DARK_MODE` | 20（20H1 前为 19） | 深/浅标题栏开关 | Win10 起 |
| `DWMWA_CAPTION_COLOR` | 35 | 标题栏底色 = `Background` 令牌 | Win11 起 |
| `DWMWA_TEXT_COLOR` | 36 | 标题文字 = `TextForeground` 令牌 | Win11 起 |
| `DWMWA_BORDER_COLOR` | 34 | 边框 = `BorderNormal` 令牌 | Win11 起 |

值按 COLORREF `0x00BBGGRR` 传（忽略 alpha），`0x01000000` = 交还系统默认；库把它做成公开方法 `ToColorRef(Color)` 供宿主对齐口径。

**深浅不是按主题键判的**，而是按底色亮度：`0.299R + 0.587G + 0.114B < 128` 视为深。
所以任何自定义主题/套装都能拿到正确的深/浅标志，无需枚举主题键。

四条生效通路（前三条宿主零改动）：

| 通路 | 触发点 | 覆盖的场景 |
|---|---|---|
| ① 清扫 | `UI4Theme.ThemeChanged` → `ApplyOpenWindows()` 遍历 `Application.Windows` | 运行中的主题切换、`SetAccent`、系统跟随 |
| ② 补染 | 任一 UI4 控件 `Loaded` → `NotifyContentLoaded` 染它所属窗口 | 「主题已是深色、窗口之后才打开」；同一 `ThemeVersion` 内每窗口只调一次 dwmapi |
| ③ 作用域 | `UI4ThemeScope` 的根是整个 `Window` 时，变更与撤销都按该作用域染色 | 异主题窗口的标题栏与内容一致 |
| ④ 手动 | `UI4WindowTitleBar.Apply(window)` | 不含任何 UI4 控件的窗口（否则要等下一次主题切换才被 ① 扫到） |

能力探测靠**试**而不是猜：`Environment.OSVersion` 在未 manifest 声明的进程里可能虚报 6.3，所以库先试属性 20，
失败退 19；再试配色三色，第一笔失败就把 `_captionColorSupport` 记为「不支持」，此后只保留深/浅标志。
`SupportsCaptionColors` 读的就是这个探测结论。

豁免与限制：

```xml
<Window ui:UI4WindowTitleBar.Enabled="False" ... />   <!-- 改动即时生效：撤销染色或立即应用 -->
```

- 染色维度由系统给：Windows 10 1903~2004 只认深/浅标志（标题栏变深但底色仍是系统设置的颜色，不是主题 `Background`）。
  圆角、阴影、动画由 DWM 掌控，库一律不改。
- `UI4ColorPicker` 与 `UI4MessageBox` **没有系统标题栏**（`WindowStyle=None` + `AllowsTransparency=true`，
  标题区在内容里自绘），对它们调 `Apply` 不会有可见效果。
- 曾评估过的两条替代方案与放弃理由：宿主每窗口手写 P/Invoke（每窗都要写、切主题不重染、库升级还得再改）；
  `WindowChrome` 自绘标题栏（拖拽、双击、最大化还原、Aero Snap、贴边分屏、系统菜单、高 DPI、无障碍与键盘焦点全部要重写并保持与原生一致）。

### 4.11 主题盲区与已知限制

**① 有 7 个令牌库内没人消费。** 用固定串 grep 全部 `.cs`（含 `Internal/`，排除 `UI4Theme*` 自身）实测：
38 个令牌里 **31 个被库内引用，7 个只定义不使用**——它们照样写进每份共享字典，宿主可以直接
`{DynamicResource}` 取用，但改它们不会让任何库内控件变脸。
（原来这项是 30 / 8：`ListSelected` 自 2026-10-04 起被 `UI4ListView.SelectedBorderBrush` 消费，见 §4.2.1 第 4 条。）

| 令牌 | 内置 light 值 | 设计意图（注释） | 库内现状 |
|---|---|---|---|
| `TextSecondary` | `#000000` | 次级文字 | 无消费者 |
| `ListSelected` | `#2563EB` | 列表选中底 | **有消费者**：`UI4ListView` 构造函数把它挂给 `SelectedBorderBrush`（选中卡片描边）。`UI4ListBox` 那边仍用 `PressedBackground` 字面值，不跟它走 |
| `HeaderBackground` | `#F5F5F5` | 表头底色 | 无消费者（原为 `UI4DataGrid` 预留，该文件已在 net10 删除） |
| `HeaderForeground` | `#1E1E1E` | 表头文字 | 同上 |
| `RowHoverBackground` | `#F0F0F5` | 行悬浮底 | 同上 |
| `RowSelectedBackground` | `#D3D3D3` | 行选中底 | 同上 |
| `GridLine` | `#E6E6EB` | 网格线 | 同上 |
| `Separator` | `#DCDCDC` | 分隔线（菜单分隔、翻牌中缝） | 无消费者；`UI4MenuSeparatorElement.SeparatorColor` 用的是字面值 |

**② 19 个颜色类依赖属性没挂令牌。**（原来是 20 个：`UI4Panel.HoverBorderBrush` 现在由构造函数挂
`UI4.Brush.BorderHover`，已从本表删去；`UI4ListView` 新增的 `HoverBorderBrush` / `SelectedBorderBrush` 两个 DP
同样在构造函数里挂了 `UI4.Color.BorderHover` / `UI4.Color.ListSelected`，不进本表。）它们在深色 / 高对比度下不会自动变，宿主可按下表自救
（`{DynamicResource …}` 或 `{Binding Source={x:Static ui:UI4Theme.Current}}`）：

| 属性 | 当前字面默认值 | 建议接的令牌 |
|---|---|---|
| `UI4Button.HoverBorderBrush` | `null`（= 不改描边） | 保持 `null` 即可 |
| `UI4FlipTextBlock.ShadowColor` | `Black` | `Shadow` |
| `UI4ListBox.HoverForeground` | `#DC000000` | `TextMuted` |
| `UI4ListBox.PressedBackground` | `#2563EB` | `ListSelected` |
| `UI4ListBox.PressedForeground` | `#FFFFFF` | `OnAccent` |
| `UI4ListBox.NumberCircleBackground` | 冻结画刷 `#2563EB` | `ListSelected` |
| `UI4MenuElementItem.IconForeground` | `null`（= 用文字色） | 保持 `null` 或接 `Icon` |
| `UI4MenuSeparatorElement.SeparatorColor` | `#DCDCDC` | `Separator` |
| `UI4NavigationView.ItemBackground` | `Transparent` | 保持 |
| `UI4NavigationView.ItemForeground` | `Black` | `Text`（别名） |
| `UI4NavigationView.ItemHoverColor` | `#0A000000` | `HoverOverlay` |
| `UI4NavigationView.ItemHoverForeground` | `Black` | `TextForeground` |
| `UI4NavigationView.ItemPressedBackground` | `White` | `Surface` |
| `UI4NavigationView.ItemPressedForeground` | `Black` | `TextForeground` |
| `UI4Panel.BorderColor` | `#3C788CC8` | `PanelBorder` |
| `UI4Panel.HoverBorderBrush` | `#46788CC8` | `PanelBorder`（alpha 略高） |
| `UI4Radio.DotColor` | `White` | `OnAccent` |
| `UI4Switch.ThumbColor` | `White` | `OnAccent` |
| `UI4Tab.TabBackground` | `Transparent` | 保持 |
| `UI4TextBlock.PanelBackground` | `Transparent` | 保持 |

```xml
<!-- 深色下「选中项黑字压黑底」就是这么来的，改一行即可 -->
<ui:UI4ListBox PressedBackground="{DynamicResource UI4.Color.ListSelected}"
               PressedForeground="{DynamicResource UI4.Color.OnAccent}" />
```

**③ 其它逐条实测过的限制**

- **`UI4FlipTextBlock` 翻牌中缝是硬编码** `#33000000`（`UI4FlipTextBlock.cs:259`），不跟令牌。
- **`UI4ListBox` 编号角标的数字颜色**在 `Dispatcher.BeginInvoke` 里重绘，局部作用域下这一处可能取到全局色。
- **`UI4ComboBox` 宽度只增不减**：样式把 `MinWidth` 自绑到自身 `ActualWidth`（`UI4ComboBox.cs:212`）。
- **`UI4Grid` 的 `Background` 会被写回渐变**（`UpdateBackground()` 直接 `SetValue`），主题切换即覆盖宿主赋值。
- **自定义键的 `CurrentMode` / `ResolvedMode` 仍报 `Light`**：`ModeForKey` 只认 `dark` / `highcontrast` 两个键，
  其余（含 8 套与自定义主题）一律回 `Light`。判深浅请按底色亮度（同 `UI4WindowTitleBar` 的公式），别读 `ResolvedMode`。
- **AvalonEdit 的语法高亮配色不跟主题**：颜色由 XSHD 决定，不走 WPF 资源体系，库只能染编辑器外壳底与前景。
- **主题切换是瞬时的，没有交叉淡入动画**。观感上「一下就换完」是预期行为。
- **切换会整份重建 `Style` 与模板**。挂了令牌的 DP 各自带 `OnStyleRefresh` 类回调，令牌一变就
  `new Style(...)` + `new ControlTemplate(...)`。一次性探针实测（数字与复现方式见
  [架构审计报告 §9.3](架构审计报告-3.0.0主题机制评审.md)）：单个 `UI4Button` 一次 `SetTheme` 重建 6 份新
  Style+模板（`dark → highcontrast` 为 8 份），300 个按钮 `light → dark` 全部生效 78 ms，`SetAccent` 277 ms。
  要归零开销得把模板内的颜色改用 `TemplateBinding` / 动态 brush，而不是每次 `new`。

> 关于主题机制的重构优缺点评审、令牌覆盖率差量（17 → 89 处）、以及 2.0.0 `IThemeAware` 机制的历史问题清单，
> 见同目录的 [`架构审计报告-3.0.0主题机制评审.md`](架构审计报告-3.0.0主题机制评审.md)。该文档是 2026-09-29 时点的审计
> 加 2026-10-02 的复核更正，其中 §5、§1.2、§1.3、§8 围绕已删除的 `IThemeAware` 展开，仅作历史记录。

---

## 五、静态服务

### 5.1 UI4Clipboard

| 成员 | 签名 | 说明 |
|---|---|---|
| `ContainsText` | `static bool ContainsText()` | 剪贴板里有没有可读的 Unicode 文本；**不打开剪贴板**，所以不跟监听程序抢锁 |
| `TrySetTextAsync` | `static void TrySetTextAsync(string text, Action<bool> onDone = null)` | 后台线程写入，最多重试约 3 秒（30 次 × 100 ms），完成回到调用方线程回调 `true/false` |
| `TryGetTextAsync` | `static void TryGetTextAsync(Action<string> onDone)` | 后台线程读取，最多重试约 2 秒（20 次 × 100 ms），读不到回调 `null` |

**为什么要自己写一份**：WPF 的 `System.Windows.Clipboard` 基于 OLE，写入前会执行 `OleFlushClipboard`。
剪贴板是全局单锁资源，当截图工具 / 剪贴板历史程序正在监听 `WM_CLIPBOARDUPDATE` 并持有剪贴板时，
OLE 调用会在**调用线程**上重试到秒级，甚至抛 `CLIPBRD_E_CANT_OPEN`——用户看到的就是"按 Ctrl+C 卡一下"。
本类直接走 Win32：`OpenClipboard` / `EmptyClipboard` / `GlobalAlloc` + `GlobalLock` + `SetClipboardData(CF_UNICODETEXT)`，
并把重试循环固定在后台线程上。

库内消费者：`UI4TextBox`、`UI4PasswordBox`、`UI4CodeEditor` 的 Ctrl+C / Ctrl+X / Ctrl+V 与右键菜单
（经 `Internal.ClipboardCommandTakeover` 在隧道阶段接管），以及 `UI4ContextMenu` 的粘贴可用性判断（`ContainsText()`）。

用法约束两条：

- **调用方不要等写入结果**。`TrySetTextAsync` 是"发起"语义：立刻做界面反馈（删选区、显示提示），
  回调只用来记账，成功与否都不要让 UI 停在那儿。
- 回调通过 `SynchronizationContext.Current`（发起时捕获）回到发起线程；在 UI 线程上发起就一定回到 UI 线程。
- 只处理 `CF_UNICODETEXT`，不处理图片 / 富文本格式。

```csharp
UI4Clipboard.TrySetText(tb.SelectedText, ok => StatusText.Text = ok ? "已复制" : "复制失败（剪贴板被占用）");
UI4Clipboard.TryGetText(t => { if (t != null) PasteInto(t); });
if (UI4Clipboard.ContainsText()) pasteItem.IsEnabled = true;
```

### 5.2 UI4MultiLanguage

库内静态文案（消息框按钮、取色器标题、右键菜单条目文字）。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Current` | `Dictionary<UI4LanguageKey, string>`（只读） | 懒加载：首次读时按 `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName` 解析并缓存 |
| `Get` | `static string Get(UI4LanguageKey)` | 取字符串；**键不在表里时返回键名本身**（`key.ToString()`） |
| `Refresh` | `static void Refresh()` | 只把缓存置空，下次读 `Current` 时重新解析 |
| `GetStrings` | `static Dictionary<UI4LanguageKey, string> GetStrings(string lang)` | 按两字母语言名取整表；大小写不敏感，**未识别一律走英文**；传 `null` 会抛 |

语言表实际有 8 套：`zh`、`ja`、`ko`、`de`、`fr`、`es`、`ru`，其余（含 `en`）落英文。
`UI4LanguageKey` 11 个成员见 [§8.1](#81-枚举全清单)。

```csharp
CultureInfo.CurrentUICulture = new CultureInfo("en-US");
UI4MultiLanguage.Refresh();                       // 之后库内对话框/菜单都变英文
string ok = UI4MultiLanguage.Get(UI4LanguageKey.OK);
```

> 源码 `UI4MultiLanguage` 的类注释写着「支持中文简体、中文繁体、…」，但表里只有**一张** zh 字典，
> `zh-TW` 的 `TwoLetterISOLanguageName` 也是 `zh`，所以繁体目前拿到的是简体文案。要真支持繁体得再加一张表并判 `LCID`。

---

## 六、用法配方

### 6.1 App 启动序列

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ① 想让 UI4ThemeScope.Theme 能写套装键（"paper-grey" 等），必须先注册。
        //    内置 light/dark/highcontrast 由静态构造自带，不注册也能用。
        UI4ThemePacks.RegisterAll();

        // ② 把当前主题的令牌字典挂进 Application.Resources，供宿主 {DynamicResource} 消费。
        //    不调也能跑（库内控件自带资源引用），但宿主自己的样式就查不到 UI4.Brush.*。
        UI4Theme.ApplyToApplication();

        // ③ 恢复上次主题（没配 Persistence 就直接 false）
        UI4Theme.Persistence = new RegistryThemePersistence();
        if (!UI4Theme.ApplyPersisted()) UI4Theme.SetTheme(UI4ThemeMode.System);

        // ④ 主题一变就把用户偏好落盘；退出时摘掉系统跟随的事件订阅
        UI4Theme.ThemeChanged += (s, args) => UI4Theme.Save();
        Exit += (s, args) => UI4Theme.ReleaseSystemFollow();
    }
}
```

顺序上的两个坑：`RegisterAll()` 在 `Apply(套装键)` 之前，否则 `Apply` 认不出键返回 `false`（不抛异常）；
`ApplyToApplication()` 在第一个窗口 `Show()` 之前，否则窗口首帧的 `DynamicResource` 查不到键（会回落到控件默认色）。

### 6.2 主窗口骨架

```xml
<Window x:Class="App.Main"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="clr-namespace:StartUI4Controls;assembly=StartUI4Controls"
        Title="Demo" Width="1100" Height="700"
        Background="{DynamicResource UI4.Brush.Background}">

    <ui:UI4Grid>                          <!-- 默认铺 BackgroundGradientStart → End -->
        <ui:UI4NavigationView Header="StartUI4" LeftPanelWidth="220">
            <ui:UI4NavigationViewItem TextIcon="&#xE80F;" Header="总览" />
            <ui:UI4NavigationViewItem TextIcon="&#xE713;" Header="设置" />
            <ui:UI4NavigationViewBottomItem TextIcon="&#xE946;" Header="关于">
                <ui:UI4TextBlock Text="关于页" Margin="16" FontSize="18" />
            </ui:UI4NavigationViewBottomItem>
        </ui:UI4NavigationView>
    </ui:UI4Grid>
</Window>
```

标题栏不用写任何代码：窗口里出现了 UI4 控件，`Loaded` 时 `UI4WindowTitleBar` 就按有效主题把它染好（§4.10 通路 ②）。

### 6.3 数据绑定列表

```xml
<ui:UI4GridView ItemsSource="{Binding Cards}"
                ItemWidth="260" ItemHeight="180"
                ShadowDepth="12" ShadowOpacity="0.25">
    <ui:UI4GridView.ItemTemplate>
        <DataTemplate>
            <StackPanel Margin="16">
                <ui:UI4TextBlock Text="{Binding Title}" FontSize="18" />
                <ui:UI4TextBlock Text="{Binding Summary}" TextWrapping="Wrap"
                                 Foreground="{DynamicResource UI4.Brush.TextMuted}" />
            </StackPanel>
        </DataTemplate>
    </ui:UI4GridView.ItemTemplate>
</ui:UI4GridView>
```

要点：`UI4GridView.ItemWidth` 是**算列数用的基准单元**，不是卡片宽度（卡片铺满所在列，§3.7）；
文字色/前景尽量走 `{DynamicResource UI4.Brush.*}` 而不是写死，否则换主题时只有模板其余部分在变。

### 6.4 对话框与取色

```csharp
if (UI4MessageBox.Show("确认删除这条记录？", "确认",
        UI4MessageBoxButtons.OKCancel, owner: this) == true)
{
    Delete();
}

Color? picked = UI4ColorPicker.ShowDialog("选择强调色",
    UI4Theme.Current.ColorOf(UI4ThemeToken.Accent), this);
if (picked.HasValue) UI4Theme.SetAccent(picked.Value);
```

两个对话框都是自绘标题区（`WindowStyle=None`），带八向拖边框缩放，文案跟 `UI4MultiLanguage`。

### 6.5 托盘与退出清理

```xml
<ui:UI4NotifyIcon x:Name="Tray" Visibility="Collapsed" ToolTipText="我的应用"
                  IconSource="/Assets/app.ico"
                  TrayLeftMouseUp="Tray_Show" />
```

```csharp
Tray.AddItem(UI4MenuItemType.Copy, () => CopyLast(), () => hasLast);
Tray.AddItem(new UI4TrayMenuItem(UI4MenuItemType.Delete, "清空记录", () => Clear()));
Tray.Visibility = Visibility.Visible;              // 注册图标

protected override void OnClosing(CancelEventArgs e)
{
    Tray.Visibility = Visibility.Collapsed;         // NIM_DELETE
    Tray.Dispose();                                 // 幂等，Application.Exit 也会兜一次
    base.OnClosing(e);
}
```

不清理的后果是托盘残留一个"点不动的死图标"，要等鼠标划过一次才消失——这是 Shell 的行为，不是库的 bug。

### 6.6 局部换肤窗口

```xml
<Window x:Class="App.ScopeWin"
        xmlns:ui="clr-namespace:StartUI4Controls;assembly=StartUI4Controls"
        ui:UI4ThemeScope.Theme="dark"                       <!-- 整窗另一套主题 -->
        Background="{DynamicResource UI4.Brush.Background}">
    <Border ui:UI4ThemeScope.Theme="paper-grey">            <!-- 内层再套一层，合法 -->
        <ui:UI4Button Content="纸白底上的按钮" />
    </Border>
</Window>
```

```csharp
UI4ThemeScope.SetTheme(this, "");     // 撤销本窗作用域，回到全局主题
string k = UI4ThemeScope.GetTheme(this);   // 只读元素自己声明的键
```

整窗作用域会顺带重染标题栏（§4.10 通路 ③）。**托盘菜单不受作用域影响**（§3.11）。

### 6.7 宿主样式接令牌与"当前主题"状态显示

```xml
<Style x:Key="SectionTitle" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource UI4.Brush.Accent}" />
    <Setter Property="FontSize" Value="18" />
</Style>
```

```csharp
// 要在界面上显示当前主题，订阅事件而不是绑静态属性：
// 库发的是 StaticPropertyChanged，而 WPF 绑静态 CLR 属性找的是同名 <属性>Changed 静态事件，
// 所以 {Binding Path=(ui:UI4Theme.CurrentMode)} 会停在初值不再刷新。
UI4Theme.ThemeChanged += (s, e) =>
{
    ThemeFooter.Text = "主题: " + UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey)
                     + "　ResolvedMode=" + UI4Theme.ResolvedMode;
};
```

要判深浅，按底色亮度而不是读 `ResolvedMode`（自定义键一律报 `Light`，§4.11）：

```csharp
Color bg = UI4Theme.Current.ColorOf(UI4ThemeToken.Background);
bool isDark = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B < 128;   // 与 UI4WindowTitleBar 同判据
```

---

## 七、扩展约定

### 7.1 新增一个控件

按现有 30+ 个控件共同遵循的形状写，评审时只盯这几条：

1. **类型与文件**：`public class UI4Xxx : <合适基类>`，一个文件一个主控件，配套类型（Item、枚举、参数类）放同文件。
2. **DP 写法**：`DependencyProperty.Register(nameof(X), typeof(T), typeof(UI4Xxx), new PropertyMetadata(<字面默认值>, OnStyleRefresh))`，
   紧跟着 `public T X { get => (T)GetValue(XProperty); set => SetValue(XProperty, value); }`。
   字面默认值必须写——它同时也是"宿主 `ClearValue` 之后的回落点"。
3. **主题接线**：构造函数里 `SetResourceReference(XProperty, "UI4.Color.<令牌>")`。
   **不要**订阅 `UI4Theme.ThemeChanged` 去逐属性刷色（2.0.0 的 `IThemeAware` 路子已整体删掉，别加回来）。
   颜色要跟随主题的 DP 一律带 `OnStyleRefresh` 类回调重建 Style；能不改模板结构的优先用
   `DynamicResourceExtension`（更省，见 `UI4Button` 禁用态与 `UI4ComboBox` 项悬浮）。
4. **模板由代码构建**：`ControlTemplate` + `FrameworkElementFactory`，模板根元素 `Name` 用 `PART_` 前缀，
   属性回取用 `GetTemplateChild`。工程里没有 `generic.xaml`，别引入 XAML 页（引入就得同步改 `AssemblyInfo.cs` 的 `ThemeInfo`）。
5. **默认值与继承属性**：想给继承来的属性（`Padding` / `FontSize` / `BorderThickness`）设库默认值时，
   用 `静态构造 + OverrideMetadata`（如 `UI4TextBlock`），不要 `new` 一个同名隐藏 DP——那会切断继承链。
6. **需要默认双向绑定**时才用 `FrameworkPropertyMetadataOptions.BindsTwoWayByDefault`（现有：`UI4Switch.IsOn`
   与 `UI4NavigationView` 的 4 个属性）。
7. **本地赋值即退订主题**是既定语义，不要为此写"恢复跟随"的兼容 API。

### 7.2 新增一个颜色令牌

要同时改**四处**，漏一处就有主题在取该令牌时抛 `KeyNotFoundException`（`GetColor` 不兜底）：

| 位置 | 改什么 |
|---|---|
| `UI4ThemeToken.cs` | 枚举末尾追加成员，并写 `<summary>` 说明用途 |
| `UI4ThemeDefinition.Build(...)` | 形参表加一项 + `.With(UI4ThemeToken.X, x)` 链加一行（**顺序即契约**） |
| `Light()` / `Dark()` / `HighContrast()` | 三个调用各补一个命名实参 |
| `UI4ThemePacks.cs` 的 8 个 `XxxDefinition()` | 每套都补 `.With(...)`——现有 8 套都是 38 个令牌写满的，别留空 |

`UI4Theme.WriteTokens` 按 `Enum.GetValues` 遍历，所以新令牌会自动进共享字典（`UI4.Color.X` / `UI4.Brush.X`），
宿主即刻能 `{DynamicResource}`；但**库内没有控件消费它之前，它只是"可取用"**——写进本 README 的
[§4.11](#411-主题盲区与已知限制) 未消费表，直到真接上某个控件。

### 7.3 公开面的纪律

- 默认 `internal`。辅助类型（转换器、资源生成器、窗口行为）进 `StartUI4Controls.Internal`，
  不要为了让 Demo 调用而 `public`。
- 别开 `public static Style _camelField;` 这种字段（现状有 3 处，见 [§8.3](#83-不打算给你用的公开成员)，属历史遗留）。
- 新增静态文案键要在 `UI4LanguageKey` 与 **8 张语言表**里都补齐，`Get` 的兜底是返回键名而不是抛异常，
  漏翻译表现为"界面上出现英文枚举名"。
- 改公共 API 形状（删 DP、改默认值、改返回类型）要升主版本并在根 README 的迁移章节记账——
  3.0.0 删掉 `UI4Theme.*Color` 实例属性就是这么处理的。

### 7.4 文档义务

本 README 是组件库的真相源。改完代码要同步：

- [§1.1](#11-公开-api-统计) 的属性计数（234 这个数会替你把关：加了 DP 却没写进手册，总数就对不上）
- 对应控件小节的属性表与行为契约
- [§4.11](#411-主题盲区与已知限制) 的未消费令牌 / 未挂令牌属性
- [§8.4](#84-校对记录与根-readme-的口径差异) 记录与根 README 新出现的口径差异

---

## 八、附录

### 8.1 枚举全清单

| 枚举 | 成员（按声明顺序） |
|---|---|
| `UI4ThemeMode` | `Light` `Dark` `System` `HighContrast` |
| `UI4ThemeToken` | 38 个，见 [§4.1](#41-数据模型38-个令牌与三份内置定义) 分组表 |
| `ListStyleType` | `None` `Disc` `Number` |
| `UI4MenuItemType` | `Undo` `Redo` `Cut` `Copy` `Paste` `Delete` `SelectAll` |
| `UI4MessageBoxButtons` | `OK` `OKCancel` |
| `PopupActivationMode` | `None` `LeftClick` `RightClick` `DoubleClick` `LeftOrRightClick` `All`（**当前无人读取**，§3.11） |
| `UI4LanguageKey` | `OK` `Cancel` `Notice` `ColorPicker` `Undo` `Redo` `Cut` `Copy` `Paste` `Delete` `SelectAll` |

### 8.2 事件全清单

| 事件 | 宿主类型 | 委托 | 路由策略 |
|---|---|---|---|
| `UI4Switch.Toggled` | `UI4Switch` | `RoutedEventHandler` | Bubble |
| `UI4Tab.AddTab` | `UI4Tab` | `RoutedEventHandler` | Bubble |
| `UI4Tab.CloseTab` | `UI4Tab` | `TabCloseRoutedEventHandler` | Bubble |
| `UI4TabItem.CloseTab` | `UI4TabItem` | `RoutedEventHandler` | Bubble |
| `UI4NotifyIcon.TrayLeftMouseUp` | `UI4NotifyIcon` | `RoutedEventHandler` | Bubble |
| `UI4NotifyIcon.TrayRightMouseDown` | `UI4NotifyIcon` | `RoutedEventHandler` | Bubble |
| `UI4NotifyIcon.TrayMouseDoubleClick` | `UI4NotifyIcon` | `RoutedEventHandler` | Bubble |
| `UI4Theme.ThemeChanged` | 静态 | `EventHandler` | — |
| `UI4Theme.StaticPropertyChanged` | 静态 | `PropertyChangedEventHandler` | — |
| `UI4Theme.ThemeSaved` | 静态 | `EventHandler<UI4ThemeMode>` | — |
| `UI4Theme.ThemeLoading` | 静态 | `EventHandler<UI4ThemeMode>` | — |

其余控件不新增事件，用基类事件（`Button.Click`、`TextBox.TextChanged`、`Selector.SelectionChanged`、
`MenuItem.Click`、`Primitives.Popup` 等）。

### 8.3 不打算给你用的公开成员

以下是**事实上公开、但属实现细节**的成员，别依赖：小版本内可能改名或删除。

| 成员 | 位置 | 现状 |
|---|---|---|
| `public static Style _scrollViewerStyle` | `UI4ListBox` / `UI4ListView` / `UI4GridView` | 静态字段，本意是内部缓存；命名已泄露意图，改动不预告 |
| `public static Style CreateScrollViewerStyleFromXaml()` | `UI4ListView` / `UI4GridView` | 从内联 XAML 字符串建美化滚动条样式；正路是用 `UI4ScrollViewer`，或让 `Internal/ScrollBarResources.MergeInto` 的公开等价物出现后再说 |
| `IndexPlusOneConverter`、`ObjectIsStringConverter`、`NullToVisibilityConverter`、`InverseNullToVisibilityConverter`、`BoolToVisibilityConverter`、`PlaceholderVisibilityConverter`、`InnerPaddingConverter` | 各控件文件 | `public` 的 `IValueConverter`，只为模板内绑定服务；语义与控件版本绑定 |
| `UI4NavigationView.RegularItems` / `BottomItems` | `UI4NavigationView` | 只读视图，`OnItemsChanged` 整表重建，直接改它不进左栏（§3.8） |
| `UI4Theme.Current` 的 `private set` | `UI4Theme` | 只能由库内部换实例，宿主只读 |

真正 `internal`（拿不到，也不必拿）：`ClipboardCommandTakeover`、`ScrollBarResources`、`ThemeSync`、
`WindowAnimationHelper`、`WindowResizeBehavior`、`ColorToBrushConverter`，以及
`UI4NavigationView.NavigationColorToBrushConverter`、`UI4TabControl.Tab*Converter`（4 个）、`UI4TextBlock.TextOrContentConverter`。
`UI4ComboBox.StringSelectionConverter` 比这更严——它是控件内的 `private sealed class`。
其中 `ThemeSync.Apply` 值得知道存在：它记录"本控件上一次由主题写入的值"，只在当前值仍等于上次写入值时才应用新值——
这是"跟随主题但尊重用户显式改动"在命令式路径上的实现（声明式路径靠 `SetResourceReference` + 本地值优先级）。

### 8.4 校对记录（与根 README 的口径差异）

本节逐条写清「本手册与仓库根 README 说法不同处，以及为什么以本手册为准」。每条都给了源码位置或核对方式。

| # | 项 | 旧说法（根 README / 源码注释） | 现说法（对源码核对） | 依据 |
|---|---|---|---|---|
| 1 | 对外依赖属性规模 | 「250+ 个依赖属性对外开放」 | **237 个注册 DP**（235 Register + 2 RegisterAttached）+ 1 个别名 | `grep -c 'public static readonly DependencyProperty'` = 238，其中 `UI4CheckBox.BoxCornerRadiusProperty = CornerRadiusProperty` 是别名不另计。2026-10-04 的 3 个增量来自 `UI4ListView` 的 `HoverBorderBrush` / `SelectedBorderBrush` 与 `UI4ListBox.IsMenuMode` |
| 2 | 主题接线规模 | `UI4ThemeScope` 类注释与根 README 均写「26 个文件 / 89 处引用」 | **25 个文件 / 95 处** `SetResourceReference`（89 处挂控件自身属性，6 处挂内部元素） | 逐文件 grep 计数；那 6 处是 `_mainContainer.SetResourceReference(Border.BackgroundProperty, …)` 形式 |
| 3 | `UI4Button` 前景规则 | 「按背景 WCAG **相对亮度**阈值（<0.45）选白字，否则正文色」 | 在 `OnAccent` 与 `TextForeground` 之间**取与底色对比度更高者**，无亮度阈值 | `UI4Button.cs:218-231`，注释直接说明阈值判法在 HC 亮黄上会选出白字（1.07:1） |
| 4 | `UI4Button` 禁用态底色 | 「背景取主题令牌 `BorderNormal`」 | `UI4.Brush.OffBackground` + 前景 `UI4.Brush.TextMuted`，`BorderThickness=0` | `UI4Button.cs:195-198`；注释点名"不能用 BorderNormal——HC 下它是纯白"。同时根 README 那三行实测对比度表属旧实现口径 |
| 5 | `UI4NotifyIcon` 菜单配色属性 | 属性表列 `MenuBorderColor` / `MenuBackground` / `MenuHoverBg` | **三个 DP 已删除** | `UI4NotifyIcon.cs:184-186` 注释：托盘不在任何窗口可视化树上拿不到作用域，且三个 DP 没有回调（运行期改色不生效），配色改由内部 `UI4ListBox` 的资源引用驱动 |
| 6 | `UI4NotifyIcon.MenuActivation` | 「菜单触发方式」（暗示生效） | **写了不读的死属性**：DP 默认 `RightClick`，构造函数置 `None`，全文件无任何读取；右键弹菜单靠构造期自带的 `TrayRightMouseDown` 订阅 | `grep MenuActivation` 仅命中 ctor 赋值、DP 注册与包装器 4 处；`UI4NotifyIcon.cs:494-503` |
| 7 | `UI4TextBlock` 渐变属性 | 「`GradientStart`/`GradientEnd` 为上游遗留的未实现属性（声明但从未生效）」 | **已删除**，渐变字只走 `Foreground` | `UI4TextBlock.cs:68-69` 注释「已删除：BuildTextStyle 从未消费它们，设了不生效（死属性）」 |
| 8 | `UI4ComboBox.InnerPadding` | 默认 `12,10,30,10` | `12,4,30,4` | `UI4ComboBox.cs` DP 注册的 `new PropertyMetadata(new Thickness(12, 4, 30, 4), …)` |
| 9 | `UI4CheckBox.TextColor` | 默认 `LightGray` | `#1E1E1E`，并挂 `UI4.Color.TextForeground` | `UI4CheckBox.cs` DP 默认值 + ctor 资源引用 |
| 10 | `UI4MessageBox.Show` 签名 | 4 参（无 `owner`） | 5 参，`Window owner = null`；给了则 `CenterOwner`，否则 `CenterScreen`；`width` 默认常量 `DefaultWidth = 460` | `UI4MessageBox.cs:29, 261-278` |
| 11 | `UI4MultiLanguage` 语言数 | 控件详解章节写「zh / en 双语」，清单章节写 8 套 | **8 套**：`zh` `ja` `ko` `de` `fr` `es` `ru` + 其余走英文；且**无繁体专表**（`zh-TW` 落到简体表） | `UI4MultiLanguage.cs:57-78` 的 `GetStrings` switch |
| 12 | `Notice` 中文文案 | 注释写「注意」 | 「提示」 | `UI4MultiLanguage.cs` 中文表；`UI4MessageBox` 的 `<param>` 注释是旧文案 |
| 13 | `IsBrand` 归属 | 列在 `UI4Pivot` / `UI4Tab` 的属性表里 | 声明在 `UI4PivotItem` / `UI4TabItem` 上 | `UI4Pivot.cs:15`、`UI4TabControl.cs:82` |
| 14 | `SeparatorColor` 归属 | 列在 `UI4Menu` 的属性表里 | 声明在 `UI4MenuSeparatorElement` 上 | `UI4Menu.cs:364` |
| 15 | `UI4ListBox.NumberCircleBackground` | 默认「蓝色渐变」 | 冻结的**纯色**画刷 `#2563EB` | `UI4ListBox.cs` `CreateDefaultCircleBrush()` |
| 16 | 预置套装套数 | 主题章节一处写「6 套」，表格列 8 行 | **8 套**（亮 5 + 暗 3），`UI4ThemePacks.Keys` 返回 8 个 | `UI4ThemePacks.cs:75-81` |
| 17 | 版本口径 | 简介写「版本 2.0.0」、构建产物 `StartUI4.WPF.2.0.0.nupkg` | csproj 的 `Version` / `AssemblyVersion` / `FileVersion` 均为 **3.0.0** | `StartUI4Controls.csproj` |
| 18 | 「别对当前生效键重复 Register」 | 主题章节列为必须遵守的告诫 | **已由库内处理**：`Register` 覆盖当前键时会置空 `_current` 并整体 `ApplyResolved` | `UI4Theme.cs:171-175` 及其注释 |
| 19 | 令牌覆盖度 | 只说「38 个颜色令牌」 | 38 个里 **31 个被库内消费、7 个只定义不使用**；另有 19 个颜色类 DP 未挂令牌（两项原为 30 / 8 与 20，2026-10-04 因 `UI4ListView` 两个描边 DP 与 `UI4Panel.HoverBorderBrush` 挂上令牌而变） | 固定串 grep `UI4.Color.X` / `UI4.Brush.X`（排除 `UI4Theme*`），见 [§4.11](#411-主题盲区与已知限制) 两张表 |
| 20 | `Separator` 令牌用途 | 注释写「菜单分隔、翻牌中缝」 | 两处都没接：`UI4MenuSeparatorElement.SeparatorColor` 用字面 `#DCDCDC`，翻牌中缝硬编码 `#33000000` | `UI4Menu.cs:364`、`UI4FlipTextBlock.cs:259` |
| 21 | §1.1 与 §二 清单的 DP 数自相矛盾 | §1.1 长期写「234（232 Register + 2 `RegisterAttached`）」 | **237（235 + 2）**，与 §二 清单列的逐控件属性数合计一致（`grep` 逐文件相加 = 238 声明，减 1 个别名） | §1.1 那句「各控件的属性数加起来正好等于 N」要当自检单用，N 就必须取清单列合计；2026-10-04 前 §1.1 漏并了 `UI4ListView` 的 2 个描边 DP（差 2），本轮连 `UI4ListBox.IsMenuMode` 一起补齐 |

另有两处**根 README 引用了不存在的章节**（历史文档被删），已随本次瘦身一并处理：

- 4 处指向 `src/StartUI4Controls/README.md §九 / 9.3 / 9.9~9.11 / A1-4`：该审计报告已另存为本目录的
  [`架构审计报告-3.0.0主题机制评审.md`](架构审计报告-3.0.0主题机制评审.md)，链接改指它。
- 1 处指向 `主题方案分析与改进.md` 第十一/十二节：该文档已从仓库删除，标题栏的机制与通路改由本手册
  [§4.10](#410-窗口标题栏跟随主题) 完整承载。

---

## 许可证

MIT，见 [`LICENSE.txt`](LICENSE.txt)。原作者 KS.STUDIO；本目录为 .NET 10 (LTS) 移植版。

