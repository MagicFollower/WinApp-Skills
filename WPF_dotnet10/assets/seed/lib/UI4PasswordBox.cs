using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的密码输入控件，支持密码遮罩显示切换、占位符文本和自定义样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.TextBox"/>，提供以下自定义属性：</para>
    /// <list type="bullet">
    ///   <item><see cref="Password"/> — 密码字符串（注意：以明文存储在内存中）</item>
    ///   <item><see cref="PasswordChar"/> — 密码遮罩字符</item>
    ///   <item><see cref="IsPasswordMode"/> — 是否处于密码模式</item>
    ///   <item><see cref="ShowPasswordButton"/> — 是否显示密码可见性切换按钮</item>
    ///   <item><see cref="PlaceholderText"/> / <see cref="PlaceholderForeground"/> — 占位符</item>
    /// </list>
    /// <para>提供 <see cref="ClearPassword()"/> 方法用于安全地清除密码。</para>
    /// </remarks>
    public class UI4PasswordBox : TextBox
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(UI4PasswordBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty BorderNormalColorProperty =
            DependencyProperty.Register(nameof(BorderNormalColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(200, 200, 220)));
        public Color BorderNormalColor
        {
            get => (Color)GetValue(BorderNormalColorProperty);
            set => SetValue(BorderNormalColorProperty, value);
        }

        public static readonly DependencyProperty HoverBorderColorProperty =
            DependencyProperty.Register(nameof(HoverBorderColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(0, 120, 212)));
        public Color HoverBorderColor
        {
            get => (Color)GetValue(HoverBorderColorProperty);
            set => SetValue(HoverBorderColorProperty, value);
        }

        public static readonly DependencyProperty FocusBorderColorProperty =
            DependencyProperty.Register(nameof(FocusBorderColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(0, 102, 181)));
        public Color FocusBorderColor
        {
            get => (Color)GetValue(FocusBorderColorProperty);
            set => SetValue(FocusBorderColorProperty, value);
        }

        public static readonly DependencyProperty EditBackgroundProperty =
            DependencyProperty.Register(nameof(EditBackground), typeof(Brush), typeof(UI4PasswordBox),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 255, 255))));
        public Brush EditBackground
        {
            get => (Brush)GetValue(EditBackgroundProperty);
            set => SetValue(EditBackgroundProperty, value);
        }

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(nameof(TextColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(30, 30, 30)));
        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public static readonly DependencyProperty InnerPaddingProperty =
            DependencyProperty.Register(nameof(InnerPadding), typeof(Thickness), typeof(UI4PasswordBox),
                new PropertyMetadata(new Thickness(12, 5, 32, 5), OnStyleRefresh));
        public Thickness InnerPadding
        {
            get => (Thickness)GetValue(InnerPaddingProperty);
            set => SetValue(InnerPaddingProperty, value);
        }

        public static readonly DependencyProperty ShowPasswordButtonProperty =
            DependencyProperty.Register(nameof(ShowPasswordButton), typeof(bool), typeof(UI4PasswordBox),
                new PropertyMetadata(true, OnStyleRefresh));
        public bool ShowPasswordButton
        {
            get => (bool)GetValue(ShowPasswordButtonProperty);
            set => SetValue(ShowPasswordButtonProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(UI4PasswordBox),
                new PropertyMetadata(string.Empty, OnStyleRefresh));
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderForegroundProperty =
            DependencyProperty.Register(nameof(PlaceholderForeground), typeof(Brush), typeof(UI4PasswordBox),
                new PropertyMetadata(new SolidColorBrush(Colors.LightGray)));
        public Brush PlaceholderForeground
        {
            get => (Brush)GetValue(PlaceholderForegroundProperty);
            set => SetValue(PlaceholderForegroundProperty, value);
        }

        /// <summary>
        /// 获取或设置密码字符串。
        /// </summary>
        /// <remarks>
        /// <para><b>安全注意事项：</b></para>
        /// <para>此属性使用普通 <see cref="string"/> 存储密码，密码会以明文形式保留在内存中，
        /// 可能被内存转储或调试工具读取。对于高安全性场景，建议考虑使用 WPF 原生的
        /// <see cref="System.Windows.Controls.PasswordBox"/>，它使用不安全的内存存储来保护密码。</para>
        /// <para>此属性支持数据绑定，但请注意绑定目标可能会暴露密码值。</para>
        /// </remarks>
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register(nameof(Password), typeof(string), typeof(UI4PasswordBox),
                new PropertyMetadata(string.Empty, OnPasswordChanged));
        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        public static readonly DependencyProperty PasswordCharProperty =
            DependencyProperty.Register(nameof(PasswordChar), typeof(char), typeof(UI4PasswordBox),
                new PropertyMetadata('●', OnStyleRefresh));
        public char PasswordChar
        {
            get => (char)GetValue(PasswordCharProperty);
            set => SetValue(PasswordCharProperty, value);
        }

        public static readonly DependencyProperty IsPasswordModeProperty =
            DependencyProperty.Register(nameof(IsPasswordMode), typeof(bool), typeof(UI4PasswordBox),
                new PropertyMetadata(true, OnPasswordModeChanged));
        public bool IsPasswordMode
        {
            get => (bool)GetValue(IsPasswordModeProperty);
            set => SetValue(IsPasswordModeProperty, value);
        }

        /// <summary>显示/隐藏明文按钮常态色，跟随主题令牌 UI4.Color.Icon。</summary>
        private static readonly DependencyProperty RevealIconColorProperty =
            DependencyProperty.Register(nameof(RevealIconColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(110, 110, 120)));
        private Color RevealIconColor
        {
            get => (Color)GetValue(RevealIconColorProperty);
            set => SetValue(RevealIconColorProperty, value);
        }

        /// <summary>显示/隐藏明文按钮悬停色，跟随主题令牌 UI4.Color.IconHover。</summary>
        private static readonly DependencyProperty RevealIconHoverColorProperty =
            DependencyProperty.Register(nameof(RevealIconHoverColor), typeof(Color), typeof(UI4PasswordBox),
                new PropertyMetadata(Color.FromRgb(0, 102, 181)));
        private Color RevealIconHoverColor
        {
            get => (Color)GetValue(RevealIconHoverColorProperty);
            set => SetValue(RevealIconHoverColorProperty, value);
        }

        private static Brush CreateDefaultRevealBrush()
        {
            // 从资源桥取 Icon 令牌再压 60% 不透明度；每次调用都重读，故交互时总是当前主题
            var app = Application.Current;
            Color icon = (app != null ? app.TryFindResource("UI4.Color.Icon") as Color? : null) ?? Colors.Gray;
            var brush = new SolidColorBrush(Color.FromArgb(150, icon.R, icon.G, icon.B));
            brush.Freeze();
            return brush;
        }

        private static void OnPasswordModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = d as UI4PasswordBox;
            if (box != null)
            {
                box.UpdateDisplay();
                box.RebuildContextMenu();
            }
        }

        private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = d as UI4PasswordBox;
            if (box != null)
            {
                box._password = e.NewValue as string ?? string.Empty;
                if (box.IsPasswordMode)
                    box.UpdateDisplay();
            }
        }

        private static void OnStyleRefresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4PasswordBox box) box.Style = box.BuildEditStyle();
        }

        private ScrollViewer _scrollViewer;
        private ScrollBar _verticalScrollBar;
        private DispatcherTimer _fadeTimer;
        private EventHandler _fadeTimerTickHandler;
        private UI4ContextMenu _contextMenu;
        /// <summary>
        /// 内部密码字段。注意：此字段以明文存储密码，存在安全风险。
        /// </summary>
        private string _password = string.Empty;
        private int? _pendingCaretIndex;
        private bool _showPlainText = false;
        private bool _suppressTextChanged = false;

        static UI4PasswordBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4PasswordBox),
                new FrameworkPropertyMetadata(typeof(UI4PasswordBox)));
        }

        public UI4PasswordBox()
        {
            FontSize = 15d;
            Cursor = Cursors.IBeam;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            ScrollBarResources.MergeInto(Resources);

            Style = BuildEditStyle();
            Loaded += UI4PasswordBox_Loaded;
            Unloaded += UI4PasswordBox_Unloaded;
            LostFocus += (s, e) => HidePlainText();

            SetResourceReference(TextColorProperty, "UI4.Color.TextForeground");
            SetResourceReference(BorderNormalColorProperty, "UI4.Color.BorderNormal");
            SetResourceReference(HoverBorderColorProperty, "UI4.Color.BorderHover");
            SetResourceReference(FocusBorderColorProperty, "UI4.Color.BorderFocus");
            SetResourceReference(EditBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(PlaceholderForegroundProperty, "UI4.Brush.Placeholder");
            SetResourceReference(RevealIconColorProperty, "UI4.Color.Icon");
            SetResourceReference(RevealIconHoverColorProperty, "UI4.Color.IconHover");

            PreviewTextInput += OnPreviewTextInput;
            PreviewKeyDown += OnPreviewKeyDown;
            TextChanged += OnTextChanged;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted, OnPasteCanExecute));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyExecuted, OnCopyCanExecute));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnCutExecuted, OnCutCanExecute));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_ContentHost") as ScrollViewer;
            if (_scrollViewer != null)
            {
                _scrollViewer.ApplyTemplate();
                _verticalScrollBar = _scrollViewer.Template.FindName("PART_VerticalScrollBar", _scrollViewer) as ScrollBar;
                if (_verticalScrollBar != null)
                {
                    _verticalScrollBar.MouseEnter += OnScrollBarMouseEnter;
                    _verticalScrollBar.MouseLeave += OnScrollBarMouseLeave;
                }
                _scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            }
        }

        private void UI4PasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            RebuildContextMenu();
            if (_scrollViewer == null)
            {
                _scrollViewer = GetTemplateChild("PART_ContentHost") as ScrollViewer;
                if (_scrollViewer != null)
                {
                    _scrollViewer.ApplyTemplate();
                    _verticalScrollBar = _scrollViewer.Template.FindName("PART_VerticalScrollBar", _scrollViewer) as ScrollBar;
                    if (_verticalScrollBar != null)
                    {
                        _verticalScrollBar.MouseEnter += OnScrollBarMouseEnter;
                        _verticalScrollBar.MouseLeave += OnScrollBarMouseLeave;
                    }
                    _scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
                }
            }
            if (IsPasswordMode)
                UpdateDisplay();
        }

        private void UI4PasswordBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;
            if (_verticalScrollBar != null)
            {
                _verticalScrollBar.MouseEnter -= OnScrollBarMouseEnter;
                _verticalScrollBar.MouseLeave -= OnScrollBarMouseLeave;
            }
            _fadeTimer?.Stop();
            _fadeTimer = null;
            _fadeTimerTickHandler = null;
            
            // 安全改进：在控件卸载时清除密码，减少内存中密码暴露的时间窗口
            _password = string.Empty;
        }

        private void OnScrollBarMouseEnter(object sender, MouseEventArgs e)
        {
            _fadeTimer?.Stop();
            _verticalScrollBar?.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private void OnScrollBarMouseLeave(object sender, MouseEventArgs e)
        {
            if (_verticalScrollBar != null && _verticalScrollBar.Opacity > 0.9)
            {
                _verticalScrollBar.BeginAnimation(UIElement.OpacityProperty, null);
                _verticalScrollBar.Opacity = 0.4;
                StartFadeTimer();
            }
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_verticalScrollBar == null)
            {
                if (_scrollViewer != null)
                {
                    _scrollViewer.ApplyTemplate();
                    _verticalScrollBar = _scrollViewer.Template.FindName("PART_VerticalScrollBar", _scrollViewer) as ScrollBar;
                    if (_verticalScrollBar != null)
                    {
                        _verticalScrollBar.MouseEnter += OnScrollBarMouseEnter;
                        _verticalScrollBar.MouseLeave += OnScrollBarMouseLeave;
                    }
                }
                if (_verticalScrollBar == null) return;
            }
            _verticalScrollBar.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.4, TimeSpan.FromSeconds(0.25)) { FillBehavior = FillBehavior.HoldEnd });
            StartFadeTimer();
        }

        private void StartFadeTimer()
        {
            _fadeTimer?.Stop();
            if (_fadeTimerTickHandler != null && _fadeTimer != null)
                _fadeTimer.Tick -= _fadeTimerTickHandler;

            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _fadeTimerTickHandler = (s, args) =>
            {
                _fadeTimer.Stop();
                if (_verticalScrollBar != null && !_verticalScrollBar.IsMouseOver)
                {
                    _verticalScrollBar.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0, TimeSpan.FromSeconds(0.5)) { FillBehavior = FillBehavior.HoldEnd });
                }
                else if (_verticalScrollBar != null && _verticalScrollBar.IsMouseOver)
                {
                    StartFadeTimer();
                }
            };
            _fadeTimer.Tick += _fadeTimerTickHandler;
            _fadeTimer.Start();
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsPasswordMode) return;
            e.Handled = true;
            string input = e.Text;
            if (string.IsNullOrEmpty(input)) return;
            ProcessTextInput(input);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsPasswordMode)
            {
                if (ClipboardCommandTakeover.TryHandleKey(this, e)) e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                e.Handled = true;
                ProcessBackspace();
            }
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                ProcessDelete();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SelectAll();
            }
            else if ((e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
            {
                e.Handled = true;
                PasteClipboard();
            }
            else if ((e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
                     (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
            {
                e.Handled = true;
            }
        }

        private void ProcessTextInput(string input)
        {
            int start = Math.Max(0, Math.Min(_password.Length, SelectionStart));
            int length = Math.Max(0, Math.Min(_password.Length - start, SelectionLength));
            string current = _password;
            if (length > 0)
                current = current.Remove(start, length);
            current = current.Insert(start, input);
            int newPos = start + input.Length;
            _password = current;
            Password = current;
            _pendingCaretIndex = newPos;
            UpdateDisplay();
        }

        private void ProcessBackspace()
        {
            int start = Math.Max(0, Math.Min(_password.Length, SelectionStart));
            int length = Math.Max(0, Math.Min(_password.Length - start, SelectionLength));
            string current = _password;
            if (length > 0)
            {
                current = current.Remove(start, length);
                _pendingCaretIndex = start;
            }
            else if (start > 0)
            {
                current = current.Remove(start - 1, 1);
                _pendingCaretIndex = start - 1;
            }
            else
                return;
            _password = current;
            Password = current;
            UpdateDisplay();
        }

        private void ProcessDelete()
        {
            int start = Math.Max(0, Math.Min(_password.Length, SelectionStart));
            int length = Math.Max(0, Math.Min(_password.Length - start, SelectionLength));
            string current = _password;
            if (length > 0)
            {
                current = current.Remove(start, length);
                _pendingCaretIndex = start;
            }
            else if (start < current.Length)
            {
                current = current.Remove(start, 1);
                _pendingCaretIndex = start;
            }
            else
                return;
            _password = current;
            Password = current;
            UpdateDisplay();
        }

        private void PasteClipboard()
        {
            UI4Clipboard.TryGetTextAsync(delegate(string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    ProcessTextInput(text);
                }
            });
        }

        private void UpdateDisplay()
        {
            if (IsPasswordMode && !_showPlainText)
            {
                string mask = new string(PasswordChar, _password.Length);
                _suppressTextChanged = true;
                Text = mask;
                _suppressTextChanged = false;
                if (_pendingCaretIndex.HasValue)
                {
                    int idx = Math.Max(0, Math.Min(_password.Length, _pendingCaretIndex.Value));
                    SelectionStart = idx;
                    _pendingCaretIndex = null;
                }
            }
            else
            {
                _suppressTextChanged = true;
                Text = _password;
                _suppressTextChanged = false;
                if (_pendingCaretIndex.HasValue)
                {
                    int idx = Math.Max(0, Math.Min(_password.Length, _pendingCaretIndex.Value));
                    SelectionStart = idx;
                    _pendingCaretIndex = null;
                }
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            if (!IsPasswordMode)
            {
                _password = Text;
                Password = Text;
            }
        }

        private void ShowPlainText()
        {
            if (!IsPasswordMode) return;
            _showPlainText = true;
            IsPasswordMode = false;
            UpdateDisplay();
        }

        private void HidePlainText()
        {
            if (_showPlainText == false && IsPasswordMode) return;
            _showPlainText = false;
            IsPasswordMode = true;
            UpdateDisplay();
        }

        /// <summary>
        /// 清除密码并重置控件状态。
        /// </summary>
        /// <remarks>
        /// 调用此方法会清空内部密码存储，将 <see cref="Password"/> 属性设置为空字符串，
        /// 并清除显示文本。建议在用户完成登录或取消操作后调用此方法，
        /// 以减少密码在内存中的暴露时间。
        /// </remarks>
        public void ClearPassword()
        {
            _password = string.Empty;
            Password = string.Empty;
            Text = string.Empty;
            SelectionStart = 0;
            SelectionLength = 0;
        }

        private void OnRevealButtonMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ShowPlainText();
                var btn = sender as Button;
                if (btn != null)
                {
                    btn.SetResourceReference(ForegroundProperty, "UI4.Brush.Accent");
                    btn.CaptureMouse();
                }
                e.Handled = true;
            }
        }

        private void OnRevealButtonMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                var btn = sender as Button;
                if (btn != null)
                {
                    if (btn.IsMouseCaptured)
                        btn.ReleaseMouseCapture();
                    btn.Foreground = CreateDefaultRevealBrush();
                }
                HidePlainText();
                e.Handled = true;
            }
        }

        private void OnRevealButtonMouseLeave(object sender, MouseEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null && btn.IsMouseCaptured)
            {
                btn.ReleaseMouseCapture();
                btn.Foreground = CreateDefaultRevealBrush();
                HidePlainText();
            }
        }

        private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsPasswordMode)
            {
                PasteClipboard();
            }
            else
            {
                ClipboardCommandTakeover.Paste(this);
            }
            e.Handled = true;
        }

        private void OnPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsPasswordMode)
            {
                e.Handled = true;
                return;
            }

            ClipboardCommandTakeover.Copy(this);
            e.Handled = true;
        }

        private void OnCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsPasswordMode;
            e.Handled = true;
        }

        private void OnCutExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsPasswordMode)
            {
                e.Handled = true;
                return;
            }

            ClipboardCommandTakeover.Cut(this);
            e.Handled = true;
        }

        private void OnCutCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsPasswordMode;
            e.Handled = true;
        }

        private void RebuildContextMenu()
        {
            _contextMenu = null;
            ContextMenu = null;
            InitCustomMenu();
        }

        private void InitCustomMenu()
        {
            if (_contextMenu != null) return;

            _contextMenu = new UI4ContextMenu
            {
                Width = 170
            };

            _contextMenu.AddItem(UI4MenuItemType.Undo,
                () => { if (CanUndo) Undo(); },
                () => CanUndo);

            if (IsPasswordMode)
            {
                _contextMenu.AddItem(UI4MenuItemType.Paste,
                    () => PasteClipboard(),
                    () => UI4Clipboard.ContainsText());
                _contextMenu.AddItem(UI4MenuItemType.SelectAll,
                    () => SelectAll());
            }
            else
            {
                _contextMenu.AddItem(UI4MenuItemType.Cut,
                    () => ClipboardCommandTakeover.Cut(this),
                    () => !string.IsNullOrEmpty(SelectedText));
                _contextMenu.AddItem(UI4MenuItemType.Copy,
                    () => ClipboardCommandTakeover.Copy(this),
                    () => !string.IsNullOrEmpty(SelectedText));
                _contextMenu.AddItem(UI4MenuItemType.Paste,
                    () => ClipboardCommandTakeover.Paste(this),
                    () => UI4Clipboard.ContainsText());
                _contextMenu.AddItem(UI4MenuItemType.Delete,
                    () => { if (!string.IsNullOrEmpty(SelectedText)) SelectedText = string.Empty; },
                    () => !string.IsNullOrEmpty(SelectedText));
                _contextMenu.AddItem(UI4MenuItemType.SelectAll,
                    () => SelectAll());
            }

            ContextMenu = null;
            _contextMenu.Attach(this);
        }

        private Style BuildEditStyle()
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(ForegroundProperty, OwnColorBrushBinding(nameof(TextColor))));
            style.Setters.Add(new Setter(PaddingProperty, new Binding(nameof(InnerPadding)) { Source = this }));
            style.Setters.Add(new Setter(BackgroundProperty, new Binding(nameof(EditBackground)) { Source = this }));
            style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(BorderBrushProperty, OwnColorBrushBinding(nameof(BorderNormalColor))));
            style.Setters.Add(new Setter(CursorProperty, Cursors.IBeam));

            ControlTemplate template = new ControlTemplate(typeof(TextBox));
            FrameworkElementFactory borderRoot = new FrameworkElementFactory(typeof(Border));
            borderRoot.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BorderBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory placeholderText = new FrameworkElementFactory(typeof(TextBlock));
            placeholderText.SetBinding(TextBlock.TextProperty, new Binding(nameof(PlaceholderText)) { RelativeSource = RelativeSource.TemplatedParent });
            placeholderText.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(PlaceholderForeground)) { RelativeSource = RelativeSource.TemplatedParent });
            placeholderText.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { RelativeSource = RelativeSource.TemplatedParent });
            placeholderText.SetBinding(TextBlock.FontFamilyProperty, new Binding(nameof(FontFamily)) { RelativeSource = RelativeSource.TemplatedParent });
            placeholderText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            placeholderText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            placeholderText.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 0, 0, 0));
            placeholderText.SetValue(Panel.ZIndexProperty, 0);
            placeholderText.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Text))
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Converter = new PlaceholderVisibilityConverter()
            });
            grid.AppendChild(placeholderText);

            FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewer.Name = "PART_ContentHost";
            scrollViewer.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            scrollViewer.SetBinding(ScrollViewer.HorizontalScrollBarVisibilityProperty,
                new Binding(nameof(HorizontalScrollBarVisibility)) { RelativeSource = RelativeSource.TemplatedParent });
            scrollViewer.SetBinding(ScrollViewer.VerticalScrollBarVisibilityProperty,
                new Binding(nameof(VerticalScrollBarVisibility)) { RelativeSource = RelativeSource.TemplatedParent });
            scrollViewer.SetBinding(ScrollViewer.PaddingProperty, new Binding(nameof(Padding)) { RelativeSource = RelativeSource.TemplatedParent });
            grid.AppendChild(scrollViewer);

            Style revealBtnStyle = new Style(typeof(Button));
            revealBtnStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            revealBtnStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
            revealBtnStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            // 0.588 ≈ 原烘焙值 Color.FromArgb(150, IconColor) 的淡化观感；令牌色本身不透明，用 Opacity 还原。
            revealBtnStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.588d));
            revealBtnStyle.Setters.Add(new Setter(Button.ForegroundProperty, OwnColorBrushBinding(nameof(RevealIconColor))));
            revealBtnStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(0, 0, 10, 0)));

            ControlTemplate revealBtnTemplate = new ControlTemplate(typeof(Button));
            FrameworkElementFactory revealPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            revealPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            revealPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            FrameworkElementFactory revealBg = new FrameworkElementFactory(typeof(Border));
            revealBg.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            revealBg.AppendChild(revealPresenter);
            revealBtnTemplate.VisualTree = revealBg;
            revealBtnStyle.Setters.Add(new Setter(Button.TemplateProperty, revealBtnTemplate));

            Trigger revealHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            revealHoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, OwnColorBrushBinding(nameof(RevealIconHoverColor))));
            revealHoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 1d));
            revealBtnStyle.Triggers.Add(revealHoverTrigger);

            FrameworkElementFactory revealBtn = new FrameworkElementFactory(typeof(Button));
            revealBtn.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            revealBtn.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            revealBtn.SetValue(Button.WidthProperty, 26d);
            revealBtn.SetValue(Button.HeightProperty, double.NaN);
            revealBtn.SetValue(Button.StyleProperty, revealBtnStyle);
            revealBtn.SetBinding(Button.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            revealBtn.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ShowPasswordButton))
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Converter = new BoolToVisibilityConverter()
            });
            revealBtn.SetValue(Panel.ZIndexProperty, 1);

            FrameworkElementFactory eyeIcon = new FrameworkElementFactory(typeof(TextBlock));
            eyeIcon.SetValue(TextBlock.TextProperty, "\xE052");
            eyeIcon.SetValue(TextBlock.FontSizeProperty, 18d);
            eyeIcon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            eyeIcon.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            eyeIcon.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            revealBtn.AppendChild(eyeIcon);

            revealBtn.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnRevealButtonMouseDown));
            revealBtn.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnRevealButtonMouseUp));
            revealBtn.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnRevealButtonMouseLeave));

            grid.AppendChild(revealBtn);

            borderRoot.AppendChild(grid);
            template.VisualTree = borderRoot;
            style.Setters.Add(new Setter(TemplateProperty, template));

            Trigger focusTrigger = new Trigger { Property = IsFocusedProperty, Value = true };
            focusTrigger.Setters.Add(new Setter(BorderBrushProperty, OwnColorBrushBinding(nameof(FocusBorderColor))));
            focusTrigger.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1.2)));
            style.Triggers.Add(focusTrigger);

            Trigger hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, OwnColorBrushBinding(nameof(HoverBorderColor))));
            style.Triggers.Add(hoverTrigger);

            return style;
        }

        private Binding OwnColorBrushBinding(string path)
        {
            return new Binding(path)
            {
                Source = this,
                Converter = ColorToBrushConverter.Instance
            };
        }
    }
}