# 主题系统契约（38 令牌 / 资源键 / 生效通路）

主题的全部数据就是一张 `UI4ThemeToken → Color` 的表，没有 XAML 资源字典、没有 `generic.xaml`。

## 资源键命名规则（写错的代价是静默失效）

令牌枚举名**逐字**决定两个资源键：

| 形式 | 例 | 类型 |
|---|---|---|
| `UI4.Color.<令牌名>` | `UI4.Color.Accent` | `Color` |
| `UI4.Brush.<令牌名>` | `UI4.Brush.Accent` | 冻结 `SolidColorBrush` |
| 别名 `UI4.Brush.Text` | → `TextForeground` 的画刷 | |
| 别名 `UI4.Brush.Border` | → `BorderNormal` 的画刷 | |
| 别名 `UI4.Brush.Accent` | 就是 `Accent` 本体，不是别名 | |

一个主题键对应**一份共享 `ResourceDictionary`**（`UI4Theme.SharedResourcesFor(key)`），内含 38×2 键 + 3 个别名 + **3 个排印默认值**（`UI4.Font.*`，见下一节）。它同时挂在 `Application.Resources.MergedDictionaries`、被库内控件的 `SetResourceReference` 引用、被 `UI4ThemeScope` 插进子树——**同一实例**，所以切主题是原地重写，不是换字典。

资源键拼错**不报编译错、不抛异常**（`DynamicResource` 解析不到就回落到控件代码里的字面默认色），运行时只表现为「这处颜色没跟着主题变」。用 `FindResource` 时注意它**会抛**，取不到要 `?? ` 兜底。

## 38 个令牌（`UI4ThemeToken`，按用途分组）

| 组 | 令牌 |
|---|---|
| 强调色 | `Accent` `AccentDark` `AccentEnd` `OnAccent` |
| 文字 | `TextForeground` `TextSecondary` `TextMuted` `Placeholder` |
| 面 | `Background` `Surface` `MenuBackground` `OffBackground` `TrackBackground` `BackgroundGradientStart` `BackgroundGradientEnd` |
| 边与分隔 | `BorderNormal` `BorderSecondary` `BorderHover` `BorderFocus` `BorderWeak` `PanelBorder` `Separator` `GridLine` |
| 状态叠加 | `HoverOverlay` `SelectedOverlay` `RowHoverBackground` `RowSelectedBackground` `ListSelected` |
| 控件专用 | `CheckBackground` `CheckBoxUnchecked` `HoverBorderColorLight` `ProgressStart` `HeaderBackground` `HeaderForeground` `Icon` `IconHover` `Shadow` `ScrollBarThumb` |

`#AARRGGBB` 前两位是 alpha，叠加色（`HoverOverlay`/`SelectedOverlay`/`Shadow`）靠它表达半透明。完整取值表：内置三套见手册 §4.4，8 套预置见 §4.5。

**7 个令牌库内无人消费**：`TextSecondary` `HeaderBackground` `HeaderForeground` `RowHoverBackground` `RowSelectedBackground` `GridLine` `Separator`（后 5 个是原 `UI4DataGrid` 预留，该文件已在 net10 删除）。它们照样进共享字典，宿主可直接取用，但改它们不会让任何库内控件变脸——别把它们当成「列表/表头配色开关」。**`ListSelected` 自 2026-10-04 起有人消费了**：`UI4ListView` 构造函数把它挂给新加的 `SelectedBorderBrush`，所以改它会让学生卡片的选中描边变脸（`UI4ListBox.PressedBackground` 那类仍是不接主题的老 DP，见 `controls.md`）。

## 排印键 `UI4.Font.*`（不在令牌表里，但住在同一份共享字典）

字号与字体族是 3 个**固定键**，由 `UI4Theme.WriteTokens` 随每份主题字典一起发布，不是从令牌派生的：

