# StartUI4Controls 组件速查

程序集/根命名空间 `StartUI4Controls`，NuGet 包 id `StartUI4.WPF`，版本 3.0.0，`net10.0-windows` + `UseWPF=true`，`Nullable=disable`、`ImplicitUsings=disable`。第三方依赖只有 `AvalonEdit 6.3.1.120`（只有 `UI4CodeEditor` 用）。公开类型 62 个、依赖属性 234 个、公开事件 11 个、公开枚举 7 个。

XAML 前缀固定写：`xmlns:ui="clr-namespace:StartUI4Controls;assembly=StartUI4Controls"`。工程内 **0 个 .xaml**，模板全由 C# 构建（`ControlTemplate` + `FrameworkElementFactory`，滚动条样式用 `XamlReader` 解析字符串），因此宿主挂 dll 后直接写 `<ui:UI4Button/>` 就有完整外观，**不需要往 `Application.Resources` 挂资源字典**；代价是改模板必须重编 dll。

## 选型表

| 分组 | 类型 | 基类 | DP 数 | 选它的理由 / 注意 |
|---|---|---|---|---|
| 基础交互 | `UI4Button` | `Button` | 6 | 渐变 + 圆角 + 悬浮；前景在 `OnAccent` 与正文色间自动取对比度更高者；禁用态换模板 |
| 基础交互 | `UI4CheckBox` | `CheckBox` | 6(+1 别名) | `BoxCornerRadius` 与 `CornerRadius` 是同一个 DP 的两个名字 |
| 基础交互 | `UI4Radio` | `RadioButton` | 6 | 属性面与 `UI4CheckBox` 对齐；`DotColor` 默认 `White`，深色主题要接 `OnAccent` |
| 基础交互 | `UI4Switch` | `Control` | 7 | `IsOn` 默认双向绑定 + `Toggled`；`ThumbColor` 同上 |
| 文本输入 | `UI4TextBox` | `TextBox` | 10 | 占位符 / 清除按钮 / 聚焦描边 / 原生剪贴板；同文件另出 3 个转换器 |
| 文本输入 | `UI4PasswordBox` | `TextBox` | 13 | 明暗文切换、自定义掩码、**可绑定 `Password`** |
| 文本输入 | `UI4TextBlock` | `ContentControl` | 10 | 面板圆角 + 阴影 + 渐变字（渐变走 `Foreground`）；不是 `TextBlock` 子类，别当原生文本用 |
| 文本输入 | `UI4FlipTextBlock` | `ContentControl` | 11 | 翻牌数字动画；中缝色硬编码 |
| 选择 | `UI4ComboBox` | `ComboBox` | 8 | 聚焦渐变描边、弹层宽度自适应、超长项省略号 + ToolTip；**宽度只增不减** |
| 对话框 | `UI4ColorPicker` | `Window` | — | HSV 取色，静态 `ShowDialog`；`WindowStyle=None` + 八向拖边框 |
| 对话框 | `UI4MessageBox` | `Window` | — | `Show(content, title, UI4MessageBoxButtons, owner:)` 返回 `bool?`：OK=`true`、Cancel=`false`、关闭=`null` |
| 进度 | `UI4ProgressBar` | `Control` | 8 | 渐变进度条，支持不确定往返模式 |
| 进度 | `UI4ProgressRing` | `ContentControl` | 13 | 确定弧 / 旋转弧 / 中心数值 |
| 滑块 | `UI4Slider` | `Slider` | 6 | 线性滑块，带数值行 |
| 滑块 | `UI4CircleSlider` | `ContentControl` | 10 | 环形滑块，拖拽改值 |
| 容器 | `UI4Panel` | `ContentControl` | 10 | 阴影 + 悬浮缩放卡片；`BorderColor` 默认字面值不跟主题（`HoverBorderBrush` 自 2026-10-04 起由构造函数挂 `UI4.Brush.BorderHover`） |
| 容器 | `UI4Grid` | `Grid` | 2 | 默认铺 `BackgroundGradientStart → End`；**它的 `Background` 会被 `UpdateBackground()` 写回**，主题切换即覆盖宿主赋值 |
| 容器 | `UI4ScrollViewer` | `ScrollViewer` | 1 | 美化滚动条 + 滚轮平滑滚动 |
| 列表 | `UI4ListBox` | `ListBox` | 12 | `ListStyleType`：普通 / 圆点 / 编号 |
| 列表 | `UI4ListView` | `ListBox` | 17 | 卡片式列表，单列，悬浮放大不越界；`HoverBorderBrush`/`SelectedBorderBrush` 分别挂 `BorderHover`/`ListSelected` |
| 列表 | `UI4GridView` | `ListBox` | 15 | 网格卡片，按宽度自适应列数；`ItemWidth` 是**算列数的基准单元**不是卡片宽度；另有只读 `ComputedColumns` |
| 导航 | `UI4Pivot` / `UI4PivotItem` | `Selector` / `HeaderedContentControl` | 10 / 1 | 滑动切换页签 |
| 导航 | `UI4Tab` / `UI4TabItem` | `Selector` / `HeaderedContentControl` | 11 / 6 | 浏览器风格标签；关闭事件参数是 `TabCloseRoutedEventArgs` |
| 导航 | `UI4NavigationView` + `…Item` / `…BottomItem` | `ItemsControl` / `ContentControl` | 13 / 4 | 侧边导航 + 底部固定项 + 内容区，`LeftPanelWidth` 可调；`TextIcon="&#xE80F;"` 用 Segoe Fluent 码位 |
| 菜单 | `UI4Menu` + `UI4MenuElementItem` / `UI4MenuSeparatorElement` | `Menu` / `MenuItem` / `Separator` | 6 / 5 / 1 | 菜单栏：文字图标、KeyTip、分隔符 |
| 菜单 | `UI4ContextMenu` | —（纯代码组件，不是控件） | 5 个普通属性 | `AddItem(new UI4MenuItem(UI4MenuItemType.Copy, text, null, handler))` → `Attach(host)` → `Open()`；内置 7 种标准条目 + `UI4MenuIcons` |
| 编辑器 | `UI4CodeEditor` | AvalonEdit `TextEditor` | — | C# 高亮 + 行号 + 内置右键菜单；**XSHD 配色不跟主题**，库只染外壳底与前景 |
| 系统集成 | `UI4NotifyIcon` | `FrameworkElement`、`IDisposable` | 6 | 纯 P/Invoke 托盘，不依赖 WinForms；`IconSource` 指图标 |
| 系统集成 | `UI4WindowTitleBar` | —（附加属性 + 静态方法） | 1 附加 | `Enabled` 默认 `true`；窗口里出现过 UI4 控件就会在 `Loaded` 时按 DWM 染**原生**标题栏，不要写 `UI4WindowTitleBar` 元素 |
| 主题 | `UI4Theme` / `UI4ThemeScope` / `UI4ThemeDefinition` / `UI4ThemePacks` / `UI4ThemeToken` / `UI4ThemeMode` | — | — | 见 `theming.md` |
| 服务 | `UI4Clipboard` | —（静态类） | — | 原生 Win32 剪贴板读写 |
| 服务 | `UI4MultiLanguage` | —（静态类） | — | 库内静态文案 8 套语言，`UI4LanguageKey` |

