using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 自定义拖动顺序在 kv1 设置中的编解码与排序应用。
    /// </summary>
    /// <remarks>
    /// 分隔符全部取 Windows 文件名/目录名的非法字符（<c>|</c>、<c>&gt;</c>、<c>:</c>），
    /// 模块名与条目标题都不可能含它们，因此编解码严格互逆、不需要任何转义，
    /// 与 <see cref="SettingsCodec"/> 的"值原样存储"不变量一致。
    /// 格式：<c>moduleCustomOrder = 模块A|模块B</c>，<c>entryCustomOrder = 模块A:标题1|标题2&gt;模块B:标题3</c>。
    /// </remarks>
    public static class CustomOrderCodec
    {
        private const char ItemSeparator = '|';
        private const char GroupSeparator = '>';
        private const char ScopeSeparator = ':';

        public static string EncodeNames(IEnumerable<string> names)
        {
            if (names == null) return string.Empty;

            var sb = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!seen.Add(name)) continue;
                if (sb.Length > 0) sb.Append(ItemSeparator);
                sb.Append(name);
            }
            return sb.ToString();
        }

        public static List<string> DecodeNames(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var part in raw.Split(ItemSeparator))
            {
                if (part.Length == 0) continue;
                if (result.IndexOf(part) >= 0) continue;
                result.Add(part);
            }
            return result;
        }

        public static string EncodeScopes(IEnumerable<KeyValuePair<string, List<string>>> scopes)
        {
            if (scopes == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var pair in scopes)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;

                var names = EncodeNames(pair.Value);
                if (names.Length == 0) continue;

                if (sb.Length > 0) sb.Append(GroupSeparator);
                sb.Append(pair.Key).Append(ScopeSeparator).Append(names);
            }
            return sb.ToString();
        }

        public static Dictionary<string, List<string>> DecodeScopes(string raw)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(raw)) return map;

            foreach (var record in raw.Split(GroupSeparator))
            {
                var cut = record.IndexOf(ScopeSeparator);
                if (cut <= 0) continue;

                var scope = record.Substring(0, cut);
                var names = DecodeNames(record.Substring(cut + 1));
                if (names.Count == 0) continue;

                map[scope] = names;
            }
            return map;
        }

        /// <summary>
        /// 按记录好的顺序排列；不在表里的（新建的、从别处拷进来的）保持进来的相对顺序落到末尾。
        /// 表为空时原样返回，即"以当前视图顺序为起点"。
        /// </summary>
        public static List<T> Apply<T>(IEnumerable<T> items, Func<T, string> keySelector, IList<string> order)
        {
            var list = items as IList<T> ?? items.ToList();
            if (order == null || order.Count == 0) return list.ToList();

            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Count; i++)
            {
                if (!rank.ContainsKey(order[i])) rank[order[i]] = i;
            }

            return list
                .Select((item, index) => new { Key = keySelector(item), Index = index })
                .OrderBy(x => RankOf(rank, x.Key))
                .ThenBy(x => x.Index)
                .Select(x => list[x.Index])
                .ToList();
        }

        /// <summary>把表里的旧名就地换成新名，位置不变。返回是否命中过。</summary>
        public static bool Rename(IList<string> names, string oldName, string newName)
        {
            if (names == null || string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return false;

            var hit = false;
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], oldName, StringComparison.OrdinalIgnoreCase))
                {
                    names[i] = newName;
                    hit = true;
                }
                else if (string.Equals(names[i], newName, StringComparison.OrdinalIgnoreCase))
                {
                    names.RemoveAt(i);
                    i--;
                }
            }
            return hit;
        }

        private static int RankOf(Dictionary<string, int> rank, string key)
        {
            if (key == null) return int.MaxValue;

            int value;
            return rank.TryGetValue(key, out value) ? value : int.MaxValue;
        }
    }
}
