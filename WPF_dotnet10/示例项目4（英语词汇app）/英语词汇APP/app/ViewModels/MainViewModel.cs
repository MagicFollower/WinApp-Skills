using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;
using VocabDesk.Helpers;
using VocabDesk.Models;
using VocabDesk.Services;

namespace VocabDesk.ViewModels
{
    /// <summary>配色档在面板里的一个按钮项。</summary>
    public class ThemeOption
    {
        public ThemeOption(string key, string label) { Key = key; Label = label; }
        public string Key { get; private set; }
        public string Label { get; private set; }
    }

    /// <summary>
    /// 检索模式在下拉框里的一项。展示的是中文，模式本身是 SearchMode 枚举，
    /// 二者不要互相顶替（下拉框顺序变了不影响语义）。
    /// </summary>
    public class SearchModeOption
    {
        public SearchModeOption(SearchMode mode, string label, string hint)
        {
            Mode = mode; Label = label; Hint = hint;
        }
        public SearchMode Mode { get; private set; }
        public string Label { get; private set; }
        public string Hint { get; private set; }
        public override string ToString() { return Label; }
    }

    /// <summary>
    /// 左栏导航项：全部 + 每本书一项。必须派生自 UI4NavigationViewItem——控件的 OnItemsChanged
    /// 只认这两种类型才会把项送进左栏，直接往 RegularItems 加不会显示。
    /// ViewIndex 是 Corpus.Views 的下标（0=全部），切换视图只是换个数组引用。
    /// </summary>
    public sealed class NavRailItem : UI4NavigationViewItem
    {
        private readonly string _label;
        private int _wordCount;

        public NavRailItem(int viewIndex, string label, int wordCount, string glyph)
        {
            ViewIndex = viewIndex;
            _label = label;
            _wordCount = wordCount;
            Header = ComposeHeader();
            TextIcon = glyph;
            // 图标走独立的图标字体，不吃用户选的正文字体族——换字体不会把图标变成豆腐块
            TextIconFontFamily = new FontFamily("Segoe MDL2 Assets");
        }

        public int ViewIndex { get; private set; }
        /// <summary>光秃秃的书名（Header 里还带着收录数，页脚那行不要重复它）。</summary>
        public string Label { get { return _label; } }

        /// <summary>语料建好后就地补收录数：不重建项，免得左栏闪一下、选中也被清掉。</summary>
        public void SetWordCount(int wordCount)
        {
            if (_wordCount == wordCount) return;
            _wordCount = wordCount;
            Header = ComposeHeader();
        }

        private string ComposeHeader()
        {
            return _wordCount > 0
                ? _label + "  " + _wordCount.ToString("N0", CultureInfo.InvariantCulture)
                : _label;
        }
    }

    /// <summary>
    /// 主窗口的可绑定视图：设置项的真源是 SettingsService（落盘那份），这里只负责
    /// "写设置 + 立刻生效 + 通知界面"。默认值与区间只从 Typography 取，面板里不重抄常量。
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;

