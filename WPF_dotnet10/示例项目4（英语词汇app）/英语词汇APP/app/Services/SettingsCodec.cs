using System;
using System.Collections.Generic;
using System.Text;

namespace VocabDesk.Services
{
    /// <summary>
    /// 设置文件的纯文本编解码（无状态、零 I/O，便于自检）。
    ///
    /// 每行 key=value，按第一个 = 切分，值原样存储、不做任何转义。不变量：值不得含 CR/LF
    /// （Windows 路径与字体族名天然不含），首尾空格不保留——因此 Serialize 与 Parse 严格互逆。
    /// 用 JSON 反而危险：写时转义、读时只剥引号不反转义的不对称，会让含反斜杠的值每次保存都翻倍。
    /// </summary>
    public static class SettingsCodec
    {
        public const string Header = "# VocabDesk settings (kv1)";

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

            foreach (var rawLine in text.Split('\n'))
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

        private static string NormalizeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(NewlineChars) >= 0) return string.Empty;
            return value.Trim();
        }
    }
}
