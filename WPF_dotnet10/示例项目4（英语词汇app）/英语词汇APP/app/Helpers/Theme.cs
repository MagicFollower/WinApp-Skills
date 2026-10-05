using System;
using System.Windows.Media;

namespace VocabDesk.Helpers
{
    /// <summary>
    /// 宿主配色单源：改观感只改这里的常量，别散着改控件属性。
    /// 这份常量表是"固定档"，不随 UI4Theme.SetTheme 变化；要跟主题走的色
    /// 一律在 XAML 里写 {DynamicResource UI4.Brush.*}。
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Background = Color.FromRgb(0xF7, 0xF9, 0xFB);
        public static readonly Color Foreground = Color.FromRgb(0x10, 0x20, 0x2A);
        public static readonly Color Muted = Color.FromRgb(0x9A, 0xAA, 0xB8);
        public static readonly Color Accent = Color.FromRgb(0x4F, 0x6B, 0xE8);
        public static readonly Color AccentHover = Color.FromRgb(0x43, 0x5A, 0xC5);
        public static readonly Color Signal = Color.FromRgb(0x1B, 0x93, 0xA8);

        /// <summary>
        /// 明暗策略：必须在开工前与需求方确认后改成三个值之一（both / light-only / dark-only），
        /// 留在 "TODO" 时 `--selftest` 会红并被发布脚本挡下。它决定三件事：
        /// 要不要注册第二套定义、App 启动时 SetTheme 钉哪个档、以及要不要给「跟随系统」按钮。
        /// 只做一档也要显式钉住当前档，别把 SetTheme 整段删掉——那样库会按系统高对比度与
        /// AppsUseLightTheme 自行解析，用户系统一改，你的"单档"就跟着改了。
        /// </summary>
        public const string Policy = "both";

        public static readonly Brush BackgroundBrush = Freeze(Background);
        public static readonly Brush ForegroundBrush = Freeze(Foreground);
        public static readonly Brush AccentBrush = Freeze(Accent);

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
