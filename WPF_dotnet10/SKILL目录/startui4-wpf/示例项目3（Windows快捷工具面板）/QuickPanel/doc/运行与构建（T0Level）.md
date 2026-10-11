# QuickPanel 零基础上手（T0Level）

> 项目全景（创建目的、技术栈、架构、代码地图、组件架构、数据流、工作流程、工作原理）见仓库根 [`../README.md`](../README.md)；**本文只管"怎么跑起来、每条命令的判据是什么"**。

- 工程根：`C:\Users\webtu\Desktop\AAA\QuickPanel`
- 本次填写人／日期：Qoder 代理（本次新建工程的一轮）／2026-10-04
- 实测环境：Windows 11 专业工作站版 `10.0.26300`（`DisplayVersion=26H2`）+ .NET SDK `10.0.401` + `Microsoft.WindowsDesktop.App 10.0.12`；DPI 125%；单机口径
- 本次填写的四挡决策（**有意决策，不是遗漏**）：明暗策略 `both`（亮 + 暗，出厂跟随系统）／分发口径两档都出／主题只用内置明暗档（不接 `UI4ThemePacks` 8 套业务档、不用 `UI4ThemeScope` 局部换肤）／设置入口为工具条右端齿轮 + 窗口内浮层四节全给

> 本文的规矩：每条命令都在本机跑过，"实测输出"里是原样贴的回显；没跑的写 `未验证` + 原因（见 §7 末"未验证项汇总"）。环境相关数字（耗时、体积、SDK 版本）标清口径，别当常量引用。

---

## 0. 前置要求

| 依赖 | 要求 | 判据命令 | 本机实测 |
|---|---|---|---|
| .NET SDK | 10.x（`net10.0-windows`） | `dotnet --list-sdks` | `10.0.401 [C:\Program Files\dotnet\sdk]` |
| WPF 桌面运行时 | `Microsoft.WindowsDesktop.App` 10.x（框架依赖档必需） | `dotnet --list-runtimes \| Select-String WindowsDesktop` | `Microsoft.WindowsDesktop.App 10.0.12 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]` |
| NuGet 源可达 | lib 唯一第三方依赖 `AvalonEdit 6.3.1.120` 要能 restore | `dotnet build app\QuickPanel.csproj` 首行无 NU 错误 | 通过（首次构建 5.67 s，见 §1；离线机器要先在线 restore 一次） |
| Windows 版本 | Win10 或 Win11；Win7/8.1 不支持（net10 的 OS 底线） | `systeminfo \| Select-String 'OS Name\|OS Version'` | `OS Name: Microsoft Windows 11 专业工作站版` / `OS Version: 10.0.26300 N/A Build 26300` |

取系统版本时的一条实测坑：注册表 `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` 的 `ProductName` 在这台 Win11 26300 上仍写 `Windows 10 Pro for Workstations`，`ReleaseId` 还是 `2009`——**别拿 `ProductName` 判代次**，要判就取 `CurrentBuild` / `DisplayVersion`。

不该进版本控制的：`app\bin\`、`app\obj\`、`lib\bin\`、`lib\obj\`、`publish\`、`publish_no_runtime\`、`%APPDATA%\QuickPanel\`（运行期设置与 selftest 报告）。

`lib/` 是组件库源码整份拷贝（48 个文件，`StartUI4Controls` v3.0.0）。**本工程改过它的 3 个文件**——不是随手改，是三条会在别的工程同样复现的缺陷，逐条记在 §7 的"lib 偏离清单"，改动的判定依据与回归办法都在那里。改之前请先读那一节：本工程的 `lib/` 已经和 Skill 种子（`assets/seed/lib`）有偏离，用 `fetch-source.ps1` 新建的其它工程**不带**这三条修复。

---

## 1. 项目启动（首次构建）

    dotnet build app\QuickPanel.csproj

判据：末尾 `已成功生成` + `0 个错误`；警告**固定 9 条**（`CS0414` 未使用字段 ×5、`CS1574` XML 注释 cref 解析不到 ×4），都来自 `lib/` 上游代码。

- 别去"修"那 9 条：修完 `lib/` 就与上游不可比（§7）。
- 警告数**多于或少于** 9 条 → 先看 `lib/` 是不是被改过或文件不全，再怀疑工具链。
- 增量构建会出现 `0 个警告`（只重编 app、没重编 lib），别把它当"警告消失了"；要数警告就 `dotnet build --no-incremental` 或先 `dotnet clean`。
- `dotnet pack` 缺 `lib/LICENSE.txt` 会报 `NU5019` 并把整条构建链拖红——`lib/` 少文件时的典型表现，恢复办法见 Skill 的 `fetch-source.ps1`。本工程不产 NuGet 包，`dotnet pack` **未验证**。

实测输出（首次构建，`scaffold.ps1` 产出后第一次跑）：

```
已成功生成。

C:\...\QuickPanel\lib\UI4ProgressRing.cs(14,22): warning CS0414: ...
C:\...\QuickPanel\lib\UI4ScrollViewer.cs(26,22): warning CS0414: ...
C:\...\QuickPanel\lib\UI4ProgressRing.cs(230,22): warning CS0414: ...
C:\...\QuickPanel\lib\UI4ScrollViewer.cs(25,22): warning CS0414: ...
C:\...\QuickPanel\lib\UI4ProgressRing.cs(15,22): warning CS0414: ...
C:\...\QuickPanel\lib\UI4ComboBox.cs(26,62): warning CS1574: ...“HoverBorderColor”
C:\...\QuickPanel\lib\UI4ComboBox.cs(26,95): warning CS1574: ...“FocusBorderColor”
C:\...\QuickPanel\lib\UI4MultiLanguage.cs(29,29): warning CS1574: ...“SetLanguage(string)”
C:\...\QuickPanel\lib\UI4Panel.cs(21,28): warning CS1574: ...“Title”
    9 个警告
    0 个错误

已用时间 00:00:05.67
```

实测输出（收尾时的非增量口径，`dotnet build app\QuickPanel.csproj --no-incremental`）：

```
已成功生成。
    9 个警告        ← CS0414 ×5 + CS1574 ×4，每条在日志里出现 2 次（WPF 临时工程二次展开），计数行才是真值
    0 个错误

