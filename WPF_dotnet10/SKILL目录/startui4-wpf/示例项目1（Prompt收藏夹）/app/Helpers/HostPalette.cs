using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using PromptFavorites.Models;
using StartUI4Controls;

namespace PromptFavorites.Helpers
{
    /// <summary>
    /// 宿主配色单源 → 38 个 <see cref="UI4ThemeToken"/> 的令牌表。
    ///
    /// 存在这个类的理由：此前 <c>App.RegisterAppTheme()</c> 用一串 <c>.With()</c> 覆盖 27 个令牌，
    /// 剩下 11 个静默继承库内置值，于是"宿主到底声明了哪些色"没有单一出处，也没法机器校验。
    /// 这里要求<b>每一档都显式给满 38 个</b>，<see cref="Services.ThemeSelfTest"/> 按这张表校验
    /// 资源键完整性与对比度门槛。
    ///
    /// 浅色档的每个值都按迁移前 <c>RegisterAppTheme()</c> 的同一套混合算式（<see cref="Mix"/>）算出来，
    /// 所以接入本表后浅色观感逐像素不变；新增的是夜景档。
    /// </summary>
    public static class HostPalette
    {
        /// <summary>取某档的完整令牌表（必定覆盖全部 38 个令牌）。</summary>
        public static Dictionary<UI4ThemeToken, Color> Tokens(AppThemeMode mode)
        {
            return mode == AppThemeMode.Dark ? DarkTokens() : LightTokens();
        }

        /// <summary>两档全部注册进库（后注册的键跟随 <paramref name="active"/>），并把宿主派生刷子写进应用资源。</summary>
        public static void Apply(AppThemeMode active)
        {
            // 两档都要注册：只注册当前档的话，切到另一档时库会回落到它自己的内置定义。
            RegisterDefinition(AppThemeMode.Light);
            RegisterDefinition(AppThemeMode.Dark);

            Theme.Mode = active;
            UI4Theme.SetTheme(active == AppThemeMode.Dark ? UI4ThemeMode.Dark : UI4ThemeMode.Light);
            PublishSemanticBrushes(Tokens(active));
        }

        /// <summary>
        /// 顶部 Toast 那枚"反色胶囊"：底取本档正文色向底混 8%，字取反方向——两档都自然成立，
        /// 不需要为暗色另写一个字面色。键挂在应用资源根上，宿主用 <c>{DynamicResource}</c> 取。
        /// </summary>
        private static void PublishSemanticBrushes(Dictionary<UI4ThemeToken, Color> p)
        {
            var app = Application.Current;
            if (app == null) return;

            var text = p[UI4ThemeToken.TextForeground];
            var bg = p[UI4ThemeToken.Background];

            app.Resources["App.Brush.Toast"] = Frozen(Color.FromArgb(0xF2,
                (byte)(text.R + (bg.R - text.R) * 0.08f),
                (byte)(text.G + (bg.G - text.G) * 0.08f),
                (byte)(text.B + (bg.B - text.B) * 0.08f)));
            app.Resources["App.Brush.ToastText"] = Frozen(Color.FromArgb(0xFF,
                (byte)(bg.R + (text.R - bg.R) * 0.12f),
                (byte)(bg.G + (text.G - bg.G) * 0.12f),
                (byte)(bg.B + (text.B - bg.B) * 0.12f)));
        }

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static void RegisterDefinition(AppThemeMode mode)
        {
            var tokens = Tokens(mode);
            // 从内置档起步：这样即便色表漏了某个令牌，也不会崩在取色上（漏项由 ThemeSelfTest 判红）。
            var def = mode == AppThemeMode.Dark ? UI4ThemeDefinition.Dark() : UI4ThemeDefinition.Light();
            def.Key = mode == AppThemeMode.Dark ? "dark" : "light";
            foreach (var pair in tokens)
                def.With(pair.Key, pair.Value);
            UI4Theme.Register(def);
        }

        // ── 浅色「终端靛」──────────────────────────────────────────────

