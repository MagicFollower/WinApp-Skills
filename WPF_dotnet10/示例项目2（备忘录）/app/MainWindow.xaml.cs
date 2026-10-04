using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MemoTask.Helpers;
using MemoTask.Models;
using MemoTask.Services;
using MemoTask.ViewModels;
using StartUI4Controls;

namespace MemoTask
{
    public partial class MainWindow : Window
    {
        /// <summary>内容区宽到多少才并排显示列表与编辑器（窄一档改成单栏）。</summary>
        private const double WideContentWidth = 760;

        private readonly MainViewModel _vm;
        private bool _navReady;

        /// <summary>最大化时 Left/Top/Width/Height 报的是整屏外框，记一份还原后的正常态尺寸用来存盘。</summary>
        private Rect _normalBounds = Rect.Empty;

        public MainWindow()
        {
            // XAML 设计器与 --selftest 都不走这条路；运行时用带 VM 的重载。
            InitializeComponent();
        }

        internal MainWindow(MainViewModel vm) : this()
        {
            _vm = vm;
            DataContext = vm;

            // 导航项的字号默认 10 px，低于可读下限。ItemFontSize 的 DP 登记在 UI4NavigationView 自身，
            // 这里按 DP 直接写值（XAML 侧现在也能写，保留代码写法是为了与其它两处运行期设置同处一地）。
            Nav.SetValue(UI4NavigationView.ItemFontSizeProperty, 12.0);

            var descriptor = DependencyPropertyDescriptor.FromName(
                "SelectedItem", typeof(UI4NavigationView), typeof(UI4NavigationViewItem));
            descriptor.AddValueChanged(Nav, OnNavSelectionChanged);

            SizeChanged += OnSizeChanged;
            LocationChanged += delegate { TrackNormalBounds(); };
            StateChanged += delegate { TrackNormalBounds(); };
            Closed += delegate { descriptor.RemoveValueChanged(Nav, OnNavSelectionChanged); };
        }

        private void TrackNormalBounds()
        {
            if (WindowState != WindowState.Normal) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            _normalBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        }

        private void OnNavSelectionChanged(object sender, EventArgs e)
        {
            if (!_navReady) return;
            UI4NavigationViewItem item = Nav.SelectedItem;
            if (item == null) return;
            string key = item.Tag as string;
            if (!string.IsNullOrEmpty(key))
            {
                _vm.Settings.Current.LastPage = key;
                _vm.NotifyPageChanged(key);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RestoreWindowBounds();
            TrackNormalBounds();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_navReady) return;
            _navReady = true;
            OpenStartPage();
            UpdatePanes();
        }

        private void OpenStartPage()
        {
            string setting = _vm.Settings.Current.StartPage;
            string key = string.Equals(setting, "last", StringComparison.OrdinalIgnoreCase)
                ? _vm.Settings.Current.LastPage
                : setting;
            if (string.IsNullOrEmpty(key)) key = "notes";

            UI4NavigationViewItem target = ItemFor(key);
            if (target == null) target = NavNotes;
            Nav.SelectedItem = target;
            _vm.NotifyPageChanged((string)target.Tag);
        }

        private UI4NavigationViewItem ItemFor(string key)
        {
            switch (key)
            {
                case "notes": return NavNotes;
                case "todos": return NavTodos;
                case "settings": return NavSettings;
                default: return null;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePanes();
        }

        private void UpdatePanes()
        {
            // 左栏固定 96，内容区才是决定并排还是单栏的那个宽度
            double content = ActualWidth - 96;
            if (double.IsNaN(content) || content <= 0) return;
            _vm.Notes.IsWide = content >= WideContentWidth;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.N:
                        Navigate("notes");
                        _vm.Notes.NewNoteCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.F:
                        Navigate("notes");
                        _vm.Notes.RequestSearchFocus();
                        e.Handled = true;
                        break;
                    case Key.S:
                        _vm.FlushAll();
                        _vm.UpdateFooter();
                        e.Handled = true;
                        break;
                    case Key.D:
                        Navigate("todos");
                        _vm.Todos.ToggleSelectedCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.D1:
                        Navigate("notes");
                        e.Handled = true;
                        break;
                    case Key.D2:
                        Navigate("todos");
                        e.Handled = true;
                        break;
                    case Key.D3:
                        Navigate("settings");
                        e.Handled = true;
                        break;
                }
                if (e.Handled) return;
            }

            if (e.Key == Key.Escape)
            {
                if (_vm.CurrentPage == "todos") _vm.Todos.CancelEditCommand.Execute(null);
                else if (_vm.CurrentPage == "notes") _vm.Notes.CloseDetailCommand.Execute(null);
                e.Handled = true;
            }

            base.OnPreviewKeyDown(e);
        }

        private void Navigate(string key)
        {
            UI4NavigationViewItem item = ItemFor(key);
            if (item != null) Nav.SelectedItem = item;
        }

        private void RestoreWindowBounds()
        {
            WindowBounds saved = _vm.Settings.Current.Window;
            if (saved == null || double.IsNaN(saved.Width) || saved.Width < MinWidth) return;

            double dpiX = 1.0, dpiY = 1.0;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            Width = saved.Width;
            Height = saved.Height;
            if (double.IsNaN(saved.Left) || double.IsNaN(saved.Top)) return;

            // 显示器坐标是物理像素，窗口坐标是 DIP，换算要在同一单位里比
            double left = saved.Left * dpiX;
            double top = saved.Top * dpiY;
            double width = saved.Width * dpiX;
            double height = saved.Height * dpiY;
            if (!Monitors.IsVisibleOnSomeMonitor(left, top, width, height))
            {
                Monitors.ClampToNearestMonitor(ref left, ref top, width, height);
            }
            Left = left / dpiX;
            Top = top / dpiY;
            WindowState = saved.Maximized ? WindowState.Maximized : WindowState.Normal;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _vm.FlushAll();
            SaveBounds();
            _vm.Settings.Save();
            base.OnClosing(e);
        }

        private void SaveBounds()
        {
            WindowBounds current = _vm.Settings.Current.Window;
            bool maximized = WindowState == WindowState.Maximized;
            Rect frame = maximized && !_normalBounds.IsEmpty
                ? _normalBounds
                : new Rect(Left, Top, ActualWidth, ActualHeight);
            current.Maximized = maximized;
            if (frame.Width <= 0 || frame.Height <= 0) return;
            current.Left = frame.X;
            current.Top = frame.Y;
            current.Width = frame.Width;
            current.Height = frame.Height;
        }
    }
}
