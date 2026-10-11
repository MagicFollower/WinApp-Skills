# StartUI4Controls 组件库架构审计报告

> **审计日期**：2026年9月29日  
> **审计范围**：`src/StartUI4Controls` 全部 27 个源文件（约 12,000+ 行代码）  
> **审计视角**：架构完整性、设计准确性、代码一致性、主题覆盖度

---

## 〇、2026-10-02 更正（主题覆盖度审计表作废说明）

**更正一处旧说法**：本文 §5.1「IThemeAware 实现覆盖」表与 §7「主题覆盖 ★★★★☆ 14/17 控件实现 IThemeAware」
**至少 9 行是错的，不能当依据**。以 2026-10-02 对源码的逐文件 grep 为准：

1. 表里列了 `UI4DataGrid`，但该文件**不存在**（net10 版已删除，实际文件叫 `UI4GridView.cs`）。
2. 表把 `UI4TextBox` / `UI4CheckBox` / `UI4Radio` / `UI4Switch` / `UI4Slider` / `UI4ProgressBar` / `UI4PasswordBox`
   标为「实现 IThemeAware ✅」，实际这 7 个**一个都没实现**——它们走的是构造函数 `SetResourceReference` 的声明式通道。
3. 表把 `UI4TextBlock` 标为「✅/✅/✅」，实际该文件全文 grep `Theme` **零匹配**，完全没有主题代码。
4. 真实数字：实现 `IThemeAware` 的是 **9 个**（Button / ColorPicker / ComboBox / FlipTextBlock / ListBox /
   Menu / MessageBox / NavigationView / Panel），另有 1 个手工订阅 `ThemeChanged`（ContextMenu）、
   **10 个完全未接主题**（ListView / GridView / Tab / CircleSlider / ProgressRing / NotifyIcon / Grid /
   ScrollViewer / TextBlock / CodeEditor）。

同日已完成主题套装化重构（单通道声明式，见根目录 `README.md` 第十节）：`IThemeAware` 接口与
`TrackControl` 追踪机制**已整体删除**，§5、§1.2、§1.3、§8 中围绕它们的「问题 / 建议」随之失效，
仅作为 2.0.0 时点的历史记录保留。仍有效的部分是 §2（样式全量重建模式）、§3（DP 设计）、§4（代码重复）、§6（命名）。

---

## 一、主题系统架构（UI4Theme）

### 1.1 设计评价：良好

[UI4Theme](UI4Theme.cs) 采用 **单例 + 弱引用追踪** 模式，核心设计合理：

| 设计点 | 评价 | 说明 |
|--------|------|------|
| 弱引用追踪 | ✅ 正确 | 避免控件被主题系统持有导致内存泄漏 |
| `Freeze()` 冻结 Brush | ✅ 正确 | 9 个冻结 Brush 零分配跨线程安全 |
| Dispatcher 合并刷新 | ✅ 正确 | `DispatcherPriority.Input` 合并同一帧内的多次切换 |
| 线程安全 | ✅ 正确 | `lock (_trackLock)` 保护追踪列表 |

### 1.2 问题：颜色令牌与冻结 Brush 不对称

| 冻结 Brush（9 个） | 缺失的冻结 Brush |
|---------------------|-----------------|
| AccentBrush, AccentDarkBrush, BackgroundBrush, SurfaceBrush, TextForegroundBrush, OnWhiteBrush, PlaceholderBrush, BorderNormalBrush, MenuBackgroundBrush | **缺少**：`BorderSecondaryBrush`, `BorderHoverBrush`, `BorderFocusBrush`, `IconBrush`, `IconHoverBrush`, `HoverOverlayBrush`, `SelectedOverlayBrush`, `TrackBackgroundBrush`, `CheckBackgroundBrush`, `OffBackgroundBrush`, `ListSelectedBrush`, `HeaderBackgroundBrush`, `HeaderForegroundBrush`, `RowHoverBrush`, `RowSelectedBrush`, `GridLineBrush`, `ProgressStartBrush` |

**影响**：控件在 `BuildXxxStyle()` 中被迫用 `new SolidColorBrush(color)` 创建临时画刷，每次属性变更都产生分配。例如 `UI4CheckBox.BuildCheckBoxStyle()` 创建 `new SolidColorBrush(UI4Theme.Current.CheckBoxUncheckedBackground)`。

**严重度**：中 — 仅在主题切换和属性变更时发生，非高频路径，但与冻结 Brush 的设计哲学不一致。

### 1.3 问题：`IThemeAware` 为 `internal` 接口

`IThemeAware`（UI4Theme.cs 第 303-306 行）声明为 `internal`，这意味着：
- 外部消费者无法实现自定义主题感知控件
- 主题系统对扩展不友好

**建议**：如果这是有意限制，应在文档中说明。如果未来需要支持第三方控件主题化，应改为 `public`。

---

## 二、样式构建模式分析

### 2.1 统一模式：`OnStyleRefresh` → `BuildXxxStyle()`

所有控件遵循相同模式：
```
DP 变更 → OnStyleRefresh → Style = BuildXxxStyle() → 完整重建
```

| 控件 | 回调名 | 模式一致性 |
|------|--------|-----------|
| UI4Button | `OnStyleRefresh` | ✅ |
| UI4TextBox | `OnStyleRefresh` | ✅ |
| UI4CheckBox | `OnStyleRefresh` | ✅ |
| UI4Radio | `OnStyleRefresh` | ✅ |
| UI4Slider | `OnStyleRefresh` | ✅ |
| UI4ComboBox | `OnStyleRefresh` | ✅ |
| UI4ListBox | `OnStyleRefresh` | ✅ |
| UI4ListView | `OnStyleUpdate` | ⚠️ 命名不一致 |
| UI4DataGrid | `OnStyleChanged` | ⚠️ 命名不一致 |
| UI4TabControl | `OnStyleChanged` | ⚠️ 命名不一致 |
| UI4TextBlock | `OnStyleRefresh` | ✅ |
| UI4PasswordBox | `OnStyleRefresh` | ✅ |

**问题**：回调命名不统一（`OnStyleRefresh` / `OnStyleUpdate` / `OnStyleChanged`），建议统一为 `OnStyleRefresh`。

### 2.2 核心问题：每次属性变更完整重建 Style

**所有控件** 在任意 DP 变更时都完整重建整个 `Style` 对象（含 `ControlTemplate`、`Trigger`、`Binding` 等），这导致：

| 问题 | 影响 | 示例 |
|------|------|------|
| 临时对象风暴 | 每次属性变更创建 20-50 个临时对象 | `UI4ComboBox.BuildComboStyle()` 约 225 行，每次创建 ~40 个对象 |
| Binding 重新建立 | 所有 TemplateBinding/Binding 重新创建 | UI4ComboBox 有 ~20 个 Binding |
| 触发器重新创建 | Trigger/EventSetter 全部重建 | UI4ListBox 有 3 个 EventSetter |

**根因**：使用 `FrameworkElementFactory` 构建模板，无法像 XAML 模板那样被 WPF 引擎缓存和共享。

**建议**：对于不频繁变更的属性（如 `CornerRadius`），可以考虑使用 `TemplateBinding` 或 `Binding` 到 DP，而非在 `BuildXxxStyle()` 中硬编码值。这样属性变更时 WPF 引擎自动更新，无需重建整个 Style。

---

## 三、DependencyProperty 设计审计

### 3.1 默认值硬编码与主题脱节

