using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace StartUI4Controls
{
    /// <summary>
    /// 主题模式枚举。
    /// </summary>
    public enum UI4ThemeMode
    {
        /// <summary>亮色主题。</summary>
        Light,
        /// <summary>暗色主题。</summary>
        Dark,
        /// <summary>跟随系统（读 <c>AppsUseLightTheme</c> 并监听 <see cref="SystemEvents.UserPreferenceChanged"/>）。</summary>
        System,
        /// <summary>高对比度主题，等价于 <see cref="UI4Theme.Apply"/>(“highcontrast”)。</summary>
        HighContrast
    }

    /// <summary>
    /// 统一主题系统：由 <see cref="UI4ThemeDefinition"/> 数据驱动，提供语义化颜色令牌，
    /// 并把每个主题键的令牌写成<b>一份共享资源字典</b>挂进 <see cref="Application.Resources"/>
    /// （<c>UI4.Brush.X</c> / <c>UI4.Color.X</c>）；宿主用 <c>{DynamicResource}</c>、库内控件用
    /// <c>SetResourceReference</c> 消费同一份字典，因此切换主题不需要逐控件刷新。
    /// <see cref="ThemeChanged"/> 只剩真正命令式的消费者（DWM 标题栏、AvalonEdit 语法高亮）。
    /// </summary>
    public class UI4Theme
    {
        // ────────────────────────────────────────────────────────
        //  静态 Current + 模式 + 通知
        // ────────────────────────────────────────────────────────

        private static UI4Theme _current;
        private static UI4ThemeMode _requestedMode = UI4ThemeMode.Light;
        private static UI4ThemeMode _resolvedMode = UI4ThemeMode.Light;
        private static string _resolvedKey = "light";
        private static int _themeVersion;

        private static readonly Dictionary<string, UI4ThemeDefinition> _definitions =
            new Dictionary<string, UI4ThemeDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                { "light", UI4ThemeDefinition.Light() },
                { "dark", UI4ThemeDefinition.Dark() },
                { "highcontrast", UI4ThemeDefinition.HighContrast() },
            };

        /// <summary>每个主题键对应一个可共享的 <see cref="UI4Theme"/> 实例（全局与 <see cref="UI4ThemeScope"/> 复用，避免重复构建）。</summary>
        private static readonly Dictionary<string, UI4Theme> _instances =
            new Dictionary<string, UI4Theme>(StringComparer.OrdinalIgnoreCase);


        /// <summary>主题代号，每次全局主题重写（切换 / 设置强调色）自增。供标题栏等外部染色器判断是否需要重染。</summary>
        internal static int ThemeVersion
        {
            get { return _themeVersion; }
        }

        static UI4Theme()
        {
            // 标题栏不属于 WPF 客户区，只能由 DWM 染色；主题引擎一被触碰就挂上窗口钩子
            UI4WindowTitleBar.Install();
        }

        /// <summary>获取当前主题实例。首次访问时自动初始化为 Light 主题。</summary>
        public static UI4Theme Current
        {
            get
            {
                if (_current == null)
                    _current = InstanceOf("light");
                return _current;
            }
            private set => _current = value;
        }

        /// <summary>获取最近一次 <see cref="SetTheme"/> 请求的模式（可能是 <see cref="UI4ThemeMode.System"/>）。</summary>
        public static UI4ThemeMode CurrentMode => _requestedMode;

        /// <summary>把 <see cref="UI4ThemeMode.System"/> 解析为实际的 Light / Dark / HighContrast；其余模式与 <see cref="CurrentMode"/> 相同。</summary>
        public static UI4ThemeMode ResolvedMode => _resolvedMode;

        /// <summary>当前生效的主题键（<c>light</c> / <c>dark</c> / <c>highcontrast</c> / 自定义）。</summary>
        public static string ResolvedKey => _resolvedKey;

        /// <summary>静态属性变化通知，供 XAML 绑定 <c>UI4Theme.CurrentMode</c> / <c>ResolvedMode</c> 等使用。</summary>
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>主题切换事件。控件不经由本事件刷新（颜色走资源引用），订阅方是 DWM 标题栏染色与 AvalonEdit 高亮。</summary>
        public static event EventHandler ThemeChanged;

        /// <summary>
        /// 设置当前主题模式。调用后共享字典整体换入 <see cref="Application.Resources"/>，
        /// 全部资源引用式控件与宿主 <c>{DynamicResource}</c> 同时跟随；解析后的实际主题与当前相同时不重复换入。
        /// </summary>
        public static void SetTheme(UI4ThemeMode mode)
        {
            bool requestedChanged = mode != _requestedMode;
            _requestedMode = mode;

            string key;
            if (mode == UI4ThemeMode.System)
            {
                EnableSystemFollow();
                key = ResolveSystemKey();
            }
            else
            {
                DisableSystemFollow();
                key = KeyForMode(mode);
            }

            if (requestedChanged)
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(CurrentMode)));

            UI4ThemeMode resolved = ModeForKey(key);
            ApplyResolved(key, resolved);
        }

        private static string KeyForMode(UI4ThemeMode mode)
        {
            switch (mode)
            {
                case UI4ThemeMode.Dark: return "dark";
                case UI4ThemeMode.HighContrast: return "highcontrast";
                default: return "light";
            }
        }

        /// <summary>主题键回报给 <see cref="UI4ThemeMode"/> 的形式；自定义键统一报 <see cref="UI4ThemeMode.Light"/>。</summary>
        private static UI4ThemeMode ModeForKey(string key)
        {
            if (string.Equals(key, "dark", StringComparison.OrdinalIgnoreCase)) return UI4ThemeMode.Dark;
            if (string.Equals(key, "highcontrast", StringComparison.OrdinalIgnoreCase)) return UI4ThemeMode.HighContrast;
            return UI4ThemeMode.Light;
        }

        /// <summary>按已注册主题键应用主题（如 "light" / "dark" / "highcontrast" / 自定义）。未知键返回 false。</summary>
        public static bool Apply(string definitionKey)
        {
            if (!_definitions.ContainsKey(definitionKey)) return false;
            UI4ThemeMode mode = ModeForKey(definitionKey);
            _requestedMode = mode;
            DisableSystemFollow();
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(CurrentMode)));
            ApplyResolved(definitionKey, mode);
            return true;
        }

        /// <summary>
        /// 注册一份自定义主题定义（同名覆盖）。覆盖的若是<b>当前生效的键</b>，会立即整体重新应用一次：
        /// 重建 <see cref="Current"/>、换入新的共享字典、把同键的 <see cref="UI4ThemeScope"/> 重指向新实例，并触发 <see cref="ThemeChanged"/>。
        /// </summary>
        public static void Register(UI4ThemeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _definitions[definition.Key] = definition;
            _instances.Remove(definition.Key);
            _sharedResources.Remove(definition.Key);

            // 覆盖的正是当前生效的键时，就地整体重来一遍。只清缓存不重建 Current 的话，
            // ApplyResolved 会判「键没变」而留着旧实例：引用式控件用新令牌，
            // UI4Theme.Current 驱动的命令式路径（DWM 标题栏、编号角标）却还停在旧令牌。
            if (_current != null && string.Equals(definition.Key, _resolvedKey, StringComparison.OrdinalIgnoreCase))
            {
                _current = null;
                ApplyResolved(_resolvedKey, ModeForKey(_resolvedKey));
            }
            else
            {
                // 覆盖的不是当前键，但可能有作用域正用着它：那些作用域也立刻换到新字典，不等下一次挂载
                UI4ThemeScope.RebindIfScoped(definition.Key);
            }
        }

        /// <summary>主题键是否已注册。供 <see cref="UI4ThemeScope"/> 校验作用域键。</summary>
        internal static bool IsRegistered(string key) => key != null && _definitions.ContainsKey(key);

        /// <summary>已注册的主题键集合。</summary>
        public static IEnumerable<string> ThemeKeys => _definitions.Keys;

        private static void ApplyResolved(string key, UI4ThemeMode resolved)
        {
            bool resolvedChanged = !string.Equals(key, _resolvedKey, StringComparison.OrdinalIgnoreCase) || _current == null;
            _resolvedKey = key;
            _resolvedMode = resolved;
            if (resolvedChanged)
            {
                Current = InstanceOf(key);
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ResolvedMode)));
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ResolvedKey)));
                NotifyThemeChanged();
            }
            WriteToApplicationResources();
        }

        // ────────────────────────────────────────────────────────
        //  系统主题跟随
        // ────────────────────────────────────────────────────────

        private static bool _followingSystem;
        private static bool _followSystemHighContrast;

        /// <summary>
        /// <see cref="UI4ThemeMode.System"/> 下是否优先跟随系统高对比度（默认 false，保持既有亮/暗行为）。
        /// 设为 true 且当前处于 System 模式时立即重新解析主题。
        /// </summary>
        public static bool FollowSystemHighContrast
        {
            get { return _followSystemHighContrast; }
            set
            {
                if (_followSystemHighContrast == value) return;
                _followSystemHighContrast = value;
                if (_requestedMode == UI4ThemeMode.System) ReResolveSystemTheme();
            }
        }

        private static bool QuerySystemIsDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("AppsUseLightTheme");
                        if (v is int && (int)v == 0) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>解析「跟随系统」应使用的主题键：高对比度优先，其次按系统亮/暗设置。</summary>
        private static string ResolveSystemKey()
        {
            if (_followSystemHighContrast && SystemParameters.HighContrast) return "highcontrast";
            return QuerySystemIsDark() ? "dark" : "light";
        }

        private static void EnableSystemFollow()
        {
            if (_followingSystem) return;
            _followingSystem = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private static void DisableSystemFollow()
        {
            if (!_followingSystem) return;
            _followingSystem = false;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        /// <summary>显式停止系统跟随（如应用退出时）。</summary>
        public static void ReleaseSystemFollow() => DisableSystemFollow();

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General &&
                e.Category != UserPreferenceCategory.Color &&
                e.Category != UserPreferenceCategory.Accessibility) return;
            if (_requestedMode != UI4ThemeMode.System) return;

            var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => ReResolveSystemTheme()));
            }
            else
            {
                ReResolveSystemTheme();
            }
        }

        private static void ReResolveSystemTheme()
        {
            if (_requestedMode != UI4ThemeMode.System) return;
            string key = ResolveSystemKey();
            ApplyResolved(key, ModeForKey(key));
        }

        // ────────────────────────────────────────────────────────
        //  资源桥：令牌 → 共享资源字典 → Application.Resources
        //  ────────────────────────────────────────────────────────

        /// <summary>
        /// 每个主题键共享一份资源字典。全局（挂进 <see cref="Application.Resources"/> 的 MergedDictionaries）
        /// 与所有同键的 <see cref="UI4ThemeScope"/> 挂的是<b>同一个实例</b>，因此 <see cref="SetAccent"/>
        /// 只需原地改一次，全局与各作用域同时跟随，无需逐字典同步。
        /// </summary>
        private static readonly Dictionary<string, ResourceDictionary> _sharedResources =
            new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase);

        /// <summary>取（并缓存）主题键对应的共享资源字典。</summary>
        internal static ResourceDictionary SharedResourcesFor(string key)
        {
            ResourceDictionary dict;
            if (_sharedResources.TryGetValue(key, out dict)) return dict;
            dict = new ResourceDictionary();
            WriteTokens(dict, InstanceOf(key));
            _sharedResources[key] = dict;
            // 新建的这一份会作废旧实例：把仍挂着旧字典的作用域就地换过来，否则它们会一直用旧主题
            UI4ThemeScope.ReplaceSharedDictionary(key, dict);
            return dict;
        }

        /// <summary>
        /// 已经由本类挂进 <see cref="Application.Resources"/> 的字典实例账本。
        /// 摘除只认这份账本，不认 <see cref="_sharedResources"/>——<see cref="Register"/> 会清那张缓存，
        /// 靠它去找旧实例就会把已经挂上的字典永远留在树里。
        /// </summary>
        private static readonly List<ResourceDictionary> _installedResources = new List<ResourceDictionary>();

        /// <summary>把当前主题令牌写入 <see cref="Application.Resources"/>（幂等）。宿主可用 <c>{DynamicResource UI4.Brush.Surface}</c> 消费。</summary>
        public static void ApplyToApplication() => WriteToApplicationResources();

        private static void WriteToApplicationResources()
        {
            Application app = Application.Current;
            if (app == null) return;

            ResourceDictionary dict = SharedResourcesFor(_resolvedKey);
            System.Collections.ObjectModel.Collection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
            if (merged.Contains(dict)) return;

            // 原位替换上一次挂上的那一份：先 Remove 再 Add 会留一个引用解析不到的空窗，
            // 期间控件回落到属性的硬编码默认色（实测 dark→highcontrast 会短暂读到 #0078D4）。
            // 账本里若还有别的份数（Register 覆盖当前键留下的），一并摘掉。
            int slot = -1;
            for (int i = _installedResources.Count - 1; i >= 0; i--)
            {
                int index = merged.IndexOf(_installedResources[i]);
                if (index < 0)
                {
                    // 宿主自己把它摘了：账本跟着忘掉，不替宿主补挂
                    _installedResources.RemoveAt(i);
                    continue;
                }
                if (slot < 0)
                {
                    merged[index] = dict;
                    slot = index;
                }
                else merged.RemoveAt(index);
                _installedResources.RemoveAt(i);
            }
            if (slot < 0) merged.Add(dict);
            _installedResources.Add(dict);
        }

        /// <summary>
        /// 把一个主题实例的全部令牌写入资源表（<c>UI4.Color.X</c> / <c>UI4.Brush.X</c> + 三个别名）。
        /// <see cref="Application.Resources"/> 与 <see cref="UI4ThemeScope"/> 的作用域字典共用这份键集合，避免两处漂移。
        /// </summary>
        internal static void WriteTokens(IDictionary res, UI4Theme theme)
        {
            foreach (UI4ThemeToken token in Enum.GetValues(typeof(UI4ThemeToken)))
            {
                Color c = theme.ColorOf(token);
                res["UI4.Color." + token] = c;
                res["UI4.Brush." + token] = CreateFrozen(c);
            }
            // 常用别名，方便宿主书写
            res["UI4.Brush.Text"] = CreateFrozen(theme.ColorOf(UI4ThemeToken.TextForeground));
            res["UI4.Brush.Border"] = CreateFrozen(theme.ColorOf(UI4ThemeToken.BorderNormal));
            res["UI4.Brush.Accent"] = CreateFrozen(theme.ColorOf(UI4ThemeToken.Accent));
        }

        // ────────────────────────────────────────────────────────
        //  持久化（默认关闭）
        // ────────────────────────────────────────────────────────

        /// <summary>主题持久化实现；为 null 时不持久化（默认）。</summary>
        public static IThemePersistence Persistence { get; set; }

        /// <summary><see cref="Save"/> 后触发。</summary>
        public static event EventHandler<UI4ThemeMode> ThemeSaved;
        /// <summary><see cref="ApplyPersisted"/> 读取到已保存模式、应用前触发。</summary>
        public static event EventHandler<UI4ThemeMode> ThemeLoading;

        /// <summary>把当前请求模式写入 <see cref="Persistence"/>（未配置持久化时仅触发事件）。</summary>
        public static void Save()
        {
            if (Persistence != null) Persistence.Save(_requestedMode);
            ThemeSaved?.Invoke(null, _requestedMode);
        }

        /// <summary>从 <see cref="Persistence"/> 读取并应用；无配置或无已存值时返回 false。</summary>
        public static bool ApplyPersisted()
        {
            if (Persistence == null) return false;
            UI4ThemeMode? mode = Persistence.Load();
            if (!mode.HasValue) return false;
            ThemeLoading?.Invoke(null, mode.Value);
            SetTheme(mode.Value);
            return true;
        }

        // ────────────────────────────────────────────────────────
        //  实例：令牌数据 + 冻结画刷
        // ────────────────────────────────────────────────────────

        private readonly Dictionary<UI4ThemeToken, Color> _colors = new Dictionary<UI4ThemeToken, Color>();
        private UI4ThemeDefinition _def;

        /// <summary>取令牌颜色。</summary>
        public Color ColorOf(UI4ThemeToken token) => _colors[token];

        /// <summary>取令牌对应的冻结画刷。</summary>
        public SolidColorBrush BrushOf(UI4ThemeToken token) => CreateFrozen(_colors[token]);

        internal static UI4Theme FromDefinition(UI4ThemeDefinition def)
        {
            var t = new UI4Theme();
            t._def = def;
            foreach (UI4ThemeToken token in Enum.GetValues(typeof(UI4ThemeToken)))
                t._colors[token] = def.GetColor(token);
            return t;
        }

        /// <summary>
        /// 取（并缓存）主题键对应的实例；未注册键返回 null。
        /// 全局主题与所有同键作用域共用同一实例，因此 <see cref="SetAccent"/> 之类的改动天然对两侧同时生效。
        /// </summary>
        internal static UI4Theme InstanceOf(string key)
        {
            if (key == null) return null;
            UI4Theme theme;
            if (_instances.TryGetValue(key, out theme)) return theme;
            UI4ThemeDefinition def;
            if (!_definitions.TryGetValue(key, out def)) return null;
            theme = FromDefinition(def);
            _instances[key] = theme;
            return theme;
        }

        /// <summary>
        /// 解析某个元素在 <see cref="UI4ThemeScope"/> 下应当使用的主题实例；
        /// 祖先上没有作用域时返回 null（表示「跟随全局」）。
        /// </summary>
        internal static UI4Theme EffectiveThemeFor(DependencyObject element)
        {
            string key = UI4ThemeScope.ResolveKey(element);
            if (key == null) return null;
            return InstanceOf(key);
        }

        /// <summary>
        /// 覆盖强调色：自动派生 <see cref="UI4ThemeToken.AccentDark"/>（约 85% 亮度）。
        /// 触发一次主题刷新与资源字典更新；同时写回当前主题的 <see cref="UI4ThemeDefinition"/>，
        /// 使显式声明该键的 <see cref="UI4ThemeScope"/> 也继承覆盖。
        /// </summary>
        public static void SetAccent(Color accent)
        {
            var t = Current;
            Color accentDark = Darken(accent, 0.85f);
            t._colors[UI4ThemeToken.Accent] = accent;
            t._colors[UI4ThemeToken.AccentDark] = accentDark;

            UI4ThemeDefinition def;
            if (_definitions.TryGetValue(_resolvedKey, out def))
                def.With(UI4ThemeToken.Accent, accent).With(UI4ThemeToken.AccentDark, accentDark);

            // 共享字典原地重写：全局与所有同键作用域同时跟随，无需逐字典同步
            WriteTokens(SharedResourcesFor(_resolvedKey), t);

            NotifyThemeChanged();
            WriteToApplicationResources();
        }

        /// <summary>
        /// 主题代号自增并广播 <see cref="ThemeChanged"/>。
        /// 控件本身不再经由本事件刷新（颜色走资源引用），仅剩真正命令式的消费者订阅：
        /// DWM 标题栏染色与 AvalonEdit 语法高亮切换。
        /// </summary>
        private static void NotifyThemeChanged()
        {
            _themeVersion++;
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input,
                    new Action(delegate { ThemeChanged?.Invoke(null, EventArgs.Empty); }));
            }
            else
            {
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        private static SolidColorBrush CreateFrozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));
        }
    }
}