| 键 | 类型 | 库内默认 | 库内引用点 |
|---|---|---|---|
| `UI4.Font.Size.Base` | `double` | `15`（`UI4Theme.DefaultFontSizeBase`） | `UI4Button.cs:137`（样式 Setter 挂 `DynamicResourceExtension`）、`UI4TextBox.cs:161`、`UI4ComboBox.cs:193`、`UI4ListBox.cs:251`、`UI4PasswordBox.cs:237` |
| `UI4.Font.Size.Code` | `double` | `14`（`DefaultFontSizeCode`） | `UI4CodeEditor.cs:37` |
| `UI4.Font.Family` | `FontFamily` | `Segoe UI`（`DefaultFontFamily`） | 库内无人引用（字体族是继承性属性，宿主设 `Window.FontFamily` 即可），只作兜底键供宿主绑 |

三条口径（第三条是本包相对上游的改动，别当成上游契约）：

1. **这 3 个键永远解析得到**，因为每份主题字典都带库内默认值。宿主不覆盖就是 15/14/Segoe UI；缺键的写法（早期分叉版让控件引用这三个键却没人发布）会让所有 `UI4*` 控件静默掉到 WPF 裸默认 **12 px**，不报编译错也不抛异常——`DisplaySelfTest` 那条"库没有发布 UI4.Font.Size.Base 的兜底默认值"就是钉这个的。
2. **要改就在应用资源根的自有项上覆盖**：`Application.Resources["UI4.Font.Size.Base"] = 17d`（宿主自己的 `Helpers/Typography.cs` 之类集中发布）。WPF 在同一层先查 `Application.Resources` 的自有项、再查 `MergedDictionaries`，所以**切主题不会把覆盖值冲掉**（库原地重写的是 MergedDictionaries 里那份）。反过来，把键写进 `UI4Theme.SharedResourcesFor(key)` 返回的那份字典**内部**，下次重写就没了。
3. 层级字号（标题/标签/图标各一档）不是库的事：库里只有 `Base` 与 `Code` 两档，其余档位由宿主发布自己的 `App.Font.Size.*` 键（见 SKILL.md Step 4.5）。

## UI4Theme API

| 成员 | 签名 | 说明 |
|---|---|---|
| `Current` | 只读 `UI4Theme` | 首次访问自动初始化为 `light`；`Current.ColorOf(令牌)` 取色 |
| `ColorOf` / `BrushOf` | `Color` / `SolidColorBrush` | 实例方法 |
| `CurrentMode` | 只读 `UI4ThemeMode` | 最近一次 `SetTheme` **请求**的模式，可能是 `System` |
| `ResolvedMode` | 只读 `UI4ThemeMode` | 解析后的实际模式；**自定义键与 8 套一律报 `Light`** |
| `ResolvedKey` | 只读 `string` | 当前生效键（`light`/`dark`/`highcontrast`/套装键/自定义键） |
| `ThemeKeys` | `IEnumerable<string>` | 已注册键集合 |
| `SetTheme` | `void SetTheme(UI4ThemeMode)` | 按模式切；`System` 开启系统跟随并解析真实键 |
| `Apply` | `bool Apply(string key)` | 按键切（套装与自定义走这里）；**未知键返回 `false` 且不抛异常** |
| `Register` | `void Register(UI4ThemeDefinition)` | 注册自定义主题，同名覆盖；`null` 抛 `ArgumentNullException` |
| `SetAccent` | `void SetAccent(Color)` | 只覆盖强调色，`AccentDark` 自动按 ×0.85 亮度派生 |
| `ApplyToApplication` | `void` | 把当前主题共享字典挂进 `Application.Resources`（幂等） |
| `FollowSystemHighContrast` | `static bool`，默认 `false` | `System` 模式下是否优先跟随系统高对比度 |
| `ReleaseSystemFollow` | `void` | 显式停止系统跟随（退出时必调，退订 `SystemEvents`） |
| `Persistence` | `static IThemePersistence`，默认 `null` | `null` = 不持久化 |
| `Save` / `ApplyPersisted` | `void` / `bool` | 写入 / 读取并应用**请求模式** |
| `ThemeChanged` | `static event EventHandler` | 主题重写后异步广播（`Dispatcher.BeginInvoke`，`Input` 优先级） |
| `StaticPropertyChanged` | `static event PropertyChangedEventHandler` | `CurrentMode`/`ResolvedMode`/`ResolvedKey` 变化 |
| `ThemeSaved` / `ThemeLoading` | `static event EventHandler<UI4ThemeMode>` | `Save` 之后 / `ApplyPersisted` 应用之前 |
| `ToColorRef` | `int ToColorRef(Color)` | COLORREF `0x00BBGGRR`，与 DWM 染色对齐口径 |