| 控件 | 属性 | 硬编码默认值 | 主题对应值 | 问题 |
|------|------|-------------|-----------|------|
| UI4TextBox | `BorderNormalColor` | `Color.FromRgb(200, 200, 220)` | `BorderNormalColor` Light: 同值 ✅ | 初始一致，但主题切换后用户设置会覆盖 |
| UI4TextBox | `EditBackground` | `Color.FromRgb(255, 255, 255)` | `SurfaceColor` Light: White ✅ | Dark 模式下默认值错误 |
| UI4TextBox | `TextColor` | `Color.FromRgb(30, 30, 30)` | `TextForegroundColor` Light: 同值 ✅ | Dark 模式下默认值错误 |
| UI4CheckBox | `CheckBackground` | `Color.FromRgb(0, 102, 181)` | `CheckBackgroundColor` Light: 同值 ✅ | — |
| UI4Radio | `TextColor` | `Colors.Black` | `TextForegroundColor` Light: `#1E1E1E` | ⚠️ 不一致 |
| UI4ListBox | `BorderNormalColor` | `Color.FromRgb(37, 99, 235)` | 无对应主题色 | ⚠️ 蓝色边框？ |
| UI4Slider | `TrackBackground` | `Color.FromArgb(255, 255, 255, 255)` | `TrackBackgroundColor` Light: `Argb(10,0,0,0)` | ⚠️ 不一致 |
| UI4ProgressBar | `TrackBackground` | `Color.FromArgb(10, 0, 0, 0)` | `TrackBackgroundColor` 同值 ✅ | — |

**关键问题**：DP 默认值在编译时确定，无法感知主题。当用户不显式设置这些属性时，Dark 模式下控件颜色将是错误的（白色背景、黑色文字等）。

**建议**：
1. 在构造函数中将 DP 默认值设置为主题令牌值
2. 或在 `BuildXxxStyle()` 中使用 `ReadLocalValue()` 检测用户是否显式设置，未设置时使用主题值

### 3.2 `new` 关键字隐藏基类属性

| 控件 | 隐藏的属性 | 风险 |
|------|-----------|------|
| UI4TextBlock | `ForegroundProperty`, `PaddingProperty`, `FontSizeProperty`, `FontWeightProperty` | ⚠️ 4 个 `new` 隐藏，可能导致 XAML 绑定歧义 |

`UI4TextBlock` 用 `new` 重新注册了 4 个继承自 `ContentControl`/`Control` 的属性。这会导致：
- 通过基类引用设置属性时行为不一致
- XAML 中可能解析到错误的 DP

---

## 四、代码重复分析

### 4.1 滚动条淡入淡出逻辑 — 3 处重复

| 位置 | 行数 | 逻辑 |
|------|------|------|
| UI4TextBox（第 190-251 行） | ~60 行 | ScrollBar 淡入淡出 + DispatcherTimer |
| UI4PasswordBox（第 292-351 行） | ~60 行 | **完全相同**的 ScrollBar 淡入淡出逻辑 |
| UI4DataGrid（第 358-386 行） | ~30 行 | 类似逻辑（async Task 版本） |

**建议**：提取为 `ScrollBarFadeHelper` 共享类。

### 4.2 右键菜单初始化 — 4 处重复

| 位置 | 菜单项 |
|------|--------|
| UI4TextBox.InitCustomMenu() | Undo/Cut/Copy/Paste/Delete/SelectAll |
| UI4PasswordBox.InitCustomMenu() | 密码/明文模式分别配置 |
| UI4TextBlock.InitCustomMenu() | Copy/SelectAll |
| UI4DataGrid.InitContextMenu() | Copy/SelectAll |

### 4.3 `ColorToBrushConverter` — 3 处独立定义

| 位置 | 类名 |
|------|------|
| UI4Radio（第 195-208 行） | `ColorToBrushConverter`（private nested） |
| UI4NavigationView（第 41-52 行） | `NavigationColorToBrushConverter`（internal） |
| UI4TabControl（第 722-735 行） | `TabColorToBrushConverter`（internal） |

**建议**：统一为一个 `internal` 共享的 `ColorToBrushConverter`。

### 4.4 `BoolToVisibilityConverter` — 2 处独立定义

| 位置 | 可见性 |
|------|--------|
| UI4TextBox（第 425-431 行） | `public` |
| UI4TabControl（第 705-720 行） | `internal` |

---

## 五、主题响应覆盖度审计

### 5.1 IThemeAware 实现覆盖

| 控件 | 实现 IThemeAware | TrackControl/UntrackControl | OnThemeChanged |
|------|:---:|:---:|:---:|
| UI4Button | ✅ | ✅ | ✅ |
| UI4TextBox | ✅ | ✅ | ✅ |
| UI4CheckBox | ✅ | ✅ | ✅ |
| UI4Radio | ✅ | ✅ | ✅ |
| UI4Switch | ✅ | ✅ | ✅ |
| UI4Slider | ✅ | ✅ | ✅ |
| UI4ProgressBar | ✅ | ✅ | ✅ |
| UI4ComboBox | ✅ | ✅ | ✅ |
| UI4PasswordBox | ✅ | ✅ | ✅ |
| UI4ListBox | ✅ | ✅ | ✅ |
| UI4TextBlock | ✅ | ✅ | ✅ |
| UI4NavigationView | ✅ | ✅ | ✅ |
| UI4DataGrid | ✅ | ✅ | ✅ |
| UI4MessageBox | ✅ | ✅ | ✅ |
| **UI4ListView** | ❌ | ❌ | ❌ |
| **UI4Tab** | ❌ | ❌ | ❌ |
| **UI4ScrollViewer** | ❌ | ❌ | ❌ |
| **UI4ContextMenu** | ❌（非控件） | — | — |

### 5.2 未实现主题响应的控件分析

**UI4ListView**：
- 不实现 `IThemeAware`，不调用 `TrackControl`
- `ItemBackground` 默认 `White`，`ItemBorderBrush` 默认 `Argb(60,120,140,200)` — Dark 模式下不协调
- 使用 XAML 字符串解析的 ScrollViewer 样式（第 380-593 行），内含硬编码颜色 `#50000000`

**UI4Tab**：
- 不实现 `IThemeAware`
- `HeaderBackground` 默认 `Argb(10,0,0,0)` — Dark 模式下太浅
- `TabSelectedBackground` 默认 `Colors.White` — Dark 模式下刺眼
- `TabForeground` 默认 `Argb(200,0,0,0)` — Dark 模式下不可见
- 所有颜色都是面向 Light 模式的硬编码值

---

## 六、命名规范审计

### 6.1 属性命名不一致

| 属性概念 | 不同命名 | 出现位置 |
|---------|---------|---------|
| 边框颜色 | `BorderNormalColor` / `ItemBorderBrush` | TextBox, CheckBox, ListBox vs ListView |
| 表面背景 | `EditBackground` / `SurfaceColor` / `PanelBackground` | TextBox/ComboBox vs Theme vs ListBox/TextBlock |
| 文字颜色 | `TextColor` / `TextForegroundColor` | TextBox/CheckBox/Radio vs Theme |
| 悬停背景 | `HoverBackground` / `HoverOverlayColor` / `RowHoverBackground` | Button/ListBox vs Theme vs DataGrid |
| 选中背景 | `PressedBackground` / `SelectedOverlayColor` / `RowSelectedBackground` | ListBox vs Theme vs DataGrid |

### 6.2 类型不一致

同一概念在不同控件中使用不同类型：

| 属性 | UI4Button | UI4TextBox | UI4CheckBox | UI4DataGrid |
|------|-----------|-----------|-------------|-------------|
| 背景 | `Brush` | `Brush` | `Color` | `Brush` |
| 边框 | `Brush`（HoverBorderBrush） | `Color`（BorderNormalColor） | `Color` | `Brush` |
| 文字 | — | `Color`（TextColor） | `Color`（TextColor） | `Brush`（HeaderForeground） |

