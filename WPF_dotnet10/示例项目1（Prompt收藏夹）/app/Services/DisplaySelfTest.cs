using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PromptFavorites.Converters;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using StartUI4Controls;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 排印与缩放的自检段。这里的键全是运行期解析的资源字符串，写错既不报编译错也不抛异常，
    /// 只表现为"字号没跟着变"，所以判据要自己出题，覆盖四件容易回退的事：
    /// ① 库自己把 <c>UI4.Font.*</c> 的兜底默认值发布出来了（缺键的话所有控件静默掉到 WPF 裸默认 12px）；
    /// ② 宿主在应用资源根上的覆盖值有效，且<b>不会</b>被换档重写共享字典冲掉（两层不同，README §4.2.1 记的就是这条）；
    /// ③ 字号阶梯在默认基准下等于既有设计的 11/12/13/14/16——换成层级键是一次平移而非改设计，这条钉住"没改"；
    /// ④ 区间钳位与缩放换算器的边界（含参数写错时必须回 UnsetValue，静默接受等于把窗口下限变成 0）。
    /// </summary>
    internal static class DisplaySelfTest
    {
        public static void Run(SelfTestResult r)
        {
            r.Section("display");

            if (Application.Current == null)
            {
                r.Check(false, "--selftest 必须在 Application 已建立之后跑（当前 Application.Current 为空）");
                return;
            }

            CheckLibraryDefaults(r);
            CheckHostOverrideSurvivesModeSwitch(r);
            CheckPublishWritesEveryRoleKey(r);
            CheckFixedSizeKeys(r);
            CheckLadder(r);
            CheckClamps(r);
            CheckFamilyChoices(r);
            CheckZoomedSizeConverter(r);
            CheckOverlayXamlParses(r);
        }

        /// <summary>
        /// 字体候选表的契约：出厂字体栈<b>固定第 0 位</b>、去重、丢掉空白项、其余部分按序排列。
        /// 这条对着的是"设置里恢复默认后下拉框不跟着变"——ComboBox 挂了 ItemsSource 之后
        /// 把 SelectedItem 设成列表外的值会被静默清空，所以"默认值必须是列表的一项"不是巧合而是契约。
        /// </summary>
        private static void CheckFamilyChoices(SelfTestResult r)
        {
            var def = Typography.DefaultFontFamilySource;

            var only = Typography.BuildFamilyChoices(null);
            r.Check(only.Count == 1 && only[0] == def,
                "候选表为空时应该只剩出厂项，实际 " + only.Count + " 项");

            var messy = Typography.BuildFamilyChoices(new[]
            {
                "Segoe UI", def, "Arial", "   ", "Segoe UI", null, "Cambria"
            });
            r.Check(messy.Count == 4 && messy[0] == def
                    && messy[1] == "Arial" && messy[2] == "Cambria" && messy[3] == "Segoe UI",
                "候选表不是「出厂项在首位 + 去重 + 丢空白 + 其余按序」：" + string.Join(" | ", messy.ToArray()));

            // 出厂项本身必须能当 FontFamily 用（下拉项要用它渲染预览，构造函数抛了就是打开面板才炸）
            try
            {
                var probe = new FontFamily(def);
                r.Check(probe.Source == def, "出厂字体栈构造后 Source 变了：" + probe.Source);
            }
            catch (Exception ex)
            {
                r.Check(false, "出厂字体栈不能构造 FontFamily：" + ex.GetType().Name + " " + ex.Message);
            }

            var real = Typography.BuildFamilyChoices(
                Fonts.SystemFontFamilies.Select(f => f.Source).ToList());
            r.Check(real.Count >= 2 && real[0] == def,
                "真实系统字体表不满足契约（首位应是出厂项），实际 " + real.Count + " 项，首位=[" + real[0] + "]");
            r.Note("字体候选表：" + real.Count + " 项，首位=" + real[0]);
        }

        /// <summary>
        /// 主窗口与设置面板两块 XAML 能在运行期解析——这里不 Show，只构造一次控件（BAML 加载就发生在构造函数里）。
        /// 值得单独一条的理由：这两处用到了 <c>x:Static</c> 引常量、<c>DynamicResource</c> 引层级键、
        /// 带参数的转换器与 DataTemplate，这类错误只在窗口拉起或"点开设置"那一刻以 XamlParseException 出现，
        /// 而没人点的话它就是"点了没反应"。
        /// </summary>
        private static void CheckOverlayXamlParses(SelfTestResult r)
        {
            try
            {
                var overlay = new Views.SettingsOverlay();
                r.Check(overlay != null, "设置面板构造返回空");
            }
            catch (Exception ex)
            {
                r.Check(false, "设置面板 XAML 解析失败：" + ex.GetType().Name + " " + ex.Message);
            }

            try
            {
                // 只构造不 Show：验证 BAML 与绑定表达式本身能加载，不弹出任何窗口
                var window = new MainWindow();
                r.Check(window != null, "主窗口构造返回空");
            }
            catch (Exception ex)
            {
                r.Check(false, "主窗口 XAML 解析失败：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        private const string BaseKey = "UI4.Font.Size.Base";
        private const string CodeKey = "UI4.Font.Size.Code";
        private const string FamilyKey = "UI4.Font.Family";

        private static void CheckLibraryDefaults(SelfTestResult r)
        {
            var app = Application.Current;

            // 走到这里宿主还没发布任何覆盖值（SelfTest 排在 Settings.Load 之前），
            // 所以解析到的必须就是库的兜底默认值本身。
            var baseSize = app.TryFindResource(BaseKey);
            var codeSize = app.TryFindResource(CodeKey);
            var family = app.TryFindResource(FamilyKey) as FontFamily;

            r.Check(baseSize is double && Math.Abs((double)baseSize - UI4Theme.DefaultFontSizeBase) < 1e-9,
                "库没有发布 " + BaseKey + " 的兜底默认值，解析到 " + (baseSize == null ? "空" : baseSize));
            r.Check(codeSize is double && Math.Abs((double)codeSize - UI4Theme.DefaultFontSizeCode) < 1e-9,
                "库没有发布 " + CodeKey + " 的兜底默认值，解析到 " + (codeSize == null ? "空" : codeSize));
            r.Check(family != null && family.Source.Contains("Segoe UI"),
                "库没有发布 " + FamilyKey + " 的兜底默认值，解析到 "
                + (family == null ? "空" : family.Source));
        }

        private static void CheckHostOverrideSurvivesModeSwitch(SelfTestResult r)
        {
            var app = Application.Current;
            const double probe = 20;

            app.Resources[BaseKey] = probe;
            HostPalette.Apply(AppThemeMode.Dark);   // 这一步会整体重写库的共享字典

            var after = app.TryFindResource(BaseKey);
            r.Check(after is double && Math.Abs((double)after - probe) < 1e-9,
                "换档把宿主覆盖的字号冲掉了：期望 " + probe + "，实际 " + after);

            app.Resources.Remove(BaseKey);
            var back = app.TryFindResource(BaseKey);
            r.Check(back is double && Math.Abs((double)back - UI4Theme.DefaultFontSizeBase) < 1e-9,
                "撤掉宿主覆盖后没回到库默认：" + (back == null ? "空" : back));

            HostPalette.Apply(AppThemeMode.Light);
        }

        private static void CheckPublishWritesEveryRoleKey(SelfTestResult r)
        {
            var app = Application.Current;
            Typography.Publish(app, string.Empty, Typography.DefaultBaseSize);

            var expected = new[]
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

            for (int i = 0; i < expected.Length; i++)
            {
                var got = app.TryFindResource(expected[i].Key);
                r.Check(got is double && Math.Abs((double)got - expected[i].Size) < 1e-9,
                    "发布后 " + expected[i].Key + " 解析为 " + (got == null ? "空" : got)
                    + "，期望 " + expected[i].Size);
            }

            // 圆角半径必须是 CornerRadius 而不是 double：DynamicResource 不做类型转换，
            // 挂错类型会在 CornerRadius 依赖属性上运行期炸掉。
            var roundRadius = app.TryFindResource("App.Radius.RoundButton");
            var chipRadius = app.TryFindResource("App.Radius.Chip");
            r.Check(roundRadius is CornerRadius
                    && Math.Abs(((CornerRadius)roundRadius).TopLeft
                                - Typography.RoundButtonSize(Typography.DefaultBaseSize) / 2) < 1e-9,
                "App.Radius.RoundButton 不是值为半径的 CornerRadius，实际 " + roundRadius);
            r.Check(chipRadius is CornerRadius
                    && Math.Abs(((CornerRadius)chipRadius).TopLeft
                                - Typography.ChipHeight(Typography.DefaultBaseSize) / 2) < 1e-9,
                "App.Radius.Chip 不是值为半径的 CornerRadius，实际 " + chipRadius);

            r.Check(app.TryFindResource(FamilyKey) is FontFamily,
                "发布后 " + FamilyKey + " 不是 FontFamily");

            // 自检不应该把覆盖值留在应用资源里给后面的段用
            for (int i = 0; i < expected.Length; i++) app.Resources.Remove(expected[i].Key);
            app.Resources.Remove("App.Radius.RoundButton");
            app.Resources.Remove("App.Radius.Chip");
            app.Resources.Remove(FamilyKey);
        }

        /// <summary>
        /// 固定件尺寸（圆钮直径 / 胶囊高度）。这两条对着的是"字体放大后 + 号内容偏到右下"那个缺陷：
        /// 尺寸钉死 28×28 而库的默认 Padding 是 10,0,10,0，内容区只剩 8 px，字身一大就被从左上角起画。
        /// 现在尺寸随字号长，但**默认基准下必须仍等于原来的 28 与 24**——否则这次修复顺手改了观感。
        /// </summary>
        private static void CheckFixedSizeKeys(SelfTestResult r)
        {
            r.Check(Math.Abs(Typography.RoundButtonSize(Typography.DefaultBaseSize)
                             - Typography.DefaultRoundButtonSize) < 1e-9,
                "默认基准下圆钮直径不再是 28：" + Typography.RoundButtonSize(Typography.DefaultBaseSize));
            r.Check(Math.Abs(Typography.ChipHeight(Typography.DefaultBaseSize)
                             - Typography.DefaultChipHeight) < 1e-9,
                "默认基准下胶囊高度不再是 24：" + Typography.ChipHeight(Typography.DefaultBaseSize));

            // 放大档下必须长到装得下字身（留 1.3 倍行高的余量），且单调不减
            double prevRound = 0, prevChip = 0;
            for (double b = Typography.MinBaseSize; b <= Typography.MaxBaseSize; b += 1)
            {
                double round = Typography.RoundButtonSize(b);
                double chip = Typography.ChipHeight(b);
                r.Check(round >= prevRound && chip >= prevChip,
                        "尺寸在基准 " + b + " 处回退：" + prevRound + "/" + prevChip
                        + " -> " + round + "/" + chip);
                prevRound = round;
                prevChip = chip;

                r.Check(round >= Typography.SizeOf("Icon", b) * 1.3,
                    "基准 " + b + " 时圆钮 " + round + " px 装不下 " + Typography.SizeOf("Icon", b)
                    + " px 的图标字身");
                r.Check(chip >= Typography.SizeOf("Caption", b) * 1.3,
                    "基准 " + b + " 时胶囊 " + chip + " px 装不下 " + Typography.SizeOf("Caption", b)
                    + " px 的胶囊字身");
            }

            r.Note("固定件尺寸：基准=" + Typography.DefaultBaseSize + " 圆钮="
                + Typography.RoundButtonSize(Typography.DefaultBaseSize) + " 胶囊="
                + Typography.ChipHeight(Typography.DefaultBaseSize)
                + "；基准=" + Typography.MaxBaseSize + " 圆钮="
                + Typography.RoundButtonSize(Typography.MaxBaseSize) + " 胶囊="
                + Typography.ChipHeight(Typography.MaxBaseSize));
        }

        private static void CheckLadder(SelfTestResult r)
        {
            // 默认基准下的阶梯必须逐值等于改成层级键之前的字面字号，否则这次替换就是一次改设计。
            var atDefault = new[]
            {
                new { Role = "Caption", Want = 11.0 },
                new { Role = "Small", Want = 12.0 },
                new { Role = "Medium", Want = 13.0 },
                new { Role = "Lead", Want = 14.0 },
                new { Role = "Icon", Want = 16.0 },
                new { Role = "Code", Want = 14.0 }
            };

            for (int i = 0; i < atDefault.Length; i++)
            {
                double got = Typography.SizeOf(atDefault[i].Role, Typography.DefaultBaseSize);
                r.Check(Math.Abs(got - atDefault[i].Want) < 1e-9,
                    "层级 " + atDefault[i].Role + " 在基准 " + Typography.DefaultBaseSize
                    + " 下是 " + got + "，既有设计是 " + atDefault[i].Want);
            }

            var roles = new[] { "Caption", "Small", "Medium", "Lead", "Icon", "Code" };
            foreach (var baseSize in new[] { Typography.MinBaseSize, Typography.DefaultBaseSize, Typography.MaxBaseSize })
            {
                for (int i = 0; i < roles.Length; i++)
                {
                    double got = Typography.SizeOf(roles[i], baseSize);
                    r.Check(got >= 10 && got <= baseSize + 4,
                        "层级 " + roles[i] + " 在基准 " + baseSize + " 下越界：" + got);
                }

                // 单调性：小字号层级不能比大字号层级更大（抹平层级就是"全拉平"那种做法）
                r.Check(Typography.SizeOf("Caption", baseSize) <= Typography.SizeOf("Small", baseSize)
                        && Typography.SizeOf("Small", baseSize) <= Typography.SizeOf("Medium", baseSize)
                        && Typography.SizeOf("Medium", baseSize) <= Typography.SizeOf("Lead", baseSize),
                    "层级顺序在基准 " + baseSize + " 下被打乱");
            }

            r.Note("字号阶梯：基准=" + Typography.DefaultBaseSize
                + " → 胶囊=" + Typography.SizeOf("Caption", Typography.DefaultBaseSize)
                + " 标签=" + Typography.SizeOf("Small", Typography.DefaultBaseSize)
                + " 次要=" + Typography.SizeOf("Medium", Typography.DefaultBaseSize)
                + " 正文=" + Typography.DefaultBaseSize
                + " 提示=" + Typography.SizeOf("Lead", Typography.DefaultBaseSize)
                + " 代码=" + Typography.SizeOf("Code", Typography.DefaultBaseSize)
                + " 图标=" + Typography.SizeOf("Icon", Typography.DefaultBaseSize));
        }

        private static void CheckClamps(SelfTestResult r)
        {
            var baseCases = new[]
            {
                new { In = 7.0, Want = Typography.MinBaseSize },
                new { In = 33.0, Want = Typography.MaxBaseSize },
                new { In = double.NaN, Want = Typography.DefaultBaseSize },
                new { In = double.PositiveInfinity, Want = Typography.DefaultBaseSize },
                new { In = 15.0, Want = 15.0 }
            };
            for (int i = 0; i < baseCases.Length; i++)
            {
                double got = Typography.ClampBase(baseCases[i].In);
                r.Check(Math.Abs(got - baseCases[i].Want) < 1e-9,
                    "ClampBase(" + baseCases[i].In + ")=" + got + "，期望 " + baseCases[i].Want);
            }

            var zoomCases = new[]
            {
                new { In = 10.0, Want = Typography.MinZoomPercent },
                new { In = 500.0, Want = Typography.MaxZoomPercent },
                new { In = -1.0, Want = Typography.MinZoomPercent },
                new { In = double.NaN, Want = Typography.DefaultZoomPercent },
                new { In = 100.0, Want = 100.0 }
            };
            for (int i = 0; i < zoomCases.Length; i++)
            {
                double got = Typography.ClampZoom(zoomCases[i].In);
                r.Check(Math.Abs(got - zoomCases[i].Want) < 1e-9,
                    "ClampZoom(" + zoomCases[i].In + ")=" + got + "，期望 " + zoomCases[i].Want);
            }

            r.Check(Typography.SafeFamily(null) != null
                    && Typography.SafeFamily(null).Source == Typography.DefaultFontFamilySource,
                "空字体族名没回退出厂栈");
            r.Check(Typography.SafeFamily("   ").Source == Typography.DefaultFontFamilySource,
                "纯空白字体族名没回退出厂栈");
            r.Check(Math.Abs(Typography.SizeOf("不存在的层级", 15) - 15) < 1e-9,
                "写错的层级名应该退回基准本身而不是崩");
        }

        private static void CheckZoomedSizeConverter(SelfTestResult r)
        {
            var conv = new ZoomedSizeConverter();
            var work = SystemParameters.WorkArea;

            var scaled = conv.Convert(1.5, typeof(double), "W:900", CultureInfo.InvariantCulture);
            r.Check(scaled is double && Math.Abs((double)scaled - Math.Min(1350, work.Width)) < 1e-9,
                "W:900 在 150% 下应折算成 " + Math.Min(1350, work.Width) + "，实际 " + scaled);

            var shrunk = conv.Convert(0.5, typeof(double), "H:500", CultureInfo.InvariantCulture);
            r.Check(shrunk is double && Math.Abs((double)shrunk - 250) < 1e-9,
                "H:500 在 50% 下应是 250，实际 " + shrunk);

            var capped = conv.Convert(2.0, typeof(double), "W:99999", CultureInfo.InvariantCulture);
            r.Check(capped is double && Math.Abs((double)capped - work.Width) < 1e-9,
                "窗口下限越过工作区宽度时没钳住：" + capped + " vs " + work.Width);

            // XAML 里的 ConverterParameter 是字符串，写错了不能静默给个 0（那等于窗口没有下限）
            r.Check(Equals(conv.Convert(1.0, typeof(double), "900", CultureInfo.InvariantCulture),
                           DependencyProperty.UnsetValue),
                "参数少了 W:/H: 前缀时应该回 UnsetValue");
            r.Check(Equals(conv.Convert(1.0, typeof(double), "W:abc", CultureInfo.InvariantCulture),
                           DependencyProperty.UnsetValue),
                "参数里的像素值不是数字时应该回 UnsetValue");
            var nanZoom = conv.Convert(double.NaN, typeof(double), "W:900", CultureInfo.InvariantCulture);
            r.Check(nanZoom is double && Math.Abs((double)nanZoom - Math.Min(900, work.Width)) < 1e-9,
                "系数非法时应该退回设计尺寸而不是 0：" + nanZoom);
        }
    }
}
