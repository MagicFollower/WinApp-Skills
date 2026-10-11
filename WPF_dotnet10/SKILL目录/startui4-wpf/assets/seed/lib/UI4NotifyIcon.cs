using System;
using System.Collections.Generic;
using DrawingIcon = System.Drawing.Icon;
using SystemIcons = System.Drawing.SystemIcons;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StartUI4Controls
{
    public enum PopupActivationMode
    {
        None,
        LeftClick,
        RightClick,
        DoubleClick,
        LeftOrRightClick,
        All
    }

    public class UI4TrayMenuItem
    {
        public UI4MenuItemType Type { get; set; }
        public string Text { get; set; }
        public ImageSource Icon { get; set; }
        public string IconText { get; set; }
        public Action Command { get; set; }
        public Func<bool> CanExecute { get; set; }

        public UI4TrayMenuItem(UI4MenuItemType type, string text, ImageSource icon, Action command, Func<bool> canExecute = null)
        {
            Type = type;
            Text = text;
            Icon = icon;
            Command = command;
            CanExecute = canExecute;
        }

        public UI4TrayMenuItem(UI4MenuItemType type, string text, string iconText, Action command, Func<bool> canExecute = null)
        {
            Type = type;
            Text = text;
            IconText = iconText;
            Command = command;
            CanExecute = canExecute;
        }
    }

    public class UI4NotifyIcon : FrameworkElement, IDisposable
    {
        private const int CallbackMessage = 0x400;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_SETVERSION = 0x00000004;
        private const int NOTIFYICON_VERSION_4 = 4;

        private HwndSource _messageSource;
        private NotifyIconData _iconData;
        private DrawingIcon _icon;
        private bool _isIconCreated;
        private bool _isDisposed;
        private Popup _trayPopup;
        private UI4ListBox _listBox;
        private readonly List<UI4TrayMenuItem> _menuItems = new List<UI4TrayMenuItem>();
        private Window _hookedWindow;
        private DispatcherTimer _closeCheckTimer;

        static UI4NotifyIcon()
        {
            VisibilityProperty.OverrideMetadata(typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(Visibility.Visible, OnVisibilityChanged));
        }

        public UI4NotifyIcon()
        {
            MenuActivation = PopupActivationMode.None;
            TrayRightMouseDown += OnTrayRightClick;
            BuildMessageWindow();
            BuildTrayPopup();
            _closeCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _closeCheckTimer.Tick += CheckMouseOutsidePopup;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            if (Application.Current != null)
                Application.Current.Exit += OnApplicationExit;
        }

        public static readonly RoutedEvent TrayLeftMouseUpEvent = EventManager.RegisterRoutedEvent(
            nameof(TrayLeftMouseUp), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UI4NotifyIcon));

        public event RoutedEventHandler TrayLeftMouseUp
        {
            add { AddHandler(TrayLeftMouseUpEvent, value); }
            remove { RemoveHandler(TrayLeftMouseUpEvent, value); }
        }

        public static readonly RoutedEvent TrayRightMouseDownEvent = EventManager.RegisterRoutedEvent(
            nameof(TrayRightMouseDown), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UI4NotifyIcon));

        public event RoutedEventHandler TrayRightMouseDown
        {
            add { AddHandler(TrayRightMouseDownEvent, value); }
            remove { RemoveHandler(TrayRightMouseDownEvent, value); }
        }

        public static readonly RoutedEvent TrayMouseDoubleClickEvent = EventManager.RegisterRoutedEvent(
            nameof(TrayMouseDoubleClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UI4NotifyIcon));

        public event RoutedEventHandler TrayMouseDoubleClick
        {
            add { AddHandler(TrayMouseDoubleClickEvent, value); }
            remove { RemoveHandler(TrayMouseDoubleClickEvent, value); }
        }

        public PopupActivationMode MenuActivation
        {
            get => (PopupActivationMode)GetValue(MenuActivationProperty);
            set => SetValue(MenuActivationProperty, value);
        }

        public static readonly DependencyProperty MenuActivationProperty =
            DependencyProperty.Register(nameof(MenuActivation), typeof(PopupActivationMode), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(PopupActivationMode.RightClick));

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register(nameof(IconSource), typeof(ImageSource), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(null, OnIconSourceChanged));

        public string ToolTipText
        {
            get => (string)GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(string.Empty, OnToolTipTextChanged));

        public double MenuWidth
        {
            get => (double)GetValue(MenuWidthProperty);
            set => SetValue(MenuWidthProperty, value);
        }

        public static readonly DependencyProperty MenuWidthProperty =
            DependencyProperty.Register(nameof(MenuWidth), typeof(double), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(160d));

        public Thickness MenuItemPadding
        {
            get => (Thickness)GetValue(MenuItemPaddingProperty);
            set => SetValue(MenuItemPaddingProperty, value);
        }

        public static readonly DependencyProperty MenuItemPaddingProperty =
            DependencyProperty.Register(nameof(MenuItemPadding), typeof(Thickness), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(new Thickness(12, 8, 12, 8)));

        // MenuBorderColor / MenuBackground / MenuHoverBg 已删除：托盘不在任何窗口的可视化树上，
        // 拿不到 UI4ThemeScope，这三个 DP 又没有 PropertyChangedCallback（运行期改色根本不生效）。
        // 菜单配色改由内部 UI4ListBox 的资源引用驱动，跟随全局主题。

        public CornerRadius MenuCornerRadius
        {
            get => (CornerRadius)GetValue(MenuCornerRadiusProperty);
            set => SetValue(MenuCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty MenuCornerRadiusProperty =
            DependencyProperty.Register(nameof(MenuCornerRadius), typeof(CornerRadius), typeof(UI4NotifyIcon),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        public void AddItem(UI4TrayMenuItem item)
        {
            _menuItems.Add(item);
        }

        public void AddItem(UI4MenuItemType type, Action command, Func<bool> canExecute = null)
        {
            var key = TypeToKey(type);
            var label = UI4MultiLanguage.Get(key);
            var item = new UI4TrayMenuItem(type, label, UI4MenuIcons.GetIcon(type), command, canExecute);
            _menuItems.Add(item);
        }

        public void ClearMenuItems()
        {
            _menuItems.Clear();
            _listBox?.Items.Clear();
        }

        public void OpenMenu()
        {
            if (_menuItems.Count == 0) return;
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                IntPtr hwnd = new WindowInteropHelper(mainWindow).Handle;
                uint currentThread = (uint)Thread.CurrentThread.ManagedThreadId;
                uint foreThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                if (currentThread != foreThread)
                    AttachThreadInput(currentThread, foreThread, true);
                SetForegroundWindow(hwnd);
                if (currentThread != foreThread)
                    AttachThreadInput(currentThread, foreThread, false);
            }

            RebuildAllRows();
            _trayPopup.PlacementTarget = mainWindow;
            _trayPopup.Placement = PlacementMode.MousePoint;
            _trayPopup.HorizontalOffset = -10;
            _trayPopup.VerticalOffset = 10;
            _listBox.Opacity = 0;
            _trayPopup.IsOpen = true;
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            _listBox.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            HookCloseEvents();
            _closeCheckTimer.Start();
        }

        public void CloseMenu()
        {
            _closeCheckTimer.Stop();
            if (_trayPopup != null)
                _trayPopup.IsOpen = false;
            if (_listBox != null)
                _listBox.SelectedIndex = -1;
            UnhookCloseEvents();
        }

        private static UI4LanguageKey TypeToKey(UI4MenuItemType type)
        {
            switch (type)
            {
                case UI4MenuItemType.Undo: return UI4LanguageKey.Undo;
                case UI4MenuItemType.Redo: return UI4LanguageKey.Redo;
                case UI4MenuItemType.Cut: return UI4LanguageKey.Cut;
                case UI4MenuItemType.Copy: return UI4LanguageKey.Copy;
                case UI4MenuItemType.Paste: return UI4LanguageKey.Paste;
                case UI4MenuItemType.Delete: return UI4LanguageKey.Delete;
                case UI4MenuItemType.SelectAll: return UI4LanguageKey.SelectAll;
                default: return UI4LanguageKey.Copy;
            }
        }

        private void BuildTrayPopup()
        {
            _listBox = new UI4ListBox
            {
                Width = MenuWidth,
                ItemPadding = MenuItemPadding,
                CornerRadius = MenuCornerRadius,
                IsMenuMode = true,
            };
            _listBox.PreviewMouseLeftButtonUp += OnListBoxClick;

            // 用 Border 包裹 ListBox 并启用 ClipToBounds，确保内容不会溢出 Popup 边界
            var border = new Border
            {
                Width = MenuWidth,
                ClipToBounds = true,
                Child = _listBox
            };

            _trayPopup = new Popup
            {
                Child = border,
                Placement = PlacementMode.MousePoint,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Slide,
                Width = MenuWidth
            };
            _trayPopup.Closed += OnTrayPopupClosed;
        }

        private void RebuildAllRows()
        {
            _listBox.Items.Clear();
            foreach (var item in _menuItems)
            {
                // 计算 Grid 可用宽度：Popup宽度 - ScrollViewer Padding(4*2) - ListBoxItem Border Margin(2*2) - ItemPadding
                double gridWidth = MenuWidth - 8 - 4 - MenuItemPadding.Left - MenuItemPadding.Right;

                var container = new Grid
                {
                    Width = gridWidth,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bool enable = item.CanExecute?.Invoke() ?? true;
                double opacityDisable = enable ? 1.0 : 0.3;
                double textOpacityDisable = enable ? 1.0 : 0.4;
                if (item.Icon != null)
                {
                    var icon = new System.Windows.Controls.Image
                    {
                        Source = item.Icon,
                        Width = 16,
                        Height = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = opacityDisable
                    };
                    Grid.SetColumn(icon, 0);
                    container.Children.Add(icon);
                }
                else if (!string.IsNullOrEmpty(item.IconText))
                {
                    var iconText = new TextBlock
                    {
                        Text = item.IconText,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = opacityDisable
                    };
                    Grid.SetColumn(iconText, 0);
                    container.Children.Add(iconText);
                }

                var textBlock = new TextBlock
                {
                    Text = item.Text,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    Opacity = textOpacityDisable,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = item.Text
                };
                Grid.SetColumn(textBlock, 1);
                container.Children.Add(textBlock);
                container.Tag = item;
                _listBox.Items.Add(container);
            }
            _listBox.Width = MenuWidth;
        }

        private void BuildMessageWindow()
        {
            if (_messageSource != null) return;
            var parameters = new HwndSourceParameters("UI4NotifyIconMessageWindow")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            };
            _messageSource = new HwndSource(parameters);
            _messageSource.AddHook(WndProc);
            _iconData = CreateNotifyIconData(_messageSource.Handle);
        }

        private void CreateTrayIcon()
        {
            if (_isDisposed || Visibility != Visibility.Visible || _isIconCreated || _messageSource == null) return;
            UpdateIconData();
            if (Shell_NotifyIcon(NIM_ADD, ref _iconData))
            {
                _isIconCreated = true;
                _iconData.VersionOrTimeout = NOTIFYICON_VERSION_4;
                Shell_NotifyIcon(NIM_SETVERSION, ref _iconData);
            }
        }

        private void UpdateTrayIcon()
        {
            if (!_isIconCreated) return;
            UpdateIconData();
            Shell_NotifyIcon(NIM_MODIFY, ref _iconData);
        }

        private void RemoveTrayIcon()
        {
            if (!_isIconCreated) return;
            Shell_NotifyIcon(NIM_DELETE, ref _iconData);
            _isIconCreated = false;
        }

        private void UpdateIconData()
        {
            _icon?.Dispose();
            _icon = CreateIcon(IconSource);
            _iconData.IconHandle = _icon?.Handle ?? IntPtr.Zero;
            _iconData.ToolTipText = LimitToolTipText(ToolTipText);
            _iconData.ValidMembers = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        }

        private static string LimitToolTipText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length > 127 ? text.Substring(0, 127) : text;
        }

        private DrawingIcon CreateIcon(ImageSource source)
        {
            if (source == null) return SystemIcons.Application;

            var uri = GetIconUri(source);
            if (uri != null)
            {
                var icon = TryCreateIconFromUri(uri);
                if (icon != null)
                    return icon;
            }

            return SystemIcons.Application;
        }

        private static Uri GetIconUri(ImageSource source)
        {
            if (Uri.TryCreate(source.ToString(), UriKind.RelativeOrAbsolute, out var sourceUri))
                return sourceUri;

            if (source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
                return bitmapImage.UriSource;

            return null;
        }

        private static DrawingIcon TryCreateIconFromUri(Uri uri)
        {
            try
            {
                var resourceStream = Application.GetResourceStream(uri)?.Stream;
                if (resourceStream != null)
                    return new DrawingIcon(resourceStream);
            }
            catch
            {
            }

            try
            {
                var contentStream = Application.GetContentStream(uri)?.Stream;
                if (contentStream != null)
                    return new DrawingIcon(contentStream);
            }
            catch
            {
            }

            try
            {
                string filePath = null;
                if (uri.IsAbsoluteUri && uri.IsFile)
                {
                    filePath = uri.LocalPath;
                }
                else if (!uri.IsAbsoluteUri)
                {
                    string relativePath = uri.OriginalString.TrimStart('/', '\\');
                    filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                }

                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                    return new DrawingIcon(filePath);
            }
            catch
            {
            }

            return null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != CallbackMessage) return IntPtr.Zero;

            int mouseMessage = lParam.ToInt32();
            switch (mouseMessage)
            {
                case WM_LBUTTONUP:
                    RaiseEvent(new RoutedEventArgs(TrayLeftMouseUpEvent, this));
                    handled = true;
                    break;
                case WM_LBUTTONDBLCLK:
                    RaiseEvent(new RoutedEventArgs(TrayMouseDoubleClickEvent, this));
                    handled = true;
                    break;
                case WM_RBUTTONDOWN:
                    RaiseEvent(new RoutedEventArgs(TrayRightMouseDownEvent, this));
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private void OnTrayRightClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_trayPopup.IsOpen)
            {
                CloseMenu();
                return;
            }
            OpenMenu();
        }

        private void OnListBoxClick(object sender, MouseButtonEventArgs e)
        {
            var hit = _listBox.InputHitTest(e.GetPosition(_listBox)) as DependencyObject;
            while (hit != null && hit != _listBox)
            {
                if (hit is ListBoxItem lbi)
                {
                    if (lbi.Content is FrameworkElement fe && fe.Tag is UI4TrayMenuItem item)
                    {
                        bool canExec = item.CanExecute?.Invoke() ?? true;
                        if (canExec)
                        {
                            item.Command?.Invoke();
                            CloseMenu();
                        }
                        e.Handled = true;
                    }
                    break;
                }
                hit = VisualTreeHelper.GetParent(hit);
            }
        }

        private void CheckMouseOutsidePopup(object sender, EventArgs e)
        {
            if (!_trayPopup.IsOpen) return;
            System.Windows.Point mousePos = Mouse.GetPosition(_listBox);
            Rect menuBounds = VisualTreeHelper.GetDescendantBounds(_listBox);
            if (!menuBounds.Contains(mousePos))
                CloseMenu();
        }

        private void OnTrayPopupClosed(object sender, EventArgs e)
        {
            _listBox.SelectedIndex = -1;
            UnhookCloseEvents();
            _closeCheckTimer.Stop();
        }

        private void HookCloseEvents()
        {
            UnhookCloseEvents();
            _hookedWindow = Application.Current?.MainWindow;
            if (_hookedWindow != null)
            {
                _hookedWindow.Deactivated += OnWindowDeactivated;
                _hookedWindow.LocationChanged += OnWindowLocationChanged;
                _hookedWindow.StateChanged += OnWindowStateChanged;
            }
        }

        private void UnhookCloseEvents()
        {
            if (_hookedWindow != null)
            {
                _hookedWindow.Deactivated -= OnWindowDeactivated;
                _hookedWindow.LocationChanged -= OnWindowLocationChanged;
                _hookedWindow.StateChanged -= OnWindowStateChanged;
                _hookedWindow = null;
            }
        }

        private void OnWindowDeactivated(object sender, EventArgs e) => CloseMenu();
        private void OnWindowLocationChanged(object sender, EventArgs e) => CloseMenu();
        private void OnWindowStateChanged(object sender, EventArgs e) => CloseMenu();
        private void OnApplicationExit(object sender, ExitEventArgs e) => Dispose();
        private void OnLoaded(object sender, RoutedEventArgs e) => CreateTrayIcon();
        private void OnUnloaded(object sender, RoutedEventArgs e) => RemoveTrayIcon();

        private static void OnIconSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (UI4NotifyIcon)d;
            if (icon._isIconCreated)
                icon.UpdateTrayIcon();
            else
                icon.CreateTrayIcon();
        }

        private static void OnToolTipTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (UI4NotifyIcon)d;
            icon.UpdateTrayIcon();
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (UI4NotifyIcon)d;
            if ((Visibility)e.NewValue == Visibility.Visible)
                icon.CreateTrayIcon();
            else
                icon.RemoveTrayIcon();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            TrayRightMouseDown -= OnTrayRightClick;
            CloseMenu();
            RemoveTrayIcon();
            if (_trayPopup != null)
                _trayPopup.Closed -= OnTrayPopupClosed;
            if (_listBox != null)
                _listBox.PreviewMouseLeftButtonUp -= OnListBoxClick;
            if (_closeCheckTimer != null)
                _closeCheckTimer.Tick -= CheckMouseOutsidePopup;
            if (Application.Current != null)
                Application.Current.Exit -= OnApplicationExit;
            ClearMenuItems();
            _icon?.Dispose();
            _icon = null;
            if (_messageSource != null)
            {
                _messageSource.RemoveHook(WndProc);
                _messageSource.Dispose();
                _messageSource = null;
            }
            GC.SuppressFinalize(this);
        }

        private static NotifyIconData CreateNotifyIconData(IntPtr handle)
        {
            return new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NotifyIconData)),
                WindowHandle = handle,
                TaskbarIconId = 0,
                CallbackMessageId = CallbackMessage,
                ValidMembers = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                IconHandle = IntPtr.Zero,
                ToolTipText = string.Empty,
                VersionOrTimeout = NOTIFYICON_VERSION_4
            };
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint cbSize;
            public IntPtr WindowHandle;
            public uint TaskbarIconId;
            public int ValidMembers;
            public uint CallbackMessageId;
            public IntPtr IconHandle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ToolTipText;
            public int IconState;
            public int StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string BalloonText;
            public uint VersionOrTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string BalloonTitle;
            public int BalloonFlags;
            public Guid TaskbarIconGuid;
            public IntPtr CustomBalloonIconHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int message, [In] ref NotifyIconData data);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}
