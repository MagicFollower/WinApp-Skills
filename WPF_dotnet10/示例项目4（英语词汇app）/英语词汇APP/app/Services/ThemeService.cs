using System;
using System.Collections.Generic;
using StartUI4Controls;
using VocabDesk.Helpers;

namespace VocabDesk.Services
{
    /// <summary>
    /// 配色档的单源：稳定键（英文，落盘用）→ 库的调用 → 展示文案。
    ///
    /// 本工程按开工确认只暴露内置三档（浅色 / 深色 / 跟随系统），不注册 8 套预置套装键，
    /// 所以这里没有套装通路，也就没有"RegisterAll 必须早于 Apply"那条顺序坑。
    ///
    /// 另一条承重的约束：无论策略是哪一档，当前档都必须被显式 SetTheme 钉住。
    /// 单档策略下若因为旧设置文件残留而让 Apply 落空，库会按 AppsUseLightTheme 与
    /// 系统高对比度自行解析，界面就会跟着系统跑——用 ApplyEffective 兜住。
    /// </summary>
    internal static class ThemeService
    {
        public const string Light = "light";
        public const string Dark = "dark";
        public const string System = "system";

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
            return keys;
        }

        public static bool IsAllowed(string key)
        {
            return AvailableKeys().Contains(Normalize(key));
        }

        /// <summary>
        /// 启动时用的一档应用：设置里的键在当前策略下不允许（改过 Policy，或旧文件里存着已撤掉的档）
        /// 就退回首档，保证一定有一次显式 SetTheme 落地。
        /// </summary>
        public static bool ApplyEffective(string requestedKey)
        {
            string key = IsAllowed(requestedKey) ? Normalize(requestedKey) : AvailableKeys()[0];
            return Apply(key);
        }

        /// <summary>应用一档。返回 false 只有一种含义：这一档不在库里登记的三档之内。</summary>
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
            foreach (string known in new[] { Light, Dark, System })
            {
                if (string.Equals(key, known, StringComparison.OrdinalIgnoreCase)) return known;
            }
            return key.Trim().ToLowerInvariant();
        }
    }
}
