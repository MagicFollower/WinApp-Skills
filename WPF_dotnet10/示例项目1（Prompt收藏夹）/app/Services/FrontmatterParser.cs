using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    public static class FrontmatterParser
    {
        private const string Separator = "---";
        private static readonly string[] SepArray = { "---" };

        public static void Parse(string fileContent, out FrontmatterData data, out string body)
        {
            data = new FrontmatterData();
            body = fileContent ?? string.Empty;

            if (string.IsNullOrEmpty(fileContent)) return;

            var trimmed = fileContent.TrimStart('\r', '\n');
            if (!trimmed.StartsWith("---")) return;

            var afterFirst = trimmed.Substring(3).TrimStart('\r', '\n');
            var endIdx = FindEndMarker(afterFirst);
            if (endIdx < 0) return;

            var fmBlock = afterFirst.Substring(0, endIdx);
            body = afterFirst.Substring(endIdx + 3);
            if (body.StartsWith("\r\n")) body = body.Substring(2);
            else if (body.StartsWith("\n") || body.StartsWith("\r")) body = body.Substring(1);

            ParseFrontmatter(fmBlock, data);
        }

        private static int FindEndMarker(string text)
        {
            var lines = text.Split('\n');
            var pos = 0;
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Trim() == Separator) return pos;
                pos += rawLine.Length + 1;
            }
            return -1;
        }

        private static void ParseFrontmatter(string block, FrontmatterData data)
        {
            var lines = block.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r').Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var colonIdx = line.IndexOf(':');
                if (colonIdx <= 0) continue;

                var key = line.Substring(0, colonIdx).Trim();
                var value = line.Substring(colonIdx + 1).Trim();

                switch (key)
                {
                    case "title":
                        data.Title = value;
                        break;
                    case "module":
                        data.Module = value;
                        break;
                    case "favorite":
                        bool fav;
                        if (bool.TryParse(value, out fav))
                            data.Favorite = fav;
                        break;
                    case "useCount":
                        int count;
                        if (int.TryParse(value, out count))
                            data.UseCount = count;
                        break;
                    case "createdAt":
                        DateTime dt;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out dt))
                            data.CreatedAt = dt;
                        break;
                    case "updatedAt":
                        DateTime udt;
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out udt))
                            data.UpdatedAt = udt;
                        break;
                    case "lastUsedAt":
                        if (!string.IsNullOrEmpty(value) && value.ToLowerInvariant() != "null")
                        {
                            DateTime ldt;
                            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out ldt))
                                data.LastUsedAt = ldt;
                        }
                        break;
                    default:
                        // 本程序不认识的字段原样留着，写回时带回（值可能含冒号，只按第一个冒号切）
                        data.Unknown.Add(new KeyValuePair<string, string>(key, value));
                        break;
                }
            }
        }

        public static string Serialize(FrontmatterData data, string body)
        {
            var sw = new StringWriter();
            sw.WriteLine(Separator);
            sw.WriteLine("title: " + (data.Title ?? string.Empty));
            sw.WriteLine("module: " + (data.Module ?? string.Empty));
            sw.WriteLine("favorite: " + data.Favorite.ToString().ToLowerInvariant());
            sw.WriteLine("useCount: " + data.UseCount);
            sw.WriteLine("createdAt: " + data.CreatedAt.ToString("o"));
            sw.WriteLine("updatedAt: " + data.UpdatedAt.ToString("o"));
            sw.WriteLine("lastUsedAt: " + (data.LastUsedAt.HasValue
                ? data.LastUsedAt.Value.ToString("o")
                : "null"));
            // 本程序不认识的字段跟在已知字段后面原样带回，不重排、不改值
            foreach (var pair in data.Unknown)
                sw.WriteLine(pair.Key + ": " + pair.Value);
            sw.Write(Separator);
            sw.Write("\r\n");
            sw.Write(body ?? string.Empty);
            return sw.ToString();
        }
    }
}
