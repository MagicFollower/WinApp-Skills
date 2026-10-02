using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 设置编解码的自检（由 PromptFavorites.exe --selftest 触发，退出码即失败断言数）。
    /// 只跑纯函数，不读写用户设置文件、不弹窗。
    /// </summary>
    internal static class SettingsSelfTest
    {
        public static int Run()
        {
            var report = new StringBuilder();
            int failed = 0;

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

            foreach (var value in hostile)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                map["rootPath"] = value;

                var parsed = SettingsCodec.Parse(SettingsCodec.Serialize(map));
                string back;
                parsed.TryGetValue("rootPath", out back);

                if (!string.Equals(value.Trim(), back ?? string.Empty, StringComparison.Ordinal))
                {
                    failed++;
                    report.AppendLine("FAIL 往返: [" + value + "] -> [" + back + "]");
                }
            }

            var seed = new Dictionary<string, string>(StringComparer.Ordinal);
            seed["rootPath"] = @"C:\Users\webtu\Documents\Prompts";
            seed["lastModule"] = "编程模块";
            var firstText = SettingsCodec.Serialize(seed);
            var currentText = firstText;
            for (int round = 0; round < 5; round++)
            {
                var reparsed = SettingsCodec.Parse(currentText);
                var reserialized = SettingsCodec.Serialize(reparsed);
                if (reserialized != currentText)
                {
                    failed++;
                    report.AppendLine("FAIL 稳定性: 第 " + (round + 1) + " 轮序列化文本发生变化");
                    break;
                }
                if (reserialized.Length > firstText.Length)
                {
                    failed++;
                    report.AppendLine("FAIL 稳定性: 第 " + (round + 1) + " 轮体积增长 "
                        + firstText.Length + " -> " + reserialized.Length);
                    break;
                }
                currentText = reserialized;
            }

            foreach (var value in hostile)
            {
                var legacyText = "{\n  \"rootPath\": \"" + LegacyEscape(value) + "\",\n"
                    + "  \"lastModule\": \"编程模块\"\n}";

                if (!SettingsCodec.LooksLikeLegacyJson(legacyText))
                {
                    failed++;
                    report.AppendLine("FAIL 旧格式未被识别: [" + value + "]");
                    continue;
                }

                var parsed = SettingsCodec.ParseLegacyJson(legacyText);
                string back;
                parsed.TryGetValue("rootPath", out back);

                var expected = SettingsCodec.CollapseSeparators(value);
                var actual = SettingsCodec.CollapseSeparators(back ?? string.Empty);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    failed++;
                    report.AppendLine("FAIL 旧格式还原: [" + value + "] -> [" + back + "]");
                }
            }

            var doubled = SettingsCodec.CollapseSeparators(
                "C:" + new string('\\', 1 << 16) + "Users" + new string('\\', 1 << 16) + "Prompts");
            if (doubled.Length > 64)
            {
                failed++;
                report.AppendLine("FAIL 分隔符折叠失效，长度=" + doubled.Length);
            }

            var geometry = new SettingsService();
            var badValues = new Dictionary<string, string>(StringComparer.Ordinal);
            badValues["windowWidth"] = "NaN";
            badValues["windowHeight"] = "Infinity";
            badValues["windowLeft"] = "1e400";
            geometry.ApplyMap(badValues);
            if (double.IsNaN(geometry.WindowWidth) || double.IsInfinity(geometry.WindowHeight)
                || double.IsNaN(geometry.WindowLeft) || double.IsInfinity(geometry.WindowLeft))
            {
                failed++;
                report.AppendLine("FAIL 非法几何值被接受");
            }

            var sane = new SettingsService();
            var goodValues = new Dictionary<string, string>(StringComparer.Ordinal);
            goodValues["windowState"] = "Maximized";
            goodValues["sortMode"] = "UpdatedAt";
            sane.ApplyMap(goodValues);
            if (sane.WindowState != System.Windows.WindowState.Maximized)
            {
                failed++;
                report.AppendLine("FAIL windowState 未还原");
            }

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
            if (orderBack.Count != orderNames.Length)
            {
                failed++;
                report.AppendLine("FAIL 名称表往返数量: " + orderNames.Length + " -> " + orderBack.Count);
            }
            else
            {
                for (int i = 0; i < orderNames.Length; i++)
                {
                    if (!string.Equals(orderNames[i], orderBack[i], StringComparison.Ordinal))
                    {
                        failed++;
                        report.AppendLine("FAIL 名称表往返位置 " + i + ": [" + orderNames[i] + "] -> [" + orderBack[i] + "]");
                    }
                }
            }

            var orderKv = new Dictionary<string, string>(StringComparer.Ordinal);
            orderKv["moduleCustomOrder"] = orderText;
            string orderKvBack;
            SettingsCodec.Parse(SettingsCodec.Serialize(orderKv)).TryGetValue("moduleCustomOrder", out orderKvBack);
            if (!string.Equals(orderText, orderKvBack ?? string.Empty, StringComparison.Ordinal))
            {
                failed++;
                report.AppendLine("FAIL 顺序键经 kv1 往返发生变化: [" + orderKvBack + "]");
            }

            var scopes = new List<KeyValuePair<string, List<string>>>();
            scopes.Add(new KeyValuePair<string, List<string>>("编程模块",
                new List<string> { "无损转录器", "批量改名 a=b" }));
            scopes.Add(new KeyValuePair<string, List<string>>("WebDAV 备份",
                new List<string> { "手机端" }));

            var scopeText = CustomOrderCodec.EncodeScopes(scopes);
            var scopeBack = CustomOrderCodec.DecodeScopes(scopeText);
            if (scopeBack.Count != 2
                || scopeBack["编程模块"].Count != 2
                || scopeBack["编程模块"][1] != "批量改名 a=b"
                || scopeBack["WebDAV 备份"][0] != "手机端")
            {
                failed++;
                report.AppendLine("FAIL 模块分组往返: [" + scopeText + "]");
            }

            var rows = new List<string> { "a", "b", "c", "新建的" };
            var manual = CustomOrderCodec.Apply(rows, s => s, new List<string> { "c", "a", "已删除的" });
            if (manual.Count != 4 || manual[0] != "c" || manual[1] != "a"
                || manual[2] != "b" || manual[3] != "新建的")
            {
                failed++;
                report.AppendLine("FAIL 手动顺序应用: 表内项在前、未记录者按原相对顺序落末尾");
            }

            if (CustomOrderCodec.Apply(rows, s => s, null).Count != 4)
            {
                failed++;
                report.AppendLine("FAIL 空顺序表应原样返回且不改动");
            }

            var positions = new List<string> { "x", "y", "z" };
            CustomOrderCodec.Rename(positions, "y", "改名后");
            if (positions.Count != 3 || positions[1] != "改名后")
            {
                failed++;
                report.AppendLine("FAIL 改名后位置发生变化: [" + string.Join("|", positions) + "]");
            }

            var status = failed == 0
                ? "PASS 断言组=" + (hostile.Length * 2 + 9)
                : report.ToString();

            var body = "SettingsSelfTest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " failed=" + failed + "\r\n" + status;

            // 单文件发布下 AppDomain.CurrentDomain.BaseDirectory 可能指向会被清理的临时解包目录，
            // 产物优先落设置目录旁边；写失败不影响退出码（契约仍是"退出码＝失败断言数"）。
            try
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptFavorites");
                Directory.CreateDirectory(appData);
                File.WriteAllText(Path.Combine(appData, "selftest.txt"), body);
            }
            catch
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), body);
                }
                catch
                {
                }
            }

            return failed;
        }

        private static string LegacyEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