**建议**：统一为 `Brush` 类型（与 WPF 原生控件一致），或统一为 `Color` 类型。

---

## 七、架构完整性评分

| 维度 | 评分 | 关键发现 |
|------|------|---------|
| **主题系统** | ★★★★☆ | 核心设计正确，冻结 Brush 覆盖不全 |
| **样式构建** | ★★★☆☆ | 全量重建模式导致对象风暴，Binding 无法利用 WPF 缓存 |
| **DP 设计** | ★★★☆☆ | 默认值硬编码与 Dark 模式脱节，`new` 隐藏基类属性 |
| **代码复用** | ★★☆☆☆ | 滚动条淡入淡出、右键菜单、ColorToBrushConverter 多处重复 |
| **命名一致性** | ★★★☆☆ | 回调名、属性名、类型在不同控件间不统一 |
| **主题覆盖** | ★★★★☆ | 14/17 控件实现 IThemeAware，UI4ListView/UI4Tab 缺失 |
| **内存管理** | ★★★★★ | 弱引用追踪 + Unloaded 清理，设计正确 |
| **安全性** | ★★★★☆ | PasswordBox 明文存储已标注风险，Unloaded 时清除密码 |
| **可扩展性** | ★★★☆☆ | IThemeAware 为 internal，外部无法扩展主题感知 |

---

## 八、优先改进建议

| 优先级 | 改进项 | 影响范围 | 工作量 |
|--------|--------|---------|--------|
| **P0** | UI4ListView/UI4Tab 实现 IThemeAware | 2 个控件 | 小 |
| **P0** | DP 默认值在构造函数中初始化为主题令牌 | 全部控件 | 中 |
| **P1** | 提取共享 ScrollBarFadeHelper | 3 个文件 | 小 |
| **P1** | 悬浮缩放 + 阴影在 `UI4ListView` / `UI4GridView` / `UI4Panel` 三处逐字复制；缩放余量契约（`FitHoverScale` / `HoverMaxGrow` / `EdgeReserve`）已在前两处同步，**改第三处时必须同步**，否则不越界保证会破 | 3 个文件 | 中 |
| **P1** | 提取共享 ColorToBrushConverter / BoolToVisibilityConverter | 5 个文件 | 小 |
| **P1** | 统一回调命名为 `OnStyleRefresh` | 3 个文件 | 小 |
| **P2** | 补全冻结 Brush（至少覆盖常用 10 个） | UI4Theme | 小 |
| **P2** | 统一属性类型（Color vs Brush） | 跨控件 | 中 |
| **P3** | 将 IThemeAware 改为 public | 1 个文件 | 极小 |
| **P3** | UI4TextBlock 消除 `new` 隐藏 | 1 个文件 | 小 |

---

## 九、3.0.0 主题机制重构评审（2026-10-02）

> **原始提问**：「我重构了这里的主题机制，分析重构的方案优点与不足之处」
> **本节即答案**。评审对象 = `src/StartUI4Controls`（`FileVersion` 3.0.0）；
> **对照组** = 仓内仍存的 2.0.0 机制源码副本 `samples/Prompt收藏夹（示例项目）/lib`（被宿主 `PromptFavorites.csproj:50` 直接引用）。
> 所有带「实测」字样的数字来自一次性探针（§9.6 复现方式），非推算。

### 9.1 差量（可逐条核对）

| 项 | 2.0.0 | 3.0.0 |
|---|---|---|
| `UI4Theme.cs` | 732 行 | 468 行 |
| `UI4ThemeToken.cs` | 40 行 / 30 令牌 | 59 行 / 38 令牌 |
| 主题接线点 | `ThemeSync.Apply` 17 处 + 9 个控件类实现 `internal IThemeAware`（各自 `OnThemeChanged` 里重建整份 Style） | `SetResourceReference` **89 处 / 26 个文件**，接口与弱引用追踪整体删除 |
| 令牌→宿主 | 逐键平铺写进 `Application.Resources` | 每个主题键**一份共享 `ResourceDictionary`**，整份挂进 `MergedDictionaries`（`UI4Theme.cs:283-312`） |
| `ThemeChanged` 职责 | 驱动全部控件刷新 | 只剩代号自增 + DWM 标题栏 / AvalonEdit 两个命令式消费者（`UI4Theme.cs:434-451`） |
| 库总行数 | 15,348 | 14,976 |

新增令牌 8 个：`OnAccent` `Shadow` `ScrollBarThumb` `Separator` `BorderWeak` `TextMuted` `BackgroundGradientStart/End`。

### 9.2 优点

1. **主题覆盖从「按控件争取」变成「按令牌铺满」，量级差 17 → 89。**
   `UI4TextBox`(8) `UI4PasswordBox`(9) `UI4TabControl`(7) `UI4CheckBox`(5) 以及 2.0.0 里**完全没有主题代码**的
   `UI4ListView`/`UI4GridView`/`UI4FlipTextBlock`/`UI4Pivot`/`UI4Slider`/`UI4ProgressBar`/`UI4ProgressRing`/`UI4Radio`/`UI4Switch`/`UI4TextBlock`/`UI4Grid`/`UI4CodeEditor`
   现在全部跟随。**`UI4Button` 不再是恒蓝紫渐变**（实测 dark 下解析到 `#0099FF`，2.0.0 对照组三主题都停在 `#0078D4`）——
   这一条是 §八 P0「UI4ListView/UI4Tab 实现 IThemeAware」的另一种完成方式。
2. **写一次、全域跟随：共享字典实例是真的共享。** 实测全局 `Application.Resources` 与 `dark` 作用域拿到的是**同一个实例**
   （`0x029c56d3 == 0x029c56d3`）；`SetAccent(magenta)` 原地写一次，全局与**同键**作用域同时变 magenta，
   而**异键**作用域保持自身 `#C81E1E` 不被污染（`UI4Theme.cs:416-432` + `UI4ThemeScope.cs:80-81`）。
   旧机制要做到这点得逐字典/逐控件同步。
3. **切换更快，且不随历史控件数退化。** 实测 300 个 `UI4Button` light→dark：新 78 ms vs 旧 147 ms。
   旧路径在 `ThemeChanged` 里遍历弱引用追踪表（含 `RemoveAll` 压缩死条目）再逐个重建；新路径把摊派交给 WPF 自己的资源失效管道。
4. **消灭一整类生命周期缺陷**：`_trackedControls` + `WeakReference` + `_trackLock` + `Unloaded` 清理全部不存在了，
   控件不再被主题静态持有，也不存在「忘记 Untrack」的泄漏面。
5. **语义与 WPF 原生一致**：作用域=资源链就近优先，宿主用标准 `DynamicResource` 语义即可覆盖，
   不必理解库内部「谁在什么时候被重建」的时序。同键重复 `SetTheme` 实测 0 次重建（幂等）。
6. **键名可审计，且当前干净。** 探针 T0 扫全库 33 个去重引用键 / 89 处引用（含模板内 `DynamicResourceExtension`）：**0 个拼错**。
   WPF 里资源键拼错是静默回落默认值，这类 bug 以前无法批量发现，现在一条 grep 即可回归。

### 9.3 不足（按影响排序）

**A0-1 公共 API 破坏且无迁移路径 —— 本仓唯一的示例宿主已编译不过。**
`UI4Theme` 上的 30 个 `*Color` / 12 个 `*Brush` 实例属性被删除，实测
`dotnet build samples/StartUI4Demo` 报 3 处 `error CS1061`：`MainWindow.xaml.cs:102`（`BorderNormalColor`）、
`:103`（`HoverOverlayColor`）、`:711`（`AccentColor`）。替代写法是 `UI4Theme.Current.ColorOf(UI4ThemeToken.X)`，
但这句话没有落在任何文档里；版本号 2.0.0→3.0.0 是对的，缺的是 §9.4 的迁移条目。

