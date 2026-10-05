using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VocabDesk.Services
{
    /// <summary>
    /// 应用设置持久化（%APPDATA%\VocabDesk\settings.json，kv1 纯文本格式；文件名沿用历史叫法）。
    ///
    /// Load 与 Save 对外永不抛异常：损坏文件改名留档后回默认值，写失败只记诊断日志——
    /// 设置出问题不该让应用起不来，更不该在 UI 线程上重试等待（那会直接冻住界面）。
    /// </summary>
    public class SettingsService
    {
        /// <summary>配色档的稳定键：light / dark / system。由 ThemeService 校验并兜底。</summary>
        public string ThemeKey { get; set; }
        /// <summary>字体族名；空串表示用出厂族（Typography.DefaultFontFamilySource）。</summary>
        public string FontFamilyName { get; set; }
        /// <summary>正文基准字号，其余层级由 Typography 按差值派生。</summary>
        public double BaseFontSize { get; set; }
        /// <summary>全局缩放百分比，50–200。</summary>
        public double ZoomPercent { get; set; }

        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public bool WindowMaximized { get; set; }

        /// <summary>健康文件约 200 字节；超限即判损坏，避免脏数据拖死启动。</summary>
        internal const int MaxSettingsBytes = 64 * 1024;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        /// <summary>设置文件所在目录，只给"查看"用；不保证已存在（首次保存时才创建）。</summary>
        public static string SettingsDirectory { get { return SettingsDir; } }

        private string _lastWrittenText;

        /// <summary>构造即默认值，且不碰磁盘——这样 --selftest 里顺手 new 一个也不会读到用户那份文件。</summary>
        public SettingsService()
        {
            ApplyDefaults();
        }

        public void Load()
        {
            ApplyDefaults();
            _lastWrittenText = null;

            try
            {
                if (!File.Exists(SettingsFile)) return;

                var info = new FileInfo(SettingsFile);
                if (info.Length > MaxSettingsBytes)
                {
                    Quarantine("oversized: " + info.Length + " bytes");
                    return;
                }

                var map = SettingsCodec.Parse(File.ReadAllText(SettingsFile, Utf8NoBom));
                ApplyMap(map);
                _lastWrittenText = SettingsCodec.Serialize(ToMap());
            }
            catch (Exception ex)
            {
                Quarantine("parse failed: " + ex.GetType().Name + " " + ex.Message);
                ApplyDefaults();
            }
        }

        public void Save()
        {
            string text;
            try
            {
                text = SettingsCodec.Serialize(ToMap());
            }
            catch (Exception ex)
            {
                TryAppendDiagnostics("serialize", ex);
                return;
            }

            if (text.Length > MaxSettingsBytes)
            {
                TryAppendDiagnostics("refuse-write",
                    new IOException("serialized settings too large: " + text.Length));
                return;
            }

            // 内容没变就不写盘：滑杆每动一格都重新 Save 一次会把手动改的其它键一起冲掉，也是无谓的 IO
            if (text == _lastWrittenText && File.Exists(SettingsFile)) return;

            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

                var tempFile = SettingsFile + ".tmp";
                File.WriteAllText(tempFile, text, Utf8NoBom);
                File.Copy(tempFile, SettingsFile, true);
                TryDelete(tempFile);

                _lastWrittenText = text;
            }
            catch (Exception ex)
            {
                TryDelete(SettingsFile + ".tmp");
                TryAppendDiagnostics("save", ex);
            }
        }

        internal List<KeyValuePair<string, string>> ToMap()
        {
            var ci = CultureInfo.InvariantCulture;
            var entries = new List<KeyValuePair<string, string>>();

            entries.Add(Pair("format", "kv1"));
            entries.Add(Pair("themeKey", ThemeKey));
            entries.Add(Pair("fontFamilyName", FontFamilyName));
            entries.Add(Pair("baseFontSize", BaseFontSize.ToString(ci)));
            entries.Add(Pair("zoomPercent", ZoomPercent.ToString(ci)));
            entries.Add(Pair("windowWidth", WindowWidth.ToString(ci)));
            entries.Add(Pair("windowHeight", WindowHeight.ToString(ci)));
            entries.Add(Pair("windowLeft", WindowLeft.ToString(ci)));
            entries.Add(Pair("windowTop", WindowTop.ToString(ci)));
            entries.Add(Pair("windowMaximized", WindowMaximized ? "true" : "false"));

            return entries;
        }

        internal void ApplyMap(IDictionary<string, string> map)
        {
            if (map == null) return;

            string value;
            if (map.TryGetValue("themeKey", out value) && !string.IsNullOrWhiteSpace(value)) ThemeKey = value.Trim();
            if (map.TryGetValue("fontFamilyName", out value)) FontFamilyName = value;

            double number;
            var ci = CultureInfo.InvariantCulture;
            if (map.TryGetValue("baseFontSize", out value) &&
                double.TryParse(value, NumberStyles.Float, ci, out number))
                BaseFontSize = Helpers.Typography.ClampBase(number);
            if (map.TryGetValue("zoomPercent", out value) &&
                double.TryParse(value, NumberStyles.Float, ci, out number))
                ZoomPercent = Helpers.Typography.ClampZoom(number);

            // 字号与缩放一律钳回区间：设置文件是用户能自己开编辑器改的，
            // 500% 或 4 px 这种值不能让界面直接吃下去；解析不了的保留已生效值不覆盖
            if (map.TryGetValue("windowWidth", out value) && TrySize(value, ci, out number)) WindowWidth = number;
            if (map.TryGetValue("windowHeight", out value) && TrySize(value, ci, out number)) WindowHeight = number;
            if (map.TryGetValue("windowLeft", out value) && TryOffset(value, ci, out number)) WindowLeft = number;
            if (map.TryGetValue("windowTop", out value) && TryOffset(value, ci, out number)) WindowTop = number;

            bool flag;
            if (map.TryGetValue("windowMaximized", out value) && bool.TryParse(value, out flag))
                WindowMaximized = flag;
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? string.Empty);
        }

        private static bool TrySize(string value, CultureInfo ci, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, ci, out result)
                && result > 0 && result <= 20000;
        }

        private static bool TryOffset(string value, CultureInfo ci, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, ci, out result)
                && result > -20000 && result < 20000;
        }

        private void ApplyDefaults()
        {
            ThemeKey = "light";
            FontFamilyName = string.Empty;
            BaseFontSize = Helpers.Typography.DefaultBaseSize;
            ZoomPercent = Helpers.Typography.DefaultZoomPercent;
            WindowWidth = 980;
            WindowHeight = 640;
            WindowLeft = double.NaN;
            WindowTop = double.NaN;
            WindowMaximized = false;
        }

        /// <summary>只改名不删除，保留用户数据的取证可能。</summary>
        private static void Quarantine(string reason)
        {
            TryAppendDiagnostics("quarantine", new IOException(reason + " -> " + SettingsFile));
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var target = SettingsFile + ".corrupt-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                File.Move(SettingsFile, target);
            }
            catch
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryAppendDiagnostics(string stage, Exception ex)
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

                var log = Path.Combine(SettingsDir, "diagnostics.log");
                if (File.Exists(log) && new FileInfo(log).Length > 256 * 1024) File.Delete(log);

                File.AppendAllText(log,
                    DateTime.Now.ToString("o") + " [" + stage + "] " + ex.Message + "\r\n",
                    Utf8NoBom);
            }
            catch
            {
            }
        }
    }
}
