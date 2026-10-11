using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using PromptFavorites.Helpers;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    /// <summary>
    /// 应用设置持久化，两层落盘、同一套 kv1 纯文本格式：
    /// <b>全局引导</b>（<c>%APPDATA%\PromptFavorites\settings.json</c>）存外观与窗口，并靠 <c>rootPath</c>
    /// 记住上次用的根目录；<b>每根配置</b>（<c>&lt;根目录&gt;\.PromptFavorites\settings.json</c>）存
    /// 只在某一个根目录下成立的状态（上次模块、两套拖动顺序、排序档、收藏筛选）。
    /// </summary>
    /// <remarks>
    /// 拆两层的直接原因是"切换根目录会把上一个根的配置覆盖掉"：那些状态按<b>模块名</b>记账，过去和外观
    /// 挤在同一份全局文件里，换根后上一个根的 <c>lastModule</c> 与顺序表仍留在内存中，下一次 <c>Save</c>
    /// 就整体盖回去——同名模块还会直接串顺序。
    /// 值不做任何转义，因此不存在"写时转义、读时不解"的不对称；旧 JSON 文件仍可读取并自动迁移。
    /// <see cref="LoadGlobal"/> 与 <see cref="Save"/> 对外永不抛异常：损坏文件改名留档后回默认值，
    /// 写失败只记诊断日志，避免设置问题让应用启动失败。
    /// 根目录不可写（只读盘、离线同步盘）时每根那份退化成"继续写在全局文件里"，由
    /// <see cref="IsUsingRootSettingsFallback"/> 播报——视图状态不能因为一个写不进去的目录就静默清零。
    /// 构造函数与静态路径推导都不碰文件系统，<c>--selftest</c> 靠这一点跑纯函数。
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
        public AppThemeMode ThemeMode { get; set; }
        public bool FavoriteFilter { get; set; }

        /// <summary>上次使用的 Prompt 根目录。<b>永远留在全局引导文件里</b>——每根配置的位置要靠它才知道。</summary>
        public string RootPath { get; set; }

        /// <summary>字体族名；空串表示用出厂族（<see cref="Typography.DefaultFontFamilySource"/>）。</summary>
        public string FontFamilyName { get; set; }
        /// <summary>正文基准字号，其余层级由 <see cref="Typography"/> 按差值派生。</summary>
        public double BaseFontSize { get; set; }
        /// <summary>全局缩放百分比，50–200。</summary>
        public double ZoomPercent { get; set; }

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

        /// <summary>健康文件实测 300~530 字节（全局那层更小）；超限即判损坏，避免脏数据拖死启动。</summary>
        internal const int MaxSettingsBytes = 64 * 1024;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>根目录下的配置子目录名。点开头只是提示"这不是你的模块"，Windows 并不会自动隐藏。</summary>
        internal const string RootConfigDirectoryName = ".PromptFavorites";

        private const string RootConfigFileName = "settings.json";

        private static readonly string DefaultGlobalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PromptFavorites");

        private readonly string _globalDir;
        private readonly string _globalFile;

        public SettingsService() : this(DefaultGlobalDir)
        {
        }

        /// <summary>
        /// 自测用的重定向入口：整个实例的全局侧读写都落在给定的临时目录里。
        /// 没有这个口子，"换根目录不覆盖上一个根"这条主判据就只能靠真读写
        /// <c>%APPDATA%\PromptFavorites\settings.json</c> 来验，那是拿用户配置当测试耗材。
        /// </summary>
        internal SettingsService(string globalDir)
        {
            _globalDir = globalDir;
            _globalFile = Path.Combine(globalDir, "settings.json");
        }

        /// <summary>全局引导文件所在目录；只给"查看"用，不保证已存在（首次保存时才创建）。</summary>
        public static string SettingsDirectory { get { return DefaultGlobalDir; } }

        /// <summary>
        /// 跟着根目录走的那些键。判据是"换个根目录，这个值还有没有意义"——
        /// <c>lastModule</c> 与两套顺序按<b>模块名</b>记账，<c>sortMode</c>/<c>moduleSortMode</c>/<c>favoriteFilter</c>
        /// 是某个根下的视图选择，都只在当前根目录下成立；窗口几何、配色与排印是整机偏好，留全局。
        /// <c>rootPath</c> 刻意不在这里：它是找到本根配置的入口，只能存在固定位置。
        /// </summary>
        private static readonly HashSet<string> RootScopedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "lastModule",
            "sortMode",
            "moduleSortMode",
            "favoriteFilter",
            "moduleCustomOrder",
            "entryCustomOrder"
        };

        private string _rootDir;
        private string _rootFile;
        private string _lastWrittenGlobalText;
        private string _lastWrittenRootText;
        private bool _rootWriteFailed;
        private bool _loadedLegacyFormat;
        private List<string> _moduleOrder;

        private readonly Dictionary<string, List<string>> _entryOrders =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 当前根目录自己的配置目录（<c>&lt;根目录&gt;\.PromptFavorites</c>）；没挂上根目录时为空串。
        /// </summary>
        public string RootSettingsDirectory { get { return _rootDir ?? string.Empty; } }

        /// <summary>
        /// 每根配置写不进根目录（只读盘、离线同步盘、无权限），已回退成继续留在全局文件里。
        /// </summary>
        public bool IsUsingRootSettingsFallback { get { return _rootWriteFailed; } }

        /// <summary>
        /// 纯函数：某个根目录对应的配置目录。根为空则返回空串，且一律不碰文件系统（自测依赖这一点）。
        /// 走 <see cref="SettingsCodec.CollapseSeparators"/> 是因为历史事故会让设置里的路径分隔符翻倍，
        /// 拼出来的配置目录必须和 <see cref="AttachRoot"/> 实际使用的口径一致。
        /// </summary>
        internal static string RootConfigDirectoryFor(string rootPath)
        {
            var value = (rootPath ?? string.Empty).Trim().Trim('"');
            if (value.Length == 0) return string.Empty;
            return Path.Combine(SettingsCodec.CollapseSeparators(value), RootConfigDirectoryName);
        }

        /// <summary>配置占掉了这个名字，用户不能再拿它当模块名（否则模块表里会凭空少一项）。</summary>
        public static bool IsReservedRootEntryName(string name)
        {
            return string.Equals((name ?? string.Empty).Trim(), RootConfigDirectoryName,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 挂上某个根目录并读它自己的配置。
        /// <paramref name="inheritUnmigrated"/> 只在启动首挂时传 true：那时本根还没有配置文件，
        /// 而旧版全局文件里可能还留着它的数据键，原样带过去正好是一次静默迁移——下一次 <see cref="Save"/>
        /// 把它们写进本根、同时从全局里摘掉。<b>换根目录时必须是 false</b>：上一个根的状态
        /// 绝不能变成新根的起点，那正是这次要修的覆盖问题。
        /// 文件存在但读坏了（超限/解析失败）一律回默认，且已改名留档，不去捡全局那份陈年值。
        /// </summary>
        public void AttachRoot(string rootPath, bool inheritUnmigrated)
        {
            _rootDir = RootConfigDirectoryFor(rootPath);
            _rootFile = _rootDir.Length == 0 ? null : Path.Combine(_rootDir, RootConfigFileName);
            _lastWrittenRootText = null;
            _rootWriteFailed = false;

            if (string.IsNullOrEmpty(_rootFile))
            {
                if (!inheritUnmigrated) ApplyRootScopedDefaults();
                return;
            }

            bool existed;
            string rawText;
            var map = ReadRootMap(out existed, out rawText);
            if (map != null)
            {
                ApplyRootScopedDefaults();
                ApplyMap(map, true);
                // 锚成**文件真实内容**而不是重新序列化一遍：重新算出来的那份天生不含被摘掉的键，
                // 于是"文本没变就不写盘"会把盘上多出来的键永远留在原地。
                _lastWrittenRootText = rawText;
                return;
            }

            if (existed || !inheritUnmigrated) ApplyRootScopedDefaults();
        }

        private Dictionary<string, string> ReadRootMap(out bool existed, out string rawText)
        {
            existed = false;
            rawText = null;
            try
            {
                if (!File.Exists(_rootFile)) return null;
                existed = true;

                var info = new FileInfo(_rootFile);
                if (info.Length > MaxSettingsBytes)
                {
                    QuarantineRoot("oversized: " + info.Length + " bytes");
                    return null;
                }

                rawText = File.ReadAllText(_rootFile, Utf8NoBom);
                return SettingsCodec.Parse(rawText);
            }
            catch (Exception ex)
            {
                QuarantineRoot("parse failed: " + ex.GetType().Name + " " + ex.Message);
                return null;
            }
        }

        public void LoadGlobal()
        {
            ApplyDefaults();
            _lastWrittenGlobalText = null;
            _loadedLegacyFormat = false;

            try
            {
                if (!File.Exists(_globalFile)) return;

                var info = new FileInfo(_globalFile);
                if (info.Length > MaxSettingsBytes)
                {
                    Quarantine("oversized: " + info.Length + " bytes");
                    return;
                }

                var text = File.ReadAllText(_globalFile, Utf8NoBom);

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
                {
                    // 同上：拿文件真实内容当"已经写过的样子"。全局侧在迁移前和根不可写时会多带
                    // 几个数据键，只有和真实内容比，第一次 Save 才知道要把它们摘掉。
                    _lastWrittenGlobalText = text;
                }
            }
            catch (Exception ex)
            {
                Quarantine("parse failed: " + ex.GetType().Name + " " + ex.Message);
                ApplyDefaults();
            }
        }

        /// <summary>
        /// 两侧各写各的：<b>写成功的那侧才从另一侧消失</b>。每根侧没写成时全局侧继续带上数据键，
        /// 于是根目录不可写的场景里配置仍有去处，代价是这份副本只服务"下次还开同一个根"。
        /// </summary>
        public void Save()
        {
            var all = ToMap();

            bool hasRoot = !string.IsNullOrEmpty(_rootFile);
            bool rootWritten = false;
            if (hasRoot)
            {
                var rootText = SettingsCodec.Serialize(Partition(all, true));
                rootWritten = TryWriteFile(_rootFile, _rootDir, rootText, false,
                    text => { _lastWrittenRootText = text; });
            }

            // 没挂上根目录（根解析失败）不算"回退"，那种情况全局文件本来就带着全部键。
            _rootWriteFailed = hasRoot && !rootWritten;

            var globalEntries = new List<KeyValuePair<string, string>>();
            foreach (var entry in all)
            {
                if (rootWritten && RootScopedKeys.Contains(entry.Key)) continue;
                globalEntries.Add(entry);
            }

            var globalText = SettingsCodec.Serialize(globalEntries);
            TryWriteFile(_globalFile, _globalDir, globalText, _loadedLegacyFormat,
                text =>
                {
                    _lastWrittenGlobalText = text;
                    _loadedLegacyFormat = false;
                });
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
            entries.Add(Pair("themeMode", ThemeMode.ToString()));
            entries.Add(Pair("favoriteFilter", Text(FavoriteFilter)));
            entries.Add(Pair("rootPath", RootPath));
            entries.Add(Pair("fontFamilyName", FontFamilyName));
            entries.Add(Pair("baseFontSize", BaseFontSize.ToString(ci)));
            entries.Add(Pair("zoomPercent", ZoomPercent.ToString(ci)));

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

        /// <param name="rootSide">true 表示这张表来自每根配置文件。那种文件里 <c>rootPath</c> 一律忽略——
        /// 写侧从不输出这个键，出现它只可能是手改或整目录拷贝的产物，若让它生效就等于拿一份视图配置
        /// 去改引导指针，两个根会因此互相指到对方身上。</param>
        internal void ApplyMap(IDictionary<string, string> map, bool rootSide = false)
        {
            if (map == null) return;

            string value;
            if (map.TryGetValue("lastModule", out value)) LastModule = value;
            if (!rootSide && map.TryGetValue("rootPath", out value)) RootPath = value;

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

            // 枚举一律按**名**解析：Enum.TryParse 默许数字串（"1" 会静默变成第一个枚举值之后的
            // 某档），而设置文件是用户可以拿编辑器打开改的，数字串必须判非法而不是猜一个档。
            SortMode sortMode;
            if (map.TryGetValue("sortMode", out value) && TryParseName<SortMode>(value, out sortMode))
                SortMode = sortMode;

            ModuleSortMode moduleSortMode;
            if (map.TryGetValue("moduleSortMode", out value) && TryParseName<ModuleSortMode>(value, out moduleSortMode))
                ModuleSortMode = moduleSortMode;

            WindowState windowState;
            if (map.TryGetValue("windowState", out value) && TryParseName<WindowState>(value, out windowState))
                WindowState = windowState;

            AppThemeMode themeMode;
            if (map.TryGetValue("themeMode", out value) && TryParseName<AppThemeMode>(value, out themeMode))
                ThemeMode = themeMode;

            // 字体族名原样存、原样取（值里不含 CR/LF，kv1 的不变量成立）；字号与缩放一律钳回区间——
            // 设置文件是用户能自己用编辑器改的，500% 或 4px 这种值不能让界面直接吃下去。
            if (map.TryGetValue("fontFamilyName", out value)) FontFamilyName = value;

            double baseSize;
            if (map.TryGetValue("baseFontSize", out value)
                && double.TryParse(value, NumberStyles.Float, ci, out baseSize))
                BaseFontSize = Typography.ClampBase(baseSize);

            double zoom;
            if (map.TryGetValue("zoomPercent", out value)
                && double.TryParse(value, NumberStyles.Float, ci, out zoom))
                ZoomPercent = Typography.ClampZoom(zoom);

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

        /// <summary>
        /// 按枚举<b>名</b>解析，拒绝空值与含数字的串。<see cref="Enum.TryParse{TEnum}(string,out TEnum)"/>
        /// 会把 "1" 静默解析成第 2 个枚举成员，而设置文件是用户能自己用编辑器改的，
        /// 那种值应当判非法保留原档，而不是猜一档。
        /// </summary>
        private static bool TryParseName<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            result = default(TEnum);
            if (string.IsNullOrWhiteSpace(value)) return false;

            var text = value.Trim();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') return false;
            }

            TEnum parsed;
            if (!Enum.TryParse(text, false, out parsed)) return false;
            result = parsed;
            return true;
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

        /// <summary>
        /// 按归属取一侧的键。<see cref="RootScopedKeys"/> 是唯一出处，写侧与读侧都从它派生，
        /// 这样"某个键两边都写"或"两边都不写"这类漂移会被自测抓住。
        /// </summary>
        internal static List<KeyValuePair<string, string>> Partition(
            IEnumerable<KeyValuePair<string, string>> entries, bool rootSide)
        {
            var picked = new List<KeyValuePair<string, string>>();
            foreach (var entry in entries)
            {
                if (RootScopedKeys.Contains(entry.Key) == rootSide) picked.Add(entry);
            }
            return picked;
        }

        private void ApplyDefaults()
        {
            RootPath = string.Empty;
            MetadataCollapsed = false;
            WindowWidth = 1200;
            WindowHeight = 750;
            WindowLeft = -1;
            WindowTop = -1;
            WindowState = WindowState.Normal;
            ThemeMode = AppThemeMode.Light;
            FontFamilyName = string.Empty;
            BaseFontSize = Typography.DefaultBaseSize;
            ZoomPercent = Typography.DefaultZoomPercent;
            ApplyRootScopedDefaults();
        }

        /// <summary>只回"跟着根目录走"的那几个键，动不到外观与窗口——换根时靠它清空上一个根的状态。</summary>
        private void ApplyRootScopedDefaults()
        {
            LastModule = string.Empty;
            SortMode = SortMode.UseCount;
            ModuleSortMode = ModuleSortMode.CreatedAt;
            FavoriteFilter = false;
            _moduleOrder = null;
            _entryOrders.Clear();
        }

        /// <summary>只改名不删除，保留用户数据的取证可能。</summary>
        private void Quarantine(string reason)
        {
            TryAppendDiagnostics("quarantine", new IOException(reason + " -> " + _globalFile));
            TryMoveAside(_globalFile);
        }

        private void QuarantineRoot(string reason)
        {
            TryAppendDiagnostics("quarantine-root", new IOException(reason + " -> " + _rootFile));
            TryMoveAside(_rootFile);
        }

        private static void TryMoveAside(string file)
        {
            try
            {
                if (!File.Exists(file)) return;
                var target = file + ".corrupt-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                File.Move(file, target);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 原子性沿用旧写法：先落 <c>.tmp</c> 再覆盖，避免半截文件；文本没变就不动盘。
        /// 返回 false 表示这次没写成功（无权限、只读盘、磁盘满、超限），调用方据此决定要不要留副本。
        /// </summary>
        private bool TryWriteFile(string file, string dir, string text, bool force,
            Action<string> onWritten)
        {
            if (text.Length > MaxSettingsBytes)
            {
                TryAppendDiagnostics("refuse-write",
                    new IOException("serialized settings too large: " + text.Length + " -> " + file));
                return false;
            }

            if (!force && text == LastWritten(file) && File.Exists(file)) return true;

            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tempFile = file + ".tmp";
                File.WriteAllText(tempFile, text, Utf8NoBom);
                File.Copy(tempFile, file, true);
                TryDelete(tempFile);

                onWritten(text);
                return true;
            }
            catch (Exception ex)
            {
                TryDelete(file + ".tmp");
                TryAppendDiagnostics("save", ex);
                return false;
            }
        }

        private string LastWritten(string file)
        {
            return file == _globalFile ? _lastWrittenGlobalText : _lastWrittenRootText;
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

        private void TryAppendDiagnostics(string stage, Exception ex)
        {
            try
            {
                if (!Directory.Exists(_globalDir))
                    Directory.CreateDirectory(_globalDir);

                var log = Path.Combine(_globalDir, "diagnostics.log");
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