**A0-2 对「当前已生效的键」重新 `Register` 会分脑（实测）—— ✅ 2026-10-02 已修，见 §9.7。**
`Register` 清了 `_instances`/`_sharedResources`（`UI4Theme.cs:156-162`），但 `ApplyResolved` 见 `key == _resolvedKey` 且 `_current != null`
就不重建 `Current`（`:170-183`）。实测 `Register(新 light)` + `Apply("light")` 后：

| 消费路径 | 拿到的 Accent |
|---|---|
| 控件资源引用 / 宿主 `DynamicResource` | `#00FF00`（新字典） |
| `UI4Theme.Current.ColorOf(Accent)`（DWM 标题栏、编号角标等命令式路径） | `#C81E1E`（旧实例） |

且此路径不触发 `ThemeChanged` → 标题栏不会重染。**内容与标题栏可以长期不一致。**
`Register` 必须连带让 `_current` 失效（或直接走一次强制 `ApplyResolved`）。

**A0-3 `Register` 泄漏旧字典，作用域还会继续用旧色（实测）—— ✅ 2026-10-02 已修，见 §9.7。**
摘除循环遍历 `_sharedResources.Values`（`:309-310`），而 `Register` 恰好把旧条目从这张表里删了 → 旧实例**永远摘不下来**。
实测 8 轮 `Register`+`SetTheme` 后 `Application.Resources.MergedDictionaries` 由 2 涨到 6，其中含令牌键的字典 6 份
（同一时刻只有最后加的生效，前面靠查找优先级遮蔽）。`ScopeEntry` 里存的也是旧实例引用（`UI4ThemeScope.cs:42-54,70`），
重注册后作用域子树**继续显示旧主题**。运行时可换主题的宿主（正是上一轮做过的三套配色场景）会稳定命中。

**A1-4 「声明式」只到 DP 为止，`Style` 仍整份重建，且一次切换重建 6~8 次（实测）。**
`UI4Button` 4 个被引用的 DP 各自挂着 `OnStyleRefresh` → `btn.Style = btn.BuildPrimaryStyle()`（`UI4Button.cs:106-129`）。
实测 light→dark **6 次重建 / 6 个新 `Style` + 新 `ControlTemplate` 实例**，dark→highcontrast **8 次**；
`SetAccent` 在 300 个按钮上耗时 277 ms（2.0.0 对照 174 ms —— 因为 `WriteTokens` 一次重写 38×2 个键、
并新建 41 个冻结 brush，每个键改动都通知 sink 一遍）。
中间态还**混色**：6 次里有 2 次是「`GradientStart` 已 dark `#0099FF`、`HoverBackground` 仍 light `#0066B5`」。
> 附带发现：换字典是 `Remove(旧)` 再 `Add(新)`，中间存在引用解析不到的空窗，实测期间 DP 短暂回落到
> **硬编码元数据默认值 `#0078D4`**（dark→highcontrast 的第 1、2 次重建）。—— ✅ 2026-10-02 已改为原位替换，见 §9.7。
> 是否会被画到屏幕上本挡**证不住**：T9 只捕获到 2 个渲染帧且均为终态；但 300 控件档切换 78 ms ≈ 5 个 60Hz 帧，窗口期不短。
> 一行改法：`MergedDictionaries[idx] = dict`（索引赋值），或先 `Add` 后 `Remove`。

**A1-5 宿主本地赋值 = 该属性退订主题 —— 2026-10-02 用户判定为「预期行为」，不算缺陷。**
`SetResourceReference` 占的是本地值槽：`SetValue(GradientStart, #090909)` 后切主题，实测 `GradientStart` 保本地 `#090909`、
`GradientEnd` 跟随主题 `#6428C8` —— 同一按钮两半不同步是「合法」的。XAML 里写 `GradientStart="…"` 同样静默断开跟随。
原话：「你是指用户指定的颜色不随主题变化？**这是正常的**」——本地值压过 Style Setter 是 WPF 的优先级规则，
"我指定的颜色不该被主题改掉"是意图而非事故。所以**不加**兼容层、不加 `[Obsolete]` 转发属性、不加 `ReattachToTheme()` 之类的恢复 API。
仍然成立的两条边界（是"说清楚"，不是"要修"）：① 赋过值之后 `ClearValue` 实测回落到 **`#0078D4`（元数据默认）而不是主题色**；
② 常态 `Foreground` 是算出来的 Style Setter、不是资源引用，覆盖它干净可逆；禁用态前景写在模板里，宿主 `Foreground` 盖不住。
（2.0.0 的 `ThemeSync` 用 `_applied` 记账区分"这个值是不是主题写的"，该能力随重构消失——按上面的判定，不再需要补回来。）

**A1-6 同族硬编码残留仍在，且这类残留对 grep 免疫。**
`UI4ListBox.cs:73`、`:162`、`:207` 的 `Color.FromRgb (37, 99, 235)` —— 注意 `FromRgb` 与 `(` 之间**有一个空格**，
用 `FromRgb(` 扫描会整批漏检。实测 dark 与 highcontrast 下：`PressedBackground`/`NumberCircleBackground` 恒 `#2563EB`，
`HoverForeground` 恒 `FromArgb(220,0,0,0)` 半透黑 → 深色主题下选中项黑字压黑底（`:299`、`:418` 在重建时把值快照进 Trigger setter）。
说明：2.0.0 同样没接这三个 DP（`ThemeSync.Apply` 只覆盖 4 个），**不是回归**；但重构既然宣称单通道，
缺口的性质就从「接口没实现」变成「引用没挂」，问题本体没消。宿主侧可用
`PressedBackground="{DynamicResource UI4.Color.ListSelected}"` 自救（收藏夹示例即如此）。
另有 7 个令牌在库内 **0 消费者**（`BorderWeak` `GridLine` `HeaderBackground` `HeaderForeground` `ListSelected`
`RowHoverBackground` `ScrollBarThumb` `Separator`），只对外壳层有意义 —— 不是错，但它们不会随库自身观感变化而被测到。

**A2-7 死代码与陈旧文档（进 IntelliSense 的那类）。**
`Internal/ThemeSync.cs` 30 行、全库 0 引用。XML 摘要三处与实际机制矛盾：`UI4Theme.cs:29-32` 仍写「弱引用控件追踪」、
`:94` 仍写「在全部已追踪控件刷新完成后触发」；`UI4ThemeScope.cs:14-19` 仍写「② 仍走命令式刷新的控件…在子树刷新时被同步换入」——
这条通道已随重构删除。根 `README.md` §十也仍按 2.0.0 描述（本次已就地更正，见 §9.5 末行）。

**A2-8 广播与字典换入的先后顺序两处不一致。**
`ApplyResolved`：先 `NotifyThemeChanged()` 后 `WriteToApplicationResources()`（`:180-182`）；
`SetAccent`：先写字典、再广播、再换入（`:428-431`）。命令式消费者拿到的 `ThemeVersion` 与引用侧生效时刻没有先后保证，
叠上 A0-2 就会放大成「标题栏与内容不一致」。统一成「先备好资源、最后广播」即可。

**A2-9 副本分叉（2026-10-02 本轮已全部收敛到 3.0.0）。**