`UI4ThemeDefinition`：`Light()` / `Dark()` / `HighContrast()` 三个静态工厂（每次新建）、`Key`、`With(令牌, Color)`（链式）、`Has`、`GetColor`、`Clone()`（`Key` 变 `<原键>.clone`）。

**自定义主题必须覆盖全部 38 个令牌**——`GetColor` 对未定义令牌抛 `KeyNotFoundException`。正确姿势是克隆再改：

```csharp
UI4ThemeDefinition ocean = UI4ThemeDefinition.Dark().Clone();   // 38 个令牌齐全
ocean.Key = "ocean";
ocean.With(UI4ThemeToken.Accent, Color.FromRgb(0x00, 0x96, 0xAA));
UI4Theme.Register(ocean);
UI4Theme.Apply("ocean");     // 也可写进 ui:UI4ThemeScope.Theme="ocean"
```

`new UI4ThemeDefinition("my")` 只填三个令牌就注册 = 运行期崩在取色上。

## 8 套预置主题（`UI4ThemePacks`）

`RegisterAll()` 一次注册 8 套（亮 5 + 暗 3，每套都写满 38 个令牌）。**键是英文稳定契约**，中文与深浅标记只用于展示：

| 键 | 展示名 | 深浅 | 基线 | 场景 |
|---|---|---|---|---|
| `data-console` | 数据台 | 亮 | `Light()` | 表格/列表密集的后台，正文近黑高对比 |
| `reading` | 阅读 | 亮 | `Light()` | 长文与文档，暖纸底低饱和 |
| `paper-white` | 纸白 | 亮 | `Light()` | 暖白纸质档，紫粉强调 |
| `paper-grey` | 灰纸 | 亮 | `Light()` | 冷灰纸质档，与纸白只差色温 |
| `form` | 录入 | 亮 | `Light()` | 表单与设置页，字段边界清晰、焦点环醒目 |
| `oncall` | 值守 | 暗 | `Dark()` | 夜间长时监控，琥珀强调不抢告警色 |
| `terminal` | 终端 | 暗 | `Dark()` | 日志与代码，冷青强调，层次靠边框 |
| `showcase` | 展示 | 暗 | `Dark()` | 投屏看板，深靛底配紫→品红渐变 |

`Apply(UI4ThemePacks.DataConsole)` 用常量（等价于写键字符串）；`DisplayName` / `ShadeName` / `DisplayLabel` 拿展示文案；`DefinitionFor(key)` 取定义（想改令牌再注册时用）；`Keys` 遍历。改色板只改 `UI4ThemePacks.cs` 里对应的 `XxxDefinition()` 工厂。

## 三条必须知道的性质

1. **切主题不需要逐控件刷新**。控件不实现任何主题接口（2.0.0 的 `IThemeAware`/`TrackControl` 已整体删除）。`ThemeChanged` 现在只有两个命令式消费者：DWM 标题栏染色、AvalonEdit 刷新。
2. **本地赋值 = 该属性退订主题，这是设计不是缺陷**。`SetResourceReference` 占的也是本地值槽，宿主写 `GradientStart="#090909"` 会把引用顶掉；此后 `ClearValue` 回落到**代码里的字面默认色而不是主题色**。只想改一处观感就照这个语义写，想整棵子树换观感用 `UI4ThemeScope`。宿主本地常量（例：`{x:Static h:Theme.Accent}`、自建 `App.Brush.*`）不跟 `SetTheme`。
3. **换字典不留空窗**。写回 `Application.Resources` 是**原位替换**（`merged[index] = dict` 再摘多余的），不是 `Remove`+`Add`——后者会在中间留一个解析不到的窗口，实测 `dark → highcontrast` 期间控件会短暂读到硬编码默认色。摘除只认库内 `_installedResources` 账本。

