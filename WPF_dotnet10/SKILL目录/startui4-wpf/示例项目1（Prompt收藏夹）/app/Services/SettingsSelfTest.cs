using System;
using System.Collections.Generic;
using System.IO;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 设置编解码的自检段（由 <see cref="SelfTest"/> 汇总，最终成为 --selftest 的退出码）。
    /// 只跑纯函数，不读写用户设置文件、不弹窗。
    /// </summary>
    internal static class SettingsSelfTest
    {
        public static void Run(SelfTestResult r)
        {
            r.Section("settings");

            var hostile = new[]
            {
                @"C:\Users\webtu\Documents\Prompts",
                @"C:\Users\webtu\临时""测试""目录",
                @"\\server\share\Prompts",
                @"C:\temp\",
                @"C:\a b\提示词\模块 (x86)",
                "编程模块",
                @"C:\notes",
                @"C:\\\\already",
                @"D:\",
                string.Empty
            };

            // kv1 不变量：写侧不转义、读侧不反转义，值原样回来（只允许首尾空格被裁掉）。
            // 这条直接对着当年那次"每存一次反斜杠翻倍"的指数膨胀事故。
            foreach (var value in hostile)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                map["rootPath"] = value;

                var parsed = SettingsCodec.Parse(SettingsCodec.Serialize(map));
                string back;
                parsed.TryGetValue("rootPath", out back);

                r.Check(string.Equals(value.Trim(), back ?? string.Empty, StringComparison.Ordinal),
                    "kv1 往返: [" + value + "] -> [" + back + "]");
            }

            // 序列化文本对同一份数据必须逐轮恒定、体积不增。
            var seed = new Dictionary<string, string>(StringComparer.Ordinal);
            seed["rootPath"] = @"C:\Users\webtu\Documents\Prompts";
            seed["lastModule"] = "编程模块";
            var firstText = SettingsCodec.Serialize(seed);
            var currentText = firstText;
            for (int round = 0; round < 5; round++)
            {
                var reparsed = SettingsCodec.Parse(currentText);
                var reserialized = SettingsCodec.Serialize(reparsed);

                bool stable = reserialized == currentText;
                r.Check(stable, "稳定性: 第 " + (round + 1) + " 轮序列化文本发生变化");
                if (!stable) break;

                bool noGrowth = reserialized.Length <= firstText.Length;
                r.Check(noGrowth, "稳定性: 第 " + (round + 1) + " 轮体积增长 "
                    + firstText.Length + " -> " + reserialized.Length);
                if (!noGrowth) break;

                currentText = reserialized;
            }

            // 旧 JSON 只读一次：识别、严格单遍逆转义、分隔符折叠抢救。
            foreach (var value in hostile)
            {
                var legacyText = "{\n  \"rootPath\": \"" + LegacyEscape(value) + "\",\n"
                    + "  \"lastModule\": \"编程模块\"\n}";

                bool recognized = SettingsCodec.LooksLikeLegacyJson(legacyText);
                r.Check(recognized, "旧格式识别: [" + value + "]");
                if (!recognized) continue;

                var parsed = SettingsCodec.ParseLegacyJson(legacyText);
                string back;
                parsed.TryGetValue("rootPath", out back);

                var expected = SettingsCodec.CollapseSeparators(value);
                var actual = SettingsCodec.CollapseSeparators(back ?? string.Empty);
                r.Check(string.Equals(expected, actual, StringComparison.Ordinal),
                    "旧格式还原: [" + value + "] -> [" + back + "]");
            }

            var doubled = SettingsCodec.CollapseSeparators(
                "C:" + new string('\\', 1 << 16) + "Users" + new string('\\', 1 << 16) + "Prompts");
            r.Check(doubled.Length <= 64, "分隔符折叠失效，长度=" + doubled.Length);

            var geometry = new SettingsService();
            var badValues = new Dictionary<string, string>(StringComparer.Ordinal);
            badValues["windowWidth"] = "NaN";
            badValues["windowHeight"] = "Infinity";
            badValues["windowLeft"] = "1e400";
            geometry.ApplyMap(badValues);
            r.Check(!double.IsNaN(geometry.WindowWidth) && !double.IsInfinity(geometry.WindowHeight)
                    && !double.IsNaN(geometry.WindowLeft) && !double.IsInfinity(geometry.WindowLeft),
                "非法几何值被接受");

            var sane = new SettingsService();
            var goodValues = new Dictionary<string, string>(StringComparer.Ordinal);
            goodValues["windowState"] = "Maximized";
            goodValues["sortMode"] = "UpdatedAt";
            sane.ApplyMap(goodValues);
            r.Check(sane.WindowState == System.Windows.WindowState.Maximized, "windowState 未还原");
            r.Check(sane.SortMode == Models.SortMode.UpdatedAt, "sortMode 未还原");

            // 自定义拖动顺序：名字可以含 '=' 和引号，但绝不能含 '| > :'（Windows 文件名非法字符），
            // 所以分隔符取这三个字符、值侧不做任何转义。下面断言这条不变量成立。
            var orderNames = new[]
            {
                "编程模块",
                "临时 目录",
                "模块 (x86)",
                "带\"引号\"的名字",
                "a=b",
                @"C:\风格路径"
            };

            var orderText = CustomOrderCodec.EncodeNames(orderNames);
            var orderBack = CustomOrderCodec.DecodeNames(orderText);
            r.Check(orderBack.Count == orderNames.Length,
                "名称表往返数量: " + orderNames.Length + " -> " + orderBack.Count);
            for (int i = 0; i < orderNames.Length && i < orderBack.Count; i++)
            {
                r.Check(string.Equals(orderNames[i], orderBack[i], StringComparison.Ordinal),
                    "名称表往返位置 " + i + ": [" + orderNames[i] + "] -> [" + orderBack[i] + "]");
            }

            var orderKv = new Dictionary<string, string>(StringComparer.Ordinal);
            orderKv["moduleCustomOrder"] = orderText;
            string orderKvBack;
            SettingsCodec.Parse(SettingsCodec.Serialize(orderKv))
                .TryGetValue("moduleCustomOrder", out orderKvBack);
            r.Check(string.Equals(orderText, orderKvBack ?? string.Empty, StringComparison.Ordinal),
                "顺序键经 kv1 往返发生变化: [" + orderKvBack + "]");

            var scopes = new List<KeyValuePair<string, List<string>>>();
            scopes.Add(new KeyValuePair<string, List<string>>("编程模块",
                new List<string> { "无损转录器", "批量改名 a=b" }));
            scopes.Add(new KeyValuePair<string, List<string>>("WebDAV 备份",
                new List<string> { "手机端" }));

            var scopeText = CustomOrderCodec.EncodeScopes(scopes);
            var scopeBack = CustomOrderCodec.DecodeScopes(scopeText);
            r.Check(scopeBack.Count == 2
                    && scopeBack["编程模块"].Count == 2
                    && scopeBack["编程模块"][1] == "批量改名 a=b"
                    && scopeBack["WebDAV 备份"][0] == "手机端",
                "模块分组往返: [" + scopeText + "]");

            var rows = new List<string> { "a", "b", "c", "新建的" };
            var manual = CustomOrderCodec.Apply(rows, s => s, new List<string> { "c", "a", "已删除的" });
            r.Check(manual.Count == 4 && manual[0] == "c" && manual[1] == "a"
                    && manual[2] == "b" && manual[3] == "新建的",
                "手动顺序应把表内项排前、未记录者按原相对顺序落末尾");

            r.Check(CustomOrderCodec.Apply(rows, s => s, null).Count == 4,
                "空顺序表应原样返回且不改动");

            var positions = new List<string> { "x", "y", "z" };
            CustomOrderCodec.Rename(positions, "y", "改名后");
            r.Check(positions.Count == 3 && positions[1] == "改名后",
                "改名后位置发生变化: [" + string.Join("|", positions) + "]");

            // 档位持久化（明暗切换）：按名还原，非法值、数字串与本项目不消费的 System 一律回默认 Light。
            var themeStore = new SettingsService();
            var themeValues = new Dictionary<string, string>(StringComparer.Ordinal);
            themeValues["themeMode"] = "Dark";
            themeStore.ApplyMap(themeValues);
            r.Check(themeStore.ThemeMode == Models.AppThemeMode.Dark, "themeMode=Dark 未还原");

            var themeFallback = new SettingsService();
            var junkValues = new Dictionary<string, string>(StringComparer.Ordinal);
            junkValues["themeMode"] = "1";
            themeFallback.ApplyMap(junkValues);
            r.Check(themeFallback.ThemeMode == Models.AppThemeMode.Light,
                "themeMode 的数字串被接受（应按名解析失败、保留默认档）");

            junkValues["themeMode"] = "System";
            themeFallback.ApplyMap(junkValues);
            r.Check(themeFallback.ThemeMode == Models.AppThemeMode.Light,
                "本项目不跟随系统：themeMode=System 不该被接受");

            // 同一口径要覆盖既有枚举键，否则"只有新键严格"会留下第二种行为。
            var enumFallback = new SettingsService();
            var numeric = new Dictionary<string, string>(StringComparer.Ordinal);
            numeric["sortMode"] = "2";
            numeric["moduleSortMode"] = "1";
            numeric["windowState"] = "0";
            enumFallback.ApplyMap(numeric);
            r.Check(enumFallback.SortMode == Models.SortMode.UseCount
                    && enumFallback.ModuleSortMode == Models.ModuleSortMode.CreatedAt
                    && enumFallback.WindowState == System.Windows.WindowState.Normal,
                "sortMode/moduleSortMode/windowState 的数字串被接受（应按名解析失败、保留默认值）");

            var enumRoundTrip = new SettingsService();
            enumRoundTrip.SortMode = Models.SortMode.Custom;
            enumRoundTrip.ThemeMode = Models.AppThemeMode.Dark;
            var roundBack = SettingsCodec.Parse(SettingsCodec.Serialize(enumRoundTrip.ToMap()));
            var restored = new SettingsService();
            restored.ApplyMap(roundBack);
            r.Check(restored.SortMode == Models.SortMode.Custom && restored.ThemeMode == Models.AppThemeMode.Dark,
                "sortMode/themeMode 经 kv1 往返后发生变化");

            // 排印三键：写侧键名与读侧键名必须逐字一致——kv1 没有 schema，任一侧打错一个字母
            // 就是"设置面板改了、重启回到默认"这种查不出来的丢设置。
            var typoStore = new SettingsService();
            typoStore.FontFamilyName = "Segoe UI Variable Text";
            typoStore.BaseFontSize = 17.5;
            typoStore.ZoomPercent = 133;
            var typoMap = typoStore.ToMap();
            r.Check(Has(typoMap, "fontFamilyName") && Has(typoMap, "baseFontSize") && Has(typoMap, "zoomPercent"),
                "排印三键没写进 kv1: " + string.Join(",", System.Array.ConvertAll(
                    typoMap.ToArray(), p => p.Key)));

            var typoRestored = new SettingsService();
            typoRestored.ApplyMap(SettingsCodec.Parse(SettingsCodec.Serialize(typoMap)));
            r.Check(typoRestored.FontFamilyName == "Segoe UI Variable Text"
                    && System.Math.Abs(typoRestored.BaseFontSize - 17.5) < 1e-9
                    && System.Math.Abs(typoRestored.ZoomPercent - 133) < 1e-9,
                "排印三键经 kv1 往返后发生变化: [" + typoRestored.FontFamilyName + ", "
                + typoRestored.BaseFontSize + ", " + typoRestored.ZoomPercent + "]");

            // 区间：越界与非法值都不能原样吃下去。钳到边界（500→200、-1→50），
            // 解析不了或 NaN/Infinity 这类"能解析但不是可用尺寸"的回默认值。
            var outOfRange = new Dictionary<string, string>(StringComparer.Ordinal);
            outOfRange["baseFontSize"] = "500";
            outOfRange["zoomPercent"] = "-1";
            var clamped = new SettingsService();
            clamped.ApplyMap(outOfRange);
            r.Check(clamped.BaseFontSize == Helpers.Typography.MaxBaseSize
                    && clamped.ZoomPercent == Helpers.Typography.MinZoomPercent,
                "越界排印值没被钳回区间: base=" + clamped.BaseFontSize + " zoom=" + clamped.ZoomPercent);

            var junk = new Dictionary<string, string>(StringComparer.Ordinal);
            junk["baseFontSize"] = "abc";
            junk["zoomPercent"] = "NaN";
            var junked = new SettingsService();
            junked.BaseFontSize = 20;
            junked.ZoomPercent = 150;
            junked.ApplyMap(junk);
            r.Check(junked.BaseFontSize == 20, "解析不了的 baseFontSize 覆盖了已生效值");
            r.Check(junked.ZoomPercent == Helpers.Typography.DefaultZoomPercent,
                "NaN 的 zoomPercent 被直接接受（应回默认档）: " + junked.ZoomPercent);

            // 空字体族名要能原样回来，它代表"用出厂字体栈"，不是"字体没设置"
            var emptyFamily = new Dictionary<string, string>(StringComparer.Ordinal);
            emptyFamily["fontFamilyName"] = "";
            var cleared = new SettingsService();
            cleared.FontFamilyName = "楷体";
            cleared.ApplyMap(emptyFamily);
            r.Check(cleared.FontFamilyName == string.Empty,
                "空字体族名没被还原成出厂档: [" + cleared.FontFamilyName + "]");

            CheckTwoTierOwnership(r);
        }

        // ── 两层配置的键归属（纯函数，不碰任何文件）──────────────────

        /// <summary>
        /// 数据类键跟着根目录走、外观类留全局，这条分界只有一个出处（<c>RootScopedKeys</c>）。
        /// 下面几组断言分别对着三种漂移：
        /// ① 某个键两侧都不写——改了不生效、重启回默认；
        /// ② 某个键两侧都写——换根时全局那份会盖回本根那份，等于白拆；
        /// ③ <c>rootPath</c> 漏进每根文件——它既是找配置的入口又跟着配置走，自己找不到自己，
        ///    还要多背一次当年"分隔符每存一次翻倍"的膨胀风险。
        /// </summary>
        private static void CheckTwoTierOwnership(SelfTestResult r)
        {
            var full = new SettingsService();
            full.RootPath = @"C:\Users\webtu\Documents\Prompts";
            full.LastModule = "写作";
            full.MetadataCollapsed = true;
            full.SortMode = Models.SortMode.UpdatedAt;
            full.ModuleSortMode = Models.ModuleSortMode.Name;
            full.FavoriteFilter = true;
            full.ThemeMode = Models.AppThemeMode.Dark;
            full.FontFamilyName = "楷体";
            full.BaseFontSize = 17;
            full.ZoomPercent = 125;
            full.WindowWidth = 1440;
            full.WindowHeight = 900;
            full.WindowLeft = 30;
            full.WindowTop = 40;
            full.WindowState = System.Windows.WindowState.Maximized;
            full.SetModuleOrder(new[] { "写作", "编程" });
            full.SetEntryOrder("写作", new[] { "无损转录器", "批量改名" });

            var all = full.ToMap();
            var globalSide = SettingsService.Partition(all, false);
            var rootSide = SettingsService.Partition(all, true);

            r.Check(globalSide.Count + rootSide.Count == all.Count,
                "键归属有漏网：全量 " + all.Count + " ≠ 全局 " + globalSide.Count
                + " + 每根 " + rootSide.Count);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool duplicated = false;
            foreach (var entry in globalSide) if (!seen.Add(entry.Key)) duplicated = true;
            foreach (var entry in rootSide) if (!seen.Add(entry.Key)) duplicated = true;
            r.Check(!duplicated, "同一个键被两侧都写（换根时全局那份会盖回本根）");

            r.Check(!Has(rootSide, "rootPath") && Has(globalSide, "rootPath"),
                "rootPath 归属不对：它是找每根配置的入口，只能待在全局引导文件里");

            var rootKeys = new[]
            {
                "lastModule", "sortMode", "moduleSortMode",
                "favoriteFilter", "moduleCustomOrder", "entryCustomOrder"
            };
            foreach (var key in rootKeys)
            {
                r.Check(Has(rootSide, key) && !Has(globalSide, key),
                    "数据键没跟着根目录走: " + key);
            }

            var globalKeys = new[]
            {
                "themeMode", "fontFamilyName", "baseFontSize", "zoomPercent",
                "metadataCollapsed", "windowWidth", "windowHeight",
                "windowLeft", "windowTop", "windowState"
            };
            foreach (var key in globalKeys)
            {
                r.Check(Has(globalSide, key) && !Has(rootSide, key),
                    "整机偏好不该跟着根目录走: " + key);
            }

            // 分头落盘再合起来读，一个值都不能丢——kv1 没有 schema，任一侧键名打错就是改了不生效。
            var restored = new SettingsService();
            restored.ApplyMap(SettingsCodec.Parse(SettingsCodec.Serialize(globalSide)));
            restored.ApplyMap(SettingsCodec.Parse(SettingsCodec.Serialize(rootSide)));

            r.Check(restored.RootPath == full.RootPath && restored.LastModule == "写作"
                    && restored.MetadataCollapsed && restored.FavoriteFilter
                    && restored.SortMode == Models.SortMode.UpdatedAt
                    && restored.ModuleSortMode == Models.ModuleSortMode.Name
                    && restored.ThemeMode == Models.AppThemeMode.Dark
                    && restored.FontFamilyName == "楷体"
                    && System.Math.Abs(restored.BaseFontSize - 17) < 1e-9
                    && System.Math.Abs(restored.ZoomPercent - 125) < 1e-9
                    && System.Math.Abs(restored.WindowWidth - 1440) < 1e-9
                    && restored.WindowState == System.Windows.WindowState.Maximized,
                "两层分头往返后有设置有值丢失或变形");

            var moduleOrder = restored.ModuleOrder;
            r.Check(moduleOrder != null && moduleOrder.Count == 2
                    && moduleOrder[0] == "写作" && moduleOrder[1] == "编程",
                "模块顺序表没能经两层往返还原");

            var entryOrder = restored.GetEntryOrder("写作");
            r.Check(entryOrder != null && entryOrder.Count == 2 && entryOrder[1] == "批量改名",
                "条目顺序表没能经两层往返还原");

            // 路径推导是纯函数，但换根时"写到哪"就从这里出，判错一个分隔符就写到别处去了。
            r.Check(SettingsService.RootConfigDirectoryFor(@"C:\Prompts")
                    == Path.Combine(@"C:\Prompts", ".PromptFavorites"),
                "每根配置目录推导错误");
            r.Check(SettingsService.RootConfigDirectoryFor(@"D:\")
                    == Path.Combine(@"D:\", ".PromptFavorites"),
                "盘符根的配置目录推导错误（换根最容易撞到这档）");
            r.Check(SettingsService.RootConfigDirectoryFor(@"\\server\share\Prompts")
                    == Path.Combine(@"\\server\share\Prompts", ".PromptFavorites"),
                "UNC 根的配置目录推导错误");
            r.Check(SettingsService.RootConfigDirectoryFor(string.Empty) == string.Empty
                    && SettingsService.RootConfigDirectoryFor(null) == string.Empty,
                "空根不该推导出一个看起来能用的路径");

            r.Check(SettingsService.IsReservedRootEntryName(".PromptFavorites")
                    && SettingsService.IsReservedRootEntryName(" .promptfavorites ")
                    && !SettingsService.IsReservedRootEntryName("编程模块")
                    && !SettingsService.IsReservedRootEntryName(null),
                "配置目录的保留名判定不对（模块名占了它，配置就成了看不见也删不掉的目录）");
        }

        private static bool Has(List<KeyValuePair<string, string>> entries, string key)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Key == key) return true;
            return false;
        }

        private static string LegacyEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
