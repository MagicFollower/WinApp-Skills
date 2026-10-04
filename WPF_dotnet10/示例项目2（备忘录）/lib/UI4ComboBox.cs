using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的下拉组合框控件，支持圆角、自定义边框和滚动条样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.ComboBox"/>，提供以下自定义属性：</para>
    /// <list type="bullet">
    ///   <item><see cref="CornerRadius"/> — 圆角半径</item>
    ///   <item><see cref="BorderNormalColor"/> / <see cref="HoverBorderColor"/> / <see cref="FocusBorderColor"/> — 边框颜色状态</item>
    ///   <item><see cref="TextColor"/> — 文字颜色</item>
    ///   <item><see cref="EditBackground"/> — 编辑区域背景</item>
    /// </list>
    /// </remarks>
    public class UI4ComboBox : ComboBox
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(UI4ComboBox),
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
                typeof(UI4ComboBox),
                new PropertyMetadata(Color.FromRgb(200, 200, 220), OnStyleRefresh));
        public Color BorderNormalColor
        {
            get => (Color)GetValue(BorderNormalColorProperty);
            set => SetValue(BorderNormalColorProperty, value);
        }
        public static readonly DependencyProperty FocusGradientStartProperty =
            DependencyProperty.Register(
                nameof(FocusGradientStart),
                typeof(Color),
                typeof(UI4ComboBox),
                new PropertyMetadata(Color.FromRgb(0, 120, 212), OnStyleRefresh));
        public Color FocusGradientStart
        {
            get => (Color)GetValue(FocusGradientStartProperty);
            set => SetValue(FocusGradientStartProperty, value);
        }
        public static readonly DependencyProperty FocusGradientEndProperty =
            DependencyProperty.Register(
                nameof(FocusGradientEnd),
                typeof(Color),
                typeof(UI4ComboBox),
                new PropertyMetadata(Color.FromRgb(147, 51, 234), OnStyleRefresh));
        public Color FocusGradientEnd
        {
            get => (Color)GetValue(FocusGradientEndProperty);
            set => SetValue(FocusGradientEndProperty, value);
        }
        public static readonly DependencyProperty EditBackgroundProperty =
            DependencyProperty.Register(
                nameof(EditBackground),
                typeof(Brush),
                typeof(UI4ComboBox),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 255, 255)), OnStyleRefresh));
        public Brush EditBackground
        {
            get => (Brush)GetValue(EditBackgroundProperty);
            set => SetValue(EditBackgroundProperty, value);
        }
        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(
                nameof(TextColor),
                typeof(Color),
                typeof(UI4ComboBox),
                new PropertyMetadata(Color.FromRgb(30, 30, 30), OnStyleRefresh));
        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }
        public static readonly DependencyProperty InnerPaddingProperty =
            DependencyProperty.Register(
                nameof(InnerPadding),
                typeof(Thickness),
                typeof(UI4ComboBox),
                new PropertyMetadata(new Thickness(12, 4, 30, 4), OnStyleRefresh));
        public Thickness InnerPadding
        {
            get => (Thickness)GetValue(InnerPaddingProperty);
            set => SetValue(InnerPaddingProperty, value);
        }
        public static readonly DependencyProperty DropCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(DropCornerRadius),
                typeof(CornerRadius),
                typeof(UI4ComboBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));
        public CornerRadius DropCornerRadius
        {
            get => (CornerRadius)GetValue(DropCornerRadiusProperty);
            set => SetValue(DropCornerRadiusProperty, value);
        }
        private static bool _globalScrollResLoaded = false;
        private static void OnStyleRefresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4ComboBox cbx)
                cbx.Style = cbx.BuildComboStyle();
        }
        static UI4ComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4ComboBox),
                new FrameworkPropertyMetadata(typeof(UI4ComboBox)));
            LoadGlobalScrollResource();
        }
        private static void LoadGlobalScrollResource()
        {
            if (_globalScrollResLoaded) return;
            ScrollBarResources.MergeInto(Application.Current.Resources);
            _globalScrollResLoaded = true;
        }
        private static void OnDropDownPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
                ShowDropDownScrollBars(scrollViewer);
        }
        private static void OnDropDownScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!(sender is ScrollViewer scrollViewer) || (e.VerticalChange == 0 && e.HorizontalChange == 0))
                return;

            ShowDropDownScrollBars(scrollViewer);
        }
        private static void ShowDropDownScrollBars(ScrollViewer scrollViewer)
        {
            foreach (ScrollBar scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
            {
                scrollBar.BeginAnimation(OpacityProperty, null);
                scrollBar.Opacity = 0.6;
            }

            if (!(scrollViewer.Tag is DispatcherTimer timer))
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                timer.Tick += (sender, e) =>
                {
                    timer.Stop();
                    foreach (ScrollBar scrollBar in FindVisualChildren<ScrollBar>(scrollViewer))
                    {
                        scrollBar.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
                    }
                };
                scrollViewer.Tag = timer;
            }

            timer.Stop();
            timer.Start();
        }
        private static IEnumerable FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T matchedChild)
                    yield return matchedChild;

                foreach (T descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
        public UI4ComboBox()
        {
            SetResourceReference(FontSizeProperty, "UI4.Font.Size.Base");
            Style = BuildComboStyle();

            // 声明式跟随主题：文本/边框/编辑区底/焦点渐变两端都挂令牌，切换主题触发 OnStyleRefresh 重建
            SetResourceReference(TextColorProperty, "UI4.Color.TextForeground");
            SetResourceReference(BorderNormalColorProperty, "UI4.Color.BorderNormal");
            SetResourceReference(EditBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(FocusGradientStartProperty, "UI4.Color.Accent");
            SetResourceReference(FocusGradientEndProperty, "UI4.Color.AccentEnd");
        }

        private Style BuildComboStyle()
        {
            Style style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(TextColor)));
            style.Setters.Add(new Setter(PaddingProperty, InnerPadding));
            style.Setters.Add(new Setter(BackgroundProperty, EditBackground));
            style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1, 1, 1, 1)));
            style.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(BorderNormalColor)));
            style.Setters.Add(new Setter(ComboBox.MinWidthProperty, new Binding(nameof(ActualWidth)) { RelativeSource = RelativeSource.Self }));
            style.Setters.Add(new Setter(CursorProperty, Cursors.Hand));
            Style itemStyle = new Style(typeof(ComboBoxItem));
            ControlTemplate itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.SetBinding(Border.PaddingProperty, new Binding(nameof(Padding)) { RelativeSource = RelativeSource.TemplatedParent });
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetBinding(ContentPresenter.VerticalAlignmentProperty, new Binding(nameof(VerticalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
            contentPresenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new Binding(nameof(HorizontalContentAlignment)) { RelativeSource = RelativeSource.TemplatedParent });
            itemBorder.AppendChild(contentPresenter);
            itemTemplate.VisualTree = itemBorder;
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            Trigger itemHoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            itemHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("UI4.Brush.HoverOverlay")));
            itemHoverTrigger.Setters.Add(new Setter(CursorProperty, Cursors.Hand));
            itemStyle.Triggers.Add(itemHoverTrigger);
            Trigger itemSelectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            itemSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("UI4.Brush.SelectedOverlay")));
            itemSelectedTrigger.Setters.Add(new Setter(CursorProperty, Cursors.Hand));
            itemStyle.Triggers.Add(itemSelectedTrigger);
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));
            itemStyle.Setters.Add(new Setter(Control.HeightProperty, 36d));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 0, 0, 0)));
            itemStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("UI4.Brush.HoverOverlay")));
            hoverTrigger.Setters.Add(new Setter(CursorProperty, Cursors.Hand));
            itemStyle.Triggers.Add(hoverTrigger);
            style.Setters.Add(new Setter(ComboBox.ItemContainerStyleProperty, itemStyle));
            ControlTemplate template = new ControlTemplate(typeof(ComboBox));
            FrameworkElementFactory rootGrid = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory borderRoot = new FrameworkElementFactory(typeof(Border));
            borderRoot.Name = "PART_Border";
            borderRoot.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BorderBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = RelativeSource.TemplatedParent });
            borderRoot.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
            FrameworkElementFactory mainGrid = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory colDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            FrameworkElementFactory colDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            colDef2.SetValue(ColumnDefinition.WidthProperty, new GridLength(36d));
            mainGrid.AppendChild(colDef1);
            mainGrid.AppendChild(colDef2);
            Style toggleBtnStyle = new Style(typeof(ToggleButton));
            toggleBtnStyle.Setters.Add(new Setter(ToggleButton.BackgroundProperty, Brushes.Transparent));
            toggleBtnStyle.Setters.Add(new Setter(ToggleButton.BorderThicknessProperty, new Thickness(0)));
            toggleBtnStyle.Setters.Add(new Setter(ToggleButton.CursorProperty, Cursors.Hand));
            toggleBtnStyle.Setters.Add(new Setter(ToggleButton.ForegroundProperty, new DynamicResourceExtension("UI4.Brush.Icon")));
            ControlTemplate toggleBtnTemplate = new ControlTemplate(typeof(ToggleButton));
            FrameworkElementFactory toggleBtnBorder = new FrameworkElementFactory(typeof(Border));
            toggleBtnBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            toggleBtnBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            FrameworkElementFactory arrowPath = new FrameworkElementFactory(typeof(Path));
            arrowPath.SetValue(Path.DataProperty, Geometry.Parse("M 0 0 L 6 6 L 12 0"));
            arrowPath.SetValue(Path.StrokeProperty, new DynamicResourceExtension("UI4.Brush.Icon"));
            arrowPath.SetValue(Path.StrokeThicknessProperty, 1.5);
            arrowPath.SetValue(Path.FillProperty, Brushes.Transparent);
            arrowPath.SetValue(Path.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrowPath.SetValue(Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrowPath.SetValue(Path.RenderTransformProperty, new RotateTransform(0, 6, 3));
            arrowPath.Name = "ArrowPath";
            toggleBtnBorder.AppendChild(arrowPath);
            toggleBtnTemplate.VisualTree = toggleBtnBorder;
            Trigger toggleBtnHoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            toggleBtnHoverTrigger.Setters.Add(new Setter(Path.StrokeProperty, new DynamicResourceExtension("UI4.Brush.IconHover")) { TargetName = "ArrowPath" });
            toggleBtnTemplate.Triggers.Add(toggleBtnHoverTrigger);
            Trigger toggleBtnCheckedTrigger = new Trigger
            {
                Property = ToggleButton.IsCheckedProperty,
                Value = true
            };
            toggleBtnCheckedTrigger.Setters.Add(new Setter(Path.RenderTransformProperty, new RotateTransform(180, 6, 3)) { TargetName = "ArrowPath" });
            toggleBtnTemplate.Triggers.Add(toggleBtnCheckedTrigger);
            toggleBtnStyle.Setters.Add(new Setter(ToggleButton.TemplateProperty, toggleBtnTemplate));
            FrameworkElementFactory toggleBtn = new FrameworkElementFactory(typeof(ToggleButton));
            toggleBtn.SetValue(Grid.ColumnProperty, 1);
            toggleBtn.SetValue(ToggleButton.StyleProperty, toggleBtnStyle);
            toggleBtn.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(IsDropDownOpen)) { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.TwoWay });
            mainGrid.AppendChild(toggleBtn);
            FrameworkElementFactory contentHostGrid = new FrameworkElementFactory(typeof(Grid));
            contentHostGrid.SetValue(Grid.ColumnProperty, 0);
            contentHostGrid.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            // 选中项文本比控件宽时不得溢出到箭头列和边框外
            contentHostGrid.SetValue(UIElement.ClipToBoundsProperty, true);
            FrameworkElementFactory contentPresenter1 = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter1.Name = "PART_ContentPresenter";
            contentPresenter1.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            contentPresenter1.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter1.SetBinding(ContentPresenter.MarginProperty, new Binding(nameof(Padding)) { RelativeSource = RelativeSource.TemplatedParent });
            contentPresenter1.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(SelectionBoxItem)) { RelativeSource = RelativeSource.TemplatedParent });
            // 未显式指定 SelectionBoxItemTemplate 时使用默认模板：字符串单行省略号截断并带完整文本提示，
            // 非字符串内容仍交给 ContentPresenter 原样呈现
            contentPresenter1.SetBinding(ContentPresenter.ContentTemplateProperty,
                new Binding(nameof(SelectionBoxItemTemplate))
                {
                    RelativeSource = RelativeSource.TemplatedParent,
                    TargetNullValue = CreateDefaultSelectionTemplate()
                });
            contentHostGrid.AppendChild(contentPresenter1);

            // 非字符串选中内容不走截断模板，直接原样呈现（与上游行为一致）
            FrameworkElementFactory rawPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            rawPresenter.Name = "PART_RawContentPresenter";
            rawPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            rawPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            rawPresenter.SetBinding(ContentPresenter.MarginProperty, new Binding(nameof(Padding)) { RelativeSource = RelativeSource.TemplatedParent });
            rawPresenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(SelectionBoxItem)) { RelativeSource = RelativeSource.TemplatedParent });
            rawPresenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(nameof(SelectionBoxItemTemplate)) { RelativeSource = RelativeSource.TemplatedParent });
            rawPresenter.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(SelectionBoxItem)) { RelativeSource = RelativeSource.TemplatedParent, Converter = StringSelectionConverter.WhenNotString });
            contentHostGrid.AppendChild(rawPresenter);
            Style editableTextBoxStyle = new Style(typeof(TextBox));
            editableTextBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
            editableTextBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Transparent));
            editableTextBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(0)));
            editableTextBoxStyle.Setters.Add(new Setter(TextBox.FocusVisualStyleProperty, null));
            ControlTemplate editBoxTemplate = new ControlTemplate(typeof(TextBox));
            FrameworkElementFactory editBoxBorder = new FrameworkElementFactory(typeof(Border));
            editBoxBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            editBoxBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            FrameworkElementFactory editBoxScrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            editBoxScrollViewer.Name = "PART_ContentHost";
            editBoxScrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            editBoxScrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            editBoxBorder.AppendChild(editBoxScrollViewer);
            editBoxTemplate.VisualTree = editBoxBorder;
            editableTextBoxStyle.Setters.Add(new Setter(TextBox.TemplateProperty, editBoxTemplate));
            FrameworkElementFactory editableTextBox = new FrameworkElementFactory(typeof(TextBox));
            editableTextBox.Name = "PART_EditableTextBox";
            editableTextBox.SetValue(TextBox.VisibilityProperty, Visibility.Collapsed);
            editableTextBox.SetValue(TextBox.StyleProperty, editableTextBoxStyle);
            editableTextBox.SetValue(TextBox.MarginProperty, new Thickness(6, 0, 6, 0));
            editableTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(Text)) { RelativeSource = RelativeSource.TemplatedParent, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            editableTextBox.SetBinding(TextBox.ForegroundProperty, new Binding(nameof(Foreground)) { RelativeSource = RelativeSource.TemplatedParent });
            editableTextBox.SetValue(TextBox.CursorProperty, Cursors.IBeam);
            editableTextBox.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            editableTextBox.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentHostGrid.AppendChild(editableTextBox);
            contentHostGrid.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                if (this is UI4ComboBox cbx && !cbx.IsDropDownOpen)
                {
                    cbx.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }), handledEventsToo: true);
            mainGrid.AppendChild(contentHostGrid);
            borderRoot.AppendChild(mainGrid);
            rootGrid.AppendChild(borderRoot);
            FrameworkElementFactory dropPopup = new FrameworkElementFactory(typeof(Popup));
            dropPopup.Name = "PART_Popup";
            dropPopup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            dropPopup.SetValue(Popup.AllowsTransparencyProperty, true);
            dropPopup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            dropPopup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(IsDropDownOpen)) { RelativeSource = RelativeSource.TemplatedParent });
            // 弹出层宽度随最宽选项自适应，但以控件宽度为下限，避免长选项被裁切显示不全
            dropPopup.SetBinding(FrameworkElement.MinWidthProperty, new Binding(nameof(ActualWidth)) { RelativeSource = RelativeSource.TemplatedParent });
            FrameworkElementFactory dropBorder = new FrameworkElementFactory(typeof(Border));
            dropBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(DropCornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            dropBorder.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("UI4.Brush.Surface"));
            dropBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 1));
            dropBorder.SetValue(Border.BorderBrushProperty, new DynamicResourceExtension("UI4.Brush.BorderNormal"));
            FrameworkElementFactory dropScroll = new FrameworkElementFactory(typeof(ScrollViewer));
            dropScroll.SetBinding(FrameworkElement.MaxHeightProperty, new Binding(nameof(MaxDropDownHeight)) { RelativeSource = RelativeSource.TemplatedParent });
            dropScroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            dropScroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            dropScroll.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnDropDownPreviewMouseWheel), true);
            dropScroll.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnDropDownScrollChanged));
            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            dropScroll.AppendChild(itemsPresenter);
            dropBorder.AppendChild(dropScroll);
            dropPopup.AppendChild(dropBorder);
            rootGrid.AppendChild(dropPopup);
            template.VisualTree = rootGrid;
            Trigger editableTrigger = new Trigger
            {
                Property = ComboBox.IsEditableProperty,
                Value = true
            };
            editableTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed) { TargetName = "PART_ContentPresenter" });
            editableTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed) { TargetName = "PART_RawContentPresenter" });
            editableTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible) { TargetName = "PART_EditableTextBox" });
            template.Triggers.Add(editableTrigger);
            Trigger focusTrigger = new Trigger
            {
                Property = UIElement.IsKeyboardFocusWithinProperty,
                Value = true
            };
            LinearGradientBrush focusGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(FocusGradientStart, 0),
                    new GradientStop(FocusGradientEnd, 1)
                }
            };
            focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, focusGrad) { TargetName = "PART_Border" });
            focusTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1.2, 1.2, 1.2, 1.2)) { TargetName = "PART_Border" });
            template.Triggers.Add(focusTrigger);
            Trigger hoverTrigger1 = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger1.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("UI4.Brush.HoverBorderColorLight")) { TargetName = "PART_Border" });
            template.Triggers.Add(hoverTrigger1);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static DataTemplate CreateDefaultSelectionTemplate()
        {
            DataTemplate dataTemplate = new DataTemplate();

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetBinding(TextBlock.TextProperty, new Binding { Converter = StringSelectionConverter.TextOnly });
            text.SetBinding(FrameworkElement.ToolTipProperty, new Binding { Converter = StringSelectionConverter.TextOnly });
            text.SetBinding(UIElement.VisibilityProperty, new Binding { Converter = StringSelectionConverter.WhenString });
            grid.AppendChild(text);

            dataTemplate.VisualTree = grid;
            return dataTemplate;
        }

        private sealed class StringSelectionConverter : IValueConverter
        {
            public static readonly StringSelectionConverter TextOnly = new StringSelectionConverter(Mode.Text);
            public static readonly StringSelectionConverter WhenString = new StringSelectionConverter(Mode.VisibilityWhenString);
            public static readonly StringSelectionConverter WhenNotString = new StringSelectionConverter(Mode.VisibilityWhenNotString);

            private enum Mode
            {
                Text,
                VisibilityWhenString,
                VisibilityWhenNotString
            }

            private readonly Mode _mode;

            private StringSelectionConverter(Mode mode)
            {
                _mode = mode;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                bool isString = value is string;
                switch (_mode)
                {
                    case Mode.Text:
                        return isString ? value : null;
                    case Mode.VisibilityWhenString:
                        return isString ? Visibility.Visible : Visibility.Collapsed;
                    default:
                        return isString ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}