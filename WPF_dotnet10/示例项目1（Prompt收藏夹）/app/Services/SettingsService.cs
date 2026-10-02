using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 应用设置持久化（%APPDATA%\PromptFavorites\settings.json，kv1 纯文本格式）。
    /// </summary>
    /// <remarks>
    /// 值不做任何转义，因此不存在"写时转义、读时不解"的不对称；旧 JSON 文件仍可读取并自动迁移。
    /// <see cref="Load"/> 与 <see cref="Save"/> 对外永不抛异常：损坏文件改名留档后回默认值，
    /// 写失败只记诊断日志，避免设置问题让应用启动失败。
    /// </remarks>
    public class SettingsService
    {
        public string LastModule { get; set; }
        public bool MetadataCollapsed { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public WindowState WindowState { get; set; }
        public SortMode SortMode { get; set; }
        public ModuleSortMode ModuleSortMode { get; set; }
        public bool FavoriteFilter { get; set; }
        public string RootPath { get; set; }

        /// <summary>左栏拖动顺序；null 表示从没拖过，此时以视图当前顺序为起点且不写配置。</summary>
        public IList<string> ModuleOrder { get { return _moduleOrder; } }

        /// <summary>某模块的条目拖动顺序；没有记录过时返回 null。</summary>
        public IList<string> GetEntryOrder(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName)) return null;

            List<string> order;
            return _entryOrders.TryGetValue(moduleName, out order) ? order : null;
        }

        public void SetModuleOrder(IEnumerable<string> names)
        {
            var list = CustomOrderCodec.DecodeNames(CustomOrderCodec.EncodeNames(names));
            _moduleOrder = list.Count > 0 ? list : null;
        }

        public void SetEntryOrder(string moduleName, IEnumerable<string> titles)
        {
            if (string.IsNullOrEmpty(moduleName)) return;

            var list = CustomOrderCodec.DecodeNames(CustomOrderCodec.EncodeNames(titles));
            if (list.Count == 0)
            {
                _entryOrders.Remove(moduleName);
                return;
            }
            _entryOrders[moduleName] = list;
        }

        /// <summary>模块改名：换掉顺序表里的名字，并把它名下记的条目顺序表搬到新模块名。</summary>
        public void RenameModuleInOrders(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return;
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;

            if (_moduleOrder != null) CustomOrderCodec.Rename(_moduleOrder, oldName, newName);

            List<string> entryOrder;
            if (_entryOrders.TryGetValue(oldName, out entryOrder))
            {
                _entryOrders.Remove(oldName);
                _entryOrders[newName] = entryOrder;
            }
        }

        /// <summary>条目改名：顺序表里就地换名，位置不变。旧名不在表里时什么都不做。</summary>
        public void RenameEntryInOrder(string moduleName, string oldTitle, string newTitle)
        {
            if (string.IsNullOrEmpty(moduleName)) return;

            List<string> entryOrder;
            if (_entryOrders.TryGetValue(moduleName, out entryOrder))
                CustomOrderCodec.Rename(entryOrder, oldTitle, newTitle);
        }

        /// <summary>顺序表只占设置文件预算的一半，超了就先丢顺序，不能拖累其余设置写不进去。</summary>
        internal const int MaxOrderChars = 32 * 1024;

        /// <summary>健康文件约 200~300 字节；超限即判损坏，避免脏数据拖死启动。</summary>
        internal const int MaxSettingsBytes = 64 * 1024;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PromptFavorites");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        /// <summary>设置文件所在目录，只给"查看"用；不保证已存在（首次保存时才创建）。</summary>
        public static string SettingsDirectory { get { return SettingsDir; } }

        private string _lastWrittenText;
        private bool _loadedLegacyFormat;
        private List<string> _moduleOrder;

        private readonly Dictionary<string, List<string>> _entryOrders =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public void Load()
        {
            ApplyDefaults();
            _lastWrittenText = null;
            _loadedLegacyFormat = false;

            try
            {
                if (!File.Exists(SettingsFile)) return;

                var info = new FileInfo(SettingsFile);
                if (info.Length > MaxSettingsBytes)
                {
                    Quarantine("oversized: " + info.Length + " bytes");
                    return;
                }

                var text = File.ReadAllText(SettingsFile, Utf8NoBom);

                Dictionary<string, string> map;
                if (SettingsCodec.LooksLikeLegacyJson(text))
                {
                    map = SettingsCodec.ParseLegacyJson(text);
                    _loadedLegacyFormat = true;
                }
                else
                {
                    map = SettingsCodec.Parse(text);
                }

                ApplyMap(map);

                if (!_loadedLegacyFormat)
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

            if (!_loadedLegacyFormat && text == _lastWrittenText && File.Exists(SettingsFile))
                return;

            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                var tempFile = SettingsFile + ".tmp";
                File.WriteAllText(tempFile, text, Utf8NoBom);
                File.Copy(tempFile, SettingsFile, true);
                TryDelete(tempFile);

                _lastWrittenText = text;
                _loadedLegacyFormat = false;
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
            entries.Add(Pair("lastModule", LastModule));
            entries.Add(Pair("metadataCollapsed", Text(MetadataCollapsed)));
            entries.Add(Pair("windowWidth", WindowWidth.ToString(ci)));
            entries.Add(Pair("windowHeight", WindowHeight.ToString(ci)));
            entries.Add(Pair("windowLeft", WindowLeft.ToString(ci)));
            entries.Add(Pair("windowTop", WindowTop.ToString(ci)));
            entries.Add(Pair("windowState", WindowState.ToString()));
            entries.Add(Pair("sortMode", SortMode.ToString()));
            entries.Add(Pair("moduleSortMode", ModuleSortMode.ToString()));
            entries.Add(Pair("favoriteFilter", Text(FavoriteFilter)));
            entries.Add(Pair("rootPath", RootPath));

            var moduleOrderText = CustomOrderCodec.EncodeNames(_moduleOrder);
            var entryOrderText = CustomOrderCodec.EncodeScopes(_entryOrders);
            if (moduleOrderText.Length + entryOrderText.Length > MaxOrderChars)
            {
                TryAppendDiagnostics("refuse-order", new IOException(
                    "custom order too large: " + moduleOrderText.Length + "+" + entryOrderText.Length));
            }
            else
            {
                if (moduleOrderText.Length > 0)
                    entries.Add(Pair("moduleCustomOrder", moduleOrderText));
                if (entryOrderText.Length > 0)
                    entries.Add(Pair("entryCustomOrder", entryOrderText));
            }

            return entries;
        }

        internal void ApplyMap(IDictionary<string, string> map)
        {
            if (map == null) return;

            string value;
            if (map.TryGetValue("lastModule", out value)) LastModule = value;
            if (map.TryGetValue("rootPath", out value)) RootPath = value;

            bool flag;
            if (map.TryGetValue("metadataCollapsed", out value) && bool.TryParse(value, out flag))
                MetadataCollapsed = flag;
            if (map.TryGetValue("favoriteFilter", out value) && bool.TryParse(value, out flag))
                FavoriteFilter = flag;

            double number;
            var ci = CultureInfo.InvariantCulture;
            if (map.TryGetValue("windowWidth", out value) && TrySize(value, ci, out number)) WindowWidth = number;
            if (map.TryGetValue("windowHeight", out value) && TrySize(value, ci, out number)) WindowHeight = number;
            if (map.TryGetValue("windowLeft", out value) && TryOffset(value, ci, out number)) WindowLeft = number;
            if (map.TryGetValue("windowTop", out value) && TryOffset(value, ci, out number)) WindowTop = number;

            SortMode sortMode;
            if (map.TryGetValue("sortMode", out value) && Enum.TryParse(value, out sortMode))
                SortMode = sortMode;

            ModuleSortMode moduleSortMode;
            if (map.TryGetValue("moduleSortMode", out value) && Enum.TryParse(value, out moduleSortMode))
                ModuleSortMode = moduleSortMode;

            WindowState windowState;
            if (map.TryGetValue("windowState", out value) && Enum.TryParse(value, out windowState))
                WindowState = windowState;

            if (map.TryGetValue("moduleCustomOrder", out value))
            {
                var moduleOrder = CustomOrderCodec.DecodeNames(value);
                _moduleOrder = moduleOrder.Count > 0 ? moduleOrder : null;
            }

            if (map.TryGetValue("entryCustomOrder", out value))
            {
                foreach (var pair in CustomOrderCodec.DecodeScopes(value))
                    _entryOrders[pair.Key] = pair.Value;
            }
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value ?? string.Empty);
        }

        private static string Text(bool value)
        {
            return value ? "true" : "false";
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
            LastModule = string.Empty;
            RootPath = string.Empty;
            MetadataCollapsed = false;
            WindowWidth = 1200;
            WindowHeight = 750;
            WindowLeft = -1;
            WindowTop = -1;
            WindowState = WindowState.Normal;
            SortMode = SortMode.UseCount;
            ModuleSortMode = ModuleSortMode.CreatedAt;
            FavoriteFilter = false;
            _moduleOrder = null;
            _entryOrders.Clear();
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
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                var log = Path.Combine(SettingsDir, "diagnostics.log");
                if (File.Exists(log) && new FileInfo(log).Length > 256 * 1024)
                    File.Delete(log);

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
