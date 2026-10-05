using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using StartUI4Controls;
using VocabDesk.Helpers;

namespace VocabDesk.Services
{
    /// <summary>
    /// 由 "VocabDesk.exe --selftest" 触发。约定：返回值 = 失败断言数（0 为全通过），
    /// 发布脚本拿它当门禁。只跑纯函数与主题定义，不开窗、不读用户设置、不挂 UI 异常钩子。
    /// 报告写 %APPDATA%\VocabDesk\selftest.txt，写不进去回落程序目录，且报告失败不得改变退出码。
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

            // 6) 资源键完整性：8 套预置逐套装进字典后，38 个令牌的 UI4.Color.* / UI4.Brush.* 与三个别名都要查得到。
            //    XAML 里键名写错不报错、不抛异常，只表现为"这处颜色不跟主题"，所以在这里用 TryFindResource 抓成 FAIL。
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                failed += Check(report, "预置套装资源键齐全", false, "Application.Current 为空——这条断言要在 OnStartup 里跑");
            }
            else
            {
                var badKeys = new List<string>();
                UI4ThemePacks.RegisterAll();
                foreach (string packKey in UI4ThemePacks.Keys)
                {
                    UI4Theme.Register(UI4ThemePacks.DefinitionFor(packKey));
                    if (!UI4Theme.Apply(packKey)) { badKeys.Add("apply:" + packKey); continue; }
                    foreach (UI4ThemeToken t in Enum.GetValues(typeof(UI4ThemeToken)))
                    {
                        if (app.TryFindResource("UI4.Color." + t) == null) badKeys.Add(packKey + ">UI4.Color." + t);
                        if (app.TryFindResource("UI4.Brush." + t) == null) badKeys.Add(packKey + ">UI4.Brush." + t);
                    }
                    foreach (string alias in new string[] { "UI4.Brush.Text", "UI4.Brush.Border", "UI4.Brush.Accent" })
                    {
                        if (app.TryFindResource(alias) == null) badKeys.Add(packKey + ">" + alias);
                    }
                }
                failed += Check(report, "预置套装 × 38 令牌资源键齐全", badKeys.Count == 0,
                    badKeys.Count == 0 ? "" : badKeys.Count + " 个缺失，前 6 个：" + string.Join(",", badKeys.GetRange(0, Math.Min(6, badKeys.Count))));

                // 7) 字典里的值与定义表必须同源：不一致说明有第二份定义在写同一批键（宿主覆盖同名键属预期，但要看得见）
                var dark = UI4ThemeDefinition.Dark();
                UI4Theme.Register(dark);
                UI4Theme.Apply(dark.Key);
                Color dictBg = (Color)app.TryFindResource("UI4.Color.Background");
                Color defBg = dark.GetColor(UI4ThemeToken.Background);
                failed += Check(report, "字典底色与 Dark() 定义同源", dictBg == defBg,
                    "字典 " + dictBg + " vs 定义 " + defBg);
            }

            // 排印与缩放这段要写在报告里，所以放在捕获文本之前
            failed += RunSettingsSection(report);
            failed += RunDisplaySection(report, app);

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
                new { Key = "App.Size.Chip", Size = Typography.ChipHeight(Typography.DefaultBaseSize) }
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
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk");
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
