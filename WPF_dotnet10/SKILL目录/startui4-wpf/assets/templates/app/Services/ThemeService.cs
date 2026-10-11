using System;
using System.Collections.Generic;
using StartUI4Controls;
using __APPNAME__.Helpers;

namespace __APPNAME__.Services
{
    /// <summary>
    /// 配色档的单源：稳定键（英文，落盘用）→ 库的调用（SetTheme 或 Apply 套装键）→ 展示文案。
    ///
    /// 两个反直觉点都收在这里，别在界面代码里各写一遍：
    /// ① 套装键必须先 UI4ThemePacks.RegisterAll() 再 UI4Theme.Apply(key)，顺序错时 Apply 只返回 false
    ///    且不抛异常，界面纹丝不动；
    /// ② 可选项由 Theme.Policy 挡决定，light-only / dark-only 的策略下不给对应方向的档，
    ///    但"当前档"仍要显式 SetTheme 钉住——否则库会按 AppsUseLightTheme 与系统高对比度自行解析。
    /// </summary>
    internal static class ThemeService
    {
        public const string Light = "light";
        public const string Dark = "dark";
        public const string System = "system";
        public const string PaperGrey = "paper-grey";

        /// <summary>当前明暗策略下允许的档，顺序即面板里的展示顺序。</summary>
        public static List<string> AvailableKeys()
        {
            string policy = Theme.Policy;
            bool allowLight = !string.Equals(policy, "dark-only", StringComparison.OrdinalIgnoreCase);
            bool allowDark = !string.Equals(policy, "light-only", StringComparison.OrdinalIgnoreCase);

            var keys = new List<string>();
            if (allowLight) keys.Add(Light);
            if (allowDark) keys.Add(Dark);
            // 「跟随系统」只在 both 策略下提供：单档策略要的就是"钉死"，给一个会随系统跑的档等于没定
            if (allowLight && allowDark) keys.Add(System);
            if (allowLight) keys.Add(PaperGrey);
            return keys;
        }

        public static bool IsAllowed(string key)
        {
            return AvailableKeys().Contains(Normalize(key));
        }

        /// <summary>应用一档。返回 false 只有一种含义：这一档在当前策略下不允许，或套装没注册成功。</summary>
        public static bool Apply(string key)
        {
            switch (Normalize(key))
            {
                case Light:
                    UI4Theme.SetTheme(UI4ThemeMode.Light);
                    return true;
                case Dark:
                    UI4Theme.SetTheme(UI4ThemeMode.Dark);
                    return true;
                case System:
                    UI4Theme.SetTheme(UI4ThemeMode.System);
                    return true;
                case PaperGrey:
                    UI4ThemePacks.RegisterAll();
                    return UI4Theme.Apply(UI4ThemePacks.PaperGrey);
                default:
                    return false;
            }
        }

        public static string DisplayLabel(string key)
        {
            switch (Normalize(key))
            {
                case Light: return "浅色";
                case Dark: return "深色";
                case System: return "跟随系统";
                case PaperGrey: return "灰纸套装（预置）";
                default: return key;
            }
        }

        /// <summary>实际生效的键（system 档要问库解析成了什么，别自己猜）。</summary>
        public static string ResolvedKey(string requestedKey)
        {
            string key = Normalize(requestedKey);
            if (key != System) return key;
            return UI4Theme.ResolvedKey;
        }

        private static string Normalize(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return Light;
            foreach (string known in new[] { Light, Dark, System, PaperGrey })
            {
                if (string.Equals(key, known, StringComparison.OrdinalIgnoreCase)) return known;
            }
            return key.Trim().ToLowerInvariant();
        }
    }
}
