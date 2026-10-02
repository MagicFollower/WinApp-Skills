using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace StartUI4Controls
{
    /// <summary>
    /// 面向业务场景的预置主题套装：在内置 <see cref="UI4ThemeDefinition.Light"/> /
    /// <see cref="UI4ThemeDefinition.Dark"/> 之上派生 8 套（亮 5 + 暗 3），让业务功能直接套用而不是各自自定义配色。
    /// <code>
    /// UI4ThemePacks.RegisterAll();                 // 一次性注册全部套装
    /// UI4Theme.Apply(UI4ThemePacks.DataConsole);   // 键固定英文，可写进 UI4ThemeScope.Theme
    /// string cn = UI4ThemePacks.DisplayName(UI4Theme.ResolvedKey);   // 界面展示用中文
    /// string row = UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey); // 「数据台（亮） · data-console」，下拉用
    /// </code>
    /// 键是稳定契约（写进 XAML 与代码），中文名与深浅标记只用于展示；
    /// <see cref="DisplayName"/> / <see cref="ShadeName"/> 对未知键返回空/原样回显。
    /// 每套的取值理由写在各自的工厂方法注释里；全部 38 个令牌都有定义，未覆盖的继承基线主题。
    /// </summary>
    public static class UI4ThemePacks
    {
        /// <summary>数据台：表格/列表密集的后台，靠面板层次与网格线分层，正文对比度拉满。</summary>
        public const string DataConsole = "data-console";
        /// <summary>阅读：长文与文档，暖纸底、低饱和，强调色只用于链接与焦点。</summary>
        public const string Reading = "reading";
        /// <summary>纸白：最亮的一档纸质阅读，近纯白微暖底 + 墨褐强调。</summary>
        public const string PaperWhite = "paper-white";
        /// <summary>灰纸：白灰纸质阅读，中性灰底压一档、石墨强调，长时最不刺眼。</summary>
        public const string PaperGrey = "paper-grey";
        /// <summary>录入：表单与设置页，字段边界清晰、焦点环醒目。</summary>
        public const string Form = "form";
        /// <summary>值守：夜间长时监控大屏，压暗纯白、琥珀强调，不与业务的红/绿告警色抢位。</summary>
        public const string OnCall = "oncall";
        /// <summary>终端：日志与代码，冷青强调、层次主要靠边框而非底色台阶。</summary>
        public const string Terminal = "terminal";
        /// <summary>展示：投屏看板与媒体页，深靛底配紫→品红渐变，远距离可读。</summary>
        public const string Showcase = "showcase";

        static readonly Dictionary<string, string> _displayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "light", "浅色·通用" },
                { "dark", "深色·通用" },
                { "highcontrast", "高对比度" },
                { DataConsole, "数据台" },
                { Reading, "阅读" },
                { PaperWhite, "纸白" },
                { PaperGrey, "灰纸" },
                { Form, "录入" },
                { OnCall, "值守" },
                { Terminal, "终端" },
                { Showcase, "展示" },
            };

        /// <summary>展示文本里的深浅标记。</summary>
        public const string ShadeLight = "亮";

        /// <summary>展示文本里的深浅标记。</summary>
        public const string ShadeDark = "暗";

        static readonly Dictionary<string, string> _shades =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { DataConsole, ShadeLight },
                { Reading, ShadeLight },
                { PaperWhite, ShadeLight },
                { PaperGrey, ShadeLight },
                { Form, ShadeLight },
                { OnCall, ShadeDark },
                { Terminal, ShadeDark },
                { Showcase, ShadeDark },
            };

        /// <summary>套装的英文键，按「亮 5 + 暗 3」排列；不含内置三套。</summary>
        public static IEnumerable<string> Keys
        {
            get
            {
                return new[] { DataConsole, Reading, PaperWhite, PaperGrey, Form, OnCall, Terminal, Showcase };
            }
        }

        /// <summary>取展示名；未登记的键（含宿主自定义主题）原样返回，便于排错时看到真键名。</summary>
        public static string DisplayName(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string cn;
            return _displayNames.TryGetValue(key, out cn) ? cn : key;
        }

        /// <summary>
        /// 套装的深浅标记（<see cref="ShadeLight"/> / <see cref="ShadeDark"/>）；
        /// 内置三套与宿主自定义键返回空串 —— 它们的中文名自带深浅义，深浅也不由键名承诺。
        /// </summary>
        public static string ShadeName(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string shade;
            return _shades.TryGetValue(key, out shade) ? shade : string.Empty;
        }

        /// <summary>下拉/列表用的一行文本：「中文（亮/暗） · key」；中文名与键相同时只留一个。</summary>
        public static string DisplayLabel(string key)
        {
            string cn = DisplayName(key);
            if (cn == key) return key;
            string shade = ShadeName(key);
            if (shade.Length > 0) cn += "（" + shade + "）";
            return cn + " · " + key;
        }

        /// <summary>注册全部 8 套（可重复调用；覆盖当前生效键时库会立即重应用）。</summary>
        public static void RegisterAll()
        {
            UI4Theme.Register(DataConsoleDefinition());
            UI4Theme.Register(ReadingDefinition());
            UI4Theme.Register(PaperWhiteDefinition());
            UI4Theme.Register(PaperGreyDefinition());
            UI4Theme.Register(FormDefinition());
            UI4Theme.Register(OnCallDefinition());
            UI4Theme.Register(TerminalDefinition());
            UI4Theme.Register(ShowcaseDefinition());
        }

        /// <summary>按键取一份套装定义（每次调用都新建，可安全改令牌后再注册）。</summary>
        public static UI4ThemeDefinition DefinitionFor(string key)
        {
            switch (key)
            {
                case DataConsole: return DataConsoleDefinition();
                case Reading: return ReadingDefinition();
                case PaperWhite: return PaperWhiteDefinition();
                case PaperGrey: return PaperGreyDefinition();
                case Form: return FormDefinition();
                case OnCall: return OnCallDefinition();
                case Terminal: return TerminalDefinition();
                case Showcase: return ShowcaseDefinition();
                default: throw new ArgumentException("未知套装键: " + key, nameof(key));
            }
        }

        static UI4ThemeDefinition FromLight(string key)
        {
            UI4ThemeDefinition def = UI4ThemeDefinition.Light();
            def.Key = key;
            return def;
        }

        static UI4ThemeDefinition FromDark(string key)
        {
            UI4ThemeDefinition def = UI4ThemeDefinition.Dark();
            def.Key = key;
            return def;
        }

        /// <summary>数据台（亮）。冷灰白底 + 深蓝强调：面板比底白一档、网格线可辨，正文用近黑以拿高对比。</summary>
        public static UI4ThemeDefinition DataConsoleDefinition()
        {
            return FromLight(DataConsole)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x17, 0x49, 0x7F))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x24, 0x66, 0xA3))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0x16, 0x20, 0x2B))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0x3D, 0x4A, 0x5C))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x64, 0x74, 0x8B))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x7A, 0x8A, 0x9C))
                .With(UI4ThemeToken.Background, Color.FromRgb(0xF5, 0xF7, 0xFA))
                .With(UI4ThemeToken.Surface, Colors.White)
                .With(UI4ThemeToken.MenuBackground, Colors.White)
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0xCB, 0xD5, 0xE1))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0xB6, 0xC2, 0xD0))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0xED, 0xF1, 0xF6))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x17, 0x49, 0x7F))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0xDC, 0xE3, 0xEC))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0xE6, 0xEB, 0xF2))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0xE2, 0xE8, 0xF0))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x0E, 0x0B, 0x1F, 0x33))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x26, 0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0xEE, 0xF4, 0xFB))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0xDC, 0xEA, 0xF9))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0xF1, 0xF5, 0xF9))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0x16, 0x20, 0x2B))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0xE2, 0xE8, 0xF0))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0xCB, 0xD5, 0xE1))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0x93, 0xB4, 0xD8))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0xE9, 0xEE, 0xF4))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x64, 0x74, 0x8B))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x1D, 0x5F, 0xA8))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0x8C, 0xB6, 0xC2, 0xD0))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x2D, 0x1A, 0x24, 0x33))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0xF8, 0xFA, 0xFC))
                .With(UI4ThemeToken.BackgroundGradientEnd, Colors.White);
        }

        /// <summary>阅读（亮）。暖纸白降低大面积亮度刺激；网格线/边框收成米色阶，避免冷灰在长文里显脏。</summary>
        public static UI4ThemeDefinition ReadingDefinition()
        {
            return FromLight(Reading)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x17, 0x57, 0x54))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x22, 0x73, 0x6F))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0x23, 0x21, 0x1D))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0x4A, 0x46, 0x3F))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x6E, 0x68, 0x5F))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x8A, 0x84, 0x7A))
                .With(UI4ThemeToken.Background, Color.FromRgb(0xFB, 0xF9, 0xF4))
                .With(UI4ThemeToken.Surface, Color.FromRgb(0xFF, 0xFD, 0xFA))
                .With(UI4ThemeToken.MenuBackground, Color.FromRgb(0xFF, 0xFD, 0xF8))
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0xD9, 0xD2, 0xC4))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0xC6, 0xBE, 0xAE))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0xEF, 0xEA, 0xDF))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x17, 0x57, 0x54))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0xE4, 0xDE, 0xD1))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0xED, 0xE8, 0xDE))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0xE7, 0xE1, 0xD5))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x0F, 0x3B, 0x2F, 0x1E))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x26, 0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0xF5, 0xF1, 0xE8))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0xEA, 0xE3, 0xD3))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0xF5, 0xF1, 0xE8))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0x23, 0x21, 0x1D))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0xEA, 0xE4, 0xD8))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0xC6, 0xBE, 0xAE))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0xA9, 0xC9, 0xC6))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0xEF, 0xEA, 0xE0))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x6E, 0x68, 0x5F))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x1F, 0x6F, 0x6B))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x2C, 0x8A, 0x85))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0x8C, 0xC9, 0xC2, 0xB2))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x2D, 0x4A, 0x46, 0x3F))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0xFD, 0xFB, 0xF7))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        /// <summary>
        /// 纸白（亮）。最亮的一档纸质阅读：底近纯白只留一丝暖，面板纯白，强调用墨褐而不是蓝，
        /// 让"纸"是主角、强调只负责指路。禁用底与网格线都走暖灰，避免冷灰在白底上发脏。
        /// </summary>
        public static UI4ThemeDefinition PaperWhiteDefinition()
        {
            return FromLight(PaperWhite)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x56, 0x43, 0x2A))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x7A, 0x62, 0x42))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0x1A, 0x19, 0x17))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0x45, 0x42, 0x39))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x6E, 0x6A, 0x5F))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x7A, 0x76, 0x6B))
                .With(UI4ThemeToken.Background, Color.FromRgb(0xFC, 0xFC, 0xFA))
                .With(UI4ThemeToken.Surface, Colors.White)
                .With(UI4ThemeToken.MenuBackground, Colors.White)
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0xDE, 0xDA, 0xD0))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0xC9, 0xC4, 0xB7))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0xF0, 0xED, 0xE6))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x56, 0x43, 0x2A))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0xE5, 0xE1, 0xD8))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0xEF, 0xEC, 0xE4))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0xE9, 0xE5, 0xDC))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x0F, 0x40, 0x38, 0x2C))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x26, 0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0xF4, 0xF2, 0xEC))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0xEA, 0xE6, 0xDB))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0xF4, 0xF2, 0xEC))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0x1A, 0x19, 0x17))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0xE9, 0xE5, 0xDC))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0xC9, 0xC4, 0xB7))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0xC7, 0xB9, 0x9F))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0xF0, 0xED, 0xE6))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x6E, 0x6A, 0x5F))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x6D, 0x57, 0x38))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x7A, 0x62, 0x42))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0x8C, 0xCB, 0xC5, 0xB6))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x2D, 0x4A, 0x46, 0x3F))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0xFD, 0xFD, 0xFB))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        /// <summary>
        /// 灰纸（亮）。白灰纸质阅读：底比纸白压一档到中性浅灰，面板仍留白以分层，强调用石墨灰蓝；
        /// 长时阅读最不着眼，适合文档站与阅读器默认档。灰阶刻意不带蓝，避免和「数据台」的冷蓝混成一片。
        /// </summary>
        public static UI4ThemeDefinition PaperGreyDefinition()
        {
            return FromLight(PaperGrey)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x3E, 0x48, 0x52))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x5A, 0x67, 0x73))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0x1F, 0x21, 0x23))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0x46, 0x4A, 0x4E))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x6A, 0x6F, 0x74))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x76, 0x7B, 0x80))
                .With(UI4ThemeToken.Background, Color.FromRgb(0xF1, 0xF1, 0xEF))
                .With(UI4ThemeToken.Surface, Color.FromRgb(0xFA, 0xFA, 0xF9))
                .With(UI4ThemeToken.MenuBackground, Color.FromRgb(0xFA, 0xFA, 0xF9))
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0xD6, 0xD6, 0xD3))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0xBF, 0xBF, 0xBB))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0xE8, 0xE8, 0xE5))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x3E, 0x48, 0x52))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0xDE, 0xDE, 0xDA))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0xE9, 0xE9, 0xE6))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0xE2, 0xE2, 0xDE))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x0F, 0x33, 0x36, 0x38))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x26, 0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0xEA, 0xEA, 0xE7))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0xDD, 0xDE, 0xDA))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0xEA, 0xEA, 0xE7))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0x1F, 0x21, 0x23))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0xE2, 0xE2, 0xDE))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0xBF, 0xBF, 0xBB))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0xAF, 0xBA, 0xC2))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0xE8, 0xE8, 0xE5))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x6A, 0x6F, 0x74))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x4E, 0x5A, 0x63))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x5A, 0x67, 0x73))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0x8C, 0xB9, 0xB9, 0xB5))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x2D, 0x46, 0x49, 0x4B))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0xF4, 0xF4, 0xF2))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0xFA, 0xFA, 0xFA));
        }

        /// <summary>录入（亮）。边框比其它亮色套装更重（字段边界是主角），焦点环直接用强调青；禁用底压成冷灰以显"不可写"。</summary>
        public static UI4ThemeDefinition FormDefinition()
        {
            return FromLight(Form)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x08, 0x5C, 0x6B))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x15, 0x7A, 0x8C))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0x10, 0x20, 0x2A))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0x33, 0x49, 0x5A))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x5E, 0x74, 0x84))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x77, 0x8C, 0x9B))
                .With(UI4ThemeToken.Background, Color.FromRgb(0xF7, 0xF9, 0xFB))
                .With(UI4ThemeToken.Surface, Colors.White)
                .With(UI4ThemeToken.MenuBackground, Colors.White)
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0xB9, 0xC6, 0xD2))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0x94, 0xA6, 0xB5))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0xEF, 0xF4, 0xF8))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x08, 0x5C, 0x6B))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0xC9, 0xD6, 0xE2))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0xE3, 0xEB, 0xF1))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0xE3, 0xEB, 0xF1))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x0F, 0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x26, 0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0xEA, 0xF5, 0xF8))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0xD6, 0xEE, 0xF4))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0xEE, 0xF4, 0xF8))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0x10, 0x20, 0x2A))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0xDD, 0xE7, 0xEE))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0xB9, 0xC6, 0xD2))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0x8F, 0xC6, 0xD4))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0xEA, 0xF0, 0xF4))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x5E, 0x74, 0x84))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x0B, 0x72, 0x85))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x1B, 0x93, 0xA8))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0x8C, 0xAF, 0xC2, 0xCE))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x2D, 0x10, 0x20, 0x2A))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0xF7, 0xFA, 0xFC))
                .With(UI4ThemeToken.BackgroundGradientEnd, Colors.White);
        }

        /// <summary>值守（暗）。正文刻意不用纯白（长时下瞄刺眼），强调色取琥珀——把红/绿留给业务的告警语义。</summary>
        public static UI4ThemeDefinition OnCallDefinition()
        {
            return FromDark(OnCall)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0xE0, 0xA4, 0x58))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0xB9, 0x86, 0x3F))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0xF0, 0xC0, 0x7A))
                .With(UI4ThemeToken.OnAccent, Color.FromRgb(0x10, 0x14, 0x18))
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0xD8, 0xDE, 0xE6))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0xAE, 0xB8, 0xC4))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x8C, 0x97, 0xA4))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x74, 0x7F, 0x8C))
                .With(UI4ThemeToken.Background, Color.FromRgb(0x0E, 0x11, 0x16))
                .With(UI4ThemeToken.Surface, Color.FromRgb(0x16, 0x1B, 0x22))
                .With(UI4ThemeToken.MenuBackground, Color.FromRgb(0x16, 0x1B, 0x22))
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0x33, 0x3D, 0x48))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0x40, 0x4B, 0x57))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0x1A, 0x21, 0x2A))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0xE0, 0xA4, 0x58))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0xF0, 0xC0, 0x7A))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0x2A, 0x32, 0x3C))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0x1E, 0x25, 0x2D))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0x22, 0x2A, 0x33))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x2E, 0xE0, 0xA4, 0x58))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0x1B, 0x22, 0x2B))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0x24, 0x30, 0x3C))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x2F, 0x3E, 0x4E))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0x12, 0x17, 0x1D))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0xD8, 0xDE, 0xE6))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0x26, 0x2F, 0x3A))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0xE0, 0xA4, 0x58))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0x3A, 0x45, 0x50))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0x4A, 0x58, 0x66))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0x1A, 0x20, 0x28))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x8C, 0x97, 0xA4))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0xE0, 0xA4, 0x58))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0xF0, 0xC0, 0x7A))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0xB3, 0x3A, 0x45, 0x50))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x73, 0x00, 0x00, 0x00))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0x0E, 0x11, 0x16))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0x16, 0x1B, 0x22));
        }

        /// <summary>终端（暗）。层次主要交给边框与更深的底，底色台阶刻意压小——日志里大面积底色不该抢文字。</summary>
        public static UI4ThemeDefinition TerminalDefinition()
        {
            return FromDark(Terminal)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x3F, 0xB6, 0xD3))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x2E, 0x8C, 0xA6))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0x6F, 0xD3, 0xE8))
                .With(UI4ThemeToken.OnAccent, Color.FromRgb(0x06, 0x13, 0x1F))
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0xD6, 0xE4, 0xF0))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0xA7, 0xBA, 0xCD))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x87, 0x9C, 0xB2))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x6E, 0x82, 0x97))
                .With(UI4ThemeToken.Background, Color.FromRgb(0x0B, 0x12, 0x20))
                .With(UI4ThemeToken.Surface, Color.FromRgb(0x11, 0x1A, 0x2B))
                .With(UI4ThemeToken.MenuBackground, Color.FromRgb(0x11, 0x1A, 0x2B))
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0x26, 0x36, 0x50))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0x33, 0x45, 0x61))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0x13, 0x1E, 0x31))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x3F, 0xB6, 0xD3))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0x6F, 0xD3, 0xE8))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0x1F, 0x2C, 0x44))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0x17, 0x23, 0x3A))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0x1B, 0x29, 0x42))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x2E, 0x3F, 0xB6, 0xD3))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0x16, 0x23, 0x3A))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0x1D, 0x33, 0x50))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x24, 0x46, 0x6B))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0x0E, 0x17, 0x27))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0xD6, 0xE4, 0xF0))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0x1B, 0x29, 0x42))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x3F, 0xB6, 0xD3))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0x2C, 0x3E, 0x5A))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0x3E, 0x56, 0x75))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0x14, 0x1D, 0x2E))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x87, 0x9C, 0xB2))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0x3F, 0xB6, 0xD3))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0x6F, 0xD3, 0xE8))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0xB3, 0x2C, 0x3E, 0x5A))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x80, 0x00, 0x08, 0x14))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0x0B, 0x12, 0x20))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0x11, 0x1A, 0x2B));
        }

        /// <summary>展示（暗）。投屏距离远，正文最亮、强调色饱和拉高；紫→品红两端都单独验过白字对比度。</summary>
        public static UI4ThemeDefinition ShowcaseDefinition()
        {
            return FromDark(Showcase)
                .With(UI4ThemeToken.Accent, Color.FromRgb(0x6B, 0x4B, 0xE8))
                .With(UI4ThemeToken.AccentDark, Color.FromRgb(0x57, 0x38, 0xC9))
                .With(UI4ThemeToken.AccentEnd, Color.FromRgb(0xB8, 0x43, 0x9A))
                .With(UI4ThemeToken.OnAccent, Colors.White)
                .With(UI4ThemeToken.TextForeground, Color.FromRgb(0xEE, 0xF0, 0xFA))
                .With(UI4ThemeToken.TextSecondary, Color.FromRgb(0xC4, 0xC9, 0xE8))
                .With(UI4ThemeToken.TextMuted, Color.FromRgb(0x9F, 0xA6, 0xCC))
                .With(UI4ThemeToken.Placeholder, Color.FromRgb(0x86, 0x8D, 0xB8))
                .With(UI4ThemeToken.Background, Color.FromRgb(0x17, 0x1A, 0x2E))
                .With(UI4ThemeToken.Surface, Color.FromRgb(0x22, 0x26, 0x45))
                .With(UI4ThemeToken.MenuBackground, Color.FromRgb(0x22, 0x26, 0x45))
                .With(UI4ThemeToken.BorderNormal, Color.FromRgb(0x3E, 0x45, 0x76))
                .With(UI4ThemeToken.BorderSecondary, Color.FromRgb(0x4C, 0x54, 0x90))
                .With(UI4ThemeToken.BorderWeak, Color.FromRgb(0x1F, 0x23, 0x40))
                .With(UI4ThemeToken.BorderHover, Color.FromRgb(0x6B, 0x4B, 0xE8))
                .With(UI4ThemeToken.BorderFocus, Color.FromRgb(0xB8, 0x43, 0x9A))
                .With(UI4ThemeToken.PanelBorder, Color.FromRgb(0x34, 0x3A, 0x63))
                .With(UI4ThemeToken.GridLine, Color.FromRgb(0x2A, 0x2F, 0x55))
                .With(UI4ThemeToken.Separator, Color.FromRgb(0x30, 0x36, 0x5F))
                .With(UI4ThemeToken.HoverOverlay, Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF))
                .With(UI4ThemeToken.SelectedOverlay, Color.FromArgb(0x33, 0x6B, 0x4B, 0xE8))
                .With(UI4ThemeToken.RowHoverBackground, Color.FromRgb(0x2A, 0x2F, 0x55))
                .With(UI4ThemeToken.RowSelectedBackground, Color.FromRgb(0x35, 0x40, 0x7A))
                .With(UI4ThemeToken.ListSelected, Color.FromRgb(0x4C, 0x54, 0x90))
                .With(UI4ThemeToken.HeaderBackground, Color.FromRgb(0x1B, 0x1E, 0x36))
                .With(UI4ThemeToken.HeaderForeground, Color.FromRgb(0xEE, 0xF0, 0xFA))
                .With(UI4ThemeToken.TrackBackground, Color.FromRgb(0x2C, 0x31, 0x59))
                .With(UI4ThemeToken.CheckBackground, Color.FromRgb(0x6B, 0x4B, 0xE8))
                .With(UI4ThemeToken.CheckBoxUnchecked, Color.FromRgb(0x44, 0x4B, 0x7C))
                .With(UI4ThemeToken.HoverBorderColorLight, Color.FromRgb(0x5A, 0x63, 0x98))
                .With(UI4ThemeToken.OffBackground, Color.FromRgb(0x23, 0x27, 0x41))
                .With(UI4ThemeToken.Icon, Color.FromRgb(0x9F, 0xA6, 0xCC))
                .With(UI4ThemeToken.IconHover, Color.FromRgb(0xB8, 0x43, 0x9A))
                .With(UI4ThemeToken.ProgressStart, Color.FromRgb(0xB8, 0x43, 0x9A))
                .With(UI4ThemeToken.ScrollBarThumb, Color.FromArgb(0xB3, 0x44, 0x4B, 0x7C))
                .With(UI4ThemeToken.Shadow, Color.FromArgb(0x8C, 0x05, 0x07, 0x0F))
                .With(UI4ThemeToken.BackgroundGradientStart, Color.FromRgb(0x17, 0x1A, 0x2E))
                .With(UI4ThemeToken.BackgroundGradientEnd, Color.FromRgb(0x22, 0x26, 0x45));
        }
    }
}
