using System;
using System.Globalization;
using System.Windows.Media;

namespace MemoTask.Helpers
{
    /// <summary>
    /// 宿主配色单源：改观感只改这里的常量，别散着改控件属性。
    /// 明档与暗档各一份常量表，由 App.RegisterAppTheme 铺成库的 38 个语义令牌；
    /// 界面里要跟主题走的颜色一律写 {DynamicResource UI4.Brush.*}，不要引用这里的常量。
    /// </summary>
    internal static class Theme
    {
        /// <summary>
        /// 明暗策略：both = 注册亮+暗两份宿主定义，启动按设置里的请求模式解析（默认真的跟随系统），
        /// 设置页给「浅色 / 深色 / 跟随系统 / 高对比度」四挡切换。
        /// 这个值留在 "TODO" 时 `--selftest` 会红并被发布脚本挡下。
        /// </summary>
        public const string Policy = "both";

        // 亮档
        public static readonly Color LightBackground = Color.FromRgb(0xF7, 0xF9, 0xFB);
        public static readonly Color LightForeground = Color.FromRgb(0x10, 0x20, 0x2A);
        public static readonly Color LightMuted = Color.FromRgb(0x6B, 0x7C, 0x8C);
        public static readonly Color LightGradientStart = Color.FromRgb(0xEF, 0xF4, 0xF9);
        public static readonly Color LightGradientEnd = Color.FromRgb(0xFA, 0xFC, 0xFD);

        // 暗档
        public static readonly Color DarkBackground = Color.FromRgb(0x1B, 0x20, 0x28);
        public static readonly Color DarkSurface = Color.FromRgb(0x24, 0x2B, 0x35);
        public static readonly Color DarkForeground = Color.FromRgb(0xE6, 0xEC, 0xF2);
        public static readonly Color DarkMuted = Color.FromRgb(0x9A, 0xAB, 0xBA);
        public static readonly Color DarkAccent = Color.FromRgb(0x7A, 0x93, 0xF5);
        public static readonly Color DarkGradientStart = Color.FromRgb(0x23, 0x2A, 0x34);
        public static readonly Color DarkGradientEnd = Color.FromRgb(0x19, 0x1E, 0x25);

        // 品牌强调色：亮暗两档共用一份，暗档自动提亮一档，避免 #4F6BE8 压在近黑底上发闷。
        public static readonly Color Accent = Color.FromRgb(0x4F, 0x6B, 0xE8);
        public static readonly Color AccentHover = Color.FromRgb(0x43, 0x5A, 0xC5);
        public static readonly Color Signal = Color.FromRgb(0x1B, 0x93, 0xA8);

        /// <summary>
        /// 状态色。38 个令牌里没有「危险 / 警示」这类语义位（见组件手册 §4.1 分组表），
        /// 所以它们是有意的固定色相，不随主题走：逾期就该是红的，换肤不该改变含义。
        /// 取值亮暗两档都能压住（对亮底与近黑底的对比度都在 3.6:1 以上）。
        /// </summary>
        public static readonly Color Danger = Color.FromRgb(0xE5, 0x48, 0x4D);
        public static readonly Color Warning = Color.FromRgb(0xB2, 0x6A, 0x00);
        public static readonly Color Success = Color.FromRgb(0x21, 0x8A, 0x5B);

        /// <summary>设置页「恢复默认强调色」用的预设色板。</summary>
        public static readonly Color[] AccentPresets =
        {
            Color.FromRgb(0x4F, 0x6B, 0xE8),
            Color.FromRgb(0x1B, 0x93, 0xA8),
            Color.FromRgb(0x21, 0x8A, 0x5B),
            Color.FromRgb(0xB2, 0x6A, 0x00),
            Color.FromRgb(0xE5, 0x48, 0x4D),
            Color.FromRgb(0x8B, 0x5C, 0xF6),
        };

        public static readonly Brush AccentBrush = Freeze(Accent);
        public static readonly Brush DangerBrush = Freeze(Danger);
        public static readonly Brush WarningBrush = Freeze(Warning);

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

        /// <summary>按当前生效主题的底色判深浅，供状态色与前景挑档。</summary>
        public static Color OnAccentFor(Color accent)
        {
            return IsDark(accent) ? Colors.White : Color.FromRgb(0x10, 0x10, 0x10);
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        /// <summary>接受 #RRGGBB / RRGGBB / #AARRGGBB；解析失败返回 false，不抛。</summary>
        public static bool TryParseHex(string text, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            if (s[0] == '#') s = s.Substring(1);
            if (s.Length != 6 && s.Length != 8) return false;
            byte a = 255, r, g, b;
            int i = 0;
            if (s.Length == 8)
            {
                if (!TryByte(s.Substring(0, 2), out a)) return false;
                i = 2;
            }
            if (!TryByte(s.Substring(i, 2), out r)) return false;
            if (!TryByte(s.Substring(i + 2, 2), out g)) return false;
            if (!TryByte(s.Substring(i + 4, 2), out b)) return false;
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        private static bool TryByte(string hex, out byte value)
        {
            ushort v;
            if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
            {
                value = (byte)v;
                return true;
            }
            value = 0;
            return false;
        }

        private static SolidColorBrush Freeze(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
