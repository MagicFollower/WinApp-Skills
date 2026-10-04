using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using StartUI4Controls;
using QuickPanel.Helpers;
using QuickPanel.Models;

namespace QuickPanel.Services
{
    /// <summary>
    /// 由 "QuickPanel.exe --selftest" 触发。约定：返回值 = 失败断言数（0 为全通过），
    /// 发布脚本拿它当门禁。只跑纯函数与主题定义，不开窗、不读用户设置、不挂 UI 异常钩子。
    /// 报告写 %APPDATA%\QuickPanel\selftest.txt，写不进去回落程序目录，且报告失败不得改变退出码。
    /// </summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            var report = new StringBuilder();
            int failed = 0;

            // 1) 令牌总数：新增令牌没同步进手册与宿主配色，这里先崩在自测而不是运行期观感
            int tokenCount = Enum.GetValues(typeof(UI4ThemeToken)).Length;
            failed += Check(report, "令牌总数为 38", tokenCount == 38, "实际 " + tokenCount);

            // 2) 三份内置定义必须逐令牌可取色：漏一个就在 GetColor 上抛 KeyNotFoundException
            foreach (Func<UI4ThemeDefinition> factory in new Func<UI4ThemeDefinition>[]
                     { () => UI4ThemeDefinition.Light(), () => UI4ThemeDefinition.Dark(), () => UI4ThemeDefinition.HighContrast() })
            {
                var def = factory();
                var missing = new List<string>();
                foreach (UI4ThemeToken t in Enum.GetValues(typeof(UI4ThemeToken)))
                {
                    try { def.GetColor(t); } catch (KeyNotFoundException) { missing.Add(t.ToString()); }
                    catch (Exception ex) { missing.Add(t + ":" + ex.GetType().Name); }
                }
                failed += Check(report, def.Key + " 全令牌可取色", missing.Count == 0, string.Join(",", missing));
            }

            // 3) 宿主单源色的可读性：正文对底按 AA 4.5:1，强调色对白按非文本 3:1
            failed += Check(report, "正文/底色对比度 ≥ 4.5",
                Contrast(Theme.Foreground, Theme.Background) >= 4.5,
                Contrast(Theme.Foreground, Theme.Background).ToString("0.00") + ":1");
            failed += Check(report, "强调色对白对比度 ≥ 3.0",
                Contrast(Theme.Accent, Colors.White) >= 3.0,
                Contrast(Theme.Accent, Colors.White).ToString("0.00") + ":1");

            // 4) 混色边界与亮度判据
            failed += Check(report, "Mix(w<=0) 返回源色", Theme.Mix(Theme.Accent, Theme.Foreground, 0f) == Theme.Accent, "");
            failed += Check(report, "Mix(w>=1) 返回目标色", Theme.Mix(Theme.Accent, Theme.Foreground, 1f) == Theme.Foreground, "");
            failed += Check(report, "IsDark 与标题栏同判据", Theme.IsDark(Colors.Black) && !Theme.IsDark(Colors.White), "");

            // 5) 明暗策略必须是显式决策：留在 "TODO" 就挡住发布，别让工程带着"没定"上路
            failed += Check(report, "明暗策略已显式决策（Theme.Policy）",
                Theme.Policy == "both" || Theme.Policy == "light-only" || Theme.Policy == "dark-only",
                "当前 \"" + Theme.Policy + "\"，要求 both / light-only / dark-only");

