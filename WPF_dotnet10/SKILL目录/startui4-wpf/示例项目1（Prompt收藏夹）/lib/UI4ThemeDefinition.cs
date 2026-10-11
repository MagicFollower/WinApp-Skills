using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace StartUI4Controls
{
    /// <summary>
    /// 一份主题的颜色数据：键为 <see cref="UI4ThemeToken"/>，值为对应 <see cref="Color"/>。
    /// 内置 <see cref="Light"/> 与 <see cref="Dark"/> 两份；宿主可 <see cref="Clone"/> 后改令牌再注册，实现自定义主题。
    /// </summary>
    public sealed class UI4ThemeDefinition
    {
        private readonly Dictionary<UI4ThemeToken, Color> _colors;

        public UI4ThemeDefinition(string key)
        {
            Key = key;
            _colors = new Dictionary<UI4ThemeToken, Color>();
        }

        /// <summary>主题标识，如 "light" / "dark" / 自定义键。</summary>
        public string Key { get; set; }

        /// <summary>取令牌颜色；未定义时抛 <see cref="KeyNotFoundException"/>。</summary>
        public Color GetColor(UI4ThemeToken token)
        {
            return _colors[token];
        }

        /// <summary>是否定义了该令牌。</summary>
        public bool Has(UI4ThemeToken token)
        {
            return _colors.ContainsKey(token);
        }

        /// <summary>设置单个令牌（链式）。</summary>
        public UI4ThemeDefinition With(UI4ThemeToken token, Color color)
        {
            _colors[token] = color;
            return this;
        }

        /// <summary>深拷贝，用于在内置主题基础上派生自定义主题。</summary>
        public UI4ThemeDefinition Clone()
        {
            var copy = new UI4ThemeDefinition(Key + ".clone");
            foreach (var kv in _colors) copy._colors[kv.Key] = kv.Value;
            return copy;
        }

        private static UI4ThemeDefinition Build(string key, Color accent, Color accentDark, Color accentEnd,
            Color text, Color textSecondary, Color background, Color surface,
            Color border, Color borderSecondary, Color borderHover, Color borderFocus,
            Color placeholder, Color hoverOverlay, Color selectedOverlay, Color track,
            Color check, Color icon, Color iconHover, Color panelBorder,
            Color off, Color menu, Color listSelected, Color headerBg, Color headerFg,
            Color rowHover, Color rowSelected, Color gridLine, Color progressStart,
            Color checkBoxUnchecked, Color hoverBorderLight,
            Color onAccent, Color shadow, Color scrollBarThumb,
            Color separator, Color borderWeak, Color textMuted,
            Color backgroundGradientStart, Color backgroundGradientEnd)
        {
            return new UI4ThemeDefinition(key)
                .With(UI4ThemeToken.Accent, accent)
                .With(UI4ThemeToken.AccentDark, accentDark)
                .With(UI4ThemeToken.AccentEnd, accentEnd)
                .With(UI4ThemeToken.TextForeground, text)
                .With(UI4ThemeToken.TextSecondary, textSecondary)
                .With(UI4ThemeToken.Background, background)
                .With(UI4ThemeToken.Surface, surface)
                .With(UI4ThemeToken.BorderNormal, border)
                .With(UI4ThemeToken.BorderSecondary, borderSecondary)
                .With(UI4ThemeToken.BorderHover, borderHover)
                .With(UI4ThemeToken.BorderFocus, borderFocus)
                .With(UI4ThemeToken.Placeholder, placeholder)
                .With(UI4ThemeToken.HoverOverlay, hoverOverlay)
                .With(UI4ThemeToken.SelectedOverlay, selectedOverlay)
                .With(UI4ThemeToken.TrackBackground, track)
                .With(UI4ThemeToken.CheckBackground, check)
                .With(UI4ThemeToken.Icon, icon)
                .With(UI4ThemeToken.IconHover, iconHover)
                .With(UI4ThemeToken.PanelBorder, panelBorder)
                .With(UI4ThemeToken.OffBackground, off)
                .With(UI4ThemeToken.MenuBackground, menu)
                .With(UI4ThemeToken.ListSelected, listSelected)
                .With(UI4ThemeToken.HeaderBackground, headerBg)
                .With(UI4ThemeToken.HeaderForeground, headerFg)
                .With(UI4ThemeToken.RowHoverBackground, rowHover)
                .With(UI4ThemeToken.RowSelectedBackground, rowSelected)
                .With(UI4ThemeToken.GridLine, gridLine)
                .With(UI4ThemeToken.ProgressStart, progressStart)
                .With(UI4ThemeToken.CheckBoxUnchecked, checkBoxUnchecked)
                .With(UI4ThemeToken.HoverBorderColorLight, hoverBorderLight)
                .With(UI4ThemeToken.OnAccent, onAccent)
                .With(UI4ThemeToken.Shadow, shadow)
                .With(UI4ThemeToken.ScrollBarThumb, scrollBarThumb)
                .With(UI4ThemeToken.Separator, separator)
                .With(UI4ThemeToken.BorderWeak, borderWeak)
                .With(UI4ThemeToken.TextMuted, textMuted)
                .With(UI4ThemeToken.BackgroundGradientStart, backgroundGradientStart)
                .With(UI4ThemeToken.BackgroundGradientEnd, backgroundGradientEnd);
        }

        /// <summary>内置亮色主题（与移植时点的 CreateLight 取值一致）。</summary>
        public static UI4ThemeDefinition Light()
        {
            return Build("light",
                accent: Color.FromRgb(0, 120, 212),
                accentDark: Color.FromRgb(0, 102, 181),
                accentEnd: Color.FromRgb(147, 51, 234),
                text: Color.FromRgb(30, 30, 30),
                textSecondary: Colors.Black,
                background: Colors.White,
                surface: Colors.White,
                border: Color.FromRgb(200, 200, 220),
                borderSecondary: Color.FromRgb(180, 180, 200),
                borderHover: Color.FromRgb(0, 120, 212),
                borderFocus: Color.FromRgb(0, 102, 181),
                placeholder: Colors.LightGray,
                hoverOverlay: Color.FromArgb(20, 0, 0, 0),
                selectedOverlay: Color.FromArgb(10, 0, 0, 0),
                track: Color.FromArgb(10, 0, 0, 0),
                check: Color.FromRgb(0, 102, 181),
                icon: Color.FromRgb(120, 120, 140),
                iconHover: Color.FromRgb(60, 60, 80),
                panelBorder: Color.FromArgb(60, 120, 140, 200),
                off: Color.FromRgb(200, 200, 210),
                menu: Color.FromRgb(248, 248, 248),
                listSelected: Color.FromRgb(37, 99, 235),
                headerBg: Color.FromRgb(245, 245, 245),
                headerFg: Color.FromRgb(30, 30, 30),
                rowHover: Color.FromRgb(240, 240, 245),
                rowSelected: Color.FromRgb(211, 211, 211),
                gridLine: Color.FromRgb(230, 230, 235),
                progressStart: Color.FromRgb(0, 150, 230),
                checkBoxUnchecked: Colors.LightGray,
                hoverBorderLight: Color.FromRgb(140, 140, 170),
                onAccent: Colors.White,
                shadow: Colors.Black,
                scrollBarThumb: Color.FromArgb(0x50, 0, 0, 0),
                separator: Color.FromRgb(220, 220, 220),
                borderWeak: Color.FromArgb(0x1A, 0, 0, 0),
                textMuted: Color.FromArgb(0xC8, 0, 0, 0),
                backgroundGradientStart: Color.FromRgb(225, 236, 245),
                backgroundGradientEnd: Colors.White);
        }

        /// <summary>内置暗色主题（与移植时点的 CreateDark 取值一致）。</summary>
        public static UI4ThemeDefinition Dark()
        {
            return Build("dark",
                accent: Color.FromRgb(0, 153, 255),
                accentDark: Color.FromRgb(0, 120, 212),
                accentEnd: Color.FromRgb(100, 40, 200),
                text: Color.FromRgb(230, 230, 230),
                textSecondary: Color.FromRgb(204, 204, 204),
                background: Color.FromRgb(32, 32, 38),
                surface: Color.FromRgb(40, 40, 48),
                border: Color.FromRgb(60, 60, 75),
                borderSecondary: Color.FromRgb(54, 54, 74),
                borderHover: Color.FromRgb(0, 153, 255),
                borderFocus: Color.FromRgb(0, 120, 212),
                placeholder: Color.FromRgb(128, 128, 128),
                hoverOverlay: Color.FromArgb(20, 255, 255, 255),
                selectedOverlay: Color.FromArgb(10, 255, 255, 255),
                track: Color.FromArgb(20, 255, 255, 255),
                check: Color.FromRgb(0, 140, 210),
                icon: Color.FromRgb(160, 160, 180),
                iconHover: Color.FromRgb(200, 200, 220),
                panelBorder: Color.FromArgb(60, 100, 120, 180),
                off: Color.FromRgb(60, 60, 70),
                menu: Color.FromRgb(45, 45, 50),
                listSelected: Color.FromRgb(59, 123, 255),
                headerBg: Color.FromRgb(42, 42, 48),
                headerFg: Color.FromRgb(230, 230, 230),
                rowHover: Color.FromRgb(46, 46, 56),
                rowSelected: Color.FromRgb(58, 58, 72),
                gridLine: Color.FromRgb(58, 58, 69),
                progressStart: Color.FromRgb(0, 170, 255),
                checkBoxUnchecked: Color.FromRgb(80, 80, 88),
                hoverBorderLight: Color.FromRgb(96, 96, 120),
                onAccent: Colors.White,
                shadow: Colors.Black,
                scrollBarThumb: Color.FromArgb(0x66, 255, 255, 255),
                separator: Color.FromRgb(64, 64, 72),
                borderWeak: Color.FromArgb(0x33, 255, 255, 255),
                textMuted: Color.FromArgb(0x99, 255, 255, 255),
                backgroundGradientStart: Color.FromRgb(38, 38, 46),
                backgroundGradientEnd: Color.FromRgb(32, 32, 38));
        }

        /// <summary>
        /// 高对比度主题：纯黑底、白字白框、黄色强调，勾选/选中底用深蓝以保证白色对勾与文字仍可读。
        /// 供 <see cref="UI4Theme.Apply"/>("highcontrast")、<see cref="UI4ThemeMode.HighContrast"/>
        /// 与 <see cref="UI4ThemeScope"/> 使用。
        /// </summary>
        public static UI4ThemeDefinition HighContrast()
        {
            return Build("highcontrast",
                accent: Color.FromRgb(255, 255, 0),
                accentDark: Color.FromRgb(221, 221, 0),
                accentEnd: Color.FromRgb(255, 255, 0),
                text: Colors.White,
                textSecondary: Colors.White,
                background: Colors.Black,
                surface: Colors.Black,
                border: Colors.White,
                borderSecondary: Colors.White,
                borderHover: Color.FromRgb(255, 255, 0),
                borderFocus: Color.FromRgb(255, 255, 0),
                placeholder: Color.FromRgb(204, 204, 204),
                hoverOverlay: Color.FromArgb(51, 255, 255, 0),
                selectedOverlay: Color.FromArgb(77, 255, 255, 0),
                track: Color.FromArgb(51, 255, 255, 255),
                check: Color.FromRgb(0, 0, 139),
                icon: Colors.White,
                iconHover: Color.FromRgb(255, 255, 0),
                panelBorder: Colors.White,
                off: Color.FromRgb(58, 58, 58),
                menu: Colors.Black,
                listSelected: Color.FromRgb(0, 0, 139),
                headerBg: Color.FromRgb(16, 16, 16),
                headerFg: Colors.White,
                rowHover: Color.FromArgb(51, 255, 255, 255),
                rowSelected: Color.FromRgb(0, 0, 139),
                gridLine: Colors.White,
                progressStart: Color.FromRgb(255, 255, 0),
                checkBoxUnchecked: Colors.Black,
                hoverBorderLight: Color.FromRgb(255, 255, 0),
                // 高对比度强调色为黄，黄底上必须黑字才成立对比度
                onAccent: Colors.Black,
                shadow: Colors.Black,
                scrollBarThumb: Colors.White,
                separator: Colors.White,
                borderWeak: Colors.White,
                textMuted: Colors.White,
                backgroundGradientStart: Colors.Black,
                backgroundGradientEnd: Colors.Black);
        }
    }
}
