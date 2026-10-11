using System;
using System.Collections.Generic;
using System.Windows.Media;
using MemoTask.Helpers;
using MemoTask.Models;
using StartUI4Controls;

namespace MemoTask.Services
{
    /// <summary>
    /// 主题的应用与回读都走这里，界面不直接调 UI4Theme，避免四处 SetTheme 打架。
    /// 两条硬约束写死在方法里：注册必须在 base.OnStartup 之后（库在 Application.Current 为空时静默不装字典），
    /// 以及 SetAccent 只改「当前解析出来的那一个键」的定义，所以换档后必须补一次强调色。
    /// </summary>
    internal static class ThemeService
    {
        private static bool _suppressAccentReapply;

        /// <summary>App 启动时注册的两份宿主定义（亮 / 暗），高对比度继续用库内置那套不动。</summary>
        public static void RegisterDefinitions()
        {
            var light = UI4ThemeDefinition.Light();
            light.Key = "light";
            light.With(UI4ThemeToken.Background, Theme.LightBackground)
                 .With(UI4ThemeToken.Surface, Theme.Mix(Theme.LightBackground, Colors.White, 0.85f))
                 .With(UI4ThemeToken.BackgroundGradientStart, Theme.LightGradientStart)
                 .With(UI4ThemeToken.BackgroundGradientEnd, Theme.LightGradientEnd)
                 .With(UI4ThemeToken.TextForeground, Theme.LightForeground)
                 .With(UI4ThemeToken.TextMuted, Theme.Mix(Theme.LightForeground, Theme.LightBackground, 0.42f))
                 .With(UI4ThemeToken.Accent, Theme.Accent)
                 .With(UI4ThemeToken.AccentDark, Theme.AccentHover)
                 .With(UI4ThemeToken.AccentEnd, Theme.Signal)
                 .With(UI4ThemeToken.BorderNormal, Theme.Mix(Theme.LightForeground, Theme.LightBackground, 0.62f))
                 .With(UI4ThemeToken.BorderHover, Theme.Accent)
                 .With(UI4ThemeToken.BorderFocus, Theme.AccentHover)
                 .With(UI4ThemeToken.PanelBorder, Color.FromArgb(60, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B))
                 .With(UI4ThemeToken.Icon, Theme.Mix(Theme.LightForeground, Theme.LightBackground, 0.5f))
                 .With(UI4ThemeToken.IconHover, Theme.Accent);
            UI4Theme.Register(light);

            var dark = UI4ThemeDefinition.Dark();
            dark.Key = "dark";
            dark.With(UI4ThemeToken.Background, Theme.DarkBackground)
                 .With(UI4ThemeToken.Surface, Theme.DarkSurface)
                 .With(UI4ThemeToken.BackgroundGradientStart, Theme.DarkGradientStart)
                 .With(UI4ThemeToken.BackgroundGradientEnd, Theme.DarkGradientEnd)
                 .With(UI4ThemeToken.TextForeground, Theme.DarkForeground)
                 .With(UI4ThemeToken.TextMuted, Theme.Mix(Theme.DarkForeground, Theme.DarkBackground, 0.42f))
                 .With(UI4ThemeToken.Accent, Theme.DarkAccent)
                 .With(UI4ThemeToken.AccentDark, Theme.Mix(Theme.DarkAccent, Colors.Black, 0.15f))
                 .With(UI4ThemeToken.AccentEnd, Theme.Mix(Theme.Signal, Colors.White, 0.35f))
                 .With(UI4ThemeToken.BorderNormal, Theme.Mix(Theme.DarkForeground, Theme.DarkBackground, 0.7f))
                 .With(UI4ThemeToken.BorderHover, Theme.DarkAccent)
                 .With(UI4ThemeToken.BorderFocus, Theme.Mix(Theme.DarkAccent, Colors.White, 0.2f))
                 .With(UI4ThemeToken.PanelBorder, Color.FromArgb(70, Theme.DarkAccent.R, Theme.DarkAccent.G, Theme.DarkAccent.B))
                 .With(UI4ThemeToken.Icon, Theme.Mix(Theme.DarkForeground, Theme.DarkBackground, 0.45f))
                 .With(UI4ThemeToken.IconHover, Theme.DarkAccent);
            UI4Theme.Register(dark);
        }