已用时间 00:00:02.93
```

---

## 2. 本地调试

    dotnet run --project app\QuickPanel.csproj

判据：窗口出现，标题 `快捷工具面板`（= §4 的 `Title`）。工具条右端齿轮开设置浮层（Esc / 点遮罩 / 「完成」都能关）：

- 「配色」三档逐个点都要改观感，含**原生标题栏**跟着染；页脚那行从 `生效键 light　请求模式 Light` 变到 `生效键 dark　请求模式 Dark`；选「跟随系统」时页脚是 `请求模式 System　解析 Dark`（解析结果由库给，别自己猜）。
- 「正文字号」滑杆要带动整窗：卡片标题/介绍/命令串、导航标签、胶囊与圆钮尺寸、**左栏宽度与搜索框宽度**一起长（后两样是本项目加的派生键，见 §7 的自适应一条）。
- 「全局缩放」在 150% / 200% 下内容不裁切，窗口拖不到比折算后的下限更小（下限 = `860×560 × 系数`，并钳到工作区）。
- 卡片列表：鼠标扫过卡片要有放大反馈（没反馈 = `ItemMargin` 贴边把余量吃光）；卡片四边投影完整；点卡片**立刻执行**并在页脚回显"已执行「…」+ pid"。
- **分组标签**（2026-10-04 加）：在「全部工具」里每张卡的第一枚标签是它所属的大类（如 `控制面板` / `IP 与 DNS`）；在单个分组内不带（同一词重复十遍没有信息）；但只要搜索框里有词，单分组内也要带。判据别只看绿字——标签的**显示时机**由 `ToolSection.ShowGroupTags` 决定，自测能钉逻辑，钉不住它有没有真画到卡上，所以要眼睛看一遍或用 UIA 读卡片内的文本（读法见 §7.3 判据）。
- **切分组的响应**：十个分组来回切，切到「全部工具」不该再有可感停顿（改之前实测 181–213 ms，改之后 3–25 ms；成因与修法见 §7.3）。**首次**进某个分组仍要 50–100 ms（那一次的容器非建不可），这是这套修法的正常表现，不是没修好。
- **只有局部在变 = 那处写了字面色或字面字号**（本地赋值即退订，属设计）。

已知边界（不是 bug，见 §7）：下拉浮层与右键菜单不吃 `LayoutTransform`；设置浮层本身在缩放系数内，所以 200% 档下面板要往下滚才看到「全局缩放」那一节。

设置落盘在 `%APPDATA%\QuickPanel\settings.json`（kv1 纯文本，可自己开编辑器改，越界值会被钳回区间，坏文件改名留档）；默认值与允许区间只有一处来源 `app\Helpers\Typography.cs`。

无人值守自证（不开窗、退出码 = 失败断言数）：

    app\bin\Debug\net10.0-windows\QuickPanel.exe --selftest
    $LASTEXITCODE
    Get-Content "$env:APPDATA\QuickPanel\selftest.txt"

判据：退出码 `0`；报告逐行 `PASS`。本项目的门禁是**四段共 107 条**（2026-10-04 本轮：第四段"数据源"31 条，其余三段 76 条；`grep -c '^PASS' selftest.txt` 就是这个数）。第二段里"8 套预置资源键"那条换成了"出货两档 × 38 令牌 + 别名 + 排印键"，因为本工程不接套装键。四段各钉什么：

- **配色与令牌**：令牌数仍是 38、三份内置定义逐令牌可取色、宿主明暗两套定义的对比度与亮度判据自洽、`Theme.Policy` 已显式决策、出货两档的资源键齐全、字典底色与宿主 `Dark` 定义同源。
- **设置通路（kv1）**：九项互不相同的哨兵值往返、写侧键名点名、含 `=`/反斜杠/中文/首尾空格/空串的取值、CR/LF 必须被拒、越界钳位、非法值不覆盖现值、`null`/空 map 不清现值。
- **排印与缩放**：库有没有发布 `UI4.Font.*` 兜底值、宿主覆盖换档后还在、字号层级阶梯、固定件尺寸（圆钮/胶囊/**左栏/搜索框**）、区间钳位、字体候选表首位、缩放转换器边界、明暗策略与可选档自洽、主窗口与设置面板两块 XAML 能构造。
- **数据源（本项目新增）**：分组数与容量、每项字段齐全且介绍 10–48 字、命令全局唯一、**探测挡位与"真正被启动的那个文件"自洽**（不是看命令尾巴）、`ProbePath` 三挡拼接、`regedit` 与 `explorer.exe` 必须走 WindowsFile 挡、**不含实测不存在的常见误传命令**、家庭版缺失项点名一致、过滤口径五例、分组视图随查询词过滤并可复原、分组图标是单个私有区码位、每项的分组标题与所属分组一致、「全部工具」与搜索中才显示分组标签、IP 与 DNS 那 11 条点名齐全且都不设探测、图标缓存那两条的悬浮说明与危险标签各就各位（含 `DetailTip` 回落通路）、切分组只点亮一节、卡片/分组/右栏常驻三块模板能实例化、导航项共用同一个 `Content` 且 UIA 名字仍是分组标题。

- `dotnet run` 会占住终端（GUI 进程不退出），自动化里用 `Start-Process -PassThru` 起 exe、按 pid 读 `MainWindowTitle`，验完 `Stop-Process -Id` **只关自己起的那个 pid**。
- 带空格/中文的路径要用引号包住再传给 `Start-Process -FilePath`。
- 抓屏自证时别用"前台窗口"当判据：这台机器上 PixPin 会抢前台，`CopyFromScreen` 抓到的是它的窗口。用 `PrintWindow(hwnd, hdc, 2)` 按窗口自身 DC 取图，与遮挡和 z-order 无关。

实测输出（`dotnet run` 计时，Debug + `--no-build`，从起进程到按 pid 读到窗口句柄）：

```
dotnet run -> window: 2266 ms  pid=28144 title=[快捷工具面板]
```

实测输出（`--selftest` 退出码 + 报告全文，107 条 PASS + 2 行 INFO）：

```
exit=0

