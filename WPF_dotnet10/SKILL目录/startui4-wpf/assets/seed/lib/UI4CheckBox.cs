using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的复选框控件，支持圆角、自定义勾选背景和文字颜色。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.CheckBox"/>，提供以下自定义属性：</para>
    /// <list type="bullet">
    ///   <item><see cref="CornerRadius"/> — 复选框圆角半径</item>
    ///   <item><see cref="CheckBackground"/> — 勾选时的背景色</item>
    ///   <item><see cref="BorderNormalColor"/> — 边框颜色</item>
    ///   <item><see cref="TextColor"/> — 文字颜色</item>
    /// </list>
    /// </remarks>
    public class UI4CheckBox : CheckBox
    {
        /// <summary>
        /// 获取或设置复选框的圆角半径。
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(UI4CheckBox),
                new PropertyMetadata(new CornerRadius(6), OnStyleRefresh));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// 已废弃。请使用 <see cref="CornerRadius"/> 代替。
        /// </summary>
        [Obsolete("Use CornerRadius instead.")]
        public static readonly DependencyProperty BoxCornerRadiusProperty = CornerRadiusProperty;

        /// <summary>
        /// 已废弃。请使用 <see cref="CornerRadius"/> 代替。
        /// </summary>
        [Obsolete("Use CornerRadius instead.")]
        public CornerRadius BoxCornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CheckBackgroundProperty =
            DependencyProperty.Register(
                nameof(CheckBackground),
                typeof(Color),
                typeof(UI4CheckBox),
                new PropertyMetadata(Color.FromRgb(0, 102, 181)));
        public Color CheckBackground
        {
            get => (Color)GetValue(CheckBackgroundProperty);
            set => SetValue(CheckBackgroundProperty, value);
        }

        public static readonly DependencyProperty BorderNormalColorProperty =
            DependencyProperty.Register(
                nameof(BorderNormalColor),
                typeof(Color),
                typeof(UI4CheckBox),
                new PropertyMetadata(Color.FromRgb(180, 180, 200)));

        public Color BorderNormalColor
        {
            get => (Color)GetValue(BorderNormalColorProperty);
            set => SetValue(BorderNormalColorProperty, value);
        }

        public static readonly DependencyProperty BoxSizeProperty =
            DependencyProperty.Register(
                nameof(BoxSize),
                typeof(double),
                typeof(UI4CheckBox),
                new PropertyMetadata(18d, OnStyleRefresh));

        public double BoxSize
        {
            get => (double)GetValue(BoxSizeProperty);
            set => SetValue(BoxSizeProperty, value);
        }

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(
                nameof(TextColor),
                typeof(Color),
                typeof(UI4CheckBox),
                new PropertyMetadata(Color.FromRgb(30, 30, 30)));

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public static readonly DependencyProperty TextMarginProperty =
            DependencyProperty.Register(
                nameof(TextMargin),
                typeof(Thickness),
                typeof(UI4CheckBox),
                new PropertyMetadata(new Thickness(8, 0, 0, 0), OnStyleRefresh));

        public Thickness TextMargin
        {
            get => (Thickness)GetValue(TextMarginProperty);
            set => SetValue(TextMarginProperty, value);
        }

        /// <summary>未勾选时的填充色，跟随主题令牌 UI4.Color.CheckBoxUnchecked。</summary>
        private static readonly DependencyProperty UncheckedBoxColorProperty =
            DependencyProperty.Register(
                nameof(UncheckedBoxColor),
                typeof(Color),
                typeof(UI4CheckBox),
                new PropertyMetadata(Color.FromRgb(250, 250, 252)));

        private Color UncheckedBoxColor
        {
            get => (Color)GetValue(UncheckedBoxColorProperty);
            set => SetValue(UncheckedBoxColorProperty, value);
        }

        /// <summary>悬停时的边框色，跟随主题令牌 UI4.Color.HoverBorderColorLight。</summary>
        private static readonly DependencyProperty HoverBoxColorProperty =
            DependencyProperty.Register(
                nameof(HoverBoxColor),
                typeof(Color),
                typeof(UI4CheckBox),
                new PropertyMetadata(Color.FromRgb(0, 120, 212)));

        private Color HoverBoxColor
        {
            get => (Color)GetValue(HoverBoxColorProperty);
            set => SetValue(HoverBoxColorProperty, value);
        }

        private static void OnStyleRefresh(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4CheckBox ctrl)
                ctrl.Style = ctrl.BuildCheckBoxStyle();
        }

        static UI4CheckBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4CheckBox),
                new FrameworkPropertyMetadata(typeof(UI4CheckBox)));
        }

        public UI4CheckBox()
        {
            FontSize = 15d;
            Style = BuildCheckBoxStyle();

            SetResourceReference(BorderNormalColorProperty, "UI4.Color.BorderNormal");
            SetResourceReference(TextColorProperty, "UI4.Color.TextForeground");
            SetResourceReference(CheckBackgroundProperty, "UI4.Color.CheckBackground");
            SetResourceReference(UncheckedBoxColorProperty, "UI4.Color.CheckBoxUnchecked");
            SetResourceReference(HoverBoxColorProperty, "UI4.Color.HoverBorderColorLight");
        }

        private Style BuildCheckBoxStyle()
        {
            Style style = new Style(typeof(CheckBox));

            ControlTemplate template = new ControlTemplate(typeof(CheckBox));

            FrameworkElementFactory rootStack = new FrameworkElementFactory(typeof(StackPanel));
            rootStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            rootStack.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory boxBorder = new FrameworkElementFactory(typeof(Border));
            boxBorder.Name = "boxBorder";
            boxBorder.SetValue(Border.WidthProperty, BoxSize);
            boxBorder.SetValue(Border.HeightProperty, BoxSize);
            boxBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            boxBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 1));
            boxBorder.SetBinding(Border.BorderBrushProperty, ColorBrushBinding(nameof(BorderNormalColor)));
            boxBorder.SetBinding(Border.BackgroundProperty, ColorBrushBinding(nameof(UncheckedBoxColor)));

            FrameworkElementFactory checkMark = new FrameworkElementFactory(typeof(Path));
            checkMark.Name = "checkMark";
            checkMark.SetValue(Path.StrokeProperty, Brushes.White);
            checkMark.SetValue(Path.StrokeThicknessProperty, 2.5d);
            checkMark.SetValue(Path.StrokeStartLineCapProperty, PenLineCap.Round);
            checkMark.SetValue(Path.StrokeEndLineCapProperty, PenLineCap.Round);
            checkMark.SetValue(Path.DataProperty, Geometry.Parse("M3,9 L7,13 L14,4"));
            checkMark.SetValue(Path.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkMark.SetValue(Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkMark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            boxBorder.AppendChild(checkMark);

            rootStack.AppendChild(boxBorder);

            FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.Name = "PART_TextBlock";

            textBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Content)) { RelativeSource = RelativeSource.TemplatedParent });
            textBlock.SetBinding(TextBlock.MarginProperty, new Binding(nameof(TextMargin)) { RelativeSource = RelativeSource.TemplatedParent });
            textBlock.SetBinding(TextBlock.ForegroundProperty, ColorBrushBinding(nameof(TextColor)));
            textBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { RelativeSource = RelativeSource.TemplatedParent });
            textBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            rootStack.AppendChild(textBlock);

            template.VisualTree = rootStack;

            Trigger checkedTrigger = new Trigger
            {
                Property = ToggleButton.IsCheckedProperty,
                Value = true
            };
            checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible) { TargetName = "checkMark" });
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, OwnColorBrushBinding(nameof(CheckBackground))) { TargetName = "boxBorder" });
            template.Triggers.Add(checkedTrigger);

            Trigger hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, OwnColorBrushBinding(nameof(HoverBoxColor))) { TargetName = "boxBorder" });
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static Binding ColorBrushBinding(string path)
        {
            return new Binding(path)
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Converter = ColorToBrushConverter.Instance
            };
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