| 副本 | 同步前 | 本轮做了什么 | 编译 |
|---|---|---|---|
| `C:\…\UI_10\StartUI4.WPF`（工作区） | 3.0.0，Demo 编译不过 | 修 A0-2/A0-3/空窗；Demo 迁 3 处 API + 去掉菜单显式赋色 | 库 ✅ Demo ✅，运行时换肤 D1-D6 全 PASS |
| `E:\Qoder灵感项目\UI_10\StartUI4.WPF` | 2.0.0（重构前快照） | 覆盖 30 个文件（28 src + Demo + 根 README），原件备份 | 库 ✅ Demo ✅（3.0.0） |
| `E:\Qoder灵感项目\StartUI4.WPF` | 2.0.0，文档最全 | 同上 30 个文件；`Agent手册` 12 篇 + `主题方案…md` 等加「机制更正」头 | 库 ✅ Demo ✅（3.0.0） |
| `E:\Qoder灵感项目\StartUI4.WPF_net48` | 1.0.20 追踪式 | 三方判定后覆盖 24 个 .cs（转 LF、逐文件保留 BOM）；`UI4Menu.cs`/`ScrollBarResources.cs` 手工合并（保住 net48 自己的差异）；独有的 `UI4DataGrid.cs` 迁 8 处旧 API 并去掉 `IThemeAware`；Demo 同步迁移 | 库 ✅ Demo ✅ |

两处**没动、需要你知道**的遗留差异：① net48 的 `UI4Menu.InitStyles` 失败分支仍是上游的 `MessageBox.Show`，而 net10 侧早已改成「落盘 + 抛出」（模态框会卡自动化回归）——本轮按「只同步主题机制」的口径没顺手带过去；
② 各副本里 `samples/Prompt收藏夹（示例项目）/lib/` 仍是 2.0.0 的库源码副本，且被 `PromptFavorites.csproj:50` 直接引用，所以**收藏夹示例跑的还不是新机制**（你此前定过口径：只改项目代码、不动 `lib/`）。
（另注：`.sln` 里 `tools/Net10Regression`、`tools/clipboard-lock-check` 两个项目只存在于 `E:\Qoder灵感项目\StartUI4.WPF`，工作区与 E-UI_10 里整解决方案构建会先被 MSB3202 挡住，与代码无关。）

两件事要认账：① 重构只落在本工作区，`Agent手册` 那几章讲的生效时序在 3.0.0 里已经不成立，
随副本同步时会把旧机制再教给下一个人；② 宿主 `PromptFavorites.csproj:50` 引用的是本仓 2.0.0 的
`../lib/StartUI4Controls.csproj`，`StartUI4Demo` 又编译不过（A0-1）—— **改完主题机制后没有任何一个界面能跑到它**，
本轮全部数字只能靠探针自证。建议：把 `src` 同步到两份兄弟副本 + `lib`，并把 Demo 的 3 处 API 迁到 `ColorOf`。

### 9.4 若要收口，建议的最小顺序

| 步 | 做什么 | 为什么是这一步 | 状态 |
|---|---|---|---|
| 1 | `Register` 连带失效 `_current` 并强制走一次完整 apply；摘除旧字典改为「按实例集合」而非「按 `_sharedResources` 表」 | 消 A0-2 + A0-3，两处各 <10 行，且是宿主换肤必踩 | ✅ 2026-10-02（§9.7） |
| 2 | 换字典改 `MergedDictionaries[idx] = dict` | 消 A1-4 的空窗回落，一行 | ✅ 2026-10-02（§9.7） |
| 3 | 每个控件的 `OnStyleRefresh` 合并（标志位 + `Dispatcher.BeginInvoke(Render)` 一次重建），或让模板用 `TemplateBinding`/动态 brush 而非每次 `new` | A1-4 的 6~8 倍重建与混色中间态；`UI4ListBox.cs:259-260` 这类 `new SolidColorBrush(...)` 快照一并消 | ⬜ 未做（重建次数已因第 2 步降到 4） |
| 4 | 补 `UI4ListBox` 三个 DP 的引用；扫一遍 `FromRgb (`/`FromArgb (` 变体 | A1-6，含其 grep 免疫性 | ⬜ 未做 |
| 5 | 文档：根 `README.md` §十 按单通道改写 + 增「2.x → 3.0.0 迁移」表；删 `ThemeSync.cs`；改三处 XML 摘要 | A0-1、A2-7 是让下一个人不再踩的前提 | ◐ 根 README §十 与三处 XML 摘要已改；`ThemeSync.cs` 未删；迁移表以 §9.7 + Demo 实改代替 |

### 9.5 对本文旧结论的更正

| 位置 | 旧结论 | 3.0.0 实际 |
|---|---|---|
| §一 1.1 表 | 「弱引用追踪 ✅ 正确」「`lock (_trackLock)` 线程安全 ✅」 | 追踪机制已整体删除，两行评估作废 |
| §一 1.2 | 「冻结 Brush 缺 17 个，控件被迫 `new SolidColorBrush`」 | 字典侧每令牌都同时给 `UI4.Color.X`/`UI4.Brush.X`；**但** `Style` 重建路径仍在 `new`（`UI4ListBox.cs:259-260`、`:299`、`:418`），且 `SetAccent` 一次新建 41 个 brush |
| §一 1.3 | 「`IThemeAware` 为 internal，外部无法扩展」 | 接口已删除；外部扩展主题的新方式是注册 `UI4ThemeDefinition` + 挂资源引用 |
| §七 表「主题覆盖」 | ★★★★☆ 14/17 控件实现 IThemeAware | 26/26 文件走引用（89 处），评分维度需重写 |
| §七 表「内存管理」 | ★★★★★ 弱引用追踪 + Unloaded 清理 | 追踪没了；但新增 A0-3 的字典堆积面，评分依据要换成该泄漏 |
| §七 表「可扩展性」 | ★★★☆☆ IThemeAware internal | 应改为「公共 API 一次性删除、无迁移路径」（A0-1） |
| §八 P0/P3 | 「UI4ListView/UI4Tab 实现 IThemeAware」「将 IThemeAware 改为 public」 | 前者已以引用方式完成、后者对象不存在 |
| §〇 第 23-24 行 | 「单通道声明式见根目录 `README.md` 第十节」 | 根 README §十当时**仍在描述旧的两通道**，现已就地更正（见根 §十 1./2./6. 小节） |

### 9.6 探针复现（工作区外临时目录，不入仓）

```bash
# 新机制（src，3.0.0）
cd "C:/Users/webtu/Desktop/UI_10/_bak_prompt/refactor_probe" && dotnet build probe.csproj && dotnet run --no-build
# 对照组（lib，2.0.0 源码副本）
cd "C:/Users/webtu/Desktop/UI_10/_bak_prompt/old_control" && dotnet build old.csproj && dotnet run --no-build
# 覆盖率差量 / 令牌消费者审计
bash "C:/Users/webtu/Desktop/UI_10/_bak_prompt/coverage.sh"
bash "C:/Users/webtu/Desktop/UI_10/_bak_prompt/sweep.sh"
```

探针用 `DependencyPropertyDescriptor` 监听 `Style` 属性变化来数重建次数，用 `CompositionTarget.Rendering` 读实际画笔；
窗口一律 `Left=-4000` 离屏、`ShowActivated=false`，不抢焦点、不弹窗。
**探针局限**：① 离屏窗口的合成节拍不受控，A1-4 的「撕裂是否可见」未证成也未证伪；
② 同一进程内测试相互污染主题定义（T3/T4/T6 依次改写 light/dark 的 Accent），所以 T9 的「切换前」底色是 `#0A14AA` 而非出厂 `#0078D4`——
读表时以「同一次切换的前后差」为准；③ 耗时为单次量级参考，未做多轮中位数。

### 9.7 2026-10-02 收口落地：A0-2 / A0-3 / 换字典空窗

改了 4 处，全在 `UI4Theme.cs` + `UI4ThemeScope.cs`：

