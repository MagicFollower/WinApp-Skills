using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{

    /// <summary>
    /// 列表样式类型枚举。
    /// </summary>
    public enum ListStyleType
    {
        None,
        Disc,
        Number
    }

    /// <summary>
    /// 将索引值转换为从 1 开始的序号字符串的转换器。
    /// </summary>
    public class IndexPlusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
                return (index + 1).ToString();
            return "1";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// 现代风格的列表框控件，支持自定义滚动条样式和列表样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.ListBox"/>，提供自定义 ScrollViewer 样式和滚动条样式。</para>
    /// </remarks>
    public class UI4ListBox : ListBox
    {

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(UI4ListBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty BorderNormalColorProperty =
            DependencyProperty.Register(
                nameof(BorderNormalColor),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromRgb (37, 99, 235), OnStyleRefresh));

        public Color BorderNormalColor
        {
            get => (Color)GetValue(BorderNormalColorProperty);
            set => SetValue(BorderNormalColorProperty, value);
        }

        public static readonly DependencyProperty PanelBackgroundProperty =
            DependencyProperty.Register(
                nameof(PanelBackground),
                typeof(Brush),
                typeof(UI4ListBox),
                new PropertyMetadata(Brushes.White, OnStyleRefresh));

        public Brush PanelBackground
        {
            get => (Brush)GetValue(PanelBackgroundProperty);
            set => SetValue(PanelBackgroundProperty, value);
        }

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(
                nameof(TextColor),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromArgb(255, 0, 0, 0), OnStyleRefresh));

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public static readonly DependencyProperty ItemPaddingProperty =
            DependencyProperty.Register(
                nameof(ItemPadding),
                typeof(Thickness),
                typeof(UI4ListBox),
                new PropertyMetadata(new Thickness(12, 8, 12, 8), OnStyleRefresh));

        public Thickness ItemPadding
        {
            get => (Thickness)GetValue(ItemPaddingProperty);
            set => SetValue(ItemPaddingProperty, value);
        }

        public static readonly DependencyProperty ItemCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ItemCornerRadius),
                typeof(CornerRadius),
                typeof(UI4ListBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));

        public CornerRadius ItemCornerRadius
        {
            get => (CornerRadius)GetValue(ItemCornerRadiusProperty);
            set => SetValue(ItemCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty HoverBackgroundProperty =
            DependencyProperty.Register(
                nameof(HoverBackground),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromArgb(10, 245, 255, 255), OnStyleRefresh));
        public Color HoverBackground
        {
            get => (Color)GetValue(HoverBackgroundProperty);
            set => SetValue(HoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty HoverForegroundProperty =
            DependencyProperty.Register(
                nameof(HoverForeground),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromArgb(220, 0, 0, 0), OnStyleRefresh));
        public Color HoverForeground
        {
            get => (Color)GetValue(HoverForegroundProperty);
            set => SetValue(HoverForegroundProperty, value);
        }

        public static readonly DependencyProperty PressedBackgroundProperty =
            DependencyProperty.Register(
                nameof(PressedBackground),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromRgb (37, 99, 235), OnStyleRefresh));
        public Color PressedBackground
        {
            get => (Color)GetValue(PressedBackgroundProperty);
            set => SetValue(PressedBackgroundProperty, value);
        }

        public static readonly DependencyProperty PressedForegroundProperty =
            DependencyProperty.Register(
                nameof(PressedForeground),
                typeof(Color),
                typeof(UI4ListBox),
                new PropertyMetadata(Color.FromRgb(255, 255, 255), OnStyleRefresh));
        public Color PressedForeground
        {
            get => (Color)GetValue(PressedForegroundProperty);
            set => SetValue(PressedForegroundProperty, value);
        }

        public static readonly DependencyProperty ListStyleTypeProperty =
            DependencyProperty.Register(
                nameof(ListStyleType),
                typeof(ListStyleType),
                typeof(UI4ListBox),
                new PropertyMetadata(ListStyleType.None, OnStyleRefresh));
        public ListStyleType ListStyleType
        {
            get => (ListStyleType)GetValue(ListStyleTypeProperty);
            set => SetValue(ListStyleTypeProperty, value);
        }

        public static readonly DependencyProperty NumberCircleBackgroundProperty =
            DependencyProperty.Register(
                nameof(NumberCircleBackground),
                typeof(Brush),
                typeof(UI4ListBox),
                new PropertyMetadata(CreateDefaultCircleBrush(), OnStyleRefresh));
        public Brush NumberCircleBackground
        {
            get => (Brush)GetValue(NumberCircleBackgroundProperty);
            set => SetValue(NumberCircleBackgroundProperty, value);
        }

        private static Brush CreateDefaultCircleBrush()
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb (37, 99, 235));
            brush.Freeze();
            return brush;
        }

        public static Style _scrollViewerStyle;

        private static void OnStyleRefresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4ListBox list)
                list.Style = list.BuildListStyle();
        }

        static UI4ListBox()
        {
            BorderThicknessProperty.OverrideMetadata(typeof(UI4ListBox),
                new FrameworkPropertyMetadata(new Thickness(0), OnBorderThicknessChanged));

            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4ListBox),
                new FrameworkPropertyMetadata(typeof(UI4ListBox)));

            _scrollViewerStyle = ScrollBarResources.GetScrollViewerStyle();
        }

        private static void OnBorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4ListBox list)
                list.Style = list.BuildListStyle();
        }

        public UI4ListBox()
        {
            SetResourceReference(FontSizeProperty, "UI4.Font.Size.Base");
            Style = BuildListStyle();

            // 声明式跟随主题：文本/边框/面板底/悬停底都挂令牌，切换主题触发 OnStyleRefresh 重建
            SetResourceReference(TextColorProperty, "UI4.Color.TextForeground");
            SetResourceReference(BorderNormalColorProperty, "UI4.Color.BorderNormal");
            SetResourceReference(PanelBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(HoverBackgroundProperty, "UI4.Color.HoverOverlay");
        }

        /// <summary>立即按当前主题刷新（用于未挂载到可视树、收不到 Loaded 的场景，如 Popup 预构建内容）。</summary>
        public void RefreshTheme()
        {
            // 颜色已改由资源引用驱动，此处只需重建 Style；保留方法以兼容既有调用方
            Style = BuildListStyle();
        }

        private Style BuildListStyle()
        {
            Style style = new Style(typeof(ListBox));
            style.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(TextColor)));
            style.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(BorderNormalColor)));
            style.Setters.Add(new Setter(BackgroundProperty, PanelBackground));
            style.Setters.Add(new Setter(ItemsControl.AlternationCountProperty, int.MaxValue));

            ControlTemplate template = new ControlTemplate(typeof(ListBox));
            FrameworkElementFactory rootBorder = new FrameworkElementFactory(typeof(Border));
            rootBorder.Name = "PART_MainBorder";
            rootBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            rootBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            rootBorder.SetBinding(Border.BorderBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
            rootBorder.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            if (_scrollViewerStyle != null)
                scrollViewer.SetValue(FrameworkElement.StyleProperty, _scrollViewerStyle);
            scrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scrollViewer.SetValue(ScrollViewer.PaddingProperty, new Thickness(4, 4, 4, 4));
            scrollViewer.SetValue(FrameworkElement.FocusableProperty, false);
            scrollViewer.SetValue(Control.BorderThicknessProperty, new Thickness(0));

            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            scrollViewer.AppendChild(itemsPresenter);
            rootBorder.AppendChild(scrollViewer);
            template.VisualTree = rootBorder;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));

            Style itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            itemStyle.Setters.Add(new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty,
                new Binding(nameof(Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1) }));

            Trigger hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            // 触发器里的画刷必须走绑定而不是 SolidColorBrush 快照：使用方（如 UI4NavigationView）
            // 会把 ItemContainerStyle 赋成本地值，之后重建 Style 也覆盖不回来，快照就永久冻在首次取值那一刻。
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty,
                new Binding(nameof(HoverForeground))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1),
                    Converter = Internal.ColorToBrushConverter.Instance
                }));
            itemStyle.Triggers.Add(hoverTrigger);

            Trigger selectedTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty,
                new Binding(nameof(PressedForeground))
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1),
                    Converter = Internal.ColorToBrushConverter.Instance
                }));
            itemStyle.Triggers.Add(selectedTrigger);

            itemStyle.Setters.Add(new Setter(ListBoxItem.TagProperty, string.Empty));

            itemStyle.Setters.Add(new EventSetter(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is ListBoxItem item && !item.IsSelected)
                {
                    item.Tag = "Pressed";
                    item.Foreground = new SolidColorBrush(PressedForeground);
                }
            })));
            itemStyle.Setters.Add(new EventSetter(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (s is ListBoxItem item && item.Tag is string tag && tag == "Pressed")
                {
                    item.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        item.ClearValue(ListBoxItem.TagProperty);
                        item.ClearValue(Control.ForegroundProperty);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            })));
            itemStyle.Setters.Add(new EventSetter(UIElement.MouseLeaveEvent, new MouseEventHandler((s, e) =>
            {
                if (s is ListBoxItem item && item.Tag is string tag && tag == "Pressed")
                {
                    item.ClearValue(ListBoxItem.TagProperty);
                    item.ClearValue(Control.ForegroundProperty);
                }
            })));

            ControlTemplate itemTemplate = new ControlTemplate(typeof(ListBoxItem));
            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "PART_ItemBorder";
            itemBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(ItemCornerRadius))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1)
            });
            itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemBorder.SetValue(Border.MarginProperty, new Thickness(2, 2, 2, 2));
            itemBorder.SetBinding(Border.PaddingProperty, new Binding(nameof(ItemPadding))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1)
            });

            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.Name = "PART_ItemContent";
            contentPresenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            contentPresenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = RelativeSource.TemplatedParent });

            contentPresenter.SetBinding(TextElement.ForegroundProperty,
                new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1) });
            contentPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory contentPanel = new FrameworkElementFactory(typeof(StackPanel));
            contentPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            contentPanel.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            if (ListStyleType == ListStyleType.Disc)
            {
                FrameworkElementFactory dot = new FrameworkElementFactory(typeof(Ellipse));
                dot.SetValue(Shape.FillProperty, new SolidColorBrush(TextColor));
                dot.SetValue(FrameworkElement.WidthProperty, 6.0);
                dot.SetValue(FrameworkElement.HeightProperty, 6.0);
                dot.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
                dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                contentPanel.AppendChild(dot);
            }
            else if (ListStyleType == ListStyleType.Number)
            {
                FrameworkElementFactory numberCircle = new FrameworkElementFactory(typeof(Border));
                numberCircle.SetValue(Border.WidthProperty, 26.0);
                numberCircle.SetValue(Border.HeightProperty, 26.0);
                numberCircle.SetValue(Border.CornerRadiusProperty, new CornerRadius(16.0));
                numberCircle.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
                numberCircle.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
                numberCircle.SetBinding(Border.BackgroundProperty,
                    new Binding(nameof(NumberCircleBackground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListBox), 1) });
                FrameworkElementFactory numberText = new FrameworkElementFactory(typeof(TextBlock));
                numberText.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("UI4.Brush.OnAccent"));
                numberText.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
                numberText.SetValue(TextBlock.FontSizeProperty, 13.0);
                numberText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                numberText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                numberText.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
                numberText.SetBinding(TextBlock.TextProperty,
                    new Binding("(ItemsControl.AlternationIndex)") { RelativeSource = RelativeSource.TemplatedParent, Converter = new IndexPlusOneConverter() });
                numberCircle.AppendChild(numberText);
                contentPanel.AppendChild(numberCircle);
            }

            contentPanel.AppendChild(contentPresenter);
            itemBorder.AppendChild(contentPanel);
            itemTemplate.VisualTree = itemBorder;

            Trigger hoverBgTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverBgTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(HoverBackground)) { TargetName = "PART_ItemBorder" });
            itemTemplate.Triggers.Add(hoverBgTrigger);

            Trigger selectedBgTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            selectedBgTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(PressedBackground)) { TargetName = "PART_ItemBorder" });
            itemTemplate.Triggers.Add(selectedBgTrigger);

            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));

            style.Setters.Add(new Setter(ListBox.ItemContainerStyleProperty, itemStyle));
            return style;
        }
    }
}