## 与原生控件混用

`UI4Button : Button`、`UI4TextBox : TextBox`、`UI4ComboBox : ComboBox` 这些是原生子类，Style/Trigger/Binding 语义照旧。`UI4TextBlock`、`UI4Panel`、`UI4ListView` 等**不是**同名原生类的子类，靠名字猜基类会写错。宿主自己的 `TextBlock`/`Border`/`Grid`/`DockPanel` 可以随意混用，只要颜色走 `{DynamicResource UI4.*}` 就一样跟主题。

## 不接主题的 19 个颜色 DP（深色/高对比度下要宿主自救）

这些属性是字面默认值，`SetTheme` 不会改它们。宿主显式接令牌即可（写法：`Xxx="{DynamicResource UI4.Color.<令牌>}"`）：
原来是 20 个，`UI4Panel.HoverBorderBrush` 已在构造函数里挂上 `UI4.Brush.BorderHover` 所以从本表移出；
`UI4ListView` 新增的 `HoverBorderBrush`/`SelectedBorderBrush` 也是挂令牌的一方，不进本表。

| 属性 | 当前默认 | 建议接的令牌 |
|---|---|---|
| `UI4Button.HoverBorderBrush` | `null`（不改描边） | 保持 `null` |
| `UI4FlipTextBlock.ShadowColor` | `Black` | `Shadow` |
| `UI4ListBox.HoverForeground` | `#DC000000` | `TextMuted` |
| `UI4ListBox.PressedBackground` | `#2563EB` | `ListSelected` |
| `UI4ListBox.PressedForeground` | `#FFFFFF` | `OnAccent` |
| `UI4ListBox.NumberCircleBackground` | 冻结画刷 `#2563EB` | `ListSelected` |
| `UI4MenuElementItem.IconForeground` | `null`（用文字色） | 保持或接 `Icon` |
| `UI4MenuSeparatorElement.SeparatorColor` | `#DCDCDC` | `Separator` |
| `UI4NavigationView.ItemBackground` | `Transparent` | 保持 |
| `UI4NavigationView.ItemForeground` | `Black` | `UI4.Brush.Text` |
| `UI4NavigationView.ItemHoverColor` | `#0A000000` | `HoverOverlay` |
| `UI4NavigationView.ItemHoverForeground` | `Black` | `TextForeground` |
| `UI4NavigationView.ItemPressedBackground` | `White` | `Surface` |
| `UI4NavigationView.ItemPressedForeground` | `Black` | `TextForeground` |
| `UI4Panel.BorderColor` | `#3C788CC8` | `PanelBorder` |
| `UI4Radio.DotColor` | `White` | `OnAccent` |
| `UI4Switch.ThumbColor` | `White` | `OnAccent` |
| `UI4Tab.TabBackground` | `Transparent` | 保持 |
| `UI4TextBlock.PanelBackground` | `Transparent` | 保持 |