        public MainViewModel(SettingsService settings)
        {
            _settings = settings ?? new SettingsService();

            ThemeChoices = ThemeService.AvailableKeys()
                .Select(key => new ThemeOption(key, ThemeService.DisplayLabel(key)))
                .ToList();
            SelectThemeCommand = new RelayCommand<string>(SelectTheme);

            ModeChoices = new List<SearchModeOption>
            {
                new SearchModeOption(SearchMode.Prefix,   "词头前缀",  "ab → able, about"),
                new SearchModeOption(SearchMode.Contains, "词头包含",  "lect → select, collect"),
                new SearchModeOption(SearchMode.Suffix,   "词头后缀",  "tion → action, nation（构词法）"),
                new SearchModeOption(SearchMode.Phrase,   "短语词项",  "payment → 含该词的短语"),
                new SearchModeOption(SearchMode.Gloss,    "中文释义",  "能力 → 释义里出现「能力」的词"),
                new SearchModeOption(SearchMode.Fuzzy,    "近似拼写",  "abilty → ability（编辑距离 1）"),
            };

            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
            _searchDebounce.Tick += SearchDebounce_Tick;

            // 一次性枚举系统字体族：每次打开面板再查会卡 UI，而且候选表要"出厂项固定在第 0 位"是结构性契约
            AvailableFonts = Typography.BuildFamilyChoices(
                Fonts.SystemFontFamilies.Select(f => f.Source).ToList());

            // 显示值走一遍 clamp：设置文件是纯文本，可能被手改成 7 或 500，滑杆拿到越界值会把刻度画歪，
            // 而生效值那边（App.ApplyDisplaySettings）本来就是 clamp 后再发的
            _themeKey = NormalizeThemeKey(_settings.ThemeKey);
            _baseFontSize = Typography.ClampBase(_settings.BaseFontSize);
            _zoomPercent = Typography.ClampZoom(_settings.ZoomPercent);
            _selectedFontFamily = ResolveFamilyEntry(_settings.FontFamilyName);
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get { return _isSettingsOpen; }
            set
            {
                // 只在关闭时落盘：滑杆每动一格就写一次盘会把手动改的其它键一起冲掉，也是无谓的磁盘 IO
                if (SetProperty(ref _isSettingsOpen, value) && !value) _settings.Save();
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        // ── 语料、导航视图与检索 ───────────────────────────────────────
        //
        // 语料一次建好之后只读（构建在后台线程，实测 1.30 s），界面侧全是引用赋值：
        // 切书 = 换 Views 下标（实测 6.7–20.9 ms），检索 = 换个结果数组（实测最宽一次 8.6 ms 到屏）。
        // 不做增量 Add——那会让虚拟化列表每加一项重排一次，也是"切书变慢"的那条老路。

        /// <summary>输入停顿这么久才发一次检索。逐字检索在 14,625 词头上没必要，实测一次 0.006–4.3 ms。</summary>
        private const int DebounceMs = 150;

        // 左栏图标：「全部」用一排书，各本词书用单本书，码位取自 Segoe MDL2 Assets（源码里只写转义）。
        // 这两枚是在本机装好的字体里真渲染出来核对过的：同区段里 E8A5 之类看着像空框、E850 像电量条，
        // cmap 里有字形不等于肉眼认得出，所以别照码位表随手挑。图标字体固定，不吃用户选的正文字体族。
        private const string GlyphAll = "\uE8F1";
        private const string GlyphBook = "\uE82D";

        private readonly DispatcherTimer _searchDebounce;

        private Corpus _corpus;
        private int _viewIndex;
        private string _viewLabel = "全部";
        private string _searchText = string.Empty;
        private SearchModeOption _selectedMode;

        public IReadOnlyList<SearchModeOption> ModeChoices { get; private set; }

        public IReadOnlyList<NavRailItem> NavItems { get; private set; }

        private IReadOnlyList<WordCard> _results;
        /// <summary>右侧卡片列表的唯一数据源。虚拟化 + 项滚动只吃这一个引用。</summary>
        public IReadOnlyList<WordCard> Results
        {
            get { return _results; }
            private set
            {
                if (!SetProperty(ref _results, value)) return;
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }

        /// <summary>空结果要在界面上说清楚，不能只留一片白（实测 0 命中时右侧整块是空的，看着像坏了）。</summary>
        public bool IsEmpty
        {
            get { return _corpus != null && (_results == null || _results.Count == 0); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetProperty(ref _isBusy, value); }
        }

        private string _busyStage;
        public string BusyStage
        {
            get { return _busyStage; }
            private set { SetProperty(ref _busyStage, value); }
        }

        private double _busyFraction;
        /// <summary>0..1，进度条的 Maximum 就是 1；阶段名比数字更有用，所以两个都发。</summary>
        public double BusyFraction
        {
            get { return _busyFraction; }
            private set { SetProperty(ref _busyFraction, value); }
        }

        /// <summary>后台构建进度回报。只可在 UI 线程上调——Corpus 在工作线程发这些回调，
        /// 由窗口那侧统一过 Dispatcher（窗口才持有 Dispatcher 与关闭标志，跨线程兜底该在它那儿做）。</summary>
        public void ReportProgress(LoadProgress progress)
        {
            if (progress == null) return;
            BusyStage = progress.Stage + "…";
            BusyFraction = Math.Max(0, Math.Min(1, progress.Fraction));
        }

        public void BeginLoad()
        {
            IsBusy = true;
            BusyFraction = 0;
            BusyStage = "正在准备词书…";
            StatusMessage = "正在准备词书…";
        }

        /// <summary>构建失败也要有可见的下场：遮罩收起、页脚点名原因，界面照常能用（空结果）。</summary>
        public void LoadFailed(Exception ex)
        {
            IsBusy = false;
            BusyFraction = 0;
            StatusMessage = "词库没能加载：" + (ex != null ? ex.Message : "未知原因");
        }

        /// <summary>
        /// 启动时先把左栏摆出来（只有书名，收录数还不知道）。窗口负责把这些项塞进控件，
        /// 并且之后不再重建——Attach 只就地补数字，避免加载完左栏闪一下、选中也被清掉。
        /// </summary>
        public void BuildRailPreview(IReadOnlyList<BookInfo> books)
        {
            if (NavItems != null) return;
            var items = new List<NavRailItem> { new NavRailItem(0, "全部", 0, GlyphAll) };
            foreach (var book in books)
                items.Add(new NavRailItem(book.Index + 1, book.Label, 0, GlyphBook));
            NavItems = items;
        }

        /// <summary>语料就绪：把收录数补进已有的导航项，并把结果指向「全部」视图。</summary>
        public void Attach(Corpus corpus)
        {
            _corpus = corpus;
            _viewIndex = 0;
            _viewLabel = "全部";

            BuildRailPreview(corpus.Books);
            for (int i = 0; i < corpus.Books.Count; i++)
                NavItems[i + 1].SetWordCount(corpus.Books[i].WordCount);
            NavItems[0].SetWordCount(corpus.WordCount);

            IsBusy = false;
            BusyFraction = 1;
            Results = corpus.Views[0];
            StatusMessage = "全部 · " + corpus.WordCount.ToString("N0") + " 个词头 · "
                            + corpus.Books.Count + " 本词书";
            RaiseSelectionChanged();
        }

        /// <summary>
        /// 左栏换了书：记下视图下标与显示名，再重发一次检索（空查询时就是换视图本身）。
        /// 这里刻意不做成绑定属性——见 MainWindow 里那条 AddValueChanged 的注释。
        /// </summary>
        public void ApplyView(NavRailItem item)
        {
            if (item == null) return;
            if (_viewIndex == item.ViewIndex && Results != null) return;
            _viewIndex = item.ViewIndex;
            _viewLabel = item.Label;
            // 展开态不另存：它跟着列表选中走，而选中是"单项"的，所以同时展开数恒 ≤1
            // （实测 N=40 那一挡是 1,334 ms 的悬崖，绝不能再走回去）
            RunSearch();
        }

        public SearchModeOption SelectedMode
        {
            get { return _selectedMode; }
            set
            {
                if (SetProperty(ref _selectedMode, value) && _selectedMode != null) RunSearch();
            }
        }

        /// <summary>检索框的每次按键都走这里：停顿 DebounceMs 后发一次，避免逐字全扫。</summary>
        public void RequestSearch(string text)
        {
            string next = text ?? string.Empty;
            if (string.Equals(_searchText, next, StringComparison.Ordinal) && Results != null) return;
            _searchText = next;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void SearchDebounce_Tick(object sender, EventArgs e)
        {
            _searchDebounce.Stop();
            RunSearch();
        }

        /// <summary>真正发检索的地方：换结果数组 + 报命中条数。耗时是调试量，不进界面。</summary>
        public void RunSearch()
        {
            if (_corpus == null) return;

            WordCard[] found = _corpus.Search(
                _selectedMode != null ? _selectedMode.Mode : SearchMode.Prefix,
                _searchText, _viewIndex);

            Results = found;
            StatusMessage = _viewLabel + " · 命中 " + found.Length.ToString("N0") + " 条";
            RaiseSelectionChanged();
        }

        // ── 选中词：页脚右下那行读的是这个 ──────────────────────────────

        private WordCard _selectedCard;

        /// <summary>窗口在列表选中变化时把选中项交进来（手风琴的展开态就是这一个值）。</summary>
        public void SetSelectedCard(WordCard card)
        {
            if (ReferenceEquals(_selectedCard, card)) return;
            _selectedCard = card;
            RaiseSelectionChanged();
        }

        /// <summary>没选中时给一句操作引导，而不是留白——展开/收起这个动作不点一次是看不见的。</summary>
        public string SelectionSummary
        {
            get
            {
                return _selectedCard != null
                    ? _selectedCard.InclusionText
                    : "点卡片展开释义，再点一次收起";
            }
        }

        private void RaiseSelectionChanged()
        {
            RaisePropertyChanged(nameof(SelectionSummary));
        }

        // ── 配色档 ─────────────────────────────────────────────────────
        // 档位只存请求值（light/dark/system/套装键），解析结果由库给；切换入口只有面板里这一处，
        // 窗口上再放一个同义按钮就会出现"两份状态观感"。

        private string _themeKey;
        public string ThemeKey
        {
            get { return _themeKey; }
            set
            {
                string key = (value ?? string.Empty).Trim();
                if (string.Equals(_themeKey, key, StringComparison.OrdinalIgnoreCase)) return;
                // 先看策略挡不挡（Theme.Policy），再让库去应用：Apply 对套装键失败时只返回 false 不抛异常，
                // 界面纹丝不动，所以这里必须给一句看得见的解释
                if (!ThemeService.IsAllowed(key))
                {
                    StatusMessage = "当前明暗策略（Theme.Policy = " + Theme.Policy + "）不允许「"
                                    + ThemeService.DisplayLabel(key) + "」，未切换";
                    return;
                }
                if (!ThemeService.Apply(key))
                {
                    StatusMessage = "库没接受这一档（套装未注册或键名不认识）：" + key;
                    return;
                }
                _themeKey = key;
                _settings.ThemeKey = key;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(AppliedThemeText));
            }
        }

        public IReadOnlyList<ThemeOption> ThemeChoices { get; private set; }
        public ICommand SelectThemeCommand { get; private set; }

        private void SelectTheme(string key) { ThemeKey = key; }

        /// <summary>面板与页脚都读这一句：请求的档 + 库实际解析成的档。</summary>
        public string AppliedThemeText
        {
            get { return ThemeService.DisplayLabel(_themeKey) + " → 生效键 " + ThemeService.ResolvedKey(_themeKey); }
        }

        /// <summary>设置里存的档若在当前策略下不允许（换了策略、或手改过文件），落到允许列表的第一档。</summary>
        private static string NormalizeThemeKey(string key)
        {
            string trimmed = (key ?? string.Empty).Trim();
            if (trimmed.Length > 0 && ThemeService.IsAllowed(trimmed)) return trimmed;
            var allowed = ThemeService.AvailableKeys();
            return allowed.Count > 0 ? allowed[0] : ThemeService.Light;
        }

        // ── 字体族与字号 ───────────────────────────────────────────────

        public IReadOnlyList<string> AvailableFonts { get; private set; }

        private string _selectedFontFamily;
        public string SelectedFontFamily
        {
            get { return _selectedFontFamily; }
            set
            {
                if (SetProperty(ref _selectedFontFamily, value))
                {
                    // 选中出厂项就存空串：让"没设置过"和"设成出厂值"在设置文件里是同一个事实
                    _settings.FontFamilyName =
                        string.Equals(value, Typography.DefaultFontFamilySource, StringComparison.Ordinal)
                            ? string.Empty : (value ?? string.Empty);
                    App.ApplyDisplaySettings();
                }
            }
        }

        private double _baseFontSize;
        public double BaseFontSize
        {
            get { return _baseFontSize; }
            set
            {
                if (SetProperty(ref _baseFontSize, Typography.ClampBase(value)))
                {
                    _settings.BaseFontSize = _baseFontSize;
                    App.ApplyDisplaySettings();
                }
            }
        }

        /// <summary>
        /// 存的字体名可能已被系统卸载或被手改，那就不在列表里——回落到出厂项，
        /// 否则 ComboBox 会因为取不到匹配项把选中标签清空（看着像"字体没设置"）。
        /// 生效值那边 Typography.SafeFamily 已经有同样的回落，两处口径一致。
        /// </summary>
        private string ResolveFamilyEntry(string familySource)
        {
            if (string.IsNullOrEmpty(familySource)) return Typography.DefaultFontFamilySource;
            return AvailableFonts.Contains(familySource)
                ? familySource : Typography.DefaultFontFamilySource;
        }

        public void ResetTypography()
        {
            _settings.FontFamilyName = string.Empty;
            _settings.BaseFontSize = Typography.DefaultBaseSize;

            // 出厂项就在列表第 0 位，所以赋值后下拉框会显示它（设成列表外的值会被 ComboBox 清空）
            _selectedFontFamily = Typography.DefaultFontFamilySource;
            RaisePropertyChanged(nameof(SelectedFontFamily));

            _baseFontSize = Typography.DefaultBaseSize;
            RaisePropertyChanged(nameof(BaseFontSize));

            App.ApplyDisplaySettings();
            StatusMessage = "已恢复默认字体与字号";
        }

        // ── 全局缩放 ───────────────────────────────────────────────────

        private double _zoomPercent;
        public double ZoomPercent
        {
            get { return _zoomPercent; }
            set
            {
                if (SetProperty(ref _zoomPercent, Typography.ClampZoom(value)))
                {
                    _settings.ZoomPercent = _zoomPercent;
                    RaisePropertyChanged(nameof(ZoomFactor));
                }
            }
        }

        /// <summary>根内容的 LayoutTransform 与窗口下限都只读这一个系数。</summary>
        public double ZoomFactor { get { return _zoomPercent / 100.0; } }

        public void ResetZoom()
        {
            ZoomPercent = Typography.DefaultZoomPercent;
            StatusMessage = "已恢复 100% 缩放";
        }

        // ── 只读应用信息（排障时"用户报的到底是哪一版"就靠这几行）────────
        // 走窗口内文本展示，不弹模态框：模态框只留给错误报告。

        public string AppVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? v.ToString(3) : "未知";
            }
        }

        public string RuntimeVersion { get { return RuntimeInformation.FrameworkDescription; } }

        public string LibraryVersion
        {
            get
            {
                var v = typeof(UI4Theme).Assembly.GetName().Version;
                return v != null ? v.ToString(3) : "未知";
            }
        }

        public string SettingsFolder { get { return SettingsService.SettingsDirectory; } }

        public string PolicyText
        {
            get
            {
                string policy = Theme.Policy;
                if (string.IsNullOrEmpty(policy)) policy = "（未填）";
                string scope;
                if (string.Equals(policy, "both", StringComparison.OrdinalIgnoreCase)) scope = "亮 + 暗，允许跟随系统";
                else if (string.Equals(policy, "light-only", StringComparison.OrdinalIgnoreCase)) scope = "只做浅色档";
                else if (string.Equals(policy, "dark-only", StringComparison.OrdinalIgnoreCase)) scope = "只做深色档";
                else scope = "尚未决策（--selftest 会红）";
                return policy + "（" + scope + "）";
            }
        }
    }
}