        private static Dictionary<UI4ThemeToken, Color> LightTokens()
        {
            var t = Theme.Accent;
            var tDark = Theme.AccentHover;

            return new Dictionary<UI4ThemeToken, Color>
            {
                [UI4ThemeToken.Accent] = t,
                [UI4ThemeToken.AccentDark] = tDark,
                [UI4ThemeToken.AccentEnd] = Theme.Signal,
                [UI4ThemeToken.TextForeground] = Theme.Foreground,
                [UI4ThemeToken.TextSecondary] = Mix(Theme.Secondary, Theme.Foreground, 0.18f),
                [UI4ThemeToken.Background] = Theme.Background,
                [UI4ThemeToken.Surface] = Mix(Theme.Background, Colors.White, 0.85f),
                [UI4ThemeToken.BorderNormal] = Theme.Muted,
                [UI4ThemeToken.BorderSecondary] = Mix(Theme.Muted, Theme.Background, 0.45f),
                [UI4ThemeToken.BorderHover] = t,
                [UI4ThemeToken.BorderFocus] = tDark,
                [UI4ThemeToken.Placeholder] = Mix(Theme.Muted, Theme.Foreground, 0.38f),
                [UI4ThemeToken.CheckBackground] = tDark,
                [UI4ThemeToken.Icon] = Mix(Theme.Secondary, Theme.Foreground, 0.18f),
                [UI4ThemeToken.IconHover] = Mix(Theme.Foreground, t, 0.35f),
                [UI4ThemeToken.PanelBorder] = Color.FromArgb(60, t.R, t.G, t.B),
                [UI4ThemeToken.OffBackground] = Mix(Theme.Background, Theme.NeutralSource, 0.62f),
                [UI4ThemeToken.MenuBackground] = Mix(Theme.Background, Colors.White, 0.85f),
                [UI4ThemeToken.ListSelected] = t,
                [UI4ThemeToken.HeaderBackground] = Mix(Theme.Background, Theme.Foreground, 0.04f),
                [UI4ThemeToken.HeaderForeground] = Theme.Foreground,
                [UI4ThemeToken.RowHoverBackground] = Mix(Theme.Background, t, 0.06f),
                [UI4ThemeToken.RowSelectedBackground] = Theme.Selection,
                [UI4ThemeToken.GridLine] = Mix(Theme.Background, Theme.Muted, 0.45f),
                [UI4ThemeToken.ProgressStart] = t,
                [UI4ThemeToken.CheckBoxUnchecked] = Mix(Theme.Muted, Theme.Background, 0.3f),
                [UI4ThemeToken.HoverBorderColorLight] = Mix(Theme.Muted, t, 0.35f),

                // 以下 11 个是库 2026-10-02 补齐的令牌，浅色档沿用它们当时的内置值（观感不变），
                // 但改为由本表显式声明——"继承"与"声明后取值恰好相同"是两件不同的事，
                // 只有后者能被 --selftest 校验，也才谈得上切档时逐个对照。
                [UI4ThemeToken.OnAccent] = Colors.White,
                [UI4ThemeToken.Shadow] = Colors.Black,
                [UI4ThemeToken.ScrollBarThumb] = Color.FromArgb(0x50, 0, 0, 0),
                [UI4ThemeToken.Separator] = Color.FromRgb(0xDC, 0xDC, 0xDC),
                [UI4ThemeToken.BorderWeak] = Color.FromArgb(0x1A, 0, 0, 0),
                [UI4ThemeToken.TextMuted] = Color.FromArgb(0xC8, 0, 0, 0),
                [UI4ThemeToken.HoverOverlay] = Color.FromArgb(0x14, 0, 0, 0),
                [UI4ThemeToken.SelectedOverlay] = Color.FromArgb(0x0A, 0, 0, 0),
                [UI4ThemeToken.TrackBackground] = Color.FromArgb(0x14, 0, 0, 0),
                [UI4ThemeToken.BackgroundGradientStart] = Color.FromRgb(0xE9, 0xF1, 0xFA),
                [UI4ThemeToken.BackgroundGradientEnd] = Colors.White,
            };
        }

        // ── 夜景「终端靛 · 夜」──────────────────────────────────────────