            // 6) 资源键完整性：本项目实际出货的两档（宿主 light / 宿主 dark）逐档装字典后，
            //    38 个令牌的 UI4.Color.* / UI4.Brush.*、三个别名与三个排印键都要查得到。
            //    XAML 里键名写错不报错、不抛异常，只表现为"这处颜色不跟主题"，所以在这里用 TryFindResource 抓成 FAIL。
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                failed += Check(report, "出货档位资源键齐全", false, "Application.Current 为空——这条断言要在 OnStartup 里跑");
            }
            else
            {
                var badKeys = new List<string>();
                foreach (UI4ThemeDefinition def in new[] { Theme.BuildLightDefinition(), Theme.BuildDarkDefinition() })
                {
                    UI4Theme.Register(def);
                    if (!UI4Theme.Apply(def.Key)) { badKeys.Add("apply:" + def.Key); continue; }
                    foreach (UI4ThemeToken t in Enum.GetValues(typeof(UI4ThemeToken)))
                    {
                        if (app.TryFindResource("UI4.Color." + t) == null) badKeys.Add(def.Key + ">UI4.Color." + t);
                        if (app.TryFindResource("UI4.Brush." + t) == null) badKeys.Add(def.Key + ">UI4.Brush." + t);
                    }
                    foreach (string alias in new string[] { "UI4.Brush.Text", "UI4.Brush.Border", "UI4.Brush.Accent" })
                    {
                        if (app.TryFindResource(alias) == null) badKeys.Add(def.Key + ">" + alias);
                    }
                    foreach (string typeKey in new string[] { "UI4.Font.Size.Base", "UI4.Font.Size.Code", "UI4.Font.Family" })
                    {
                        if (app.TryFindResource(typeKey) == null) badKeys.Add(def.Key + ">" + typeKey);
                    }
                }
                failed += Check(report, "出货两档 × 38 令牌 + 别名 + 排印键资源键齐全", badKeys.Count == 0,
                    badKeys.Count == 0 ? "" : badKeys.Count + " 个缺失，前 6 个：" + string.Join(",", badKeys.GetRange(0, Math.Min(6, badKeys.Count))));

                // 7) 字典里的值与宿主定义表必须同源：不一致说明有第二份定义在写同一批键
                var dark = Theme.BuildDarkDefinition();
                UI4Theme.Register(dark);
                UI4Theme.Apply(dark.Key);
                Color dictBg = (Color)app.TryFindResource("UI4.Color.Background");
                Color defBg = dark.GetColor(UI4ThemeToken.Background);
                failed += Check(report, "字典底色与宿主 Dark 定义同源", dictBg == defBg,
                    "字典 " + dictBg + " vs 定义 " + defBg);

                // 7b) 明暗两档的亮度判据要各就各位：深色档混进亮底（或反过来）时，
                //     库按亮度挑字色的通路会跟着挑错，表现是"黑字压黑底"
                Color dictFg = (Color)app.TryFindResource("UI4.Color.TextForeground");
                failed += Check(report, "深色档底色与正文的亮度自洽",
                    Theme.IsDark(dictBg) && !Theme.IsDark(dictFg),
                    "底 " + dictBg + " 正文 " + dictFg);
                UI4Theme.Apply(Theme.BuildLightDefinition().Key);
                Color lightBg = (Color)app.TryFindResource("UI4.Color.Background");
                Color lightFg = (Color)app.TryFindResource("UI4.Color.TextForeground");
                failed += Check(report, "浅色档底色与正文的亮度自洽",
                    !Theme.IsDark(lightBg) && Theme.IsDark(lightFg),
                    "底 " + lightBg + " 正文 " + lightFg);

                // 7c) 深色档的可读性门槛与浅色档同口径（这两组常量是本次新加的，没有历史凭据可抄）
                failed += Check(report, "深色档正文/底色对比度 ≥ 4.5",
                    Contrast(Theme.DarkForeground, Theme.DarkBackground) >= 4.5,
                    Contrast(Theme.DarkForeground, Theme.DarkBackground).ToString("0.00") + ":1");
                failed += Check(report, "深色档强调色对暗底对比度 ≥ 3.0",
                    Contrast(Theme.DarkAccent, Theme.DarkBackground) >= 3.0,
                    Contrast(Theme.DarkAccent, Theme.DarkBackground).ToString("0.00") + ":1");
            }

            // 排印与缩放这段要写在报告里，所以放在捕获文本之前
            failed += RunSettingsSection(report);
            failed += RunDisplaySection(report, app);
            failed += RunCatalogSection(report, app);

            string text = report.ToString();
            TryWrite(text);
            return failed;
        }

        /// <summary>
        /// 设置通路（kv1 纯文本）的自检段。kv1 没有 schema：写侧或读侧键名打错一个字母，
        /// 运行期既不报错也不抛异常，只表现为"这一项设置没存住"。所以这里既测往返，也点名键名。
        /// 整段只碰内存与字符串，不读写 %APPDATA%。
        /// </summary>
        private static int RunSettingsSection(StringBuilder report)
        {
            int failed = 0;

            // 九项各给互不相同的哨兵值：任何一侧键名打错，就有一项在往返后保持默认值而被抓出来
            var src = new SettingsService
            {
                ThemeKey = "dark",
                FontFamilyName = "Cambria",
                BaseFontSize = 17,
                ZoomPercent = 125,
                WindowWidth = 1024,
                WindowHeight = 700,
                WindowLeft = -1200,
                WindowTop = 40,
                WindowMaximized = true
            };
            var back = new SettingsService();
            back.ApplyMap(SettingsCodec.Parse(SettingsCodec.Serialize(src.ToMap())));
            failed += Check(report, "kv1 往返后九项设置不变",
                back.ThemeKey == "dark" && back.FontFamilyName == "Cambria"
                && Math.Abs(back.BaseFontSize - 17) < 1e-9 && Math.Abs(back.ZoomPercent - 125) < 1e-9
                && Math.Abs(back.WindowWidth - 1024) < 1e-9 && Math.Abs(back.WindowHeight - 700) < 1e-9
                && Math.Abs(back.WindowLeft + 1200) < 1e-9 && Math.Abs(back.WindowTop - 40) < 1e-9
                && back.WindowMaximized,
                "实际 " + back.ThemeKey + " / " + back.FontFamilyName + " / " + back.BaseFontSize
                + " / " + back.ZoomPercent + " / " + back.WindowWidth + "x" + back.WindowHeight
                + " @ " + back.WindowLeft + "," + back.WindowTop + " / " + back.WindowMaximized);

            // 写侧键名点名：这些名字就是磁盘格式的一部分，改了就是换格式（老设置会静默失效）
            var expectedKeys = new[]
            {
                "format", "themeKey", "fontFamilyName", "baseFontSize", "zoomPercent",
                "windowWidth", "windowHeight", "windowLeft", "windowTop", "windowMaximized"
            };
            var written = new List<string>();
            foreach (var kv in src.ToMap()) written.Add(kv.Key);
            var missing = new List<string>();
            foreach (string key in expectedKeys) if (!written.Contains(key)) missing.Add(key);
            failed += Check(report, "kv1 写侧键名齐全（格式是稳定契约）", missing.Count == 0,
                "缺 " + string.Join(",", missing.ToArray()));

            // 值原样存、不转义，所以"含 = / 含反斜杠 / 含中文 / 首尾空格 / 空串"这几类必须各判一次
            var hostiles = new[] { "a=b=c", "C:\\Users\\me\\数据", "#不是注释因为在前头", "  两端空格  ", "", "Cambria" };
            foreach (string value in hostiles)
            {
                var one = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("probe", value)
                };
                var parsed = SettingsCodec.Parse(SettingsCodec.Serialize(one));
                string got;
                parsed.TryGetValue("probe", out got);
                string want = (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0) ? string.Empty : value.Trim();
                failed += Check(report, "往返保持值 [" + value + "]", got == want,
                    "得到 [" + got + "]，期望 [" + want + "]");
            }

            var crlf = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("probe", "第一行\r\n第二行")
            };
            failed += Check(report, "含 CR/LF 的值被拒绝而不是撑破行格式",
                !SettingsCodec.Serialize(crlf).Contains("第二行"),
                "值里带换行会多出一行没有 key= 的垃圾");

            // 越界钳回区间、非法值不覆盖已生效值：设置文件是用户能自己开编辑器改的
            var hostile2 = new SettingsService { ThemeKey = "light", WindowWidth = 980 };
            hostile2.ApplyMap(SettingsCodec.Parse(SettingsCodec.Serialize(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("baseFontSize", "999"),
                new KeyValuePair<string, string>("zoomPercent", "-5"),
                new KeyValuePair<string, string>("windowWidth", "abc"),
                new KeyValuePair<string, string>("windowLeft", "-40000"),
                new KeyValuePair<string, string>("themeKey", "   ")
            })));
            failed += Check(report, "越界字号与缩放被钳回区间",
                Math.Abs(hostile2.BaseFontSize - Typography.MaxBaseSize) < 1e-9
                && Math.Abs(hostile2.ZoomPercent - Typography.MinZoomPercent) < 1e-9,
                hostile2.BaseFontSize + " / " + hostile2.ZoomPercent);
            failed += Check(report, "非法值不覆盖已生效值（保留默认）",
                Math.Abs(hostile2.WindowWidth - 980) < 1e-9 && hostile2.ThemeKey == "light",
                "windowWidth=" + hostile2.WindowWidth + " themeKey=[" + hostile2.ThemeKey + "]");
            // 屏外坐标（最小化时 Windows 会给 -32000 一类值）宁可丢掉也不存住，否则下次启动窗口边都摸不到
            failed += Check(report, "超出合理范围的窗口坐标被拒绝",
                double.IsNaN(hostile2.WindowLeft), "windowLeft=" + hostile2.WindowLeft);

            // 空表与 null 不能把现值清掉：Load 走的是 ApplyDefaults + ApplyMap，坏文件路径上会喂进空 map
            var hostile3 = new SettingsService { ThemeKey = "dark", BaseFontSize = 20 };
            hostile3.ApplyMap(null);
            hostile3.ApplyMap(new Dictionary<string, string>());
            failed += Check(report, "null / 空 map 不清现值",
                hostile3.ThemeKey == "dark" && Math.Abs(hostile3.BaseFontSize - 20) < 1e-9,
                hostile3.ThemeKey + " / " + hostile3.BaseFontSize);

            return failed;
        }

        /// <summary>
        /// 排印与缩放这条通路全程是运行期字符串：键名写错既不报编译错也不抛异常，只表现为
        /// "改了字号没反应"，面板 XAML 的解析错误原本只在"点开设置"那一刻才炸。所以这段自己出题。
        /// 放在最后跑：前面几组会换档重写字典，而这里第一条要看的正是"宿主还没覆盖时"的库兜底值。
        /// </summary>
        private static int RunDisplaySection(StringBuilder report, System.Windows.Application app)
        {
            int failed = 0;
            if (app == null)
            {
                return Check(report, "排印段可跑", false, "Application.Current 为空——这段要在 OnStartup 里跑");
            }

            const string BaseKey = "UI4.Font.Size.Base";
            const string CodeKey = "UI4.Font.Size.Code";
            const string FamilyKey = "UI4.Font.Family";

            // 1) 库必须发布这三个键的兜底值。缺键的话宿主不覆盖时所有 UI4* 控件静默掉到 WPF 裸默认 12 px
            object baseDefault = app.TryFindResource(BaseKey);
            object codeDefault = app.TryFindResource(CodeKey);
            var familyDefault = app.TryFindResource(FamilyKey) as FontFamily;
            failed += Check(report, "库发布 UI4.Font.Size.Base 兜底值",
                baseDefault is double && Math.Abs((double)baseDefault - UI4Theme.DefaultFontSizeBase) < 1e-9,
                "解析到 " + (baseDefault == null ? "空" : baseDefault.ToString()));
            failed += Check(report, "库发布 UI4.Font.Size.Code 兜底值",
                codeDefault is double && Math.Abs((double)codeDefault - UI4Theme.DefaultFontSizeCode) < 1e-9,
                "解析到 " + (codeDefault == null ? "空" : codeDefault.ToString()));
            failed += Check(report, "库发布 UI4.Font.Family 兜底值",
                familyDefault != null && familyDefault.Source.Contains("Segoe UI"),
                "解析到 " + (familyDefault == null ? "空" : familyDefault.Source));

            // 2) 宿主覆盖写在应用资源根的自有项上，换档（库原地重写 MergedDictionaries 里那份）冲不掉。
            //    反过来若写进共享字典内部就会被下次重写顶掉——这条是实测结论，不是从文档推的。
            app.Resources[BaseKey] = 20d;
            ThemeService.Apply(ThemeService.Dark);
            object afterSwitch = app.TryFindResource(BaseKey);
            failed += Check(report, "宿主覆盖的字号在换档后仍解析到宿主值",
                afterSwitch is double && Math.Abs((double)afterSwitch - 20d) < 1e-9,
                "期望 20，实际 " + afterSwitch);
            app.Resources.Remove(BaseKey);
            object afterRevert = app.TryFindResource(BaseKey);
            failed += Check(report, "撤掉宿主覆盖后回到库默认",
                afterRevert is double && Math.Abs((double)afterRevert - UI4Theme.DefaultFontSizeBase) < 1e-9,
                "解析到 " + (afterRevert == null ? "空" : afterRevert.ToString()));
            ThemeService.Apply(ThemeService.Light);

            // 3) Publish 必须把每个层级键与固定件尺寸键都写进去，且半径是 CornerRadius 而不是 double
            Typography.Publish(app, string.Empty, Typography.DefaultBaseSize);
            var expectedKeys = new[]
            {
                new { Key = BaseKey, Size = Typography.DefaultBaseSize },
                new { Key = CodeKey, Size = Typography.SizeOf("Code", Typography.DefaultBaseSize) },
                new { Key = "App.Font.Size.Caption", Size = Typography.SizeOf("Caption", Typography.DefaultBaseSize) },
                new { Key = "App.Font.Size.Small", Size = Typography.SizeOf("Small", Typography.DefaultBaseSize) },
                new { Key = "App.Font.Size.Medium", Size = Typography.SizeOf("Medium", Typography.DefaultBaseSize) },
                new { Key = "App.Font.Size.Lead", Size = Typography.SizeOf("Lead", Typography.DefaultBaseSize) },
                new { Key = "App.Font.Size.Icon", Size = Typography.SizeOf("Icon", Typography.DefaultBaseSize) },
                new { Key = "App.Size.RoundButton", Size = Typography.RoundButtonSize(Typography.DefaultBaseSize) },
                new { Key = "App.Size.Chip", Size = Typography.ChipHeight(Typography.DefaultBaseSize) },
                new { Key = "App.Size.NavRail", Size = Typography.NavRailWidth(Typography.DefaultBaseSize) },
                new { Key = "App.Size.SearchBox", Size = Typography.SearchBoxWidth(Typography.DefaultBaseSize) }
            };
            foreach (var want in expectedKeys)
            {
                object got = app.TryFindResource(want.Key);
                failed += Check(report, "发布后 " + want.Key + " 可解析",
                    got is double && Math.Abs((double)got - want.Size) < 1e-9,
                    want.Size + " vs " + (got == null ? "空" : got.ToString()));
            }
            failed += Check(report, "半径键是 CornerRadius 而不是 double",
                app.TryFindResource("App.Radius.RoundButton") is CornerRadius
                && app.TryFindResource("App.Radius.Chip") is CornerRadius,
                "DynamicResource 不做类型转换，挂 double 到 CornerRadius 上会在运行期炸");
            failed += Check(report, "发布后字体族键是 FontFamily",
                app.TryFindResource(FamilyKey) is FontFamily, "");

            // 自检不留覆盖值，免得后面的段（或真实启动路径）读到自检写进去的东西
            foreach (var want in expectedKeys) app.Resources.Remove(want.Key);
            app.Resources.Remove("App.Radius.RoundButton");
            app.Resources.Remove("App.Radius.Chip");
            app.Resources.Remove(FamilyKey);

            // 4) 字号层级：基准 15 时阶梯必须等于界面既有的字面数字，且单调、有下限
            var ladder = new[]
            {
                new { Role = "Caption", Want = 11.0 },
                new { Role = "Small", Want = 12.0 },
                new { Role = "Medium", Want = 13.0 },
                new { Role = "Lead", Want = 14.0 },
                new { Role = "Icon", Want = 16.0 }
            };
            foreach (var step in ladder)
            {
                double got = Typography.SizeOf(step.Role, Typography.DefaultBaseSize);
                failed += Check(report, "层级 " + step.Role + " 在基准 15 下是 " + step.Want,
                    Math.Abs(got - step.Want) < 1e-9, "实际 " + got);
            }
            foreach (double b in new[] { Typography.MinBaseSize, Typography.DefaultBaseSize, Typography.MaxBaseSize })
            {
                failed += Check(report, "层级单调（基准 " + b + "）",
                    Typography.SizeOf("Caption", b) <= Typography.SizeOf("Small", b)
                    && Typography.SizeOf("Small", b) <= Typography.SizeOf("Medium", b)
                    && Typography.SizeOf("Medium", b) <= Typography.SizeOf("Lead", b),
                    "把层级抹平成同一个基准就是这里会红的来源");
            }
            failed += Check(report, "未知层级名退回基准本身而不是崩",
                Math.Abs(Typography.SizeOf("不存在的层级", 15) - 15) < 1e-9, "");

            // 5) 固定件尺寸：默认基准下必须仍是 28 / 24（这条是"平移不是改设计"的凭据），
            //    且 12–28 全区间都装得下对应字身（否则字号一大，钉死的圆钮会把内容挤成"偏到右下"）
            failed += Check(report, "默认基准下圆钮直径 28",
                Math.Abs(Typography.RoundButtonSize(Typography.DefaultBaseSize)
                         - Typography.DefaultRoundButtonSize) < 1e-9,
                Typography.RoundButtonSize(Typography.DefaultBaseSize).ToString());
            failed += Check(report, "默认基准下胶囊高度 24",
                Math.Abs(Typography.ChipHeight(Typography.DefaultBaseSize)
                         - Typography.DefaultChipHeight) < 1e-9,
                Typography.ChipHeight(Typography.DefaultBaseSize).ToString());
            bool sizesFit = true;
            string sizeDetail = "";
            for (double b = Typography.MinBaseSize; b <= Typography.MaxBaseSize; b += 1)
            {
                if (Typography.RoundButtonSize(b) < Typography.SizeOf("Icon", b) * 1.3
                    || Typography.ChipHeight(b) < Typography.SizeOf("Caption", b) * 1.3)
                {
                    sizesFit = false;
                    sizeDetail = "基准 " + b + " 时圆钮 " + Typography.RoundButtonSize(b)
                                 + " / 胶囊 " + Typography.ChipHeight(b) + " 装不下字身";
                    break;
                }
            }
            failed += Check(report, "12–28 全区间固定件都装得下字身", sizesFit, sizeDetail);

            // 5b) 左栏与搜索框的宽度也要随字号长（钉死值在字号 28 下会把分组名切成一个字、
            //     把占位文案切在一半）。默认基准下必须仍是界面上沿用已久的 112 / 320。
            failed += Check(report, "默认基准下左栏 112 搜索框 320",
                Math.Abs(Typography.NavRailWidth(Typography.DefaultBaseSize) - Typography.DefaultNavRailWidth) < 1e-9
                && Math.Abs(Typography.SearchBoxWidth(Typography.DefaultBaseSize) - Typography.DefaultSearchBoxWidth) < 1e-9,
                Typography.NavRailWidth(15) + " / " + Typography.SearchBoxWidth(15));
            bool widthsGrow = true; string widthDetail = "";
            double prevRail = 0, prevBox = 0;
            for (double b = Typography.MinBaseSize; b <= Typography.MaxBaseSize; b += 1)
            {
                double rail = Typography.NavRailWidth(b), box = Typography.SearchBoxWidth(b);
                if (rail < prevRail || box < prevBox) { widthsGrow = false; widthDetail = "基准 " + b + " 处回缩"; break; }
                if (rail < Typography.SizeOf("Small", b) * Typography.NavLabelChars)
                {
                    widthsGrow = false;
                    widthDetail = "基准 " + b + " 时左栏 " + rail + " 放不下 "
                                  + Typography.NavLabelChars + " 个字（" + Typography.SizeOf("Small", b) + "/字）";
                    break;
                }
                prevRail = rail; prevBox = box;
            }
            failed += Check(report, "12–28 全区间左栏容得下最长分组名且宽度单调", widthsGrow, widthDetail);
            failed += Check(report, "字号 28 时左栏确实长于默认 112",
                Typography.NavRailWidth(Typography.MaxBaseSize) > Typography.DefaultNavRailWidth,
                "实际 " + Typography.NavRailWidth(Typography.MaxBaseSize));

            // 6) 区间钳位：设置文件是用户能自己开编辑器改的，越界与非法值不能让界面直接吃下去
            failed += Check(report, "ClampBase 钳上下限并让 NaN/Infinity 回默认",
                Math.Abs(Typography.ClampBase(7) - Typography.MinBaseSize) < 1e-9
                && Math.Abs(Typography.ClampBase(33) - Typography.MaxBaseSize) < 1e-9
                && Math.Abs(Typography.ClampBase(double.NaN) - Typography.DefaultBaseSize) < 1e-9
                && Math.Abs(Typography.ClampBase(double.PositiveInfinity) - Typography.DefaultBaseSize) < 1e-9,
                "7→" + Typography.ClampBase(7) + " 33→" + Typography.ClampBase(33));
            failed += Check(report, "ClampZoom 钳 50–200",
                Math.Abs(Typography.ClampZoom(10) - Typography.MinZoomPercent) < 1e-9
                && Math.Abs(Typography.ClampZoom(500) - Typography.MaxZoomPercent) < 1e-9
                && Math.Abs(Typography.ClampZoom(-1) - Typography.MinZoomPercent) < 1e-9, "");
            failed += Check(report, "空/非法字体名回退出厂栈",
                Typography.SafeFamily(null).Source == Typography.DefaultFontFamilySource
                && Typography.SafeFamily("   ").Source == Typography.DefaultFontFamilySource, "");

            // 7) 字体候选表的契约：出厂项固定第 0 位（ComboBox 挂了 ItemsSource 后，
            //    把 SelectedItem 设成列表外的值会被静默清空，"恢复默认后下拉框不跟着变"就是这么来的）
            var only = Typography.BuildFamilyChoices(null);
            failed += Check(report, "候选表为空时只剩出厂项",
                only.Count == 1 && only[0] == Typography.DefaultFontFamilySource, "实际 " + only.Count + " 项");
            var messy = Typography.BuildFamilyChoices(new[]
            {
                "Segoe UI", Typography.DefaultFontFamilySource, "Arial", "   ", "Segoe UI", null, "Cambria"
            });
            failed += Check(report, "候选表＝出厂项首位 + 去重 + 丢空白 + 其余按序",
                messy.Count == 4 && messy[0] == Typography.DefaultFontFamilySource
                && messy[1] == "Arial" && messy[2] == "Cambria" && messy[3] == "Segoe UI",
                string.Join(" | ", messy.ToArray()));
            bool defaultConstructs;
            string constructDetail = "";
            try { defaultConstructs = new FontFamily(Typography.DefaultFontFamilySource).Source == Typography.DefaultFontFamilySource; }
            catch (Exception ex) { defaultConstructs = false; constructDetail = ex.GetType().Name + " " + ex.Message; }
            failed += Check(report, "出厂字体栈可当 FontFamily 用", defaultConstructs, constructDetail);

            // 8) 缩放换算器的边界：参数写错必须回 UnsetValue，静默接受等于把窗口下限变成 0
            var conv = new Converters.ZoomedSizeConverter();
            var work = SystemParameters.WorkArea;
            object scaled = conv.Convert(1.5, typeof(double), "W:900", System.Globalization.CultureInfo.InvariantCulture);
            failed += Check(report, "W:900 在 150% 下折算正确",
                scaled is double && Math.Abs((double)scaled - Math.Min(1350, work.Width)) < 1e-9, "实际 " + scaled);
            object capped = conv.Convert(2.0, typeof(double), "W:99999", System.Globalization.CultureInfo.InvariantCulture);
            failed += Check(report, "窗口下限越过工作区时被钳住",
                capped is double && Math.Abs((double)capped - work.Width) < 1e-9, capped + " vs " + work.Width);
            failed += Check(report, "参数缺 W:/H: 前缀时回 UnsetValue",
                Equals(conv.Convert(1.0, typeof(double), "900", System.Globalization.CultureInfo.InvariantCulture),
                       DependencyProperty.UnsetValue), "");
            failed += Check(report, "参数里的像素值不是数字时回 UnsetValue",
                Equals(conv.Convert(1.0, typeof(double), "W:abc", System.Globalization.CultureInfo.InvariantCulture),
                       DependencyProperty.UnsetValue), "");

            // 9) 明暗策略与可选档自洽：单档策略下不许出现反方向的档（"只做浅色"却给一个深色按钮，
            //    点下去就是一份没人承诺的状态）；策略还没定时第 5 组已经红了，这里不再重复报。
            var keys = ThemeService.AvailableKeys();
            bool lightOnly = string.Equals(Theme.Policy, "light-only", StringComparison.OrdinalIgnoreCase);
            bool darkOnly = string.Equals(Theme.Policy, "dark-only", StringComparison.OrdinalIgnoreCase);
            failed += Check(report, "light-only 策略下不提供深色档",
                !lightOnly || !keys.Contains(ThemeService.Dark),
                "Policy=light-only 但档位里有 " + string.Join(",", keys.ToArray()));
            failed += Check(report, "dark-only 策略下不提供浅色档",
                !darkOnly || !keys.Contains(ThemeService.Light),
                "Policy=dark-only 但档位里有 " + string.Join(",", keys.ToArray()));
            failed += Check(report, "至少有一档可选且首档能应用",
                keys.Count > 0 && ThemeService.Apply(keys[0]),
                "档位=" + string.Join(",", keys.ToArray()));
            ThemeService.Apply(ThemeService.Light);

            // 10) 两块浮层/主窗 XAML 能在运行期解析：只构造不 Show，BAML 加载就发生在构造函数里。
            //     这里用到了 x:Static 引常量、DynamicResource 引层级键、带参数的转换器与 DataTemplate，
            //     这类错误只在窗口拉起或"点开设置"那一刻以 XamlParseException 出现——没人点的话它就是"点了没反应"。
            try { failed += Check(report, "设置面板 XAML 可解析", new Views.SettingsOverlay() != null, ""); }
            catch (Exception ex) { failed += Check(report, "设置面板 XAML 可解析", false, ex.GetType().Name + " " + ex.Message); }
            try { failed += Check(report, "主窗口 XAML 可解析", new MainWindow() != null, ""); }
            catch (Exception ex) { failed += Check(report, "主窗口 XAML 可解析", false, ex.GetType().Name + " " + ex.Message); }

            report.AppendLine("INFO 字号阶梯：基准=" + Typography.DefaultBaseSize
                + " → 胶囊=" + Typography.SizeOf("Caption", Typography.DefaultBaseSize)
                + " 标签=" + Typography.SizeOf("Small", Typography.DefaultBaseSize)
                + " 次要=" + Typography.SizeOf("Medium", Typography.DefaultBaseSize)
                + " 正文=" + Typography.DefaultBaseSize
                + " 提示=" + Typography.SizeOf("Lead", Typography.DefaultBaseSize)
                + " 图标=" + Typography.SizeOf("Icon", Typography.DefaultBaseSize)
                + "；固定件 基准15 圆钮=" + Typography.RoundButtonSize(15) + " 胶囊=" + Typography.ChipHeight(15)
                + "，基准28 圆钮=" + Typography.RoundButtonSize(28) + " 胶囊=" + Typography.ChipHeight(28));
            return failed;
        }

        /// <summary>
        /// 数据源这段是本项目特有的门禁。命令表是"抄来的知识"，抄错一个字母就是运行期
        /// "Windows 找不到文件"，而界面照常显示、不报任何错——所以把调研结论里最容易回退的
        /// 几件事钉成断言：黑名单名字、SKU 集合、探测挡位、字段完整性、过滤通路、模板可构造。
        /// </summary>
        private static int RunCatalogSection(StringBuilder report, System.Windows.Application app)
        {
            int failed = 0;
            var items = ToolCatalog.Items;

            // 1) 分组结构：10 个分组，「全部工具」在首位且收录所有项
            var sections = ToolCatalog.BuildSections();
            failed += Check(report, "分组数为 10（不含全部）", ToolCatalog.Groups.Length == 10,
                "实际 " + ToolCatalog.Groups.Length);
            failed += Check(report, "首位是全部工具且收录完整",
                sections.Count == ToolCatalog.Groups.Length + 1
                && sections[0].Key == ToolCatalog.AllKey
                && sections[0].Items.Count == items.Length,
                "sections=" + sections.Count + " 首项=" + sections[0].Key + " 项数=" + sections[0].Items.Count
                + " vs 总数 " + items.Length);

            // 2) 每组容量要适合左栏一屏：5–12 项；总量 ≥ 70
            var badSized = new List<string>();
            foreach (var section in sections.Skip(1))
                if (section.Items.Count < 5 || section.Items.Count > 12) badSized.Add(section.Key + ":" + section.Items.Count);
            failed += Check(report, "每个分组 5–12 项", badSized.Count == 0, string.Join(",", badSized.ToArray()));
            failed += Check(report, "总项数 ≥ 70", items.Length >= 70, "实际 " + items.Length);

            // 3) 字段完整性：卡片要同时有名字、命令与一句读得懂的介绍
            var badField = new List<string>();
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Title)) badField.Add(item.Command + ">无标题");
                if (string.IsNullOrWhiteSpace(item.Command)) badField.Add(item.Title + ">无命令");
                int len = item.Description == null ? 0 : item.Description.Trim().Length;
                if (len < 10 || len > 48) badField.Add(item.Title + ">介绍长度 " + len);
                if (string.IsNullOrEmpty(item.Group)) badField.Add(item.Title + ">无分组");
            }
            failed += Check(report, "每项标题/命令/分组齐全且介绍 10–48 字", badField.Count == 0,
                badField.Count == 0 ? "" : badField.Count + " 处，前 6：" + string.Join(" | ", badField.GetRange(0, Math.Min(6, badField.Count)).ToArray()));

            // 4) 命令唯一：重复项会让 UIA 按名字找卡片时命中两张，也让用户以为程序出错
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dup = new List<string>();
            foreach (var item in items) if (!seen.Add(item.Command)) dup.Add(item.Command);
            failed += Check(report, "命令原文全局唯一", dup.Count == 0, string.Join(",", dup.ToArray()));

            // 5) 探测挡位自洽：文件型入口必须能探，URI/shell 型不该去探。
            //    "文件型"看的是**真正被启动的那个文件**（LaunchService 切出来的第一段），不是命令尾巴——
            //    2026-10-04 加「重建图标缓存」时按整条命令的结尾判，被 `… & start explorer.exe` 这种
            //    尾巴带 .exe 的组合命令误判成文件型（探测名根本不知道该填谁）。
            var badProbe = new List<string>();
            foreach (var item in items)
            {
                string file, args;
                LaunchService.Split(item.Command, out file, out args);
                bool fileLike = file.EndsWith(".cpl", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".msc", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                if (fileLike && (item.Probe == ToolProbe.None || string.IsNullOrEmpty(item.ProbeName)))
                    badProbe.Add(item.Command + ">启动的是文件却没给探测名");
                if (!fileLike && item.Probe != ToolProbe.None)
                    badProbe.Add(item.Command + ">启动的不是文件却设了探测");
                if (fileLike && !string.IsNullOrEmpty(item.ProbeName)
                    && !file.EndsWith(item.ProbeName, StringComparison.OrdinalIgnoreCase))
                    badProbe.Add(item.Command + ">探测名与启动的文件对不上（" + item.ProbeName + "）");
            }
            failed += Check(report, "探测挡位与启动文件自洽", badProbe.Count == 0, string.Join(" | ", badProbe.ToArray()));

            // 6) ProbePath 三挡拼接（拿假根目录出题，不碰磁盘）
            string s32 = ToolCatalog.ProbePath(new ToolItem { Probe = ToolProbe.System32File, ProbeName = "services.msc" }, "D:\\FakeRoot");
            string wr = ToolCatalog.ProbePath(new ToolItem { Probe = ToolProbe.WindowsFile, ProbeName = "regedit.exe" }, "D:\\FakeRoot");
            string none = ToolCatalog.ProbePath(new ToolItem { Probe = ToolProbe.None }, "D:\\FakeRoot");
            failed += Check(report, "ProbePath 按挡位拼路径",
                s32 == Path.Combine("D:\\FakeRoot", "System32", "services.msc")
                && wr == Path.Combine("D:\\FakeRoot", "regedit.exe")
                && none == null,
                s32 + " / " + wr + " / " + (none ?? "null"));

            // 7) regedit 的实测坑回归：它住在 %Windows% 根，拼进 System32 永远探不到
            var regedit = FindByCommand(items, "C:\\Windows\\regedit.exe");
            failed += Check(report, "regedit 用绝对路径且走 WindowsFile 挡",
                regedit != null && regedit.Probe == ToolProbe.WindowsFile && regedit.ProbeName == "regedit.exe",
                regedit == null ? "数据源里没有 regedit 这一项" : regedit.Command + " / " + regedit.Probe);

            // 8) 黑名单：社区命令表里流传、但本机实测不存在的名字，一个都不许进来
            var banned = new[]
            {
                "input.cpl", "access.cpl", "multimed.cpl", "driversdf.cpl", "wu.cpl", "netcfg.cpl",
                "nusrmgr.cpl", "netsetup.cpl", "telephonc.cpl", "fstrim.msc", "sr.msc", "rsmgr.msc",
                "storageSpaces.msc", "iscsitg.msc", "wmic.exe", "wt.exe", "pwsh.exe"
            };
            var hits = new List<string>();
            foreach (var item in items)
                foreach (string bad in banned)
                    if (item.Command.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(item.Command + ">" + bad);
            failed += Check(report, "不含实测不存在的常见误传命令", hits.Count == 0, string.Join(" | ", hits.ToArray()));

            // 9) SKU 差异是独立字段：家庭版没有的那批必须点名一致，多标少标都算红
            var wantHome = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "gpedit.msc", "secpol.msc", "lusrmgr.msc", "wf.msc", "fsmgmt.msc", "certlm.msc", "rsop.msc" };
            var gotHome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items) if (item.HomeEditionMissing) gotHome.Add(item.Command);
            var homeDiff = new List<string>();
            foreach (string k in wantHome) if (!gotHome.Contains(k)) homeDiff.Add("缺:" + k);
            foreach (string k in gotHome) if (!wantHome.Contains(k)) homeDiff.Add("多:" + k);
            failed += Check(report, "家庭版缺失项点名一致（7 项）", homeDiff.Count == 0, string.Join(",", homeDiff.ToArray()));

            // 10) 过滤口径五例
            var sample = FindByCommand(items, "services.msc");
            failed += Check(report, "Matches 命中中文标题/命令/介绍且大小写不敏感",
                ToolCatalog.Matches(sample, "服务") && ToolCatalog.Matches(sample, "SERVICES")
                && ToolCatalog.Matches(sample, "启动类型") && ToolCatalog.Matches(sample, ""),
                "样例 " + (sample == null ? "缺失" : sample.Title));
            failed += Check(report, "Matches 对不存在的词返回 false",
                !ToolCatalog.Matches(sample, "绝不可能出现的词"), "");

            // 11) 过滤真的接到了视图上：命中数、空态、以及换词后的文案。
            //     取样词用 regedit（在 run 组里只命中注册表编辑器一条）——别用 "cmd"，
            //     2026-10-04 起 run 组里有多条以 cmd /k 起头的组合命令，命中数不再是 1。
            var run = sections.Find(s => s.Key == "run");
            run.SetQuery("regedit");
            bool oneHit = !run.HasNoMatches && run.MatchCaption.Contains("命中 1");
            run.SetQuery("绝不可能出现的词");
            bool zeroHit = run.HasNoMatches && run.MatchCaption.Contains("命中 0");
            run.SetQuery(string.Empty);
            bool restored = !run.HasNoMatches && run.ItemsView.Cast<object>().Count() == run.Items.Count
                            && run.MatchCaption.StartsWith("共 ");
            failed += Check(report, "分组视图随查询词过滤并可复原", oneHit && zeroHit && restored,
                "one=" + oneHit + " zero=" + zeroHit + " restored=" + restored);

            // 12) 图标码位：非空、单字符、落在私有区（渲染成豆腐块的那类问题在这里就能挡下）
            var badGlyph = new List<string>();
            foreach (var g in ToolCatalog.Groups) if (!IsSinglePua(g.Glyph)) badGlyph.Add(g.Key);
            if (!IsSinglePua(ToolCatalog.AllGlyph)) badGlyph.Add("all");
            failed += Check(report, "分组图标都是单个私有区码位", badGlyph.Count == 0, string.Join(",", badGlyph.ToArray()));

            // 13) 卡片上的分组标签：数据源里每项都要带所属分组标题，显示时机由 Section 判定
            var badGroupTitle = new List<string>();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.GroupTitle)) badGroupTitle.Add(item.Title + ">无分组标题");
                else if (item.GroupTitle != ToolCatalog.GroupTitleOf(item.Group))
                    badGroupTitle.Add(item.Title + ">" + item.GroupTitle + "≠" + ToolCatalog.GroupTitleOf(item.Group));
            }
            failed += Check(report, "每项都带与所属分组一致的分组标题", badGroupTitle.Count == 0,
                badGroupTitle.Count == 0 ? "" : string.Join(" | ", badGroupTitle.GetRange(0, Math.Min(6, badGroupTitle.Count)).ToArray()));

            var allSec = sections[0];
            var ipSec = sections.Find(s => s.Key == "ip");
            failed += Check(report, "「全部工具」显示分组标签，单分组不显示",
                allSec.ShowGroupTags && ipSec != null && !ipSec.ShowGroupTags,
                "all=" + allSec.ShowGroupTags + " ip=" + (ipSec == null ? "无此组" : ipSec.ShowGroupTags.ToString()));
            if (ipSec != null)
            {
                ipSec.SetQuery("ipconfig");
                bool tagOnSearch = ipSec.ShowGroupTags;
                ipSec.SetQuery(string.Empty);
                failed += Check(report, "搜索中标签打开、清空后回到关",
                    tagOnSearch && !ipSec.ShowGroupTags, "搜索中=" + tagOnSearch);
            }

            // 13b) IP 与 DNS 这一组：命令必须还是那批实测过的形态，且都不设探测
            //      （cmd /k 与 powershell 起头的命令不以 .exe 结尾，按第 5 条口径设了探测就红）
            failed += Check(report, "IP 与 DNS 组收齐 11 项且都不探测",
                ipSec != null && ipSec.Items.Count == 11
                && CountWhere(ipSec.Items.ToArray(), i => i.Probe != ToolProbe.None) == 0,
                ipSec == null ? "整组缺失" : "项数=" + ipSec.Items.Count);
            var wantIpCommands = new[] { "ipconfig", "/all", "/displaydns", "/flushdns", "nslookup",
                                         "netstat -ano", "arp -a", "route print", "netsh wlan",
                                         "Get-DnsClientServerAddress", "Get-NetIPAddress" };
            var missingIp = new List<string>();
            var ipItems = ipSec == null ? new List<Models.ToolItem>() : new List<Models.ToolItem>(ipSec.Items);
            foreach (string frag in wantIpCommands)
            {
                bool hit = false;
                foreach (var item in ipItems)
                    if (item.Command.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                if (!hit) missingIp.Add(frag);
            }
            failed += Check(report, "IP 与 DNS 组点名齐全（实测跑通的那 11 条）", missingIp.Count == 0,
                string.Join(",", missingIp.ToArray()));
            failed += Check(report, "IP 组里没有误标「需管理员」的项（本机 flushdns 非管理员即成功）",
                ipSec != null && CountWhere(ipSec.Items.ToArray(), i => i.RequiresAdmin) == 0,
                ipSec == null ? "整组缺失" : "需管理员=" + CountWhere(ipSec.Items.ToArray(), i => i.RequiresAdmin));

            // 13d) 桌面图标缓存那两条：差别要看得见（介绍里一句 + 悬浮里长文），力度别标错
            var iconRefresh = FindByCommand(items, "ie4uinit.exe -show");
            var iconRebuild = FindByCommand(items, "cmd /k taskkill /f /im explorer.exe & del /q \"%localappdata%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\" \"%localappdata%\\IconCache.db\" 2>nul & start explorer.exe");
            failed += Check(report, "图标缓存两条都在且分在 run 组",
                iconRefresh != null && iconRebuild != null
                && iconRefresh.Group == "run" && iconRebuild.Group == "run",
                "刷新=" + (iconRefresh == null ? "缺" : "有") + " 重建=" + (iconRebuild == null ? "缺" : "有"));
            failed += Check(report, "两条的悬浮说明都写了且互不相同（差别要能展开读）",
                iconRefresh != null && iconRebuild != null
                && !string.IsNullOrEmpty(iconRefresh.DetailNote) && !string.IsNullOrEmpty(iconRebuild.DetailNote)
                && iconRefresh.DetailNote != iconRebuild.DetailNote,
                "长度=" + (iconRefresh == null ? 0 : iconRefresh.DetailNote.Length) + "/" + (iconRebuild == null ? 0 : iconRebuild.DetailNote.Length));
            failed += Check(report, "只有重建那条打危险标签；刷新探 ie4uinit，重建以 cmd 起头不探；两条都免管理员",
                iconRefresh != null && iconRebuild != null
                && string.IsNullOrEmpty(iconRefresh.DangerNote) && !string.IsNullOrEmpty(iconRebuild.DangerNote)
                && iconRefresh.Probe == ToolProbe.System32File && iconRefresh.ProbeName == "ie4uinit.exe"
                && iconRebuild.Probe == ToolProbe.None
                && !iconRefresh.RequiresAdmin && !iconRebuild.RequiresAdmin,
                "刷新 probe=" + (iconRefresh == null ? "?" : iconRefresh.ProbeName) + " 重建 danger=[" + (iconRebuild == null ? "?" : iconRebuild.DangerNote) + "]");
            failed += Check(report, "DetailTip 通路：写了 DetailNote 就用它，没写的仍是通用提示",
                iconRefresh != null && iconRefresh.DetailTip == iconRefresh.DetailNote
                && sample != null && sample.DetailTip == "运行框里的原文，点击卡片即执行",
                "样例=" + (sample == null ? "?" : sample.DetailTip));

            // 13c) 常驻右栏：Activate 只改 IsActive，任何时刻最多一节可见
            var shellVm = new QuickPanel.ViewModels.SectionShell(sections);
            shellVm.Activate("ip");
            int onCount = 0;
            foreach (var s in sections) if (s.IsActive) onCount++;
            bool onlyIp = onCount == 1 && ipSec != null && ipSec.IsActive;
            shellVm.Activate("ip");
            bool idempotent = onCount == 1 && ipSec != null && ipSec.IsActive;
            shellVm.Activate(null);
            int afterNull = 0;
            foreach (var s in sections) if (s.IsActive) afterNull++;
            failed += Check(report, "切分组只点亮一节，重复切与未知键都不留两节",
                onlyIp && idempotent && afterNull == 0,
                "点亮=" + onCount + " 重复后=" + idempotent + " 未知键后=" + afterNull);

            // 14) 三块模板要能实例化。模板在 Window.Resources 里（模板中的 SelectionChanged 只能解析到
            //     宿主类），所以要先有主窗口实例再查——查不到就退回"右栏一片空白"这种没有异常的症状。
            //     主窗口与设置面板 XAML 能否构造，由排印段末尾那两条负责，这里不重复报。
            try
            {
                var shell = new MainWindow();
                failed += Check(report, "卡片模板可实例化",
                    CanLoadTemplate(shell.Resources, "ToolCard"), "查不到 ToolCard 或实例化即抛");
                failed += Check(report, "分组模板可实例化",
                    CanLoadTemplate(shell.Resources, new System.Windows.DataTemplateKey(typeof(QuickPanel.ViewModels.ToolSection))),
                    "查不到分组模板或实例化即抛");
                failed += Check(report, "右栏常驻模板可实例化",
                    CanLoadTemplate(shell.Resources, new System.Windows.DataTemplateKey(typeof(QuickPanel.ViewModels.SectionShell))),
                    "查不到 SectionShell 模板或实例化即抛");

                // 14b) 右栏不重建的前提：每个导航项的 Content 必须是同一个 shell 实例。
                //      容器报出去的 UIA 名字走"数据项的纯文本"，所以名字要钉在 NavSectionItem.ToString() 上。
                var navVm = shell.DataContext as QuickPanel.ViewModels.MainViewModel;
                var navItems = shell.Nav.Items;
                bool sharedContent = navVm != null && navVm.Shell != null
                    && navItems.Count == navVm.Sections.Count && navItems.Count == 11;
                var badNavName = new List<string>();
                for (int i = 0; i < navItems.Count; i++)
                {
                    var navItem = navItems[i] as NavSectionItem;
                    string want = navVm == null ? "?" : navVm.Sections[i].Title;
                    if (navItem == null || navVm == null || !ReferenceEquals(navItem.Content, navVm.Shell)
                        || !ReferenceEquals(navItem.Section, navVm.Sections[i]))
                    {
                        sharedContent = false;
                        badNavName.Add(want + ">不是 NavSectionItem 或 Content 不共享");
                        continue;
                    }
                    if (navItem.ToString() != want) badNavName.Add(want + "≠" + navItem.ToString());
                }
                failed += Check(report, "导航项共用同一个 Content（右栏不重建的前提）", sharedContent,
                    "项数=" + navItems.Count);
                failed += Check(report, "导航项的 UIA 名字取自分组标题（ToString 通路）", badNavName.Count == 0,
                    string.Join(",", badNavName.GetRange(0, Math.Min(5, badNavName.Count)).ToArray()));
            }
            catch (Exception ex)
            {
                failed += Check(report, "卡片模板可实例化", false, ex.GetType().Name + " " + ex.Message);
            }

            report.AppendLine("INFO 数据源：项=" + items.Length + " 分组=" + ToolCatalog.Groups.Length
                + " 需管理员=" + CountWhere(items, i => i.RequiresAdmin)
                + " 家庭版缺=" + gotHome.Count
                + " 有兼容备注=" + CountWhere(items, i => !string.IsNullOrEmpty(i.CompatNote))
                + " 有危险提示=" + CountWhere(items, i => !string.IsNullOrEmpty(i.DangerNote))
                + " 本机未找到=" + CountWhere(items, i => i.Availability == ToolAvailability.Missing));
            return failed;
        }

        private static Models.ToolItem FindByCommand(Models.ToolItem[] items, string command)
        {
            foreach (var item in items)
                if (string.Equals(item.Command, command, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }

        private static int CountWhere(Models.ToolItem[] items, Func<Models.ToolItem, bool> predicate)
        {
            int n = 0;
            foreach (var item in items) if (predicate(item)) n++;
            return n;
        }

        private static bool IsSinglePua(string glyph)
        {
            if (string.IsNullOrEmpty(glyph) || glyph.Length != 1) return false;
            char c = glyph[0];
            return c >= '\uE000' && c <= '\uF8FF';
        }

        private static bool CanLoadTemplate(ResourceDictionary resources, object key)
        {
            try
            {
                if (resources == null || !resources.Contains(key)) return false;
                var template = resources[key] as System.Windows.DataTemplate;
                if (template == null) return false;
                return template.LoadContent() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int Check(StringBuilder report, string what, bool ok, string detail)
        {
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
            return ok ? 0 : 1;
        }

        /// <summary>WCAG 2.x 对比度：先线性化再取相对亮度。</summary>
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double Luminance(Color c)
        {
            return 0.2126 * Chan(c.R) + 0.7152 * Chan(c.G) + 0.0722 * Chan(c.B);
        }

        private static double Chan(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static void TryWrite(string text)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickPanel");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "selftest.txt"), text, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), text, Encoding.UTF8);
                }
                catch
                {
                    // 报告写不进去也不改退出码
                }
            }
        }
    }
}