「选中项黑字压黑底」就是这么来的，改两行即可：

```xml
<ui:UI4ListBox PressedBackground="{DynamicResource UI4.Color.ListSelected}"
               PressedForeground="{DynamicResource UI4.Color.OnAccent}" />
```

## 字号与字体族：这 6 个控件跟 `UI4.Font.Size.*` 走

库里只有两档字号键（契约全文见 `theming.md` 的「排印键」节）：

| 控件 | 位置 | 读的键 | 库内兜底 |
|---|---|---|---|
| `UI4Button` | 样式 Setter（`UI4Button.cs:137`） | `UI4.Font.Size.Base` | `15` |
| `UI4TextBox` | `UI4TextBox.cs:161` | 同上 | `15` |
| `UI4ComboBox` | `UI4ComboBox.cs:193` | 同上 | `15` |
| `UI4ListBox` | `UI4ListBox.cs:239` | 同上 | `15` |
| `UI4PasswordBox` | `UI4PasswordBox.cs:237` | 同上 | `15` |
| `UI4CodeEditor` | `UI4CodeEditor.cs:37` | `UI4.Font.Size.Code` | `14` |

三条实际影响：

1. 宿主在**应用资源根**上写 `UI4.Font.Size.Base` 就能整体改这 6 类控件的字号，切主题不会被冲掉；实例上本地赋 `FontSize` 仍是退订（照旧是设计）。
2. **另有一批控件把字号写成了本地字面值，它们不读这两个键**：`UI4CheckBox.cs:165`、`UI4GridView.cs:231`、
   `UI4ListView.cs:256` 都是 `FontSize = 15`，`UI4Menu` 的内联 XAML 是 13，`UI4MessageBox`/`UI4ColorPicker`
   按像素定标题与表单字号，`UI4CircleSlider` 用自家的 `ValueFontSize`（默认 30）。本地值既不跟排印键、
   也不跟 `Window.FontSize` 继承。真正"继承"的是那些压根没赋字号的（`UI4TextBlock`、`UI4Panel`、
   `UI4FlipTextBlock` 除默认 metadata 外）。所以宿主做"调全局字号"时，要么只承诺这 6 个控件，
   要么显式给那几类实例赋 `FontSize="{DynamicResource UI4.Font.Size.Base}"`（赋了就是退订，之后不再跟键）。
