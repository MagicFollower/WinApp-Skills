using System.Windows.Media;
using PromptFavorites.Models;

namespace PromptFavorites.Helpers
{
    /// <summary>
    /// 全局配色的<b>源色</b>单源：两档（终端靛 / 终端靛·夜）各自的一组语义色。
    /// 界面上任何一处取色都不该抄这里的十六进制，要么走 <see cref="HostPalette"/> 派生出的
    /// 38 个主题令牌（<c>UI4.Color.*</c> / <c>UI4.Brush.*</c>），要么就是这里明确标注的语义色。
    /// </summary>
    public static class Theme
    {
        /// <summary>当前档位。由 <see cref="HostPalette.Apply"/> 写入，供代码侧做诊断与默认值参考。</summary>
        public static AppThemeMode Mode { get; internal set; }

        // ── 浅色「终端靛」（源色取自 100-themes 的 terminal-blue/day 后按浅亮系重调）──

        public static readonly Color Background = Color.FromRgb(0xF9, 0xFB, 0xFF);
        public static readonly Color Panel = Color.FromRgb(0xEA, 0xF1, 0xFB);
        public static readonly Color Foreground = Color.FromRgb(0x1B, 0x27, 0x37);
        public static readonly Color Secondary = Color.FromRgb(0x67, 0x73, 0x82);
        public static readonly Color Muted = Color.FromRgb(0xBC, 0xC7, 0xD6);
        public static readonly Color Selection = Color.FromRgb(0xE2, 0xE5, 0xFB);
        public static readonly Color Signal = Color.FromRgb(0x0C, 0x8B, 0xA8);

        public static readonly Color Accent = Color.FromRgb(0x4F, 0x6B, 0xE8);
        public static readonly Color AccentHover = Color.FromRgb(0x43, 0x5A, 0xC5);

        /// <summary>中性按钮的源色。亮档必须浅（L≈0.70）：库在 OnAccent 与正文色之间取对比度更高者，
        /// 若这里给中灰，白字压上去只有 2.4:1。</summary>
        public static readonly Color NeutralSource = Color.FromRgb(0xD3, 0xDB, 0xE6);

        // ── 夜景「终端靛·夜」──

        public static readonly Color NightBackground = Color.FromRgb(0x0E, 0x16, 0x22);
        public static readonly Color NightForeground = Color.FromRgb(0xE8, 0xEE, 0xF8);
        public static readonly Color NightBorder = Color.FromRgb(0x33, 0x41, 0x5A);
        public static readonly Color NightAccent = Color.FromRgb(0x7C, 0x93, 0xFF);
        public static readonly Color NightAccentDeep = Color.FromRgb(0x5F, 0x78, 0xE6);
        public static readonly Color NightSignal = Color.FromRgb(0x2B, 0xB6, 0xD4);

        /// <summary>压在主色上的深墨：夜景档的强调色是亮靛蓝，白字压上去只有 2.67:1，深墨有 6.7:1。</summary>
        public static readonly Color NightInk = Color.FromRgb(0x0B, 0x12, 0x20);

        // ── 语义色：收藏星标。语义色不跟界面配色走，两档同一个金 ──

        public static readonly Color Favorite = Color.FromRgb(0xFF, 0xB8, 0x00);
        public static readonly Color FavoriteEnd = Color.FromRgb(0xFF, 0x8C, 0x00);
        public static readonly Color FavoriteHover = Color.FromRgb(0xE0, 0x8A, 0x00);

        public static readonly SolidColorBrush FavoriteBrush = Frozen(Favorite);

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