| 位置 | 改动 | 消掉的是 |
|---|---|---|
| `UI4Theme.cs:164-181` `Register` | 覆盖的键 == 当前生效键时置空 `_current` 并强制走一次完整 `ApplyResolved`；覆盖非当前键时调 `UI4ThemeScope.RebindIfScoped` | A0-2 分脑；非当前键的作用域滞留旧字典 |
| `UI4Theme.cs:318-360` `WriteToApplicationResources` | 新增 `_installedResources` 账本，摘除只认账本不认 `_sharedResources`；换入改为 `merged[index] = dict` 原位替换 | A0-3 字典堆积；A1-4 的解析空窗 |
| `UI4Theme.cs:307-316` `SharedResourcesFor` | 新建字典后回调 `UI4ThemeScope.ReplaceSharedDictionary` | 作用域握着被作废实例、撤销时摘错 |
| `UI4ThemeScope.cs:88-133` | 新增 `ReplaceSharedDictionary` / `RebindIfScoped`（整窗作用域顺带重染标题栏） | 同上 |

顺带把三条讲旧机制的 XML 摘要改对了（`UI4Theme` 类摘要、`ThemeChanged`/`SetTheme` 摘要、`UI4ThemeScope` 的「两条通道」段），
并修掉 `SetAccent` 摘要里指向已删属性的 `cref`（编译期 CS1574 警告）。

**修复前后（同一探针、同一台机器）**

| 断言 | 修前 | 修后 |
|---|---|---|
| T5 同键 `Register`+`Apply` 后 `Current.Accent` vs 控件解析值 | `#C81E1E` vs `#00FF00` ❌ | 两边都 `#00FF00` ✅ |
| T6 8 轮 `Register`+切换后 App 内含令牌字典份数 | 6 ❌ | 1 ✅ |
| T2 light→dark 的 `Style` 重建次数 | 6 ❌ | 4（仍非 1，见 A1-4） |
| T2 dark→highcontrast 重建次数 / 是否读到元数据默认色 | 8 次，中间态读到 `#0078D4` ❌ | 4 次，**全程不再出现默认色回落** ✅ |
| T10 覆盖非当前键：作用域字典换实例、`ThemeChanged` | 旧字典滞留 ❌ | 换实例 ✅、次数 0 ✅ |
| T10 覆盖当前键：`ThemeChanged` 次数 | 0（标题栏不重染）❌ | 1 ✅ |
| T4 `SetAccent` 传播、T0 键名审计、T3 耗时 | — | 无回归（75 ms / 268 ms，与修前同量级） |

**验收宿主 = `samples/StartUI4Demo`（带运行时换肤）**：先把它从 2.0.0 API 迁过来（3 处 `CS1061`：
`MainWindow.xaml.cs` 的 `BorderNormalColor`/`HoverOverlayColor` → 改 `UI4Theme.Current.ColorOf(令牌)`；
`AccentColor` → `ColorOf(Accent)`），并**删掉宿主右键菜单的 `BorderColor`/`HoverBackground` 显式赋色**——
`UI4ContextMenu` 用「有没有被显式赋值」决定要不要把颜色下推给内部列表，赋了值就等于当场退订主题（A1-5 在真实宿主里的一次现形）。
`demo-theme-shot.ps1` 结果（色像素计数取证，见下）：

| 挡 | 断言 | 结果 |
|---|---|---|
| D1 | 浅色页平均亮度 >0.80、白底占比 | PASS（0.925 / 83.2%，1280×820） |
| D2 | 点「强调色·橙」→ 橙色像素 0→2037、海蓝 2629→0 | PASS |
| D3 | 点「恢复内置主题」= `Register(当前键 light)` → 橙 2037→0、海蓝 0→2629 | PASS（修前橙色赖着不走） |
| D4 | 「全局高对比度」→ 平均亮度 <0.25、黑底 62.8%、黄像素 4279 | PASS |
| D5 | 高对比度下 `SetAccent(#0078D4)` → 海蓝 1943、黄 11752→1095 | PASS |
| D6 | 再 `Register(当前键 highcontrast)` → 黄回到 11752、海蓝 68 | PASS |
| D7 | 右键菜单是否跟随主题 | **SKIP**：WPF `Popup` 是独立顶层窗口，`PrintWindow` 抓主窗证不到，留人工 |

**本机取证环境的一条硬事实**：PowerShell 里 `AutomationElement.BoundingRectangle` 对这个 WPF 窗口返回 null
（`$null.GetType()` 抛「不能对 Null 值表达式调用方法」），所以「按元素坐标取样」这条路不通。
改成 ① 窗口几何走 Win32 `GetWindowRect`，② 颜色断言走全帧色像素计数，③ UIA 只负责 `Invoke`/`Select`。
**第一版脚本因此出了过假阳性**：抓帧失败 → 采样 0 点 → 平均亮度算成 0.000 → 「<0.25 所以是深色」蒙对。
现在 `Mean-Lum` 一并返回采样点数，断言里加了 `N -gt 500` 的硬门槛。

**仍未修**（本轮按你的选择没动）：A1-4 的「一次切换重建 4 份 `Style`」、
A1-6 `UI4ListBox` 三个 DP 的 `#2563EB`/半透黑残留、A2-7 的 `Internal/ThemeSync.cs` 死代码、A2-8 广播与换入顺序、A0-1 的公共 API 破坏（Demo 已迁，兼容层按你的取舍不加）。
A1-5 已从本清单移出：用户判定为预期行为，见该条。

### 9.8 2026-10-02 追加：高对比度下按钮白字压黄底（实测 1.07:1 → 19.56:1）

**成因不在调色板，在判据。** `UI4Button.ForegroundFor` 原来按「背景相对亮度 < 0.45 用 `OnAccent`，否则用正文色」二选一。
这条阈值是为「中性灰/禁用态浅底」设计的，但高对比度主题的强调色是**亮黄**（`#FFFF00`，相对亮度 0.928 → 被当成"浅底"），
而该主题的正文色本就是给黑底准备的**白** —— 于是按钮常态变成白字压黄底。`OnAccent` 令牌其实早就配对了（HC = 黑），只是走不到那个分支。

**改法**（`UI4Button.cs:218-243`）：不再用亮度阈值，改成在 `OnAccent` 与正文色之间**取与底色 WCAG 对比度更高的那个**，
并新增 `Contrast()` 辅助。这样三套主题各自最优，禁用态/中性浅底仍归正文色。

| 主题 | 按钮底（渐变两端） | 前景 改前 → 改后 | 最差对比度 改前 → 改后 |
|---|---|---|---|
| Light | `#0078D4`/`#9333EA` | 白 → 白（不变） | 4.53 → 4.53 ✅ |
| Dark | `#0099FF`/`#6428C8` | 白 → 白（不变） | 3.00 → 3.00 ⚠️ 见下 |
| HighContrast | `#FFFF00`/`#FFFF00` | **白 → 黑** | **1.07 → 19.56** ✅（悬停 `#DDDD00` 上黑字 14.42） |

取证：探针 `T11`（`refactor_probe`，逐主题实例化 `UI4Button` 读实际解析值算 WCAG 比值）+ Demo 高对比度截图
（`_bak_prompt\demo_shots\d6-hc-reset-yellow.png`，黄底黑字肉眼可辨）；Demo 的 D1–D6 断言改后仍全 PASS。

**顺带量出、没动的两件事**：
① **Dark 主题按钮 3.00:1** —— 不是这次改动引入的，是出厂 `#0099FF` 承托白字本身就不够（按钮字号 15px SemiBold，按 AA 正文要 ≥4.5）。
两个候选改法：把 Dark 的按钮底色改用 `AccentDark`（`#0078D4`，白字 4.53 ✅，代价是按钮比强调色暗一档），
或新增 `ButtonBackground` 令牌让每套主题自己指定（代价是多一个令牌、四份定义都要填）。**要哪个说一声，本轮没擅自改你的 Dark 调色。**
② `UI4ListBox` 编号角标在 HC 下是黑字压 `#2563EB`（3.4:1）—— 属 A1-6 那族硬编码，改它等于顺手做第 4 步。

