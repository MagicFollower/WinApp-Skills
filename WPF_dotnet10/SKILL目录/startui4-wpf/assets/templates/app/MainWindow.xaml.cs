using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StartUI4Controls;
using __APPNAME__.ViewModels;

namespace __APPNAME__
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 设置与缩放只认这一个视图模型；App.Settings 为空时（--selftest 里构造窗口）VM 自己兜一份默认值
            DataContext = new MainViewModel(App.Settings);

            // 不要 {Binding Path=(ui:UI4Theme.CurrentMode)}：库发的是 StaticPropertyChanged，
            // 而 WPF 绑静态 CLR 属性找的是同名 <属性>Changed 静态事件，那样会停在初值不再刷新。
            UI4Theme.ThemeChanged += (s, e) => UpdateFooter();
            UpdateFooter();

            RestoreGeometry();
            Closing += MainWindow_Closing;
        }

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.IsSettingsOpen = true;
        }

        private void SettingsOverlay_BackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.IsSettingsOpen = false;
        }

        /// <summary>浮层开合的四个入口（齿轮 / 完成 / 遮罩 / Esc）里只有这里需要按键；开着时遮罩盖住齿轮，所以齿轮只负责开。</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            var vm = Vm;
            if (vm != null && vm.IsSettingsOpen)
            {
                vm.IsSettingsOpen = false;
                e.Handled = true;
            }
        }

        private void UpdateFooter()
        {
            ThemeFooter.Text = "生效键 " + UI4Theme.ResolvedKey
                             + "　请求模式 " + UI4Theme.CurrentMode
                             + "　解析 " + UI4Theme.ResolvedMode
                             + "　" + UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey);
        }

        // ── 窗口几何持久化：存了要能回正 ────────────────────────────────
        //
        // 换显示器（或拔外接屏）之后旧坐标可能落在屏幕外，窗口边看不见就没法拖回来，
        // 所以恢复前判一次虚拟桌面边界，不在界内就回正中。

        private void RestoreGeometry()
        {
            var settings = App.Settings;
            if (settings == null) return;

            if (settings.WindowMaximized) WindowState = WindowState.Maximized;
            if (settings.WindowWidth > 0) Width = settings.WindowWidth;
            if (settings.WindowHeight > 0) Height = settings.WindowHeight;
            if (double.IsNaN(settings.WindowLeft) || double.IsNaN(settings.WindowTop)) return;

            double left = settings.WindowLeft, top = settings.WindowTop;
            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

            // 只要窗口的一个角还在屏内就接受，否则回 CenterScreen
            bool visible = left < vsRight - 40 && top < vsBottom - 40
                           && left + Width > vsLeft + 40 && top + Height > vsTop + 40;
            if (!visible) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            var settings = App.Settings;
            if (settings == null) return;

            bool maximized = WindowState == WindowState.Maximized;
            if (maximized)
            {
                // 最大化时拿到的 Bounds 是屏幕尺寸，存的应该是还原后的 Normal 尺寸，否则下次启动窗口巨大
                settings.WindowMaximized = true;
                var normal = RestoreBounds;
                settings.WindowWidth = normal.Width;
                settings.WindowHeight = normal.Height;
            }
            else
            {
                settings.WindowMaximized = false;
                settings.WindowWidth = ActualWidth;
                settings.WindowHeight = ActualHeight;
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
            }

            settings.Save();
        }
    }
}
