using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StartUI4Controls;
using VocabDesk.Models;
using VocabDesk.Services;
using VocabDesk.ViewModels;

namespace VocabDesk
{
    public partial class MainWindow : Window
    {
        /// <summary>窗口在关闭：放弃后台构建，也别再往已停的 Dispatcher 上投进度。</summary>
        private bool _shuttingDown;

        public MainWindow()
        {
            InitializeComponent();

            // 设置与缩放只认这一个视图模型；App.Settings 为空时（--selftest 里构造窗口）VM 自己兜一份默认值
            DataContext = new MainViewModel(App.Settings);

            RestoreGeometry();
            Closing += MainWindow_Closing;
            HookNavSelection();
            // 首帧画完再启动构建：遮罩与进度条要先真的出现在屏幕上，否则用户看到的是"点开就白一下"。
            // ContentRendered 可能不止一次（内容重排），进 handler 就先摘掉。
            ContentRendered += Window_ContentRendered;
        }

        /// <summary>DP 没有 CLR 事件，用 Descriptor 订阅变更（详见 Nav_SelectionChanged 的注释）。</summary>
        private void HookNavSelection()
        {
            var descriptor = System.ComponentModel.DependencyPropertyDescriptor
                .FromProperty(UI4NavigationView.SelectedItemProperty, typeof(UI4NavigationView));
            if (descriptor != null) descriptor.AddValueChanged(Nav, (s, e) => Nav_SelectionChanged());
        }

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        // ── 词库加载：后台构建 + 确定进度 ──────────────────────────────
        //
        // 实测 54,356 行 → 14,625 词头 + 四类索引 = 1.30 s，全放 UI 线程会 684 ms 起步地卡，
        // 所以这里一次 Task.Run 建好整个 Corpus，完成后一次性把 Views[0] 赋给 ItemsSource。
        // 增量往列表 Add 是另一条死路：虚拟化列表每加一项重排一次。

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            ContentRendered -= Window_ContentRendered;

            var vm = Vm;
            if (vm == null) return;

            ModeBox.ItemsSource = vm.ModeChoices;
            ModeBox.SelectedItem = vm.ModeChoices[0];

            vm.BeginLoad();
            try
            {
                // 目录探测只是几次 Exists，留在 UI 线程；它抛的消息里点名找过哪几处，正好给页脚用
                string root = Corpus.ResolveDataRoot();

                // 左栏先长出来：书名只从文件名就能定（不读内容，毫秒级），收录数等构建完再就地补。
                // 之前整条左栏要等 1.3 s 才有东西，加载期间看着像界面坏了。
                vm.BuildRailPreview(Corpus.PreviewBooks(root));
                ApplyNavItems();

                var corpus = await Task.Run(() => Corpus.Build(
                    root,
                    p => { if (!_shuttingDown) Dispatcher.BeginInvoke(new Action(() => vm.ReportProgress(p))); },
                    () => _shuttingDown));

                if (_shuttingDown) return;

                vm.Attach(corpus);
            }
            catch (OperationCanceledException)
            {
                // 关窗时放弃构建，不给已死的窗口报错
            }
            catch (Exception ex)
            {
                vm.LoadFailed(ex);
            }
        }

        /// <summary>
        /// 导航项必须由 Items 进去：库的 OnItemsChanged 只认 UI4NavigationViewItem，
        /// 直接往 RegularItems 加不会进左栏。Content 一律留空——本控件当纯导航栏用，
        /// 切换书只是换个数组引用。只在启动时调一次；构建完是就地改 Header，不重加。
        /// </summary>
        private void ApplyNavItems()
        {
            var vm = Vm;
            if (vm == null || vm.NavItems == null) return;

            Nav.Items.Clear();
            foreach (var item in vm.NavItems) Nav.Items.Add(item);
            Nav.SelectedItem = vm.NavItems[0];
        }

        /// <summary>
        /// 左栏换书。为什么是 AddValueChanged 而不是 SelectedItem="{Binding …, Mode=TwoWay}"：
        /// 实测（2026-10-05，本机 4 挡对照）只要给这个 DP 挂上双向绑定，
        /// 无论赋值来自代码还是用户点击，UI 线程就进不可返回的忙等——进程 responding=False、
        /// 工作集一路涨到 548 MB、首帧不再合成；把绑定摘掉、同一句赋值原样留着，界面正常（响应正常）。
        /// VM 里那段检索逻辑也不是原因（把它短路掉照样卡）。所以这里只用 DP 的变更通知，不让绑定参与。
        /// </summary>
        private void Nav_SelectionChanged()
        {
            var vm = Vm;
            var item = Nav.SelectedItem as NavRailItem;
            if (vm != null && item != null) vm.ApplyView(item);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.RequestSearch(SearchBox.Text);
        }

        // ── 卡片展开 / 收起 ────────────────────────────────────────────
        //
        // 展开态就是列表选中，所以"收起"= 取消选中。ListBox 自己不会因"再点一次已选项"而取消选中，
        // 这里补上：按下时先记下这张当时是不是已经选中（Preview 是隧道事件，跑在 Selector 处理选中之前，
        // 看到的才是"点下去之前"的状态），抬起时若仍是同一张就把选中清掉。

        private bool _downWasOnSelectedCard;

        private void CardList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var container = FindCardContainer(e.OriginalSource as DependencyObject);
            _downWasOnSelectedCard = container != null && container.IsSelected;
        }

        private void CardList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_downWasOnSelectedCard) return;
            _downWasOnSelectedCard = false;

            var container = FindCardContainer(e.OriginalSource as DependencyObject);
            if (container == null) return;

            CardList.SelectedItem = null;
            e.Handled = true;
        }

        private static ListBoxItem FindCardContainer(DependencyObject source)
        {
            while (source != null && !(source is ListBoxItem))
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            return source as ListBoxItem;
        }

        /// <summary>选中项交给 VM，页脚右下那行"这个词的收录情况"读的就是它。</summary>
        private void CardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.SetSelectedCard(CardList.SelectedItem as WordCard);
        }

        private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = Vm;
            var option = ModeBox.SelectedItem as SearchModeOption;
            if (vm != null && option != null) vm.SelectedMode = option;
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
            _shuttingDown = true;

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