        private static Dictionary<UI4ThemeToken, Color> DarkTokens()
        {
            var t = Theme.NightAccent;
            var tDark = Theme.NightAccentDeep;

            return new Dictionary<UI4ThemeToken, Color>
            {
                [UI4ThemeToken.Accent] = t,
                [UI4ThemeToken.AccentDark] = tDark,
                [UI4ThemeToken.AccentEnd] = Theme.NightSignal,
                [UI4ThemeToken.TextForeground] = Theme.NightForeground,
                [UI4ThemeToken.TextSecondary] = Mix(Theme.NightForeground, Theme.NightBackground, 0.22f),
                [UI4ThemeToken.Background] = Theme.NightBackground,
                [UI4ThemeToken.Surface] = Mix(Theme.NightBackground, Colors.White, 0.06f),
                [UI4ThemeToken.BorderNormal] = Theme.NightBorder,
                [UI4ThemeToken.BorderSecondary] = Mix(Theme.NightBorder, Theme.NightBackground, 0.40f),
                [UI4ThemeToken.BorderHover] = t,
                [UI4ThemeToken.BorderFocus] = Mix(t, Colors.White, 0.25f),
                [UI4ThemeToken.Placeholder] = Mix(Theme.NightBorder, Theme.NightForeground, 0.35f),
                [UI4ThemeToken.CheckBackground] = tDark,
                [UI4ThemeToken.Icon] = Mix(Theme.NightForeground, Theme.NightBackground, 0.35f),
                [UI4ThemeToken.IconHover] = Mix(Theme.NightForeground, t, 0.35f),
                [UI4ThemeToken.PanelBorder] = Color.FromArgb(0x4D, t.R, t.G, t.B),
                [UI4ThemeToken.OffBackground] = Mix(Theme.NightBackground, Theme.NightForeground, 0.12f),
                [UI4ThemeToken.MenuBackground] = Mix(Theme.NightBackground, Colors.White, 0.05f),
                [UI4ThemeToken.ListSelected] = t,
                [UI4ThemeToken.HeaderBackground] = Mix(Theme.NightBackground, Theme.NightForeground, 0.06f),
                [UI4ThemeToken.HeaderForeground] = Theme.NightForeground,
                [UI4ThemeToken.RowHoverBackground] = Mix(Theme.NightBackground, t, 0.10f),
                [UI4ThemeToken.RowSelectedBackground] = Mix(Theme.NightBackground, t, 0.22f),
                [UI4ThemeToken.GridLine] = Mix(Theme.NightBackground, Theme.NightForeground, 0.10f),
                [UI4ThemeToken.ProgressStart] = t,
                [UI4ThemeToken.CheckBoxUnchecked] = Mix(Theme.NightBackground, Theme.NightForeground, 0.30f),
                [UI4ThemeToken.HoverBorderColorLight] = Mix(Theme.NightBorder, t, 0.40f),

                // 夜景档里强调色是**亮**靛蓝，所以压在主色上的必须是深墨而不是白
                //（实测：白字压 #7C93FF 只有 2.67:1，深墨压同一色有 6.7:1）。
                // UI4Button 会在 OnAccent 与正文色之间取对比度更高的那个，这里给的就是那个更高者。
                [UI4ThemeToken.OnAccent] = Theme.NightInk,
                [UI4ThemeToken.Shadow] = Color.FromArgb(0x99, 0, 0, 0),
                [UI4ThemeToken.ScrollBarThumb] = Color.FromArgb(0x66, 255, 255, 255),
                [UI4ThemeToken.Separator] = Color.FromRgb(0x2C, 0x37, 0x48),
                [UI4ThemeToken.BorderWeak] = Color.FromArgb(0x33, 255, 255, 255),
                [UI4ThemeToken.TextMuted] = Mix(Theme.NightForeground, Theme.NightBackground, 0.45f),
                [UI4ThemeToken.HoverOverlay] = Color.FromArgb(0x14, 255, 255, 255),
                [UI4ThemeToken.SelectedOverlay] = Color.FromArgb(0x1E, t.R, t.G, t.B),
                [UI4ThemeToken.TrackBackground] = Color.FromArgb(0x33, 255, 255, 255),
                [UI4ThemeToken.BackgroundGradientStart] = Mix(Theme.NightBackground, Colors.White, 0.04f),
                [UI4ThemeToken.BackgroundGradientEnd] = Theme.NightBackground,
            };
        }

        /// <summary>与 <c>App.Mix</c> 原式一致（逐性线性插值后截断），保证浅色档逐值复现迁移前行为。</summary>
        internal static Color Mix(Color a, Color b, float w)
        {
            if (w <= 0f) return a;
            if (w >= 1f) return b;
            return Color.FromArgb(a.A,
                (byte)(a.R + (b.R - a.R) * w),
                (byte)(a.G + (b.G - a.G) * w),
                (byte)(a.B + (b.B - a.B) * w));
        }

        /// <summary>WCAG 2.x 对比度：(较亮 + 0.05) / (较暗 + 0.05)。与库内 UI4Button 的同式，门槛判定用。</summary>
        public static double Contrast(Color a, Color b)
        {
            double x = RelativeLuminance(a);
            double y = RelativeLuminance(b);
            double lighter = System.Math.Max(x, y);
            double darker = System.Math.Min(x, y);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double RelativeLuminance(Color c)
        {
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }

        private static double Channel(byte value)
        {
            double s = value / 255.0;
            return s <= 0.03928 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        /// <summary>按底色亮度判深浅，判据与 <c>UI4WindowTitleBar.IsDark</c> 逐字一致。</summary>
        public static bool IsDark(Color background)
        {
            return 0.299 * background.R + 0.587 * background.G + 0.114 * background.B < 128d;
        }
    }
}