### 9.9 2026-10-02 追加：6 套预置业务场景套装（`UI4ThemePacks`）与其中文化展示

> **本节是当时的记录**：套装数随后变成 8（见 9.10），下拉文本也加了（亮/暗）标记。表里的门槛数字仍然有效（那 6 套的色板没再动）。

**动机**：后续业务功能对接时要能直接套用主题套装，而不是每个宿主自己调一遍色。内置三套是「通用底」，
缺贴场景的色板；因此新增 `UI4ThemePacks.cs`（亮 3 + 暗 3，全部手调，未取外部配色素材）。

**API 形状**（键是稳定契约，中文只用于展示）：

| 成员 | 作用 |
|---|---|
| `DataConsole` / `Reading` / `Form` / `OnCall` / `Terminal` / `Showcase` | 6 个英文键常量，可写进 XAML 与 `UI4ThemeScope.Theme` |
| `RegisterAll()` | 一次注册 6 套；幂等（重复调用仍是 9 套，实测见 T13） |
| `DefinitionFor(key)` | 每次新建一份定义，可改令牌后再注册；未知键抛 `ArgumentException` |
| `DisplayName(key)` | 「数据台」；未登记的键（含宿主自定义主题）原样回显，排错看到真键名 |
| `DisplayLabel(key)` | 「数据台 · data-console」，下拉用 |
| `<X>Definition()` ×6 | 公开工厂，改色板只改这里；每套的理由写在方法注释上 |

内置 `UI4ThemeDefinition.Build(...)` 是 private（38 参），套装一律从 `Light()` / `Dark()` 派生后 `.With(令牌, 色)` 覆盖，
所以「未覆盖的令牌继承基线」——实测每套 Apply 后 `Application.Resources` 里 38×2 + 3 别名键齐备（T13 缺键 0）。

**门槛（T12，逐对 WCAG，不达标就回色板改）**：

| 键 | 展示名 | 最低对比度 | 结果 |
|---|---|---|---|
| `data-console` | 数据台 | 3.53:1 | ✅ |
| `reading` | 阅读 | 3.65:1 | ✅ |
| `form` | 录入 | 3.49:1 | ✅ |
| `oncall` | 值守 | 4.25:1 | ✅ |
| `terminal` | 终端 | 4.40:1 | ✅ |
| `showcase` | 展示 | 4.55:1 | ✅ |
| `light` | 浅色·通用 | 1.50:1 | ❌ 占位/面板（既有项，本轮未动） |
| `dark` | 深色·通用 | 3.00:1 | ❌ OnAccent/强调、正文/列表选中 3.84、按钮字/渐变起 3.00（既有项，见 9.8 ①） |
| `highcontrast` | 高对比度 | 11.37:1 | ✅ |

调色过程中被门槛挡回去的三处，记录以免以后又被"顺手改亮"：① 浅三套的 `AccentEnd` 起初 4.13~4.38（白字压不住渐变末端），
压深到 `#2466A3` / `#22736F` / `#157A8C`；② `form` 的 `Placeholder` 起初 2.96，改 `#778C9B`；
③ 滚动条滑块一度按 3:1 判为不达标 —— **该判据用错了**（WCAG 1.4.11 的 3:1 针对"理解内容所必需的图形"，滑块靠位置与光标被识别，
硬凑会得到近黑的重色滚动条；内置三套也都在 1.3 上下），已改为「仅记录、不参与最低值统计」，理由写进探针注释。

**Demo 侧改动**（`samples/StartUI4Demo`）：构造末尾 `UI4ThemePacks.RegisterAll()`；两个下拉（全局 `ThemeKeyCombo`、
作用域 `ScopeKeyCombo`）改为代码填充 `Content = DisplayLabel(key)` / `Tag = key`（作用域多一条 Tag 为空的「（撤销作用域）」，
XAML 里的写死选项已删，新增套装不必再改两处）；页脚与作用域状态行显示中文名。下拉宽度 200→270，避免「数据台 · data-cons…」截断。

**取证**（`_bak_prompt`，工作区外不入仓）：
- 探针 `refactor_probe`：`T12` 九套逐对断言、`T13` 注册契约（9 套顺序 = 内置3+亮3+暗3、下拉文本全含中文、
  未登记键回显、`DefinitionFor` 未知键抛异常、9×38×2+3 键齐备、字典色值与 `UI4Theme.Current` 一致、
  深浅判定与预期相符、重复 `RegisterAll` 幂等）——全绿。
- 运行时像素：`packs-shot.ps1` P1–P5 全 PASS（PrintWindow 抓帧 + 色像素计数，UIA 只用于展开下拉与选中项）。
  P1 下拉 9 条含中文含英文键；P2 数据台蓝 11558 像素 / 内置海蓝 0 / 平均亮度 0.897；P3 值守琥珀 8246 / 亮度 0.138；
  P4 作用域 10 项（1 条无「 · 」＝撤销），卡片选阅读后阅读青 1587 而全局仍值守（琥珀 3321）；P5 撤销后阅读青 15、亮度回到 0.138。
  截图 `_bak_prompt\pack_shots\`（`-Gallery` 另出 9 张 `g-<键>.png`），数字以同目录 `packs-log.txt` 为准（逐次有几像素抖动）。
- 旧 D1–D6（换肤机制回归）在同一次构建后仍全 PASS。

**顺带量到、没动的两件事**：
① 深色套装的 `CurrentMode`/`ResolvedMode` 报 `Light`（`UI4Theme.ModeForKey` 只认三个内置键，属既有设计）。
标题栏不受影响——`UI4WindowTitleBar.IsDark` 按底色亮度判，实测 6 套深浅全对（T13 逐套打印亮度）。
② Demo 左侧那列**原生 `TabItem` 标签条**在任何深色主题下都是白底黑字（含改动前的 `dark`/`highcontrast`，见
`_bak_prompt\demo_shots\d4-highcontrast.png`）——原生控件观感属宿主 XAML 层，与套装无关，本轮未动。

### 9.10 2026-10-02 追加：两套纸质阅读（`paper-white` / `paper-grey`）与展示文本的（亮/暗）标记

> **色板部分已被 9.11 取代**：本节记录的墨褐 / 石墨两套取值只活了一轮，纸白与灰纸现已改按 100-themes 的
> `polaroid/day` 与 `tundra/day` 重调。（亮/暗）标记的契约、探针改造与 Demo 布局改动仍然有效。

**需求两件事**：① 再加 2 套亮色的"纸质阅读"（白色纸质、白灰色纸质）；② 套装的展示文本要带深浅标记，好在一串中文名里区分。

**键怎么定**：给的选择是「阅读族 3 变体（`reading-white`/`reading-grey`）」/「独立场景键 `paper-*`」/「替换现有 `reading`」，
选了**独立场景键**：`PaperWhite = "paper-white"`（纸白）、`PaperGrey = "paper-grey"`（灰纸），`reading`（暖纸）一字不动。
于是套装 6 → 8（亮 5 + 暗 3），`ThemeKeys` 顺序 = 内置 3 + 亮 5 + 暗 3。

**两套的取值思路**（纸质 ≠ 把正文也调灰）：

| 套 | 底 / 面板 | 强调 | 理由 |
|---|---|---|---|
| 纸白 | `#FCFCFA` / `#FFFFFF` | 墨褐 `#6D5738`（端 `#7A6242`） | 最亮一档；底只留一丝暖，强调用墨而不是蓝，让"纸"当主角。禁用底与网格线走暖灰，冷灰在白底上发脏 |
| 灰纸 | `#F1F1EF` / `#FAFAF9` | 石墨灰蓝 `#4E5A63`（端 `#5A6773`） | 底压一档中性浅灰、面板仍留白分层；灰阶刻意不带蓝，避免和「数据台」的冷蓝混成一片 |

