using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;
using StartUI4Controls;
using __APPNAME__.Helpers;
using __APPNAME__.Services;

namespace __APPNAME__.ViewModels
{
    /// <summary>配色档在面板里的一个按钮项。</summary>
    public class ThemeOption
    {
        public ThemeOption(string key, string label) { Key = key; Label = label; }
        public string Key { get; private set; }
        public string Label { get; private set; }
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
