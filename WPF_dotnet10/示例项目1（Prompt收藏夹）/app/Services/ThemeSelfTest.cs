using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using StartUI4Controls;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 主题通路的自检段。存在的理由：这套库零 XAML、资源键由令牌枚举名<b>逐字</b>决定，
    /// 键名写错既不报编译错也不抛异常，只表现为"这一处没跟主题"——只有这里的断言能把它抓出来。
    /// 覆盖：令牌总数、宿主色表完整性、两档应用后资源键齐全与同源、对比度门槛、深浅判据一致。
    /// </summary>
    internal static class ThemeSelfTest
    {
        public static void Run(SelfTestResult r)
        {
            r.Section("theme");

            var tokens = (UI4ThemeToken[])Enum.GetValues(typeof(UI4ThemeToken));
            r.Check(tokens.Length == 38,
                "令牌总数应为 38，实际 " + tokens.Length + "（lib/ 改版后必须同步宿主色表与 README 口径）");

            CheckBuiltinDefinitions(r, tokens);

            var light = HostPalette.Tokens(AppThemeMode.Light);
            var dark = HostPalette.Tokens(AppThemeMode.Dark);
            bool lightComplete = CoversAll(light, tokens);
            bool darkComplete = CoversAll(dark, tokens);
            r.Check(lightComplete, "浅色宿主色表未覆盖全部 38 个令牌：缺 " + Missing(light, tokens));
            r.Check(darkComplete, "夜景宿主色表未覆盖全部 38 个令牌：缺 " + Missing(dark, tokens));

            if (Application.Current == null)
            {
                r.Check(false, "--selftest 必须在 Application 已建立之后跑（当前 Application.Current 为空）");
                return;
            }

            // 色表不全时后面每一步都是索引取值，取一次崩一次；缺项已经记过 FAIL，这里直接跳过
            if (!lightComplete && !darkComplete) return;

            if (lightComplete) CheckAppliedMode(r, AppThemeMode.Light, light, tokens);
            if (darkComplete) CheckAppliedMode(r, AppThemeMode.Dark, dark, tokens);
            if (!lightComplete || !darkComplete) return;

            CheckContrast(r, "浅色", light);
            CheckContrast(r, "夜景", dark);

            r.Check(!HostPalette.IsDark(light[UI4ThemeToken.Background]),
                "浅色档底色被判成深色：" + light[UI4ThemeToken.Background] + "（判据与 UI4WindowTitleBar 同式）");
            r.Check(HostPalette.IsDark(dark[UI4ThemeToken.Background]),
                "夜景档底色未被判成深色：" + dark[UI4ThemeToken.Background]);

            // 回到用户实际要用的档，别把自检中途的状态留给后面的段
            HostPalette.Apply(AppThemeMode.Light);
        }

        private static void CheckBuiltinDefinitions(SelfTestResult r, UI4ThemeToken[] tokens)
        {
            foreach (var def in new[] { UI4ThemeDefinition.Light(), UI4ThemeDefinition.Dark(),
                                        UI4ThemeDefinition.HighContrast() })
            {
                var lacking = new List<string>();
                foreach (var t in tokens)
                {
                    if (!def.Has(t)) lacking.Add(t.ToString());
                }
                r.Check(lacking.Count == 0,
                    "库内置档 " + def.Key + " 缺令牌：" + string.Join(",", lacking)
                    + "（缺一个就会在取色时抛 KeyNotFoundException）");
            }
        }

        /// <summary>应用某一档后，38×2 个资源键 + 3 个别名都要在应用资源里，且值与宿主色表同源。</summary>
        private static void CheckAppliedMode(SelfTestResult r, AppThemeMode mode,
            Dictionary<UI4ThemeToken, Color> palette, UI4ThemeToken[] tokens)
        {
            HostPalette.Apply(mode);

            string label = mode == AppThemeMode.Dark ? "夜景" : "浅色";
            var problems = new List<string>();

            foreach (var t in tokens)
            {
                string expected = "UI4.Color." + t;
                string brushKey = "UI4.Brush." + t;
                Color want = palette[t];

                object colorEntry = Application.Current.TryFindResource(expected);
                if (!(colorEntry is Color) || !Equals((Color)colorEntry, want))
                    problems.Add(expected + " 缺失或不同源");

                object brushEntry = Application.Current.TryFindResource(brushKey);
                var brush = brushEntry as SolidColorBrush;
                if (brush == null || !Equals(brush.Color, want))
                    problems.Add(brushKey + " 缺失或不同源");
                else if (!brush.IsFrozen)
                    problems.Add(brushKey + " 未冻结");
            }

            foreach (var alias in new[] { "UI4.Brush.Text", "UI4.Brush.Border", "UI4.Brush.Accent" })
            {
                if (Application.Current.TryFindResource(alias) == null)
                    problems.Add(alias + " 别名缺失");
            }

            r.Check(problems.Count == 0,
                label + "档资源键：" + problems.Count + " 处不对，例如 "
                + (problems.Count > 0 ? problems[0] : "无"));

            r.Check(Equals(UI4Theme.Current.ColorOf(UI4ThemeToken.Background),
                           palette[UI4ThemeToken.Background]),
                label + "档 UI4Theme.Current 与宿主色表底色不同源");

            string wantKey = mode == AppThemeMode.Dark ? "dark" : "light";
            r.Check(string.Equals(UI4Theme.ResolvedKey, wantKey, StringComparison.OrdinalIgnoreCase),
                label + "档生效后 ResolvedKey 应为 " + wantKey + "，实际 " + UI4Theme.ResolvedKey);
        }

        /// <summary>
        /// 对比度门槛。按库的真实机制出题：<see cref="UI4Button"/> 在 <c>OnAccent</c> 与正文色之间
        /// <b>取与底色对比度更高的那个</b>（不是按亮度阈值），所以按钮类门槛取两者的较优者判定。
        /// </summary>
        private static void CheckContrast(SelfTestResult r, string label,
            Dictionary<UI4ThemeToken, Color> p)
        {
            var bg = p[UI4ThemeToken.Background];
            var text = p[UI4ThemeToken.TextForeground];
            var secondary = p[UI4ThemeToken.TextSecondary];
            var accent = p[UI4ThemeToken.Accent];
            var neutral = p[UI4ThemeToken.OffBackground];
            var selected = p[UI4ThemeToken.RowSelectedBackground];
            var placeholder = p[UI4ThemeToken.Placeholder];
            var menu = p[UI4ThemeToken.MenuBackground];
            var icon = p[UI4ThemeToken.Icon];

            var onAccent = p[UI4ThemeToken.OnAccent];
            double bestOnAccent = Math.Max(HostPalette.Contrast(onAccent, accent),
                                           HostPalette.Contrast(text, accent));
            double bestOnNeutral = Math.Max(HostPalette.Contrast(onAccent, neutral),
                                            HostPalette.Contrast(text, neutral));

            r.Note(label + " 色表原值：底=" + Hex(bg)
                + "，面板=" + Hex(p[UI4ThemeToken.Surface])
                + "，正文=" + Hex(text)
                + "，主色=" + Hex(accent)
                + "，强调=" + Hex(p[UI4ThemeToken.AccentEnd])
                + "，边框=" + Hex(p[UI4ThemeToken.BorderNormal])
                + "，选中行=" + Hex(selected)
                + "，中性按钮底=" + Hex(neutral)
                + "，压在主色上的字=" + Hex(onAccent));

            r.Note(label + " 对比度实测："
                + " 正文/底=" + Fmt(text, bg)
                + "，次级/底=" + Fmt(secondary, bg)
                + "，按钮字/主色=" + bestOnAccent.ToString("0.00") + ":1（OnAccent="
                + Fmt(onAccent, accent) + "，正文=" + Fmt(text, accent) + "）"
                + "，按钮字/中性底=" + bestOnNeutral.ToString("0.00") + ":1"
                + "，正文/选中行=" + Fmt(text, selected)
                + "，占位符/输入底=" + Fmt(placeholder, menu)
                + "，图标/底=" + Fmt(icon, bg));

            r.Check(HostPalette.Contrast(text, bg) >= 7.0,
                label + " 正文/底 " + HostPalette.Contrast(text, bg).ToString("0.00") + ":1，门槛 7:1");
            r.Check(HostPalette.Contrast(secondary, bg) >= 4.5,
                label + " 次级文本/底 " + HostPalette.Contrast(secondary, bg).ToString("0.00") + ":1，门槛 4.5:1");
            r.Check(bestOnAccent >= 4.5,
                label + " 主色按钮上的字（OnAccent 与正文取更优者）" + bestOnAccent.ToString("0.00")
                + ":1，门槛 4.5:1");
            r.Check(bestOnNeutral >= 4.5,
                label + " 中性按钮上的字 " + bestOnNeutral.ToString("0.00") + ":1，门槛 4.5:1");
            r.Check(HostPalette.Contrast(text, selected) >= 4.5,
                label + " 正文/选中行 " + HostPalette.Contrast(text, selected).ToString("0.00") + ":1，门槛 4.5:1");
            r.Check(HostPalette.Contrast(placeholder, menu) >= 3.0,
                label + " 占位符/输入底 " + HostPalette.Contrast(placeholder, menu).ToString("0.00") + ":1，门槛 3:1");
            r.Check(HostPalette.Contrast(icon, bg) >= 3.0,
                label + " 图标/底 " + HostPalette.Contrast(icon, bg).ToString("0.00") + ":1，门槛 3:1");
        }

        private static string Fmt(Color a, Color b)
        {
            return HostPalette.Contrast(a, b).ToString("0.00") + ":1";
        }

        private static string Hex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        private static bool CoversAll(Dictionary<UI4ThemeToken, Color> palette, UI4ThemeToken[] tokens)
        {
            foreach (var t in tokens)
            {
                if (!palette.ContainsKey(t)) return false;
            }
            return palette.Count == tokens.Length;
        }

        private static string Missing(Dictionary<UI4ThemeToken, Color> palette, UI4ThemeToken[] tokens)
        {
            var lacking = new List<string>();
            foreach (var t in tokens)
            {
                if (!palette.ContainsKey(t)) lacking.Add(t.ToString());
            }
            return lacking.Count == 0 ? "（数量不对但项齐：" + palette.Count + "）" : string.Join(",", lacking);
        }
    }
}