        public static UI4ThemeMode ModeFor(string setting)
        {
            switch (setting == null ? "" : setting.Trim().ToLowerInvariant())
            {
                case "light": return UI4ThemeMode.Light;
                case "dark": return UI4ThemeMode.Dark;
                case "highcontrast": return UI4ThemeMode.HighContrast;
                default: return UI4ThemeMode.System;
            }
        }

        public static void Apply(string modeSetting, string accentHex)
        {
            _suppressAccentReapply = true;
            try
            {
                UI4Theme.SetTheme(ModeFor(modeSetting));
                UI4Theme.ApplyToApplication();
                ApplyAccent(accentHex);
            }
            finally
            {
                _suppressAccentReapply = false;
            }
        }

        /// <summary>空串 = 不覆盖，恢复宿主定义里的强调色（重新注册当前键即可就地重应用）。</summary>
        public static void ApplyAccent(string accentHex)
        {
            Color accent;
            if (!string.IsNullOrWhiteSpace(accentHex) && Theme.TryParseHex(accentHex, out accent))
            {
                UI4Theme.SetAccent(accent);
                return;
            }
            ResetAccentToDefinition();
        }

        /// <summary>
        /// 强调色是写进「当前键的定义」里的，注册一份同键的新定义 = 库里就地整体重来一遍，
        /// 不需要先切到别的键再切回来。
        /// </summary>
        public static void ResetAccentToDefinition()
        {
            RegisterDefinitions();
            UI4Theme.ApplyToApplication();
        }

        /// <summary>
        /// 换档后补强调色：SetAccent 只改了当时那个键，切到另一档时那一档的定义仍是原色。
        /// 回调里再调 SetAccent 会再次触发 ThemeChanged，所以用标志位掐掉递归。
        /// 强调色用 Func 现取而不是启动时快照，用户中途改色不用重启。
        /// </summary>
        public static void InstallAccentFollowThrough(Func<string> accentProvider)
        {
            UI4Theme.ThemeChanged += delegate
            {
                if (_suppressAccentReapply) return;
                string accentHex = accentProvider == null ? null : accentProvider();
                Color accent;
                if (string.IsNullOrWhiteSpace(accentHex)) return;
                if (!Theme.TryParseHex(accentHex, out accent)) return;
                if (UI4Theme.Current.ColorOf(UI4ThemeToken.Accent) == accent) return;

                _suppressAccentReapply = true;
                try { UI4Theme.SetAccent(accent); }
                finally { _suppressAccentReapply = false; }
            };
        }

        private static readonly Dictionary<string, string> KeyLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "light", "浅色" },
                { "dark", "深色" },
                { "highcontrast", "高对比度" },
            };

        /// <summary>当前生效键与中文标签，设置页与状态栏共用。</summary>
        public static string ResolvedDescription
        {
            get
            {
                string key = UI4Theme.ResolvedKey ?? "—";
                string label;
                if (!KeyLabels.TryGetValue(key, out label)) label = key;
                Color bg = UI4Theme.Current.ColorOf(UI4ThemeToken.Background);
                string tone = Theme.IsDark(bg) ? "暗" : "亮";
                return label + "（" + key + " · " + tone + "底）";
            }
        }

        public static Color CurrentAccent { get { return UI4Theme.Current.ColorOf(UI4ThemeToken.Accent); } }

        public static string CurrentModeSetting
        {
            get
            {
                switch (UI4Theme.CurrentMode)
                {
                    case UI4ThemeMode.Light: return "light";
                    case UI4ThemeMode.Dark: return "dark";
                    case UI4ThemeMode.HighContrast: return "highcontrast";
                    default: return "system";
                }
            }
        }
    }
}
