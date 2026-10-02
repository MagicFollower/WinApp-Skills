using System;
using System.Collections.Generic;
using System.Text;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 设置文件的纯文本编解码（无状态、零 I/O，便于自检）。
    /// </summary>
    /// <remarks>
    /// <para>新格式 kv1：每行 <c>key=value</c>，按第一个 <c>=</c> 切分，值原样存储、不做任何编码。
    /// 不变量：值不得含 CR/LF（Windows 路径与目录名天然不含），首尾空格不保留（Windows 路径不可能以空格结尾），
    /// 因此 Serialize 与 Parse 严格互逆。</para>
    /// <para>之所以放弃 JSON：旧实现"写时 EscapeJson 转义、读时只剥引号不反转义"的不对称，
    /// 会让含反斜杠的 rootPath 每保存一次翻倍，实测把设置文件撑到 16 MB 并让应用启动成无窗口进程。</para>
    /// <para>旧文件（首字符是 <c>{</c>）由 <see cref="ParseLegacyJson"/> 读取，其中 <see cref="UnescapeJson"/>
    /// 是旧 EscapeJson 的严格单遍逆函数，<see cref="CollapseSeparators"/> 用于抢救已被放大的历史路径。</para>
    /// </remarks>
    public static class SettingsCodec
    {
        public const string Header = "# PromptFavorites settings (kv1)";

        private static readonly char[] NewlineChars = { '\r', '\n' };

        public static string Serialize(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var sb = new StringBuilder(256);
            sb.Append(Header).Append("\r\n");

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (entry.Key.IndexOf('=') >= 0) continue;
                if (entry.Key.IndexOfAny(NewlineChars) >= 0) continue;

                sb.Append(entry.Key).Append('=').Append(NormalizeValue(entry.Value)).Append("\r\n");
            }

            return sb.ToString();
        }

        public static Dictionary<string, string> Parse(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return map;

            var lines = text.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line[0] == '#') continue;

                var separator = line.IndexOf('=');
                if (separator <= 0) continue;

                var key = line.Substring(0, separator).Trim();
                if (key.Length == 0) continue;

                map[key] = line.Substring(separator + 1).Trim();
            }

            return map;
        }

        public static bool LooksLikeLegacyJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("{");
        }

        public static Dictionary<string, string> ParseLegacyJson(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return map;

            var lines = text.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim().Trim(',');
                if (line.Length == 0) continue;
                if (line[0] == '{' || line[0] == '}') continue;

                var colon = line.IndexOf(':');
                if (colon <= 0) continue;

                var key = line.Substring(0, colon).Trim().Trim('"');
                if (key.Length == 0) continue;

                var value = UnescapeJson(StripOuterQuotes(line.Substring(colon + 1).Trim()));
                if (string.Equals(key, "rootPath", StringComparison.OrdinalIgnoreCase))
                    value = CollapseSeparators(value);

                map[key] = value;
            }

            return map;
        }

        /// <summary>
        /// 旧 EscapeJson 的严格单遍逆函数，只认 \\ 与 \" 两种转义（与旧转义集完全相同）。
        /// 必须单遍扫描：两次 Replace 会让 \\" 这类序列互相污染。未知转义原样保留反斜杠，
        /// 否则 C:\notes 会被当成换行转义而损坏。
        /// </summary>
        public static string UnescapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i + 1 >= value.Length)
                {
                    sb.Append('\\');
                    break;
                }

                var next = value[i + 1];
                if (next == '\\' || next == '"')
                {
                    sb.Append(next);
                    i++;
                }
                else
                {
                    sb.Append('\\');
                }
            }

            return sb.ToString();
        }

        /// <summary>折叠连续的反斜杠分隔符，保留 UNC 前导 <c>\\server</c>。</summary>
        public static string CollapseSeparators(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (path.IndexOf('\\') < 0) return path;

            bool unc = path.StartsWith("\\\\", StringComparison.Ordinal);

            var sb = new StringBuilder(path.Length);
            bool previousIsSeparator = false;
            foreach (var c in path)
            {
                if (c == '\\')
                {
                    if (previousIsSeparator) continue;
                    sb.Append(c);
                    previousIsSeparator = true;
                }
                else
                {
                    sb.Append(c);
                    previousIsSeparator = false;
                }
            }

            if (unc && !sb.ToString().StartsWith("\\\\", StringComparison.Ordinal))
                sb.Insert(0, '\\');

            return sb.ToString();
        }

        private static string NormalizeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(NewlineChars) >= 0) return string.Empty;
            return value.Trim();
        }

        private static string StripOuterQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value.Trim('"');
        }
    }
}
