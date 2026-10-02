using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(1.6);
            _toastTimer.Tick += ToastTimer_Tick;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachVm(DataContext as ViewModels.MainViewModel);
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
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null && vm.SearchText != SearchBox.Text)
                vm.SearchText = SearchBox.Text;
        }

        private void FolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var menu = new UI4ContextMenu();

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy, "\u6253\u5F00\u5F53\u524D\u76EE\u5F55", null,
                delegate { OpenCurrentFolder(); }));

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy, "\u5207\u6362\u6839\u76EE\u5F55...", null,
                delegate { SelectNewFolder(); }));

            menu.AddItem(new UI4MenuItem(
                UI4MenuItemType.Copy, "\u6253\u5F00\u8BBE\u7F6E\u76EE\u5F55", null,
                delegate { OpenSettingsFolder(); }));

            menu.Attach(FolderBtn);
            menu.Open();
        }

        private void OpenSettingsFolder()
        {
            var path = SettingsService.SettingsDirectory;

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
                UI4MessageBox.Show("\u65E0\u6CD5\u6253\u5F00\u76EE\u5F55: " + ex.Message,
                    "\u9519\u8BEF", UI4MessageBoxButtons.OK, 360);
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
                    UI4MessageBox.Show("\u65E0\u6CD5\u6253\u5F00\u76EE\u5F55: " + ex.Message,
                        "\u9519\u8BEF", UI4MessageBoxButtons.OK, 360);
                }
            }
        }

        private void SelectNewFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "\u9009\u62E9 Prompt \u6839\u76EE\u5F55"
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