3. **钉死尺寸的图标钮会被字号撑爆**：`UI4Button` 默认 `Padding=10,0,10,0`，宿主给 `Width=28 Height=28` 时内容区只剩 8 px，字号一大文字就从左上角起画、看着像"没居中"。尺寸随字号长（`App.Size.RoundButton` 之类由宿主派生），别去加 `HorizontalContentAlignment`——`ContentPresenter` 本来就是 `Center`。

## 逐条实测过的限制

- WPF 的 `Popup`（`UI4ComboBox` 下拉、`UI4ContextMenu` 右键菜单）住在自己的可视根里，**不吃祖先的 `LayoutTransform`**：宿主用 `LayoutTransform` 做全局缩放时，非 100% 档下弹层内容仍按 100% 渲染（位置对、字不跟着大）。`RenderTransform` 同样不解决，这是 WPF 机制不是库缺陷。
- `UI4ListBox` 悬浮/选中触发器里的 `Foreground` 是**绑定**到宿主的 `HoverForeground`/`PressedForeground`（2026-10-04 起）。原因：使用方（`UI4NavigationView`）把 `ItemContainerStyle` 赋成本地值后，重建 Style 也覆盖不回来，触发器里放 `SolidColorBrush` 快照就会永久冻在首次取值那一刻，表现为"切了主题列表文字色不动"。宿主给这两个 DP 赋字面值 = 又回到快照行为。
- `UI4FlipTextBlock` 翻牌中缝硬编码 `#33000000`，不跟令牌。
- `UI4ListBox` 编号角标数字色在 `Dispatcher.BeginInvoke` 里重绘，**局部作用域**（`UI4ThemeScope`）下这一处可能取到全局色。
- `UI4ComboBox` 宽度只增不减：样式把 `MinWidth` 自绑到自身 `ActualWidth`。
- `UI4Grid.Background` 会被 `UpdateBackground()` 直接 `SetValue` 写回渐变，宿主赋值在主题切换时丢失。
- 托盘菜单不受 `UI4ThemeScope` 作用域影响。
- 主题切换瞬时完成、无交叉淡入；切换会整份重建 `Style` 与模板（单个 `UI4Button` 一次 `SetTheme` 重建 6 份，300 个按钮 `light → dark` 实测 78 ms，`SetAccent` 277 ms）。界面里放几百个控件时按这个量级评估。
- `UI4NotifyIcon` 不做退出清理会在托盘留一个「点不动的死图标」，要等鼠标划过才消失——这是 Shell 行为，必须在 `OnClosing` 里 `Visibility=Collapsed` + `Dispose()`。

## 属性/枚举/事件全清单在哪

逐控件的属性名、默认值、枚举成员、事件签名都在 `component-manual.md`（上游组件手册逐字）。按需 grep，别整份读：

```bash
grep -n '^### 3\.' references/component-manual.md          # §3.1..3.11 分组定位
grep -n '^## \|^### ' references/component-manual.md        # 全目录
grep -n 'UI4NavigationView' references/component-manual.md  # 单控件详解
sed -n '2039,2089p' references/component-manual.md          # §8.1 枚举 / §8.2 事件 / §8.3 不建议用的公开成员
```
