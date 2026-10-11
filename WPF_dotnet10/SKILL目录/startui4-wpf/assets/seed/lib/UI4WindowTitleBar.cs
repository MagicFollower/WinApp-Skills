using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StartUI4Controls
{
    /// <summary>
    /// 让系统绘制的窗口标题栏（非客户区，WPF 无法用 XAML 触及）跟随主题。
    /// DWM（Desktop Window Manager，Vista 起的桌面合成组件）负责绘制标题栏、边框与圆角，
    /// 程序只能经它开放的窗口属性表达意图：通过 dwmapi 的 <c>DWMWA_USE_IMMERSIVE_DARK_MODE</c> 切换深/浅标题栏，
    /// 在支持的系统（Windows 11 起）进一步把标题栏底色、文字与边框染成当前主题令牌色。
    /// </summary>
    /// <remarks>
    /// <para>四条生效通路，前三条宿主无需改一行代码：
    /// ① <see cref="UI4Theme.ThemeChanged"/> 后遍历 <see cref="System.Windows.Application.Windows"/> 重染所有已打开窗口；
    /// ② 任一 UI4 控件加载时补染它所属的窗口（覆盖「主题已是深色、之后才打开的窗口」，同一主题代号内只调一次 dwmapi）；
    /// ③ <see cref="UI4ThemeScope"/> 的根若是整个 <see cref="Window"/>，其变更与撤销按该作用域染色。</para>
    /// <para>④ 不含任何 UI4 控件的窗口可显式调用 <see cref="Apply"/>；需要豁免的窗口把 <see cref="EnabledProperty"/> 置 false。</para>
    /// <para>能力探测而非版本号判断：未经 manifest 声明的进程里 <c>Environment.OSVersion</c> 可能虚报为 6.3，
    /// 故此处以「先试 <c>DwmSetWindowAttribute</c>，失败即认定不支持」的方式探测并缓存结果。</para>
    /// </remarks>
    public static class UI4WindowTitleBar
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private const int DWMWA_COLOR_DEFAULT = 0x01000000;
        private const int SIZEOF_INT = 4;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>设为 false 可让某个窗口不再由主题染色标题栏（默认 true）。改动即时生效：撤销或立即应用。</summary>
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(UI4WindowTitleBar),
                new FrameworkPropertyMetadata(true, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

        private static bool _installed;
        private static int _darkModeAttribute;
        // 0 = 未探测，1 = 可自定义标题栏配色，2 = 系统不支持（只切换深/浅标志）
        private static int _captionColorSupport;
        private static readonly ConditionalWeakTable<Window, PaintedVersion> _paintedVersions =
            new ConditionalWeakTable<Window, PaintedVersion>();

        /// <summary>窗口上次成功染色的主题代号（ConditionalWeakTable 的值须为引用类型，故用可变盒子）。</summary>
        private sealed class PaintedVersion
        {
            public int Value;
        }

        /// <summary>系统是否支持把标题栏底色/文字/边框染成任意颜色（Windows 11 起）。</summary>
        public static bool SupportsCaptionColors => _captionColorSupport == 1;

        /// <summary>由 <see cref="UI4Theme"/> 在首次初始化时调用；重复调用无副作用。</summary>
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            UI4Theme.ThemeChanged += delegate { ApplyOpenWindows(); };
        }

        /// <summary>
        /// 控件加载通路：染它所属的窗口。同一 <see cref="UI4Theme.ThemeVersion"/> 内对同一窗口只调用一次 dwmapi。
        /// </summary>
        internal static void NotifyContentLoaded(FrameworkElement control)
        {
            if (!_installed || control == null) return;
            Window window = Window.GetWindow(control);
            if (window == null) return;
            int version = UI4Theme.ThemeVersion;
            PaintedVersion painted;
            if (_paintedVersions.TryGetValue(window, out painted) && painted.Value == version) return;
            Apply(window);
        }

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Window window = d as Window;
            if (window == null) return;
            IntPtr hwnd = HandleOf(window);
            if (hwnd == IntPtr.Zero) return;
            if ((bool)e.NewValue) Apply(window);
            else Revert(hwnd);
        }

        /// <summary>
        /// 按窗口的有效主题立即染色其标题栏。幂等，可在任意时机手动调用。
        /// </summary>
        /// <returns>窗口可见（HWND 已创建）、未被豁免且系统接受设置时返回 true。</returns>
        public static bool Apply(Window window)
        {
            if (window == null || !GetEnabled(window)) return false;
            IntPtr hwnd = HandleOf(window);
            if (hwnd == IntPtr.Zero) return false;
            if (!ApplyTo(hwnd, UI4Theme.EffectiveThemeFor(window) ?? UI4Theme.Current)) return false;
            int version = UI4Theme.ThemeVersion;
            _paintedVersions.GetValue(window, k => new PaintedVersion()).Value = version;
            return true;
        }

        /// <summary>对当前进程内所有已打开窗口重新应用（宿主改完主题后想立刻见效时调用）。</summary>
        public static void ApplyOpenWindows()
        {
            Application app = Application.Current;
            if (app == null) return;
            foreach (Window window in app.Windows) Apply(window);
        }

        private static bool ApplyTo(IntPtr hwnd, UI4Theme theme)
        {
            bool ok = SetDarkMode(hwnd, IsDark(theme));
            if (!SetCaptionColors(hwnd, theme) && !ok) return false;
            return ok;
        }

        /// <summary>把标题栏交还系统默认（用于 <see cref="EnabledProperty"/> 置 false）。</summary>
        private static void Revert(IntPtr hwnd)
        {
            if (_darkModeAttribute != 0)
            {
                int off = 0;
                DwmSetWindowAttribute(hwnd, _darkModeAttribute, ref off, SIZEOF_INT);
            }
            if (_captionColorSupport != 1) return;
            int fallback = DWMWA_COLOR_DEFAULT;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref fallback, SIZEOF_INT);
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref fallback, SIZEOF_INT);
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref fallback, SIZEOF_INT);
        }

        private static bool SetDarkMode(IntPtr hwnd, bool dark)
        {
            int value = dark ? 1 : 0;
            int preferred = _darkModeAttribute;
            if (preferred != DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1)
            {
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, SIZEOF_INT) == 0)
                {
                    _darkModeAttribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                    return true;
                }
            }
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, SIZEOF_INT) == 0)
            {
                _darkModeAttribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                return true;
            }
            return false;
        }

        private static bool SetCaptionColors(IntPtr hwnd, UI4Theme theme)
        {
            if (_captionColorSupport == 2) return false;
            int caption = ToColorRef(theme.ColorOf(UI4ThemeToken.Background));
            if (DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, SIZEOF_INT) != 0)
            {
                _captionColorSupport = 2;
                return false;
            }
            int text = ToColorRef(theme.ColorOf(UI4ThemeToken.TextForeground));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, SIZEOF_INT);
            int border = ToColorRef(theme.ColorOf(UI4ThemeToken.BorderNormal));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, SIZEOF_INT);
            _captionColorSupport = 1;
            return true;
        }

        /// <summary>以底色亮度判定深浅，自定义主题同样适用，无需枚举主题键。</summary>
        private static bool IsDark(UI4Theme theme)
        {
            Color c = theme.ColorOf(UI4ThemeToken.Background);
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B < 128d;
        }

        /// <summary>
        /// 把 <see cref="Color"/> 转成 DWM 颜色参数所用的 COLORREF（0x00BBGGRR，忽略 alpha）。
        /// 宿主自行调用 dwmapi 时可直接复用，保证与本库染色一致。
        /// </summary>
        public static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        private static IntPtr HandleOf(Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }
    }
}
