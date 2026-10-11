using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StartUI4Controls;
using QuickPanel.Models;
using QuickPanel.ViewModels;

namespace QuickPanel
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 设置与缩放只认这一个视图模型；App.Settings 为空时（--selftest 里构造窗口）VM 自己兜一份默认值
            DataContext = new MainViewModel(App.Settings);

            BuildNavigation();

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

        /// <summary>
        /// 左栏项必须由 UI4NavigationViewItem 真身进 Items：库在 OnItemsChanged 里按类型分流到
        /// RegularItems / BottomItems，直接往 RegularItems 加不会进左栏，下次 Items 变化还会被整表清空。
        ///
        /// 十一个项的 Content 都指向同一个 SectionShell 实例（不是各自的分组对象）：右栏绑的就是
        /// SelectedItem.Content，Content 一换整棵子树要重建，而卡片不虚拟化——那正是"切到全部工具卡一下"的成因。
        /// 分组挂在 NavSectionItem.Section 上，切换时只翻各节的 IsActive。
        /// </summary>
        private void BuildNavigation()
        {
            var vm = Vm;
            if (vm == null) return;

            var iconFont = new FontFamily("Segoe MDL2 Assets");
            foreach (var section in vm.Sections)
            {
                var navItem = new NavSectionItem
                {
                    Section = section,
                    Header = section.Title,
                    TextIcon = section.Glyph,
                    TextIconFontFamily = iconFont,
                    Content = vm.Shell
                };
                Nav.Items.Add(navItem);
            }

            // 库的 NavigationView 不发 SelectionChanged（SelectedItem 只是个 BindsTwoWayByDefault 的 DP），
            // 所以挂属性变化回调。
            System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(UI4NavigationView.SelectedItemProperty, typeof(UI4NavigationView))
                .AddValueChanged(Nav, (s, e) => ActivateSelectedSection());

            if (Nav.Items.Count > 0) Nav.SelectedItem = Nav.Items[0] as UI4NavigationViewItem;
        }

        /// <summary>切换分组 = 只改显示哪一节。取不到对应分组就整屏不显示，宁可空屏也不让两节叠在一起。</summary>
        private void ActivateSelectedSection()
        {
            var vm = Vm;
            if (vm == null || vm.Shell == null) return;

            var picked = Nav.SelectedItem as NavSectionItem;
            vm.Shell.Activate(picked != null && picked.Section != null ? picked.Section.Key : null);
        }

        /// <summary>
        /// 单击即执行：卡片网格是一次"选择变化"，执行完把选择清掉，
        /// 否则再点同一张卡不会产生新的事件（看起来就是"第二下没反应"）。
        /// </summary>
        private void ToolGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var grid = sender as ListBox;   // UI4GridView 的基类就是 ListBox
            if (grid == null) return;

            var item = grid.SelectedItem as ToolItem;
            if (item == null) return;

            grid.SelectedIndex = -1;

            var result = Vm.Execute(item);
            // 模态框只留给错误报告：成功只写页脚，不打断连续操作
            if (!result.Ok)
                UI4MessageBox.Show(result.Message, "执行失败", UI4MessageBoxButtons.OK, owner: this);
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
                             + "　解析 " + UI4Theme.ResolvedMode;
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

    /// <summary>
    /// 左栏项的真身，只为把 UIA 名字留住：导航项容器的名字取的是"数据项的纯文本"，
    /// 而 Content 现在是指向共用 SectionShell 的（右栏不重建的前提），shell 的 ToString() 又是空串，
    /// 于是回落到数据项自己的 ToString()——默认会报 "StartUI4Controls.UI4NavigationViewItem"。
    /// 在这里覆写成所属分组的标题，读屏与按名字点导航项的脚本才有得读。库的分流按
    /// is UI4NavigationViewItem 判断，子类照样进左栏。
    /// </summary>
    sealed class NavSectionItem : UI4NavigationViewItem
    {
        public ToolSection Section { get; set; }

        public override string ToString()
        {
            return Section != null ? Section.Title : base.ToString();
        }
    }
}