门槛（T12）：**纸白最低 4.53:1、灰纸 4.09:1** —— 是 8 套里最高的两名，正文全程近黑（纸白 `#1A1917`、灰纸 `#1F2123`），
纸感全部落在底/面板/边框的色温上。内置 `light`（占位 1.50）与 `dark`（3.00 / 3.84 / 3.00）仍是那 4 处既有不达标，本轮没动。

**（亮/暗）标记的契约**：库里新增 `ShadeLight = "亮"` / `ShadeDark = "暗"` 两个常量与 `ShadeName(key)`；
`DisplayLabel(key)` 变成「中文（深浅） · key」，`DisplayName(key)` 仍是纯中文名（业务 UI 想只要名字时用它）。
- **只加在套装上**：内置三套与宿主自定义键的 `ShadeName` 返回空串，`DisplayLabel` 也就不带括号 ——
  前者中文名自带深浅义，后者的深浅不该由键名承诺。
- 深浅不是另写一张表：`ShadeName` 与实测底色亮度互相校验（探针 T13 逐套打印「标记 亮/暗」与「底色亮度」，
  要求说「暗」的那几套亮度必须 <128，即 `UI4WindowTitleBar.IsDark` 的同一判据）。标错就红。

**探针顺带修了一处会腐化的地方**：T12 的键集合改为从 `UI4ThemePacks.Keys` 现取（原来是硬写 9 个），
以后再加套装不必回来改探针；T12/T13 的套数文案也改成按实际数打印。

**Demo 侧**：下拉宽度 270→300（「数据台（亮） · data-console」在 270 上会被省略号截断）；
「局部主题」页那一行由 `StackPanel(Horizontal)` 改 `WrapPanel` —— 300 宽的下拉加三个按钮会顶破 `MaxWidth=760` 被裁掉，
换 WrapPanel 后放不下就自己折行。两处说明文案同步改成「中文名（亮/暗） · 英文键」。

**取证**：
- T12：11 套逐对断言，8 套全绿，仅内置 `light`/`dark` 的 4 处既有不达标。
- T13：`ThemeKeys` 11 套顺序 ✅；下拉文本 11 条全含中文、套装 8 条带（亮/暗）＝亮 5 + 暗 3、内置 3 条不带、`ocean` 原样回显；
  11×38×2+3 键齐备、字典色值与 `UI4Theme.Current` 一致、标记与实测深浅一致、重复 `RegisterAll` 幂等（仍 11 套）。
- `packs-shot.ps1` P1–P7 全 PASS：P1 项数 11 / 带标记 8（亮 5 暗 3）；P6 纸白墨褐 11392 像素、平均亮度 0.912；
  P7 灰纸石墨 11457 像素、平均亮度 0.878（比纸白暗 0.034，断言要求 >0.03）；P4 作用域下拉 12 项（11 套 + 撤销）。
  截图 `_bak_prompt\pack_shots\p6-paper-white.png`、`p7-paper-grey.png`。

### 9.11 2026-10-02 再追加：纸白 / 灰纸改按 100-themes 的 day 档重调（含一处探针假失败的更正）

**来源**：github.com/MagicFollower/100-themes（Omarchy 配色集，每套 5 变体，`day` 档 = 同色相浅底版）。
抓的是 `<主题>/day/colors.toml`，字段 `background / dark_background / lighter_background / darker_background`、
`foreground / light_foreground / dark_foreground / bright_foreground`、`accent / selection / muted`。
**注意它的明度阶梯命名是反的**：`day` 档里 `dark_background`、`lighter_background`、`darker_background` **全都比 `background` 更暗**
（命名沿用 dark 档逻辑），别按字面当"更浅的面板色"用。

**两套的映射**（按层级搬，不是抄三个值）：

| 令牌 | 纸白 ← `polaroid/day` | 灰纸 ← `tundra/day` |
|---|---|---|
| `Background` | `#F7F2EF`（暖白纸） | `#EFF5F7`（冷灰纸） |
| `HeaderBackground` | `#EBE7E3` = `dark_background` | `#E3E9EB` = `dark_background` |
| `PanelBorder` / `Separator` | `#E4DEDA` = `lighter_background` | `#DAE1E4` = `lighter_background` |
| `BorderNormal` | `#DEDAD6` = `darker_background` | `#D6DCDE` = `darker_background` |
| `ListSelected` / `RowSelectedBackground` | `#E4D0D9` = `selection` | `#D8D7E6` = `selection` |
| 四级文本 | `#2E241D` / `#4F463E` / `#6B615A`（由 `dark_foreground` 向正文混） / 表头 `#1A120C` | `#1C292D` / `#3E4A4E` / `#5C686C` / 表头 `#0B1619` |
| `Accent` | `#A1568C`（原值直接用，白字 4.98 ✅） | `#876BAB` → **压深到 `#7A5F9C`**（原值白字 4.43 不过线） |

三条搬过来的结构，比换色相更影响观感：
① **专用 `selection` 选中底**：不再拿强调色当选中底（选中一大片紫太扎眼），改用淡彩，正文深色字压上去 10.5:1；
② **四级文本梯度**：表头用 `bright_foreground`（比正文更黑一档），次级/弱化分别落到 `light_foreground` 与混过的 `dark_foreground`；
③ **灰阶阶梯**：表头、面板边框、字段边框各取 `dark_` / `lighter_` / `darker_background` 一档，层次靠色阶而不是靠描边加粗。
**唯一没照搬**："面板比页面更灰"——本库 `Surface` 同时是输入框与列表的底色（`UI4TextBox.cs:177`、`UI4ComboBox.cs:199`、
`UI4ListBox.cs:245` 都引用 `UI4.Brush.Surface`），压灰会让字段看着像禁用，所以 `Surface` 仍留白（纸白 `#FCFAF8`、灰纸 `#FAFCFD`），
灰阶只落在表头/行悬浮/网格线/边框上。

**门槛（T12 复测）**：纸白最低 **3.59:1**、灰纸 **3.64:1**（地板都是 占位/面板），其余 6 套数字不变。

**过程中抓到一处探针自身的错（假失败，不是配色问题）**：`BetterOf(a, b)` 原实现是 `Rel(a) >= Rel(b) ? a : b`，
即"谁亮谁上"，被用在「正文/列表选中」这一对上。浅底 + 淡彩选中色时它会挑白字，于是纸白报 1.47:1、灰纸报 1.42:1 ❌；
而真正的深色正文字在那块底上是 10.5:1。已改成按**与该底的对比度**挑（与 `UI4Button.ForegroundFor` 同一判据）。
这条对旧 6 套无影响（它们的 `ListSelected` 是深色强调色，两种判据同解），`dark` 的 3.84:1 也仍是 3.84:1 —— 不是靠改判据把失败改没的。

**运行时取证（`packs-shot.ps1` P1–P7 全 PASS）**：两套底色亮度都是 243、平均亮度 0.887 / 0.888 —— **亮度分不出这两套**，
所以判据换成整页 R-B 暖冷偏差：纸白 **+7.86**（暖）、灰纸 **−9.01**（冷），相差 16.86（门槛 >10）。
P7 起初按"灰纸应比纸白更暗"断言而 FAIL —— 那是我把预期写错了（两套刻意同亮度、只差色温），改判据而不是改色板，
理由写进脚本注释。强调色像素：纸白 11880、灰纸 11970。



