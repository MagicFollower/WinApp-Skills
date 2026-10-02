using System.Windows.Media;

namespace PromptFavorites.Helpers
{
    /// <summary>
    /// 全局配色：单一定死的浅色方案（终端靛，源色取自 100-themes 的 terminal-blue/day 变体后按浅亮系重调）。
    /// 这里放的是界面直接取用的几个色；完整的 30 个主题令牌由 App.RegisterAppTheme() 从这里派生。
    /// </summary>
    public static class Theme
    {
        // 源色
        public static readonly Color Background = Color.FromRgb(0xF9, 0xFB, 0xFF);
        public static readonly Color Panel = Color.FromRgb(0xEA, 0xF1, 0xFB);
        public static readonly Color Foreground = Color.FromRgb(0x1B, 0x27, 0x37);
        public static readonly Color Secondary = Color.FromRgb(0x67, 0x73, 0x82);
        public static readonly Color Muted = Color.FromRgb(0xBC, 0xC7, 0xD6);
        public static readonly Color Selection = Color.FromRgb(0xE2, 0xE5, 0xFB);
        public static readonly Color Signal = Color.FromRgb(0x0C, 0x8B, 0xA8);

        // 界面用色
        public static readonly Color Accent = Color.FromRgb(0x4F, 0x6B, 0xE8);
        public static readonly Color AccentHover = Color.FromRgb(0x43, 0x5A, 0xC5);
        public static readonly Color Neutral = Color.FromRgb(0xD3, 0xDB, 0xE6);
        public static readonly Color NeutralHover = Color.FromRgb(0xEF, 0xF2, 0xFE);
        public static readonly Color Favorite = Color.FromRgb(0xFF, 0xB8, 0x00);
        public static readonly Color FavoriteEnd = Color.FromRgb(0xFF, 0x8C, 0x00);
        public static readonly Color FavoriteHover = Color.FromRgb(0xE0, 0x8A, 0x00);

        public static readonly SolidColorBrush AccentBrush = Frozen(Accent);
        public static readonly SolidColorBrush AccentHoverBrush = Frozen(AccentHover);
        public static readonly SolidColorBrush NeutralBrush = Frozen(Neutral);
        public static readonly SolidColorBrush NeutralHoverBrush = Frozen(NeutralHover);
        public static readonly SolidColorBrush FavoriteHoverBrush = Frozen(FavoriteHover);

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
