using System.Windows;
using StartUI4Controls;

namespace __APPNAME__
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 不要 {Binding Path=(ui:UI4Theme.CurrentMode)}：库发的是 StaticPropertyChanged，
            // 而 WPF 绑静态 CLR 属性找的是同名 <属性>Changed 静态事件，那样会停在初值不再刷新。
            UI4Theme.ThemeChanged += (s, e) => UpdateFooter();
            UpdateFooter();
        }

        private void UpdateFooter()
        {
            ThemeFooter.Text = "生效键 " + UI4Theme.ResolvedKey
                             + "　请求模式 " + UI4Theme.CurrentMode
                             + "　解析 " + UI4Theme.ResolvedMode
                             + "　" + UI4ThemePacks.DisplayLabel(UI4Theme.ResolvedKey);
        }

        private void OnLight(object sender, RoutedEventArgs e) { UI4Theme.SetTheme(UI4ThemeMode.Light); }
        private void OnDark(object sender, RoutedEventArgs e) { UI4Theme.SetTheme(UI4ThemeMode.Dark); }
        private void OnSystem(object sender, RoutedEventArgs e) { UI4Theme.SetTheme(UI4ThemeMode.System); }

        private void OnPaperGrey(object sender, RoutedEventArgs e)
        {
            // Apply 对未注册的键只返回 false、不抛异常——静默无反应就先用 ThemeKeys 查一眼键名
            if (!UI4Theme.Apply("paper-grey"))
            {
                System.Diagnostics.Debug.WriteLine("未注册 paper-grey，已注册的键：" + string.Join(", ", UI4Theme.ThemeKeys));
            }
        }
    }
}
