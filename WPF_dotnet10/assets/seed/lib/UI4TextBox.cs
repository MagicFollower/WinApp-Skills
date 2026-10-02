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
    /// 现代风格的文本输入控件，支持圆角、占位符文本、自定义边框和滚动条样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.TextBox"/>，提供以下自定义属性：</para>
    /// <list type="bullet">
    ///   <item><see cref="CornerRadius"/> — 圆角半径</item>
    ///   <item><see cref="BorderNormalColor"/> / <see cref="HoverBorderColor"/> / <see cref="FocusBorderColor"/> — 边框颜色状态</item>
    ///   <item><see cref="PlaceholderText"/> / <see cref="PlaceholderForeground"/> — 占位符</item>
    ///   <item><see cref="TextColor"/> — 文字颜色</item>
    /// </list>
    /// </remarks>
    public class UI4TextBox : TextBox
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(UI4TextBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty BorderNormalColorProperty =
            DependencyProperty.Register(nameof(BorderNormalColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(200, 200, 220)));
        public Color BorderNormalColor
        {
            get => (Color)GetValue(BorderNormalColorProperty);
            set => SetValue(BorderNormalColorProperty, value);
        }

        public static readonly DependencyProperty HoverBorderColorProperty =
            DependencyProperty.Register(nameof(HoverBorderColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(0, 120, 212)));
        public Color HoverBorderColor
        {
            get => (Color)GetValue(HoverBorderColorProperty);
            set => SetValue(HoverBorderColorProperty, value);
        }

        public static readonly DependencyProperty FocusBorderColorProperty =
            DependencyProperty.Register(nameof(FocusBorderColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(0, 102, 181)));
        public Color FocusBorderColor
        {
            get => (Color)GetValue(FocusBorderColorProperty);
            set => SetValue(FocusBorderColorProperty, value);
        }

        public static readonly DependencyProperty EditBackgroundProperty =
            DependencyProperty.Register(nameof(EditBackground), typeof(Brush), typeof(UI4TextBox),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 255, 255))));
        public Brush EditBackground
        {
            get => (Brush)GetValue(EditBackgroundProperty);
            set => SetValue(EditBackgroundProperty, value);
        }

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(nameof(TextColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(30, 30, 30)));
        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public static readonly DependencyProperty InnerPaddingProperty =
            DependencyProperty.Register(nameof(InnerPadding), typeof(Thickness), typeof(UI4TextBox),
                new PropertyMetadata(new Thickness(12, 5, 32, 5), OnStyleRefresh));
        public Thickness InnerPadding
        {
            get => (Thickness)GetValue(InnerPaddingProperty);
            set => SetValue(InnerPaddingProperty, value);
        }

        public static readonly DependencyProperty ShowClearButtonProperty =
            DependencyProperty.Register(nameof(ShowClearButton), typeof(bool), typeof(UI4TextBox),
                new PropertyMetadata(false, OnStyleRefresh));
        public bool ShowClearButton
        {
            get => (bool)GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(UI4TextBox),
                new PropertyMetadata(string.Empty, OnStyleRefresh));
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderForegroundProperty =
            DependencyProperty.Register(nameof(PlaceholderForeground), typeof(Brush), typeof(UI4TextBox),
                new PropertyMetadata(new SolidColorBrush(Colors.LightGray)));
        public Brush PlaceholderForeground
        {
            get => (Brush)GetValue(PlaceholderForegroundProperty);
            set => SetValue(PlaceholderForegroundProperty, value);
        }

        /// <summary>清除按钮常态色，跟随主题令牌 UI4.Color.Icon。</summary>
        private static readonly DependencyProperty ClearIconColorProperty =
            DependencyProperty.Register(nameof(ClearIconColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(110, 110, 120)));
        private Color ClearIconColor
        {
            get => (Color)GetValue(ClearIconColorProperty);
            set => SetValue(ClearIconColorProperty, value);
        }

        /// <summary>清除按钮悬停色，跟随主题令牌 UI4.Color.IconHover。</summary>
        private static readonly DependencyProperty ClearIconHoverColorProperty =
            DependencyProperty.Register(nameof(ClearIconHoverColor), typeof(Color), typeof(UI4TextBox),
                new PropertyMetadata(Color.FromRgb(0, 102, 181)));
        private Color ClearIconHoverColor
        {
            get => (Color)GetValue(ClearIconHoverColorProperty);
            set => SetValue(ClearIconHoverColorProperty, value);
        }

        private static void OnStyleRefresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4TextBox edit) edit.Style = edit.BuildEditStyle();
        }

        private ScrollViewer _scrollViewer;
        private ScrollBar _verticalScrollBar;
        private DispatcherTimer _fadeTimer;
        private EventHandler _fadeTimerTickHandler;

        static UI4TextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4TextBox),
                new FrameworkPropertyMetadata(typeof(UI4TextBox)));
        }

        private UI4ContextMenu _contextMenu;

        public UI4TextBox()
        {
            FontSize = 15d;
            Cursor = Cursors.IBeam;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            ClipboardCommandTakeover.Install(this);

            ScrollBarResources.MergeInto(Resources);

            Style = BuildEditStyle();
            Loaded += UI4TextBox_Loaded;
            Unloaded += UI4TextBox_Unloaded;

            SetResourceReference(TextColorProperty, "UI4.Color.TextForeground");
            SetResourceReference(BorderNormalColorProperty, "UI4.Color.BorderNormal");
            SetResourceReference(HoverBorderColorProperty, "UI4.Color.BorderHover");
            SetResourceReference(FocusBorderColorProperty, "UI4.Color.BorderFocus");
            SetResourceReference(EditBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(PlaceholderForegroundProperty, "UI4.Brush.Placeholder");
            SetResourceReference(ClearIconColorProperty, "UI4.Color.Icon");
            SetResourceReference(ClearIconHoverColorProperty, "UI4.Color.IconHover");
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

        private void UI4TextBox_Unloaded(object sender, RoutedEventArgs e)
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

        private void UI4TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            InitCustomMenu();

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

            ContextMenu = null;
            _contextMenu.Attach(this);
        }

        private Style BuildEditStyle()
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(ForegroundProperty, OwnColorBrushBinding(nameof(TextColor))));
            style.Setters.Add(new Setter(BackgroundProperty, new Binding(nameof(EditBackground)) { Source = this }));
            style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(BorderBrushProperty, OwnColorBrushBinding(nameof(BorderNormalColor))));
            style.Setters.Add(new Setter(CursorProperty, Cursors.IBeam));
            style.Setters.Add(new Setter(MinHeightProperty, 30d));

            MultiBinding paddingBinding = new MultiBinding();
            paddingBinding.Bindings.Add(new Binding(nameof(InnerPadding)) { RelativeSource = RelativeSource.TemplatedParent });
            paddingBinding.Bindings.Add(new Binding(nameof(ShowClearButton)) { RelativeSource = RelativeSource.TemplatedParent });
            paddingBinding.Converter = new InnerPaddingConverter();
            style.Setters.Add(new Setter(PaddingProperty, paddingBinding));

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

            Style clearBtnStyle = new Style(typeof(Button));
            clearBtnStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            clearBtnStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
            clearBtnStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            // 0.588 ≈ 原烘焙值 Color.FromArgb(150, IconColor) 的淡化观感；令牌色本身是不透明的，用 Opacity 还原。
            clearBtnStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.588d));
            clearBtnStyle.Setters.Add(new Setter(Button.ForegroundProperty, OwnColorBrushBinding(nameof(ClearIconColor))));
            clearBtnStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(0, -5, 10, 0)));

            ControlTemplate clearBtnTemplate = new ControlTemplate(typeof(Button));
            FrameworkElementFactory clearBtnPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            clearBtnPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            clearBtnPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            FrameworkElementFactory clearBtnBg = new FrameworkElementFactory(typeof(Border));
            clearBtnBg.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            clearBtnBg.AppendChild(clearBtnPresenter);
            clearBtnTemplate.VisualTree = clearBtnBg;
            clearBtnStyle.Setters.Add(new Setter(Button.TemplateProperty, clearBtnTemplate));

            Trigger clearBtnHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            clearBtnHoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, OwnColorBrushBinding(nameof(ClearIconHoverColor))));
            clearBtnHoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 1d));
            clearBtnStyle.Triggers.Add(clearBtnHoverTrigger);

            FrameworkElementFactory clearBtn = new FrameworkElementFactory(typeof(Button));
            clearBtn.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            clearBtn.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            clearBtn.SetValue(Button.WidthProperty, 26d);
            clearBtn.SetValue(Button.HeightProperty, 20d);
            clearBtn.SetValue(Button.StyleProperty, clearBtnStyle);
            clearBtn.SetBinding(Button.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            clearBtn.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ShowClearButton))
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Converter = new BoolToVisibilityConverter()
            });
            clearBtn.SetValue(Panel.ZIndexProperty, 1);

            FrameworkElementFactory textX = new FrameworkElementFactory(typeof(TextBlock));
            textX.SetValue(TextBlock.TextProperty, "×");
            textX.SetValue(TextBlock.FontSizeProperty, 26d);
            textX.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textX.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            clearBtn.AppendChild(textX);
            clearBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) => Text = string.Empty));
            grid.AppendChild(clearBtn);

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

    /// <summary>
    /// 将布尔值转换为 <see cref="Visibility"/> 的转换器。
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 将占位符文本的可见性转换为 <see cref="Visibility"/> 的转换器。
    /// </summary>
    public class PlaceholderVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            return string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 将内边距值转换为 <see cref="Thickness"/> 的转换器。
    /// </summary>
    public class InnerPaddingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is Thickness padding && values[1] is bool showClear)
            {
                if (showClear)
                    return padding;
                else
                    return new Thickness(padding.Left, padding.Top, padding.Left, padding.Bottom);
            }
            return new Thickness(12, 5, 12, 5);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}