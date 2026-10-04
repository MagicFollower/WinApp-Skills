using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using PromptFavorites.Services;
using StartUI4Controls;

namespace PromptFavorites.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IPromptService _service;
        private readonly SettingsService _settings;
        private bool _suppressAutoSelect;

        public ModuleListViewModel Modules { get; private set; }
        public EntryListViewModel Entries { get; private set; }
        public EntryDetailViewModel Detail { get; private set; }

        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (SetProperty(ref _searchText, value))
                    OnSearchTextChanged(value);
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        // ── 设置面板：配色档 / 字体 / 全局缩放 ──────────────────────────
        //
        // 三个可调项的真源都是 SettingsService（落盘那份），这里只是它的可绑定视图：
        // setter 写设置 + 触发即时生效，默认值与区间只从 Typography 取，面板里不重抄常量。

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get { return _isSettingsOpen; }
            set
            {
                // 只在关闭时落盘：滑杆每动一格就写一次盘会把手动改的其它键一起冲掉，也是无谓的磁盘 IO。
                if (SetProperty(ref _isSettingsOpen, value) && !value)
                    _settings.Save();
            }
        }

        /// <summary>配色档。库的字典由 <see cref="HostPalette"/> 整体重写，档位只存请求值（两档，不跟随系统）。</summary>
        public AppThemeMode ThemeMode
        {
            get { return _settings.ThemeMode; }
            set
            {
                if (_settings.ThemeMode == value) return;
                _settings.ThemeMode = value;
                HostPalette.Apply(value);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ThemeModeSwitchLabel));
            }
        }

        /// <summary>切换按钮上的文案写的是<b>目标档</b>，当前档由 <see cref="ThemeMode"/> 单源决定。</summary>
        public string ThemeModeSwitchLabel
        {
            get
            {
                return _settings.ThemeMode == AppThemeMode.Dark
                    ? "切换到浅色（终端靛）"
                    : "切换到夜景（终端靛·夜）";
            }
        }

        public void ToggleThemeMode()
        {
            ThemeMode = _settings.ThemeMode == AppThemeMode.Dark
                ? AppThemeMode.Light
                : AppThemeMode.Dark;
        }

        /// <summary>
        /// 下拉里的字体族名，<b>第 0 项固定是出厂字体栈</b>。两个理由：① WPF 的 ComboBox 挂了 ItemsSource 之后，
        /// 把 SelectedItem 设成一个不在列表里的值会被静默清空，表现就是"恢复默认之后下拉框没跟着变"；
        /// ② 列表用字符串而不是 <c>FontFamily</c>，相等判定是精确的，不依赖它对方括号复合族的 Equals 口径。
        /// 一次性枚举，不每次打开面板再查系统。
        /// </summary>
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

        /// <summary>窗口内容的 LayoutTransform 与窗口下限都只读这一个系数。</summary>
        public double ZoomFactor { get { return _zoomPercent / 100.0; } }

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
        }

        public void ResetZoom()
        {
            ZoomPercent = Typography.DefaultZoomPercent;
        }

        // ── 只读应用信息 ────────────────────────────────────────────────

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

        public string DataRootPath { get { return App.RootPath; } }

        public string SettingsFolder { get { return SettingsService.SettingsDirectory; } }

        public event Action<string> ToastRequested;

        private void ShowToast(string message)
        {
            StatusMessage = message;
            var handler = ToastRequested;
            if (handler != null) handler(message);
        }

        public Window MainWindow { get; set; }

        public MainViewModel(IPromptService service, SettingsService settings)
        {
            _service = service;
            _settings = settings;

            Modules = new ModuleListViewModel(service, settings);
            Entries = new EntryListViewModel(service, settings);
            Detail = new EntryDetailViewModel(service, settings);

            Modules.SelectedModuleChanged += OnModuleSelected;
            Entries.SelectedEntryChanged += OnEntrySelected;
            Entries.RequestQuickCopy += OnQuickCopy;
            Detail.SaveRequested += OnSaveRequested;
            Detail.CopyRequested += OnCopyRequested;
            Detail.ConfirmDiscardChanges += OnConfirmDiscard;

            // 收藏有两个入口（中栏星标、右栏按钮），各自持有不同的 PromptItem 实例，
            // 这里按文件路径双向同步，否则写盘成功但界面上的星标与筛选都不更新。
            Entries.FavoriteToggled += (path, value) => Detail.SyncFavoriteFromList(path, value);
            Detail.FavoriteChanged += (path, value) => Entries.ApplyFavorite(path, value);

            _suppressAutoSelect = true;
            try
            {
                Modules.CurrentSort = settings.ModuleSortMode;
                Modules.LoadModules();

                if (!string.IsNullOrEmpty(settings.LastModule))
                    Modules.SelectModule(settings.LastModule);
                else if (Modules.Modules.Count > 0)
                    Modules.SelectedModule = Modules.Modules[0];
            }
            finally
            {
                _suppressAutoSelect = false;
            }

            Entries.IsFavoriteFilter = settings.FavoriteFilter;
            Entries.CurrentSort = settings.SortMode;

            AvailableFonts = Typography.BuildFamilyChoices(
                Fonts.SystemFontFamilies.Select(f => f.Source).ToList());

            // 显示值走一遍 clamp：设置文件是纯文本，可能被手改成 7 或 500，
            // 滑杆拿到越界值会把刻度画歪，而生效值那边（App.ApplyDisplaySettings）本来就是 clamp 后再发的。
            _baseFontSize = Typography.ClampBase(settings.BaseFontSize);
            _zoomPercent = Typography.ClampZoom(settings.ZoomPercent);
            _selectedFontFamily = ResolveFamilyEntry(settings.FontFamilyName);
        }

        /// <summary>
        /// 存的字体名可能已被系统卸载或被手改，那就不在列表里——回落到出厂项，
        /// 否则 ComboBox 会因为取不到匹配项把选中标签清空（看着像"字体没设置"）。
        /// 生效值那边 <c>Typography.SafeFamily</c> 已经有同样的回落，两处口径一致。
        /// </summary>
        private string ResolveFamilyEntry(string familySource)
        {
            if (string.IsNullOrEmpty(familySource)) return Typography.DefaultFontFamilySource;
            return AvailableFonts.Contains(familySource)
                ? familySource : Typography.DefaultFontFamilySource;
        }

        private void OnModuleSelected(PromptModule module)
        {
            if (module == null) return;
            if (!Detail.CheckDiscardChanges()) return;

            // 启动恢复上次模块时不自动选中条目（中栏不该出现选中标记）；
            // 用户手工切换模块仍然自动选中第一条。
            Entries.LoadEntries(module.Name, !_suppressAutoSelect);
            _settings.LastModule = module.Name;
        }

        private void OnEntrySelected(PromptItem entry)
        {
            if (entry == null)
            {
                if (Detail.HasChanges) return;
                Detail.LoadEntry(null);
                return;
            }

            // 收藏等操作会重建列表并把选中项还原到同一条目：既不重新读盘（否则编辑器里未保存的改动会被冲掉），
            // 也不该弹"有未保存更改"的确认框
            if (Detail.CurrentItem != null && Detail.CurrentItem.FilePath == entry.FilePath) return;

            if (!Detail.CheckDiscardChanges()) return;

            var detail = _service.LoadEntryDetail(entry.FilePath);
            Detail.LoadEntry(detail);
        }

        private void OnSearchTextChanged(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Entries.RestoreModuleView();
                if (!string.IsNullOrEmpty(_settings.LastModule))
                    Modules.SelectModule(_settings.LastModule);
            }
            else
            {
                var results = _service.Search(text);
                Entries.ShowSearchResults(results);
                Modules.SelectedModule = null;
            }
        }

        private void OnQuickCopy(PromptItem item)
        {
            try
            {
                if (item == null) return;

                if (Detail.HasChanges)
                {
                    UI4MessageBox.Show(
                        "\u6709\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF0C\u8BF7\u5148\u4FDD\u5B58\u518D\u590D\u5236\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                DoCopy(item);
            }
            catch (Exception ex)
            {
                ShowError("\u590D\u5236\u5931\u8D25", ex);
            }
        }

        private void OnCopyRequested()
        {
            try
            {
                if (Detail.CurrentItem == null) return;

                if (Detail.HasChanges)
                {
                    UI4MessageBox.Show(
                        "\u6709\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF0C\u8BF7\u5148\u4FDD\u5B58\u518D\u590D\u5236\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                if (!string.IsNullOrEmpty(Detail.BodyText))
                {
                    CopyToClipboard(Detail.BodyText, Detail.CurrentItem);
                    return;
                }

                FinishCopy(Detail.CurrentItem);
            }
            catch (Exception ex)
            {
                ShowError("\u590D\u5236\u5931\u8D25", ex);
            }
        }

        private void DoCopy(PromptItem item)
        {
            var detail = _service.LoadEntryDetail(item.FilePath);
            if (!string.IsNullOrEmpty(detail.Body))
            {
                CopyToClipboard(detail.Body, item);
                return;
            }

            FinishCopy(item);
        }

        private void CopyToClipboard(string text, PromptItem item)
        {
            UI4Clipboard.TrySetTextAsync(text, delegate(bool ok)
            {
                if (!ok)
                {
                    UI4MessageBox.Show(
                        "\u65E0\u6CD5\u5199\u5165\u526A\u8D34\u677F\uFF0C\u8BF7\u5173\u95ED\u5360\u7528\u526A\u8D34\u677F\u7684\u7A0B\u5E8F\u540E\u91CD\u8BD5\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                FinishCopy(item);
            });
        }

        private void FinishCopy(PromptItem item)
        {
            _service.RecordCopy(item);
            Entries.RefreshCurrentModule();
            ShowToast("\u5DF2\u590D\u5236");
        }

        private static void ShowError(string title, Exception ex)
        {
            UI4MessageBox.Show(ex.Message, title, UI4MessageBoxButtons.OK, 420);
        }

        private void OnSaveRequested()
        {
            Entries.RefreshCurrentModule();
            Modules.RefreshModuleCounts();
            ShowToast("\u5DF2\u4FDD\u5B58");
        }

        private bool OnConfirmDiscard()
        {
            var result = UI4MessageBox.Show(
                "\u662F\u5426\u653E\u5F03\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF1F",
                "\u672A\u4FDD\u5B58",
                UI4MessageBoxButtons.OKCancel,
                360);
            return result == true;
        }

        /// <summary>把窗口几何与会话状态采集进设置对象（不写盘）。最大化时不采集几何，避免把最大化尺寸当还原尺寸存。</summary>
        public void CaptureWindowState(Window window)
        {
            if (window == null) return;

            if (window.WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = window.ActualWidth;
                _settings.WindowHeight = window.ActualHeight;
                _settings.WindowLeft = window.Left;
                _settings.WindowTop = window.Top;
            }

            _settings.WindowState = window.WindowState;
            _settings.LastModule = Modules.SelectedModule != null
                ? Modules.SelectedModule.Name : "";
            _settings.SortMode = Entries.CurrentSort;
            _settings.ModuleSortMode = Modules.CurrentSort;
            _settings.FavoriteFilter = Entries.IsFavoriteFilter;
        }

        public void SaveWindowState(Window window)
        {
            CaptureWindowState(window);
            _settings.Save();
        }
    }
}
