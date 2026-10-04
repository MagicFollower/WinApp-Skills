using System;
using System.Windows.Media;
using StartUI4Controls;

namespace QuickPanel.Helpers
{
    /// <summary>
    /// 宿主配色单源：改观感只改这里的常量，别散着改控件属性。
    /// 明暗两套都在这里派生（Policy = both），界面里一律写 {DynamicResource UI4.Brush.*} 跟档。
    /// </summary>
    internal static class Theme
    {
        /// <summary>
        /// 明暗策略：与需求方确认后定为 both —— 亮 + 暗两套都注册，启动时跟随系统，
        /// 设置面板里给「浅色 / 深色 / 跟随系统」三个入口（见 Services/ThemeService.cs）。
        /// 改成 light-only / dark-only 时 ThemeService 会自动挡掉反方向的档。
        /// </summary>
        public const string Policy = "both";

        // ── 浅色档 ─────────────────────────────────────────────────────
        public static readonly Color Background = Color.FromRgb(0xF7, 0xF9, 0xFB);
        public static readonly Color Foreground = Color.FromRgb(0x10, 0x20, 0x2A);
        public static readonly Color Muted = Color.FromRgb(0x9A, 0xAA, 0xB8);
        public static readonly Color Accent = Color.FromRgb(0x4F, 0x6B, 0xE8);
        public static readonly Color AccentHover = Color.FromRgb(0x43, 0x5A, 0xC5);
        public static readonly Color Signal = Color.FromRgb(0x1B, 0x93, 0xA8);

        // ── 深色档 ─────────────────────────────────────────────────────
        // 底色偏冷蓝而不是纯黑：卡片要能在底上"浮起来"，纯黑底上的深灰卡片没有层次。
        public static readonly Color DarkBackground = Color.FromRgb(0x10, 0x15, 0x1B);
        public static readonly Color DarkSurface = Color.FromRgb(0x1B, 0x22, 0x2B);
        public static readonly Color DarkForeground = Color.FromRgb(0xE8, 0xEE, 0xF4);
        public static readonly Color DarkMuted = Color.FromRgb(0x8C, 0x9B, 0xAA);
        // 同一支品牌色在深底上要把亮度抬上去，否则强调色与暗底糊成一片（对比度断言会红）
        public static readonly Color DarkAccent = Color.FromRgb(0x7E, 0x93, 0xF2);
        public static readonly Color DarkAccentHover = Color.FromRgb(0x67, 0x7E, 0xE8);
        public static readonly Color DarkSignal = Color.FromRgb(0x3F, 0xC0, 0xD4);

        public static readonly Brush BackgroundBrush = Freeze(Background);
        public static readonly Brush ForegroundBrush = Freeze(Foreground);
        public static readonly Brush AccentBrush = Freeze(Accent);

        /// <summary>
        /// 浅色档定义：从 UI4ThemeDefinition.Light() 起步（38 个令牌天然齐全），
        /// 再 With 覆盖单源色。别 new UI4ThemeDefinition("light") 只填几个令牌，
        /// 未定义的令牌在取色时抛 KeyNotFoundException。
        /// </summary>
        public static UI4ThemeDefinition BuildLightDefinition()
        {
            var def = UI4ThemeDefinition.Light();
            def.Key = "light";
            return def
                .With(UI4ThemeToken.Background, Background)
                .With(UI4ThemeToken.Surface, Mix(Background, Colors.White, 0.85f))
                .With(UI4ThemeToken.TextForeground, Foreground)
                .With(UI4ThemeToken.TextMuted, Muted)
                .With(UI4ThemeToken.Accent, Accent)
                .With(UI4ThemeToken.AccentDark, AccentHover)
                .With(UI4ThemeToken.AccentEnd, Signal)
                .With(UI4ThemeToken.BorderNormal, Muted)
                .With(UI4ThemeToken.BorderHover, Accent)
                .With(UI4ThemeToken.BorderFocus, AccentHover)
                .With(UI4ThemeToken.PanelBorder, Color.FromArgb(60, Accent.R, Accent.G, Accent.B))
                .With(UI4ThemeToken.Icon, Muted)
                .With(UI4ThemeToken.IconHover, Accent);
        }

        /// <summary>深色档定义：底色与正文由单源给，卡片描边/悬浮底这类叠加色沿用库的值。</summary>
        public static UI4ThemeDefinition BuildDarkDefinition()
        {
            var def = UI4ThemeDefinition.Dark();
            def.Key = "dark";
            return def
                .With(UI4ThemeToken.Background, DarkBackground)
                .With(UI4ThemeToken.Surface, DarkSurface)
                .With(UI4ThemeToken.TextForeground, DarkForeground)
                .With(UI4ThemeToken.TextMuted, DarkMuted)
                .With(UI4ThemeToken.Accent, DarkAccent)
                .With(UI4ThemeToken.AccentDark, DarkAccentHover)
                .With(UI4ThemeToken.AccentEnd, DarkSignal)
                .With(UI4ThemeToken.BorderNormal, DarkMuted)
                .With(UI4ThemeToken.BorderHover, DarkAccent)
                .With(UI4ThemeToken.BorderFocus, DarkAccentHover)
                .With(UI4ThemeToken.PanelBorder, Color.FromArgb(70, DarkAccent.R, DarkAccent.G, DarkAccent.B))
                .With(UI4ThemeToken.Icon, DarkMuted)
                .With(UI4ThemeToken.IconHover, DarkAccent);
        }

        /// <summary>按权重混色，保留 a 的 alpha。调对比度时用它把不达标的色推向正文色。</summary>
        public static Color Mix(Color a, Color b, float w)
        {
            if (w <= 0f) return a;
            if (w >= 1f) return b;
            return Color.FromArgb(a.A,
                (byte)(a.R + (b.R - a.R) * w),
                (byte)(a.G + (b.G - a.G) * w),
                (byte)(a.B + (b.B - a.B) * w));
        }

        /// <summary>亮度判据，与 UI4WindowTitleBar / UI4Button 挑字色用的公式一致。</summary>
        public static bool IsDark(Color c)
        {
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B < 128;
        }

        private static SolidColorBrush Freeze(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
