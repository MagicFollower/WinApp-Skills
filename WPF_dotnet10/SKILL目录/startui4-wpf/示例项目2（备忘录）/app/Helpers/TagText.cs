using System;
using System.Collections.Generic;
using System.Text;

namespace MemoTask.Helpers
{
    /// <summary>
    /// 标签就是「逗号分隔的一行字」，不做受控词表：用户写什么就按什么筛。
    /// 中英文逗号、分号、顿号都当分隔符，空段丢弃。
    /// </summary>
    internal static class TagText
    {
        private static readonly char[] Separators = { ',', '，', ';', '；', '、', '\n', '\r' };

        public static List<string> Split(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            string[] parts = raw.Split(Separators);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in parts)
            {
                string tag = part.Trim();
                if (tag.Length == 0) continue;
                if (seen.Add(tag)) list.Add(tag);
            }
            return list;
        }

        public static string Join(List<string> tags)
        {
            var sb = new StringBuilder();
            foreach (string tag in tags)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(tag);
            }
            return sb.ToString();
        }

        public static bool Contains(List<string> tags, string tag)
        {
            foreach (string t in tags)
            {
                if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