PASS  令牌总数为 38  [实际 38]
PASS  light 全令牌可取色
PASS  dark 全令牌可取色
PASS  highcontrast 全令牌可取色
PASS  正文/底色对比度 ≥ 4.5  [15.77:1]
PASS  强调色对白对比度 ≥ 3.0  [4.56:1]
PASS  Mix(w<=0) 返回源色
PASS  Mix(w>=1) 返回目标色
PASS  IsDark 与标题栏同判据
PASS  明暗策略已显式决策（Theme.Policy）  [当前 "both"，要求 both / light-only / dark-only]
PASS  出货两档 × 38 令牌 + 别名 + 排印键资源键齐全
PASS  字典底色与宿主 Dark 定义同源  [字典 #FF10151B vs 定义 #FF10151B]
PASS  深色档底色与正文的亮度自洽  [底 #FF10151B 正文 #FFE8EEF4]
PASS  浅色档底色与正文的亮度自洽  [底 #FFF7F9FB 正文 #FF10202A]
PASS  深色档正文/底色对比度 ≥ 4.5  [15.69:1]
PASS  深色档强调色对暗底对比度 ≥ 3.0  [6.41:1]
PASS  kv1 往返后九项设置不变  [实际 dark / Cambria / 17 / 125 / 1024x700 @ -1200,40 / True]
PASS  kv1 写侧键名齐全（格式是稳定契约）  [缺 ]
PASS  往返保持值 [a=b=c]  [得到 [a=b=c]，期望 [a=b=c]]
PASS  往返保持值 [C:\Users\me\数据]  [得到 [C:\Users\me\数据]，期望 [C:\Users\me\数据]]
PASS  往返保持值 [#不是注释因为在前头]  [得到 [#不是注释因为在前头]，期望 [#不是注释因为在前头]]
PASS  往返保持值 [  两端空格  ]  [得到 [两端空格]，期望 [两端空格]]
PASS  往返保持值 []  [得到 []，期望 []]
PASS  往返保持值 [Cambria]  [得到 [Cambria]，期望 [Cambria]]
PASS  含 CR/LF 的值被拒绝而不是撑破行格式  [值里带换行会多出一行没有 key= 的垃圾]
PASS  越界字号与缩放被钳回区间  [28 / 50]
PASS  非法值不覆盖已生效值（保留默认）  [windowWidth=980 themeKey=[light]]
PASS  超出合理范围的窗口坐标被拒绝  [windowLeft=NaN]
PASS  null / 空 map 不清现值  [dark / 20]
PASS  库发布 UI4.Font.Size.Base 兜底值  [解析到 15]
PASS  库发布 UI4.Font.Size.Code 兜底值  [解析到 14]
PASS  库发布 UI4.Font.Family 兜底值  [解析到 Segoe UI]
PASS  宿主覆盖的字号在换档后仍解析到宿主值  [期望 20，实际 20]
PASS  撤掉宿主覆盖后回到库默认  [解析到 15]
PASS  发布后 UI4.Font.Size.Base 可解析  [15 vs 15]
PASS  发布后 UI4.Font.Size.Code 可解析  [14 vs 14]
PASS  发布后 App.Font.Size.Caption 可解析  [11 vs 11]
PASS  发布后 App.Font.Size.Small 可解析  [12 vs 12]
PASS  发布后 App.Font.Size.Medium 可解析  [13 vs 13]
PASS  发布后 App.Font.Size.Lead 可解析  [14 vs 14]
PASS  发布后 App.Font.Size.Icon 可解析  [16 vs 16]
PASS  发布后 App.Size.RoundButton 可解析  [28 vs 28]
PASS  发布后 App.Size.Chip 可解析  [24 vs 24]
PASS  发布后 App.Size.NavRail 可解析  [112 vs 112]
PASS  发布后 App.Size.SearchBox 可解析  [320 vs 320]
PASS  半径键是 CornerRadius 而不是 double  [DynamicResource 不做类型转换，挂 double 到 CornerRadius 上会在运行期炸]
PASS  发布后字体族键是 FontFamily
PASS  层级 Caption 在基准 15 下是 11  [实际 11]
PASS  层级 Small 在基准 15 下是 12  [实际 12]
PASS  层级 Medium 在基准 15 下是 13  [实际 13]
PASS  层级 Lead 在基准 15 下是 14  [实际 14]
PASS  层级 Icon 在基准 15 下是 16  [实际 16]
PASS  层级单调（基准 12）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  层级单调（基准 15）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  层级单调（基准 28）  [把层级抹平成同一个基准就是这里会红的来源]
PASS  未知层级名退回基准本身而不是崩
PASS  默认基准下圆钮直径 28  [28]
PASS  默认基准下胶囊高度 24  [24]
PASS  12–28 全区间固定件都装得下字身
PASS  默认基准下左栏 112 搜索框 320  [112 / 320]
PASS  12–28 全区间左栏容得下最长分组名且宽度单调
PASS  字号 28 时左栏确实长于默认 112  [实际 149]
PASS  ClampBase 钳上下限并让 NaN/Infinity 回默认  [7→12 33→28]
PASS  ClampZoom 钳 50–200
PASS  空/非法字体名回退出厂栈
PASS  候选表为空时只剩出厂项  [实际 1 项]
PASS  候选表＝出厂项首位 + 去重 + 丢空白 + 其余按序  [Segoe UI, Microsoft YaHei UI, sans-serif | Arial | Cambria | Segoe UI]
PASS  出厂字体栈可当 FontFamily 用
PASS  W:900 在 150% 下折算正确  [实际 1350]
PASS  窗口下限越过工作区时被钳住  [2560 vs 2560]
PASS  参数缺 W:/H: 前缀时回 UnsetValue
PASS  参数里的像素值不是数字时回 UnsetValue
PASS  light-only 策略下不提供深色档  [Policy=light-only 但档位里有 light,dark,system]
PASS  dark-only 策略下不提供浅色档  [Policy=dark-only 但档位里有 light,dark,system]
PASS  至少有一档可选且首档能应用  [档位=light,dark,system]
PASS  设置面板 XAML 可解析
PASS  主窗口 XAML 可解析
INFO 字号阶梯：基准=15 → 胶囊=11 标签=12 次要=13 正文=15 提示=14 图标=16；固定件 基准15 圆钮=28 胶囊=24，基准28 圆钮=44 胶囊=36
PASS  分组数为 10（不含全部）  [实际 10]
PASS  首位是全部工具且收录完整  [sections=11 首项=all 项数=90 vs 总数 90]
PASS  每个分组 5–12 项
PASS  总项数 ≥ 70  [实际 90]
PASS  每项标题/命令/分组齐全且介绍 10–48 字
PASS  命令原文全局唯一
PASS  探测挡位与启动文件自洽
PASS  ProbePath 按挡位拼路径  [D:\FakeRoot\System32\services.msc / D:\FakeRoot\regedit.exe / null]
PASS  regedit 用绝对路径且走 WindowsFile 挡  [C:\Windows\regedit.exe / WindowsFile]
PASS  不含实测不存在的常见误传命令
PASS  家庭版缺失项点名一致（7 项）
PASS  Matches 命中中文标题/命令/介绍且大小写不敏感  [样例 服务]
PASS  Matches 对不存在的词返回 false
PASS  分组视图随查询词过滤并可复原  [one=True zero=True restored=True]
PASS  分组图标都是单个私有区码位
PASS  每项都带与所属分组一致的分组标题
PASS  「全部工具」显示分组标签，单分组不显示  [all=True ip=False]
PASS  搜索中标签打开、清空后回到关  [搜索中=True]
PASS  IP 与 DNS 组收齐 11 项且都不探测  [项数=11]
PASS  IP 与 DNS 组点名齐全（实测跑通的那 11 条）
PASS  IP 组里没有误标「需管理员」的项（本机 flushdns 非管理员即成功）  [需管理员=0]
PASS  图标缓存两条都在且分在 run 组  [刷新=有 重建=有]
PASS  两条的悬浮说明都写了且互不相同（差别要能展开读）  [长度=247/232]
PASS  只有重建那条打危险标签；刷新探 ie4uinit，重建以 cmd 起头不探；两条都免管理员  [刷新 probe=ie4uinit.exe 重建 danger=[会关闭所有文件夹窗口]]
PASS  DetailTip 通路：写了 DetailNote 就用它，没写的仍是通用提示  [样例=运行框里的原文，点击卡片即执行]
PASS  切分组只点亮一节，重复切与未知键都不留两节  [点亮=1 重复后=True 未知键后=0]
PASS  卡片模板可实例化  [查不到 ToolCard 或实例化即抛]
PASS  分组模板可实例化  [查不到分组模板或实例化即抛]
PASS  右栏常驻模板可实例化  [查不到 SectionShell 模板或实例化即抛]
PASS  导航项共用同一个 Content（右栏不重建的前提）  [项数=11]
PASS  导航项的 UIA 名字取自分组标题（ToString 通路）
INFO 数据源：项=90 分组=10 需管理员=37 家庭版缺=7 有兼容备注=11 有危险提示=6 本机未找到=0
```

实测输出（区分性实验 A：证明数据源那批断言不是空转。把 `telephon.cpl` 逐字改成误传的 `telephonc.cpl` 后重跑，再逐字节还原）：

```
FAIL  不含实测不存在的常见误传命令  [telephonc.cpl>telephonc.cpl]
exit=1                              ← 只有这一条红，其余照旧 PASS
（还原后）0 个错误 / exit=0
```

实测输出（区分性实验 B，2026-10-04：把 `ToolSection.ShowGroupTags` 的 getter 逐字改成 `return false;` 后重跑，再还原。这条是给"卡片上的分组标签"那两条断言做的反证——它们确实咬住了逻辑，不是恒真）：

```
FAIL  「全部工具」显示分组标签，单分组不显示  [all=False ip=False]
FAIL  搜索中标签打开、清空后回到关  [搜索中=False]
exit=2
（还原后）0 个错误 / exit=0 / 103 PASS
```

---

## 3. 打包与启动

两个脚本，产物名靠 `-p:ArtifactLabel=` 分档、互不覆盖。**脚本末尾有 `pause`**，交互式双击用；无人值守要喂一行空输入让它过去：

    '' | .\publish_no_runtime.cmd
    '' | .\publish.cmd

| 档位 | 产物 | 目标机要求 | 本机实测 |
|---|---|---|---|
| 框架依赖 | `publish_no_runtime\QuickPanel_no_runtime.exe` | 必须已装 `Microsoft.WindowsDesktop.App` 10.x | `1,679,030 B`（2026-10-04 本轮，90 项数据源） |
| 自带运行时 | `publish\QuickPanel_self_contained.exe` | 免装 .NET | `70,291,809 B`（同上；**运行时包已在 NuGet 缓存里**的口径，首次跑还要下约 150 MB，耗时不在此列） |

判据（两档都要满足）：

1. 脚本内第 2 步会自己跑 `--selftest`，打出 `selftest exit code: 0`；非 0 时它提示看 `%APPDATA%\QuickPanel\selftest.txt`。
2. 产物目录里**只有那一个 exe**（`DebugType=embedded` 是全局属性，`lib` 也不会留散落 pdb）。
3. 起打包产物并读回窗口标题——**必须用本轮重新打包的 exe**：

       $p = Start-Process -FilePath .\publish\QuickPanel_self_contained.exe -PassThru
       Start-Sleep -Seconds 6
       $p.Refresh(); $p.MainWindowTitle
       Stop-Process -Id $p.Id -Force

   判据：标题等于本轮源码里的 `Title`。拿上一轮的 exe 验本轮改动会假红（实测踩过：改完标题拿旧产物匹配，得出"打包版不出窗"的错误结论）。
4. 自带运行时档**首次启动**要把原生库解到 `%TEMP%\.net\QuickPanel_self_contained\`，会比后续启动慢；记下两次的耗时再下结论。

实测输出（收尾这一轮：先 `Remove-Item` 清空两个产物目录，再跑脚本；脚本自己会跑第 2 步的 selftest）：

```
[2/3] self-test of the packaged exe (exit code = failed assertion count)
selftest exit code: 0
  C:\Users\webtu\Desktop\AAA\QuickPanel\publish_no_runtime\QuickPanel_no_runtime.exe   1679030 bytes
  files in publish_no_runtime\:  QuickPanel_no_runtime.exe
selftest exit code: 0
  C:\Users\webtu\Desktop\AAA\QuickPanel\publish\QuickPanel_self_contained.exe   70291809 bytes
  files in publish\:  QuickPanel_self_contained.exe
```

（更早一轮同口径是 `1,666,742 B` / `70,286,280 B`，脚本耗时 `4.7 s` / `11.3 s`；本轮两项耗时**未测**，只记了字节数与退出码。）

实测输出（打包版起窗回读标题 + 切分组的响应，`publish\QuickPanel_self_contained.exe`，90 项数据源那一版）：

```
FileVersion=0.1.0.0  ProductVersion=0.1.0
MainWindowTitle=[快捷工具面板]
listitems at boot=101         ← 11 个导航项 + 90 张卡片，全部工具在启动时就实体化
classic-1 select=19ms  uiReply=84ms   (kids=0)   ← 首次进「控制面板」：建它那 12 张卡
ALL-1     select=3ms  uiReply=9ms     (kids=0)   ← 切回「全部工具」
ip-1      select=2ms  uiReply=63ms    (kids=0)   ← 首次进「IP 与 DNS」：建 11 张
ALL-2     select=2ms  uiReply=10ms    (kids=0)
classic-2 select=2ms  uiReply=9ms     (kids=0)   ← 第二次进「控制面板」：只剩翻可见性
ALL-3     select=2ms  uiReply=10ms    (kids=0)
```

`uiReply` 是"选完之后向 UI 线程发一次定成本的 `FindAll(Children)` 要等多久"，用它当"界面被占住多久"的代理量。**别拿 `Current.某属性` 当这个探针**——UIA 客户端会缓存属性，那样恒返回 0 ms 是假绿。进程内的直接计时（同一份代码、同一台机器，Debug 构建）见第 7.3 节。

---

## 4. 修改标题

真源只有一处：`app\MainWindow.xaml` 的 `Title="…"`（同时是任务栏与 DWM 标题栏文字）。

    Select-String -Path app\MainWindow.xaml -Pattern 'Title="'

判据：改完重新构建并起进程，`MainWindowTitle` 回读为新值。

- **不要自绘标题栏**：窗口里出现过 UI4 控件，库会在 `Loaded` 时按当前主题把原生标题栏染好（Win10 只认深/浅标志，Win11 才染底色/文字/描边三色，是系统能力差异不是 bug）。`WindowStyle=None` + `AllowsTransparency=true` 会绕过整套染色通路。
- 完全不含 UI4 控件的窗口要手动 `UI4WindowTitleBar.Apply(win)`，否则它等到下次主题切换才被扫到。
- `UI4MessageBox` / `UI4ColorPicker` 是自绘标题区，对它们调 `Apply` 无可见效果。

实测输出：

```
8:        Title="快捷工具面板" Width="1120" Height="760"
（起本轮 Debug 产物按 pid 回读）title=[快捷工具面板]
（起本轮自带运行时打包产物按 pid 回读）TITLE=快捷工具面板
```

---

## 5. 修改图标

一个文件三处引用：`app\AppIcon.ico` → csproj 的 `<ApplicationIcon>`（exe 内嵌图标）＋ `<Resource>`（窗口/任务栏图标，`Window.Icon="AppIcon.ico"` 走它）。覆盖文件即可，不用改 csproj。

**默认图标是本地生成的字母像素图标**（不联网、不要 ImageMagick）：`make-icon.ps1` 在 16×16 逻辑网格上画应用名首字母的 5×7 点阵（底色 `#4F6BE8`、字形 `#F7F9FB`、四角削成圆角），一次写全 16/24/32/48/64/128/256 七帧 32bpp ICO。换字母、配色或尺寸集，重跑一次覆盖回去即可：

    powershell -NoProfile -ExecutionPolicy Bypass -File "<skill>\scripts\make-icon.ps1" `
      -Name QuickPanel -Out app\AppIcon.ico -Back "#4F6BE8" -Fore "#F7F9FB"

判据三条，缺一不可：

1. 生成器自己的输出末尾是 `OK AppIcon.ico letter=<首字母> … frames=7`。它内部已断言"目录条目连续无缝 + 末帧正好落到文件尾 + 每帧字节数等于算式值"，任一条不过就抛错退出，不会留下半截 ico。
2. 同一段里的 `probe 16 -> …` / `probe 32 -> …` 两行要报 `corner.alpha=0`——证明圆角透明能活着过 `<ApplicationIcon>` 这条烤进 exe 的通路。
3. 构建后取回内嵌位图逐像素比：

       dotnet build app\QuickPanel.csproj
       Add-Type -AssemblyName System.Drawing
       $ic = [System.Drawing.Icon]::ExtractAssociatedIcon("app\bin\Debug\net10.0-windows\QuickPanel.exe")
       $bmp = $ic.ToBitmap()
       $bmp.Width; $bmp.Height
       $bmp.GetPixel(0, 0).A     # 圆角，应为 0
       $bmp.GetPixel(2, 16)      # 左边缘，应为底色
       $bmp.GetPixel(16, 16)     # 中心，字形色或底色取决于字母

实测教训——换掉 `AppIcon.ico` 后文件字节数完全可以一样（4286 B 对 4286 B，同尺寸同深度），体积判据会漏判；像素点命中才算过。

- 要改用外部图（例如 `selfh.st/icons` / `github.com/selfhst/icons` 的 `ico/` 现成多尺寸 ico）就直接覆盖 `app\AppIcon.ico`，并把来源 URL、图标 ref、许可与"是否改过"记进本节——那份仓库是 **CC-BY-4.0，要署名**。取源用 CDN 直链 `https://cdn.jsdelivr.net/gh/selfhst/icons@main/ico/<ref>.ico`；判可达性要真 GET 拿到字节数，别拿 HEAD 的状态码当依据。**本项目没换外部图**，出厂就是生成式「Q」。
- `ExtractAssociatedIcon` 只回 32×32；要验其它尺寸得读 ico 的帧表（`make-icon.ps1` 的自证读的就是帧表）。
- 装了 ImageMagick 也可以用 `magick 源.png -define icon:auto-resize=256,64,48,32,16 app\AppIcon.ico`；本机 `Get-Command magick` 为空 → **未验证**（原因：未安装 ImageMagick，且默认通路不需要它）。
- 图标文件缺失时构建会失败：先删掉 `<ApplicationIcon>` 与 `<Resource>` 两行，或补一个文件。

实测输出（`scaffold.ps1` 建工程时生成图标那一段）：

```
probe 16 -> 16x16  corner.alpha=0  center=#4F6BE8 back
probe 32 -> 32x32  corner.alpha=0  center=#4F6BE8 back
frame 16  DIB  1128 B
frame 24  DIB  2440 B
frame 32  DIB  4264 B
frame 48  DIB  9640 B
frame 64  DIB  16936 B
frame 128 DIB  67624 B
frame 256 PNG  3117 B
OK AppIcon.ico  letter=Q  back=#4F6BE8  fore=#F7F9FB  105267 B  frames=7
```

实测输出（从**两档打包产物**里取回内嵌位图逐像素比，不是从 ico 文件）：

```
EXE QuickPanel_no_runtime.exe        ICON 32x32 center=#4F6BE8  corner(1,1) a=0  center a=255
EXE QuickPanel_self_contained.exe    ICON 32x32 center=#4F6BE8  corner(1,1) a=0  center a=255
```

---

## 6. 发布 / 分发

发版流程（顺序不能换）：

1. 改 `app\QuickPanel.csproj` 的 `<Version>`（当前 `0.1.0`）。
2. 重新打包要交付的那一档。
3. **守门比对**：产物的 `FileVersion` 必须等于刚写的版本，否则 `publish\` 里坐的是旧构建。

       (Get-Item .\publish\QuickPanel_self_contained.exe).VersionInfo.FileVersion

   判据：`0.1.0.0`（`<Version>0.1.0</Version>` 派生）。**先删空 `publish\` 与 `publish_no_runtime\` 再打包**，否则上一轮 exe 会蒙过这一步。
4. 拷贝清单：单 exe 就是全部（框架依赖档另需目标机有 Desktop Runtime 10.x）。
5. 每个交付包落地后再跑一次 `--selftest`，退出码 0 才算发出去：

       & .\publish\QuickPanel_self_contained.exe --selftest; $LASTEXITCODE

- 校验和（有分发目录才需要）：`Get-FileHash .\publish\QuickPanel_self_contained.exe -Algorithm SHA256`，把 `.sha256` 与 exe 同目录放。本次没有建分发目录 → **未验证**（不是跑不通，是本轮没跑）。
- 归档命名带上档位（`_self_contained` / `_no_runtime`），两档体积差两个数量级（本轮 `70,291,809 B` 对 `1,679,030 B`），别说"这个包 1.4 MB"却不标是哪档。
- 目标机排查顺序：`dotnet --list-runtimes` 有没有 WindowsDesktop 10.x → 是不是被杀软拦在首次解包 → `%APPDATA%\QuickPanel\selftest.txt` 有没有红。

实测输出（守门比对 + 两档各自再跑一次 `--selftest`）：

```
QuickPanel_self_contained.exe  FileVersion=0.1.0.0  ProductVersion=0.1.0  size=70291809
QuickPanel_no_runtime.exe      FileVersion=0.1.0.0  ProductVersion=0.1.0  size=1679030
csproj Version=0.1.0
两档各自 --selftest 退出码：0 / 0（脚本第 2 步各跑一次，起窗回读又跑一次）
```

---

## 7. 注意事项

### 7.0 这个面板的数据源是怎么来的（改命令表之前先读这一段）

`app\Services\ToolCatalog.cs` 里 90 项 / 10 组不是抄一份现成命令表，是"官方文档 + 社区资料 + 本机逐条实测"交叉出来的，三条口径：

1. **只收 Win10 与 Win11 都能解析的入口**；行为变化的不删，写进 `CompatNote` 变成卡片上的标签（共 11 项有备注，如 `desk.cpl` 在 Win11 直启会跳「设置」、`wscui.cpl` 在 Win11 转安全中心）。
2. **SKU 差异与权限差异分两列**：`HomeEditionMissing`（家庭版根本没这个文件，7 项：`gpedit/secpol/lusrmgr/wf/fsmgmt/certlm/rsop`）与 `RequiresAdmin`（37 项）互不混用。混成一句"启动失败"用户只能瞎试，所以失败文案里也分开说（`LaunchService.BuildFailureText`）。
3. **社区命令表里流传、本机实测不存在的名字一律不收**，并由自测的黑名单点名挡住：`input.cpl`（输入法设置现在只能走 `rundll32.exe shell32.dll,Options_RunDLL 7`）、`access.cpl`、`multimed.cpl`、`telephonc.cpl`（正确名是 `telephon.cpl`）、`fstrim.msc`、`sr.msc`、`storageSpaces.msc`、`wmic.exe`（Win11 24H2+ 已移除）、`wt.exe` / `pwsh.exe`（可选组件，不是内建）等 17 个。

另外三条实测纠正（写代码时容易踩）：

- `regedit.exe` 住在 `C:\Windows\`，**不在 System32**——拼进 System32 永远探不到，所以探测分 `System32File` / `WindowsFile` 两挡。
- `C:\Windows\System32` 下的 `.msc` 全是友好命名（21 个），没有 GUID 命名的隐藏文件；`SysWOW64` 的 `.cpl` 是 System32 的**严格子集**（少 `Firewall.cpl`、`TabletPC.cpl`），不是"另有额外项"。
- 启动 `.msc` 时"点了没反应"的头号来源是家庭版缺包，报的是 `Windows cannot find 'gpedit.msc'` 这类**文件缺失型硬错误**，不是权限不足。

2026-10-04 加「IP 与 DNS」这一组（11 项，全是命令行查询）时又实测纠正四条：

- **`ipconfig /flushdns` 非管理员就能成**：本机（Win11 26300）直接跑返回"已成功刷新 DNS 解析缓存。"。社区命令表普遍给它标"需管理员"，那是旧版本口径，照抄就会多打一个假标签。自测里 `IP 组里没有误标「需管理员」的项` 钉住这条。
- **`powershell.exe` 不在 `System32`**，它在 `System32\WindowsPowerShell\v1.0\`。所以 `powershell …` 起头的命令**不设探测挡位**（它启动的文件靠 PATH / App Paths 解析，不在探测的两挡里）。
  **更正一处旧说法**：本条刚写下时，自测第 5 条的口径是"整条命令原文以 `.cpl/.msc/.exe` 结尾才算文件型"。加下面「图标缓存」那两条时发现这个口径是错的——`cmd /k … & start explorer.exe` 的尾巴带 `.exe`，但它启动的是 `cmd`，于是被误判成"文件型却没给探测名"。现在口径改成**看 `LaunchService.Split` 切出来的第一段（真正被启动的那个文件）**，并加了一条"探测名要和启动的文件对得上"。顺带补上此前因此漏配探测的 6 项：5 条 `explorer.exe shell:*`（`explorer.exe` 和 `regedit.exe` 一样住在 `%Windows%` 根，实测 3,479,480 B，走 `WindowsFile` 挡）与 `rundll32.exe shell32.dll,Options_RunDLL 7`（System32）。
- **在 Git Bash 里跑这些命令会得到假阴性**：`./ipconfig.exe /displaydns` 的 `/displaydns` 被 MSYS 改写成 Windows 路径，ipconfig 回"无法识别参数"。要验就写 `cmd //c "ipconfig /displaydns"`（双斜杠是故意的）。这一条差点让我把 `/displaydns`、`/flushdns` 当成不存在的命令踢出数据源。
- **分组图标不是猜出来的**：先用 `GlyphTypeface.CharacterToGlyphMap` 把候选码位筛成"这台机器真有字形"的（45 个候选里 6 个不存在：`E96B E96C E9A5 F1B8 F38E E9E8`），再用 GDI+ 出一张 18px / 54px 双排对照图**用眼睛挑**——18px 才是左栏的实际渲染量级，54px 上好看的图糊到 18px 可能认不出。最后选了 `F6FA`（地球加放大镜）。

同日再加「命令与运行」里的图标缓存两条（`ie4uinit.exe -show` 与"停 Explorer → 删 `iconcache_*.db` → 起 Explorer"）时，实测出三条：

- **`ie4uinit.exe -show` 不重建缓存**：跑完那 15 个 `iconcache_*.db` 前后一字节不差（都是 33,437,056 B），退出码还取不到（它把活交给 Shell 就退）。所以它只能"重画"，卡片文案据此写成"只让 Shell 重画，缓存文件原样保留"，别写成"刷新/重建缓存"。
- **那批缓存被 `explorer.exe` 锁着**（独占打开失败），而旧版遗留的 `%LOCALAPPDATA%\IconCache.db` 反而没被锁——只删后者在现代 Windows 上基本无效。要真重建就必须先停 Explorer，这条代价写进 `DangerNote`（`会关闭所有文件夹窗口`）。
- **卡片正文那行放不下"两条方式的差别"**：介绍受 ≤48 字约束，且网格等分高度会让一处变长把 90 张卡全撑高。所以长解释落在命令条的悬浮说明（新增 `ToolItem.DetailNote` → `DetailTip`，没写这项的卡片仍是原来的通用提示），可见那行只放决定性差别。

主要来源：
- Microsoft Learn《Launch Windows Settings》（`ms-settings:` URI 的 Win10/Win11 弃用与新增标注，本页是版本判定的主依据）`https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings`
- Microsoft Learn 中文版同主题 `https://learn.microsoft.com/zh-cn/windows/uwp/launch-resume/launch-settings-app`
- woshub《Enable gpedit.msc on Windows 10/11 Home》（家庭版缺失的确切报错文案与 DISM 补装）`https://woshub.com/group-policy-editor-gpedit-msc-for-windows-10-home/`
- Microsoft Support KB149648《Description of Control Panel (.cpl) Files》`https://support.microsoft.com/en-us/kb/149648`（存目，页面无实质内容）
- 本机实测：`C:\Windows\System32` / `SysWOW64` 文件枚举（`dir /a`）、`HKLM\SOFTWARE\Classes\ms-settings`、`HKLM/HKCU\...\Explorer\Shell Folders` 与 `User Shell Folders`
- 社区命令表若干（cnblogs 三篇 + 一块板子），**只在能和本机实测或官方页对上时才采信**；`linux.do` 与 Wikipedia 两份抓取失败，未采信。

未验证：以上"家庭版没有 / Win10 上如何"的结论来自文档与社区口径，本机是 `ProfessionalWorkstation`，**没有在 Win10 真机或家庭版真机上复测**。数据源为此留了运行期探测（`ProbeAvailability`）：探不到就在卡片上打「本机未找到」，但仍然允许点击。

### 7.1 lib 偏离清单（本工程的 `lib/` 与 Skill 种子不同的两个文件、四处改动）

四条都是组件层缺陷，改在 `lib/` 而不是在宿主侧绕道。**Skill 的种子 `assets/seed/lib` 没有被改动**（`fetch-source.ps1 -Offline` 仍报 `LOCAL 48/48 种子完整，未联网`），所以这四条不会随 `scaffold.ps1` 带到别的工程；要回灌得走 Skill 的"源码来源与升级"第 1 条（改种子 → `make-manifest.ps1` 重钉 → 同步 `references/`）。

逐文件偏离（`diff -rq --strip-trailing-cr` 的实测输出，只有这两个文件报 differ）：

```
Files <seed>/lib/UI4GridView.cs      and <proj>/lib/UI4GridView.cs differ
Files <seed>/lib/UI4NavigationView.cs and <proj>/lib/UI4NavigationView.cs differ
```

| 文件 | 症状 | 根因 | 改法 |
|---|---|---|---|
| `UI4NavigationView.cs` | `<ui:UI4NavigationView ItemFontSize="…">` 编译期直接报 `MC3072 属性不存在` | `ItemFontSize` 的 DP 用 `ownerType = UI4NavigationView` 登记，CLR 包装却被写在 `UI4NavigationViewItem` 类里（手册的表把它列在 NavigationView 名下，是对的） | 把 DP 字段与包装属性整体挪进 `UI4NavigationView` 类，默认值 10 与 `AffectsMeasure` 原样保留 |
| `UI4NavigationView.cs` | 读屏/自动化在窗口里**枚举不到任何 `ListItem`**，左栏与整块右栏内容从 UIA 树上一起消失 | 本控件模板里没有 `ItemsPresenter`（项由内部两个 `UI4ListBox` 重新承载），默认的 `ItemsControlAutomationPeer` 按"自己的项容器"枚举，一个也找不到，并且它会顶掉默认的可视子枚举 | `OnCreateAutomationPeer` 改返回 `FrameworkElementAutomationPeer`（走可视树）。修复后实测 `ListItem` 由 0 → 87（当时是 10 个导航项 + 77 张卡片；2026-10-04 加了「IP 与 DNS」一组与右栏常驻改造后，启动回读为 99 = 11 导航 + 88 卡片） |
| `UI4NavigationView.cs` | 项字号一大，导航标签被切成一个字（`全部工具` → `全…`），外层 `LeftPanelWidth` 给多宽都没用 | 项容器样式钉死 `Width=70 / Height=70`（70 是配默认 `ItemFontSize=10` 量的），内层模板又钉死 `Width=60` 与标签 `MaxWidth=76` | 容器宽度改为绑定祖先的 `LeftPanelWidth`（宿主设了就铺满，NaN 时退回按内容自适应），高度只留 `MinHeight=70`；内层 `Width/Height` 改 `MinWidth/MinHeight`，去掉标签的 `MaxWidth=76`（裁剪交给 `TextTrimming` + 宿主给的栏宽）。实测：字号 15 → 项宽 112，字号 28 → 项宽 149，标签完整 |
| `UI4GridView.cs` | 卡片区底部出现横向滚动条，且列宽随文案长短抖动 | 模板里 `HorizontalScrollBarVisibility=Auto` 让 ScrollViewer 用无限宽测量内容，`UniformGrid` 于是按子项期望宽分列——与"ItemWidth 只算列数、卡片铺满所在列"的契约相反（同族 `UI4ListView` 那里就是 `Disabled`） | 改成 `ScrollBarVisibility.Disabled`。实测：改前 2 列 + 横向条，改后 3 列铺满、无横向条 |

判据：改完 `dotnet build --no-incremental` 仍是 `0 个错误 / 9 个警告`（警告构成没变，说明没顺手引入新的），`--selftest` 107 条全绿。

### 7.2 通用约束与坑

- **本地赋值就是退订主题**，这是设计不是 bug。宿主写一个字面色（`Foreground="#333"`）就把 `SetResourceReference` 顶掉了，之后 `ClearValue` 只能回到库内代码的字面默认色。要跟主题走就写 `{DynamicResource UI4.Brush.*}`；代码里现取资源要兜底，`Application.Current.FindResource` 对缺失键**抛异常**。
- **主题接线的硬顺序**：`UI4Theme.Register/SetTheme` 只能在 `base.OnStartup(e)` 之后、且在 `OnStartup` 内（`Application.Current == null` 时库静默不装字典，症状是宿主 `{DynamicResource}` 全空而库内控件照常好看）；装字典要早于第一个窗口 `Show()`；`--selftest` 分支排在 `OnStartup` 最前面。本项目不接套装键，所以 `App.OnStartup` 里**没有** `UI4ThemePacks.RegisterAll()`——要加 8 套业务档时记住"`RegisterAll()` 必须早于 `Apply(套装键)`，顺序错时 `Apply` 只返回 `false`、不抛异常"。
- **明暗策略是显式决策**（`app\Helpers\Theme.cs` 的 `Theme.Policy`，本工程 `both`）。留 `TODO` 时 `--selftest` 会红并挡住发布；只做一档也要显式钉住当前档，别把 `SetTheme` 整段删掉。`both` 下 `ThemeService.AvailableKeys()` 给 `light/dark/system` 三档，出厂 `SettingsService` 默认 `system`。
- **界面自适应当编码期约束**，不是收尾补丁：
  - 下限 `MinWidth/MinHeight = 860×560`，走 `ZoomedSize` 按缩放系数折算并钳到工作区（200% 档下实测钳到 `2560`，不会出现"下限超过屏幕"）。
  - 多栏不写死：`UI4GridView` 自己按宽度算列数（`ComputedColumns`），别在它外面再算一遍。
  - `app.manifest` 的 PerMonitorV2 声明别删（net10 的 WPF 仍按清单取 DPI 级别）。
  - **界面里不写字面字号**：`grep -n 'FontSize="[0-9]' app\*.xaml app\Views\*.xaml` 输出为空。字号只从 `App.Font.Size.*` / `UI4.Font.Size.*` 层级键来。
  - **尺寸也不钉死**：圆钮/胶囊走 `App.Size.*`，本项目另加两个派生键 `App.Size.NavRail`（左栏宽 = 项字号 × 最长分组名 5 字 + 留白）与 `App.Size.SearchBox`。这两个键是字号拉到 28 时"导航标签被切成一个字 / 搜索框占位文案切在一半"的修复点，`--selftest` 有对应断言。
  - 卡片高度给 `ItemHeight="NaN"`（由内容决定）而不是钉死 176：钉死值在默认字号下好看，字号一档上去介绍和标签就整片被切。
  - 窗口几何持久化要回正：恢复前判虚拟桌面边界，不在界内就回 `CenterScreen`；最大化时存 `RestoreBounds`。
- **`UI4NavigationView` 的项必须真身进 `Items`**：库在 `OnItemsChanged` 里按类型分流到 `RegularItems`/`BottomItems`，直接往 `RegularItems` 加不会进左栏，下次 `Items` 变化还会被整表清空。右栏内容绑的是 `SelectedItem.Content`，所以本项目把每个分组的 `ToolSection` 挂在项的 `Content` 上，靠 `Window.Resources` 里的隐式 `DataTemplate` 渲染。
- **绑 `ICollectionView` 的列表要显式关 `IsSynchronizedWithCurrentItem`**：默认会自动同步视图的当前项，于是搜索一改过滤，首项就被"选中"并触发 `SelectionChanged`——在本应用里等于替用户点了卡片执行命令。
- **卡片网格的图标字体只能用 `Segoe MDL2 Assets`**，不要用 `Segoe Fluent Icons`：后者是 Win11 才装的字体，Win10 上整片豆腐块。10 个分组码位是拿字体表逐个字形查过的（`segmdl2.ttf`，`CharacterToGlyphMap` 共 1833 项；候选里 `EC7B`/`E7B9`/`EDA1`/`E9F4` 无字形，已排除）。`--selftest` 钉的是"码位非空、单字符、落在私有区"，观感仍要截图看。
- **间距刻度与外溢余量**：`Margin/Padding` 只用 4 的倍数（4/8/12/16/20/24/32）——同排兄弟 ≥ 8、分组之间 ≥ 16、内容到窗口边缘 ≥ 16；卡片外边距要 ≥ `ShadowDepth + ShadowBlurRadius`（本项目 `8 + 12 = 20`，`ItemMargin` 就取 20），否则投影四边被切平；`ItemMargin` 左右 ≥ 10 才留得住悬浮放大预算（`allowed = min(HoverMaxGrow 8, sideSlack + 4 − 6)`）。别用 `ClipToBounds` 兜溢出，切边正是它的效果。静态自查：

      Select-String -Path app\*.xaml,app\Views\*.xaml -Pattern '(Margin|Padding)="([0-9,\s]*)"' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[2].Value -split '[,\s]+' } |
        Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } |
        Where-Object { $_ -ne 0 -and ($_ % 4) -ne 0 } | Sort-Object -Unique

  判据：输出为空。实测输出：**空**（`grep` 版同口径，输出也为空）。
- **别绑静态属性**：`{Binding Path=(ui:UI4Theme.CurrentMode)}` 会停在初值——库发的是 `StaticPropertyChanged`，WPF 普通绑定要找同名 `<属性>Changed` 静态事件。本项目页脚订阅 `UI4Theme.ThemeChanged` 后读 `ResolvedKey/CurrentMode/ResolvedMode`。
- **`UI4Grid.Background` 会被库覆写**：主题切换时 `UpdateBackground()` 直接 `SetValue` 写回渐变，宿主赋的底色会丢。要固定底色就用普通 `Grid` 或 `UI4Panel`。
- **`UI4NavigationView` 有 6 个颜色 DP 不跟令牌**（`ItemBackground`/`ItemForeground`/`ItemHoverColor`/`ItemHoverForeground`/`ItemPressedBackground`/`ItemPressedForeground`，深色下否则黑字压黑底）。本项目把后 5 个显式接回令牌，`ItemBackground` 按建议保持 `Transparent`——`MainWindow.xaml` 里已经逐个接好。
- **托盘 `UI4NotifyIcon` 必须在 `OnClosing` 里 `Visibility = Collapsed; Dispose();`**，否则托盘残留点不动的死图标（Shell 行为）。本项目没有托盘。
- **`UI4MessageBox.Show` 返回 `bool?`**：判 `== true`，别判 `if (result)`。本项目只在执行失败时弹它（模态框只留给错误报告，成功只写页脚）。
- **PowerShell 侧**：`.ps1` 里有中文字面量必须存成带 BOM 的 UTF-8（PS 5.1 按 ANSI 解码会把中文字节的下一个换行吞掉，两条语句并成一条，报错位置完全看不懂——本轮 `verify-flow2.ps1` 就是这么"语法错"的，补 BOM 即好）；脚本第一句设 `[Console]::OutputEncoding = UTF8`；带中文的路径别用 `powershell -Command` 内联，走 `-File` + 参数。另两条本机实测坑：`powershell -Command "... $_ ..."` 里的 `$_` 会被 Git Bash 吃掉（改成写 `.ps1`），`[Type]::$var` 反射取静态成员时 `ControlType` 的成员是**字段**不是属性（要用 `GetField`）。
- **验证陷阱**：拿上一轮产物验本轮改动（§3 判据 3）、验证步骤接管道（`build | grep` 的退出码是 grep 的）、增量构建的 `0 个警告`（§1）——这三条都会把"没通过"报成"通过"或反之。本轮另外踩到两条：抓屏时前台窗口是 PixPin 的（要用 `PrintWindow`），以及 `Stop-Process` 与 `dotnet build` 抢 exe 会报 MSB3027 文件锁定（等进程真退出再编）。

### 7.3 切分组的响应：卡顿成因与"右栏常驻"这个宿主侧修法

**症状**（用户报的三条之一）：左栏切到第一个「全部工具」明显卡一下，其它条目之间切换正常。

**先量再改**（进程内计时：`DependencyPropertyDescriptor` 挂 `Nav.SelectedItem` 变化，起 `Stopwatch`，再向 `Dispatcher` 投一条 `Input` 优先级的空操作，落到即认为 UI 线程腾出手——`ContextIdle` 会把无关的后台投递算进来，`Render` 会抢在布局前面，都不对。临时探针量完已删除，别再往交付包里带）：

| 切换目标 | 条目数 | 改之前 busy |
|---|---|---|
| 控制面板 / 管理工具 / 网络共享 / 设置直达 | 8–12 | 40–50 ms |
| 全部工具（第一次） | 77 | 328 ms |
| 全部工具（第 2–5 次） | 77 | 187 / 181 / 192 / 211 ms |

斜率 ≈ 2.2 ms/卡，截距 ≈ 18 ms → **卡的量正比于一次要实体化的卡片数**，而且每次都发生，不是只有首次冷启动。UIA 回读 `ListItem` 得 87 个（10 导航 + 77 卡）→ 全部卡片真的都建了。

**排除掉的一个猜测**：以为贵在那张 `DropShadowEffect`。把 `ShadowOpacity` 设成 0 并在库里跳过挂 `Effect` 那一行（临时补丁，量完逐字节还原），全部工具仍是 188 / 181 / 213 / 175 ms —— 投影不是成本来源，元素构造与测量才是。**结论：动 `lib/` 的投影不会让这件事变快，别从这里下手。**

**根因**：`UI4GridView` 的 `ItemsPanel` 是 `UniformGrid`，WPF 里它不虚拟化；而库的右栏绑的是 `SelectedItem.Content`，**Content 一换，`ContentControl` 就把整棵子树推倒重建**。数据是固定的（90 项写死在 `ToolCatalog`），重建纯属白干。

**修法（不改 `lib/`，见 §7.1 的纪律：这一轮用户明确要求不动 lib）**：让所有导航项的 `Content` 指向**同一个** `SectionShell` 实例，右栏因此只模板化一次；shell 的模板是一个 `ItemsControl`（`ItemsPanel` 用 `Grid`，十节叠在同一格），每个 `ToolSection` 的根 `Grid` 绑 `Visibility ← IsActive`。切分组 = 改十个布尔，不建任何容器。首次进某一节时才建它那十几张卡（`Collapsed` 子树不参与 measure，容器生成因此天然是惰性的），之后永久留着。

改完之后（同一探针、同一台机器、Debug 构建）：

| 事件 | busy |
|---|---|
| 首次进 控制面板 / IP 与 DNS / 管理工具 / 设置直达 | 102 / 78 / 59 / 56 ms |
| 第二次及以后进同一节 | 3–25 ms |
| 切到「全部工具」（4 次） | 3 / 8 / 12 / 25 ms |

对照改之前的 181–213 ms：**切回全部工具从 ~190 ms 降到 3–25 ms**。打包版的外部量法（`uiReply`，见 §3）给出同向结论：`ALL-1=9ms ALL-2=10ms ALL-3=10ms`，首次进某节才是 63–84 ms。

**三个连带后果，都是实测出来的，改这块时别漏**：

1. **导航项的 UIA 名字会退化**。容器报的名字走"数据项的纯文本"：以前 `Content` 是 `ToolSection`，它的 `ToString()` 返回分组标题，名字就顺带对了。现在 `Content` 是共用的 shell，`ToString()` 会把 `QuickPanel.ViewModels.SectionShell` 报成项名。**先试过 `AutomationProperties.SetName(导航项, 标题)`——不生效**，那条通路根本不查它。有效的做法是两处：`SectionShell.ToString()` 返回空串（让纯文本落空），并改用宿主自己的 `NavSectionItem : UI4NavigationViewItem` 覆写 `ToString()` 返回所属分组标题。自测里 `导航项的 UIA 名字取自分组标题（ToString 通路）` 钉的就是这条。**注意这条自测是进程内断言，证的是"名字来源接对了"，真正的外部回读要靠 UIA**：实测 `全部工具 / 控制面板 / … / IP 与 DNS / 设置直达 / 命令与运行` 十一个名字逐个回读正确。
2. **`--selftest` 里那条"分组模板可实例化"要拆成两条**：`ToolSection` 模板与 `SectionShell` 模板各自 `LoadContent()`，漏了后者就只剩"右栏一片空白但不报错"。
3. **内存换响应**：把十个分组都点过一遍后，窗口里同时留着 90（全部）+ 90（各分组，每项只属于一个分组）= **180 张卡的容器（按数据源算出来的，未逐项量过工作集）**。本机没测出可感副作用（`listitems at boot=101`，切完一圈也不卡），但这是这套修法的代价，别在更大的数据源上照抄。

**判据（要改这块时跑）**：`--selftest` 里 `切分组只点亮一节，重复切与未知键都不留两节` + `导航项共用同一个 Content` 两条绿；再用 UIA 连切三次「全部工具」，`uiReply` 都在几十毫秒内（若退回上百毫秒，就是 `Content` 又变回按分组赋值了）。

### 7.4 未验证项汇总

| 项 | 在哪节 | 原因 |
|---|---|---|
| `dotnet pack`（`NU5019` 那条通路） | §1 | 本工程不产 NuGet 包，本轮没跑 |
| `magick … -define icon:auto-resize` 生成图标 | §5 | 本机未装 ImageMagick（`Get-Command magick` 为空），且默认通路不需要 |
| `Get-FileHash` 出 `.sha256` 随包分发 | §6 | 本轮没有建分发目录 |
| 数据源在 **Win10 真机**与**家庭版**上的实际行为 | §7.0 | 本机是 Win11 26300 `ProfessionalWorkstation`；结论取自官方文档 + 社区口径，并留了运行期探测打「本机未找到」标签兜底。本轮新增的「IP 与 DNS」11 条同样只在本机跑过，其中 `ipconfig /flushdns` 免管理员这一条**只在 Win11 上证实**，Win10 未测 |
| 右栏常驻后 180 张卡容器的内存占用 | §7.3 | 只验了"切换不卡 + 功能正确"，没量过工作集；数据源再大一档时要先量这个 |
| 高对比度档（`highcontrast`）观感 | §2 | 四挡决策选的是 `both`（亮 + 暗），没注册高对比度宿主定义；自测仍验库内 `HighContrast()` 定义逐令牌可取色 |
| 缩放档下"弹层不跟着放大"能否接受 | §2 | 属观感判断，需要使用者自己定；本项目当前没有下拉/右键菜单在卡片上，只有设置面板里的字体下拉会碰到 |
