using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PromptFavorites.Services;
using StartUI4Controls;

namespace PromptFavorites
{
    public partial class MainWindow : Window
    {
        private ViewModels.MainViewModel _vm;
        private readonly DispatcherTimer _toastTimer;
        private readonly DispatcherTimer _searchDebounceTimer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(1.6);
            _toastTimer.Tick += ToastTimer_Tick;

            _searchDebounceTimer = new DispatcherTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachVm(DataContext as ViewModels.MainViewModel);
        }

        /// <summary>设置浮层的开合只有这几个入口（齿轮 / 完成 / 遮罩 / Esc），
        /// 状态存在 <c>MainViewModel.IsSettingsOpen</c> 一处，按钮上不另存。</summary>
        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null) vm.IsSettingsOpen = true;
        }

        private void SettingsOverlay_BackgroundClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null) vm.IsSettingsOpen = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null && vm.IsSettingsOpen)
            {
                vm.IsSettingsOpen = false;
                e.Handled = true;
            }
        }

        private void AttachVm(ViewModels.MainViewModel vm)
        {
            if (_vm != null)
                _vm.ToastRequested -= OnToastRequested;

            _vm = vm;
            if (_vm != null)
            {
                _vm.MainWindow = this;
                _vm.ToastRequested += OnToastRequested;
            }
        }

        private void OnToastRequested(string message)
        {
            ShowToast(message);
        }

        private void ShowToast(string message)
        {
            _toastTimer.Stop();
            ToastTextBlock.Text = message;
            ToastBorder.Visibility = Visibility.Visible;
            ToastBorder.Opacity = 1;

            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

            _toastTimer.Start();
        }

        private void ToastTimer_Tick(object sender, EventArgs e)
        {
            _toastTimer.Stop();

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += delegate
            {
                ToastBorder.Visibility = Visibility.Collapsed;
            };
            ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_vm != null)
                _vm.SaveWindowState(this);
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // 防抖：每次按键重启计时，停 300ms 才真的去全盘读文件（逐键搜索会把条目多时的耗时叠加成卡顿）
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();

            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null && vm.SearchText != SearchBox.Text)
                vm.SearchText = SearchBox.Text;
        }

        private void FolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var menu = new UI4ContextMenu { Width = 220 };

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy, "打开当前目录", null,
                delegate { OpenCurrentFolder(); }));

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy, "切换根目录", null,
                delegate { SelectNewFolder(); }));

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy,
                "打开设置目录（当前根）", null,
                delegate { OpenSettingsFolder(false); }));

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy,
                "打开全局设置目录", null,
                delegate { OpenSettingsFolder(true); }));

            menu.Attach(FolderBtn);
            menu.Open();
        }

        /// <summary>
        /// 两层配置各有各的去处：数据类状态在<paramref name="global"/>为 false 时打开的
        /// <c>&lt;根目录&gt;\.PromptFavorites</c>，外观与窗口在 <c>%APPDATA%\PromptFavorites</c>。
        /// 根还没挂上（根目录解析失败）时两者退化成同一个全局目录。
        /// </summary>
        private void OpenSettingsFolder(bool global)
        {
            var rootDir = App.Settings != null ? App.Settings.RootSettingsDirectory : null;
            var path = global || string.IsNullOrEmpty(rootDir)
                ? SettingsService.SettingsDirectory
                : rootDir;

            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                UI4MessageBox.Show("无法打开目录: " + ex.Message,
                    "错误", UI4MessageBoxButtons.OK, 360);
            }
        }

        private void OpenCurrentFolder()
        {
            var path = App.RootPath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception ex)
                {
                    UI4MessageBox.Show("无法打开目录: " + ex.Message,
                        "错误", UI4MessageBoxButtons.OK, 360);
                }
            }
        }

        private void SelectNewFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择 Prompt 根目录"
            };

            if (!string.IsNullOrEmpty(App.RootPath) && Directory.Exists(App.RootPath))
                dialog.InitialDirectory = App.RootPath;

            if (dialog.ShowDialog() == true)
            {
                var newPath = dialog.FolderName;
                if (newPath == App.RootPath) return;

                if (!App.ChangeRootPath(newPath)) return;
                AttachVm(DataContext as ViewModels.MainViewModel);
            }
        }
    }
}
