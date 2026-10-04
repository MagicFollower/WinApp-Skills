using System;
using System.Collections.Generic;

namespace PromptFavorites.Models
{
    public class FrontmatterData
    {
        public string Title { get; set; }
        public string Module { get; set; }
        public bool Favorite { get; set; }
        public int UseCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// frontmatter 里本程序不认识的字段（用户或 Obsidian 之类外部工具加的）。
        /// 读时收下、写时原样带回——否则"备份=复制文件夹、可以直接用外部编辑器改"这个承诺，
        /// 每次保存都会被静默吃掉一块。
        /// </summary>
        public List<KeyValuePair<string, string>> Unknown { get; private set; }

        public FrontmatterData()
        {
            Unknown = new List<KeyValuePair<string, string>>();
        }

        /// <summary>把另一份里读到的未知字段搬过来（按原顺序，重名者保留先出现的）。</summary>
        public void AdoptUnknownFrom(FrontmatterData other)
        {
            if (other == null || other.Unknown.Count == 0) return;
            foreach (var pair in other.Unknown)
            {
                bool present = false;
                foreach (var mine in Unknown)
                {
                    if (string.Equals(mine.Key, pair.Key, StringComparison.Ordinal)) { present = true; break; }
                }
                if (!present) Unknown.Add(pair);
            }
        }

        public static FrontmatterData WithDefaults(string fileName, string folderName, DateTime fileCreatedAt)
        {
            return new FrontmatterData
            {
                Title = !string.IsNullOrEmpty(fileName) ? fileName : "未命名",
                Module = !string.IsNullOrEmpty(folderName) ? folderName : "未分类",
                Favorite = false,
                UseCount = 0,
                CreatedAt = fileCreatedAt,
                UpdatedAt = fileCreatedAt,
                LastUsedAt = null
            };
        }

        public static bool IsValidTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in title)
            {
                foreach (var ic in invalid)
                {
                    if (c == ic) return false;
                }
            }
            return true;
        }
    }
}