## 局部作用域 UI4ThemeScope

任意 `FrameworkElement` 上声明键，整棵子树改用该主题，与全局互不干扰：

```xml
<Window ui:UI4ThemeScope.Theme="dark">                        <!-- 整窗另一套 -->
    <Border ui:UI4ThemeScope.Theme="paper-grey">…</Border>    <!-- 内层再套，合法 -->
</Window>
```

```csharp
UI4ThemeScope.SetTheme(card, "");        // 撤销：子树回到全局主题
string key = UI4ThemeScope.GetTheme(el); // 只读该元素自己声明的键，不含祖先作用域
```

- 唯一通路：把该键的共享字典（与全局同一实例）插进元素自身 `Resources.MergedDictionaries` 末位。
- **空串、纯空白、未注册键 = 撤销作用域，不抛异常**——XAML 里键名写错只是回到全局主题。
- 解析有效主题走 `ResolveKey`：先沿逻辑/模板父链（能穿 `Popup` 与控件模板），再退视觉父链，取最近一个已注册键。
- 元素自身 `Resources` 里的同名直接键优先于作用域字典（标准就近语义）。
- 作用域根是整个 `Window` 时，变更与撤销都会顺带 `UI4WindowTitleBar.Apply(该窗口)`。
- 因为字典实例共享，`SetAccent` 写一次，全局与**所有同键作用域**同时跟随，异键作用域不受影响。

## 系统跟随与持久化

`SetTheme(UI4ThemeMode.System)` 的解析顺序：① `FollowSystemHighContrast == true` 且 `SystemParameters.HighContrast` → `highcontrast`；② 否则读 `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`，`0`→`dark`，其它或缺失/异常→`light`。进入 `System` 订阅 `SystemEvents.UserPreferenceChanged`（只响应 `General`/`Color`/`Accessibility`，在 `Application.Current.Dispatcher` 上编组），切走或 `ReleaseSystemFollow()` 退订。

持久化默认关闭。`RegistryThemePersistence`（`HKCU\Software\StartUI4`，值名 `ThemeMode`，DWORD）或 `JsonThemePersistence(path)`（`{"mode":"Dark"}`）。**存的是请求模式（可能是 `System`），不是解析结果**。

## 标题栏（DWM）

`UI4WindowTitleBar` 是附加属性 + 静态方法，不是控件、不要写成元素。染的是非客户区：属性 20（Win10 起，20H1 前为 19）深/浅标志、35 底色=`Background`、36 文字=`TextForeground`、34 边框=`BorderNormal`（35/36/34 需 Win11）。能力靠**试**不靠猜（`Environment.OSVersion` 在未 manifest 声明的进程里可能虚报 6.3），结论读 `SupportsCaptionColors`。

四条生效通路：① `ThemeChanged` → `ApplyOpenWindows()`；② 任一 UI4 控件 `Loaded` → 补染所属窗口（同一 `ThemeVersion` 内每窗口只调一次）；③ `UI4ThemeScope` 根为窗口时按作用域染；④ 手动 `Apply(window)`——**不含任何 UI4 控件的窗口要走这条**，否则等下次主题切换才被扫到。豁免写 `ui:UI4WindowTitleBar.Enabled="False"`（即时生效）。

判深浅一律按底色亮度，别读 `ResolvedMode`：

```csharp
Color bg = UI4Theme.Current.ColorOf(UI4ThemeToken.Background);
bool isDark = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B < 128;   // 与 UI4WindowTitleBar 同判据
```

`UI4ColorPicker` 与 `UI4MessageBox` 是自绘标题区（`WindowStyle=None` + `AllowsTransparency=true`），对它们调 `Apply` 无可见效果。
