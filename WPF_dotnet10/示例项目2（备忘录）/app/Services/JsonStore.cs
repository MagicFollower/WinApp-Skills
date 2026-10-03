using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MemoTask.Services
{
    /// <summary>
    /// 极简 JSON 读写：写临时文件再改名覆盖，进程中途被杀不会留下半截文件；
    /// 读的时候坏数据先改名留档再返回默认值，绝不因为一份脏数据打不开应用。
    /// </summary>
    internal static class JsonStore
    {
        private static readonly JsonSerializerOptions ReadOptions = Build(false);
        private static readonly JsonSerializerOptions WriteOptions = Build(true);

        public static T Load<T>(string path) where T : new()
        {
            try
            {
                if (!File.Exists(path)) return new T();
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return new T();
                T value = JsonSerializer.Deserialize<T>(text, ReadOptions);
                return value ?? new T();
            }
            catch (Exception)
            {
                Quarantine(path);
                return new T();
            }
        }

        public static bool Save<T>(string path, T value)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value, WriteOptions), Encoding.UTF8);
                File.Move(tmp, path, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>把脏文件改名留档，方便用户自己救数据；留档失败也不抛。</summary>
        public static void Quarantine(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Move(path, path + ".corrupt-" + stamp, true);
            }
            catch (Exception)
            {
                // 留档失败就放弃，宁可丢这份脏数据也不能反复抛异常
            }
        }

        private static JsonSerializerOptions Build(bool indent)
        {
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                WriteIndented = indent,
                // 窗口坐标在首轮运行是 NaN（还没落过位）。STJ 默认序列化 NaN 直接抛 ArgumentException，
                // 整份 settings.json 就写不出去，用户改的设置全丢（实测：去掉这行 selftest 退 1）。
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        /// <summary>整包导出用的序列化（缩进 + 枚举写字面量）。</summary>
        public static string ToJson<T>(T value)
        {
            return JsonSerializer.Serialize(value, WriteOptions);
        }
    }
}
