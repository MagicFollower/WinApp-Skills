using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace StartUI4Controls
{
    /// <summary>
    /// 现代风格的面板容器控件，支持圆角、阴影、渐变背景和标题栏。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.ContentControl"/>，提供以下自定义属性：</para>
    /// <list type="bullet">
    ///   <item><see cref="CornerRadius"/> — 圆角半径</item>
    ///   <item><see cref="BorderColor"/> — 边框颜色（Color 类型，用于驱动 ColorAnimation）</item>
    ///   <item><see cref="ShadowDepth"/> — 阴影深度</item>
    ///   <item><see cref="Title"/> — 标题栏文字</item>
    /// </list>
    /// </remarks>
    public class UI4Panel : ContentControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(UI4Panel),
                new PropertyMetadata(new CornerRadius(12), OnStyleUpdate));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// 获取或设置面板边框颜色（Color 类型）。
        /// </summary>
        /// <remarks>
        /// 该属性用于驱动边框的 ColorAnimation 悬停效果。
        /// 如需设置 Brush 类型的边框画刷，请使用继承自 <see cref="System.Windows.Controls.Control"/> 的 BorderBrush 属性。
        /// </remarks>
        public static readonly DependencyProperty BorderColorProperty =
            DependencyProperty.Register(
                nameof(BorderColor),
                typeof(Color),
                typeof(UI4Panel),
                new PropertyMetadata(Color.FromArgb(60, 120, 140, 200), OnStyleUpdate));

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public static readonly DependencyProperty HoverBorderBrushProperty =
            DependencyProperty.Register(
                nameof(HoverBorderBrush),
                typeof(SolidColorBrush),
                typeof(UI4Panel),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(70, 120, 140, 200)), OnStyleUpdate));

        public SolidColorBrush HoverBorderBrush
        {
            get => (SolidColorBrush)GetValue(HoverBorderBrushProperty);
            set => SetValue(HoverBorderBrushProperty, value);
        }

        public static readonly DependencyProperty ShadowDepthProperty =
            DependencyProperty.Register(
                nameof(ShadowDepth),
                typeof(double),
                typeof(UI4Panel),
                new PropertyMetadata(0.0, OnStyleUpdate));

        public double ShadowDepth
        {
            get => (double)GetValue(ShadowDepthProperty);
            set => SetValue(ShadowDepthProperty, value);
        }

        public static readonly DependencyProperty ShadowBlurRadiusProperty =
            DependencyProperty.Register(
                nameof(ShadowBlurRadius),
                typeof(double),
                typeof(UI4Panel),
                new PropertyMetadata(15.0, OnStyleUpdate));

        public double ShadowBlurRadius
        {
            get => (double)GetValue(ShadowBlurRadiusProperty);
            set => SetValue(ShadowBlurRadiusProperty, value);
        }

        public static readonly DependencyProperty ShadowOpacityProperty =
            DependencyProperty.Register(
                nameof(ShadowOpacity),
                typeof(double),
                typeof(UI4Panel),
                new PropertyMetadata(0.1, OnStyleUpdate));

        public double ShadowOpacity
        {
            get => (double)GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.Register(
                nameof(ShadowColor),
                typeof(Color),
                typeof(UI4Panel),
                new PropertyMetadata(Colors.Black, OnStyleUpdate));

        public Color ShadowColor
        {
            get => (Color)GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        public static readonly DependencyProperty ContentPaddingProperty =
            DependencyProperty.Register(
                nameof(ContentPadding),
                typeof(Thickness),
                typeof(UI4Panel),
                new PropertyMetadata(new Thickness(0), OnStyleUpdate));

        public Thickness ContentPadding
        {
            get => (Thickness)GetValue(ContentPaddingProperty);
            set => SetValue(ContentPaddingProperty, value);
        }

        public static readonly DependencyProperty HoverAnimationDurationProperty =
            DependencyProperty.Register(
                nameof(HoverAnimationDuration),
                typeof(Duration),
                typeof(UI4Panel),
                new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(200)), OnStyleUpdate));

        public Duration HoverAnimationDuration
        {
            get => (Duration)GetValue(HoverAnimationDurationProperty);
            set => SetValue(HoverAnimationDurationProperty, value);
        }

        public static readonly DependencyProperty HoverScaleProperty =
            DependencyProperty.Register(
                nameof(HoverScale),
                typeof(double),
                typeof(UI4Panel),
                new PropertyMetadata(1.005, OnStyleUpdate));
        public double HoverScale
        {
            get => (double)GetValue(HoverScaleProperty);
            set => SetValue(HoverScaleProperty, value);
        }

        private static void OnStyleUpdate(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4Panel panel)
                panel.Style = panel.BuildPanelStyle();
        }

        static UI4Panel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4Panel),
                new FrameworkPropertyMetadata(typeof(UI4Panel)));
            BorderThicknessProperty.OverrideMetadata(typeof(UI4Panel),
                new FrameworkPropertyMetadata(new Thickness(1), OnStyleUpdate));
        }

        public UI4Panel()
        {
            // 声明式跟随主题：面板底与投影色挂令牌
            SetResourceReference(BackgroundProperty, "UI4.Brush.Background");
            SetResourceReference(ShadowColorProperty, "UI4.Color.Shadow");
            Style = BuildPanelStyle();
            Cursor = Cursors.Arrow;
        }

        private Style BuildPanelStyle()
        {
            Style style = new Style(typeof(ContentControl));
            ControlTemplate template = new ControlTemplate(typeof(ContentControl));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.Name = "PART_Grid";
            grid.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
            grid.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1, 1));

            FrameworkElementFactory shadowBorder = new FrameworkElementFactory(typeof(Border));
            shadowBorder.Name = "PART_ShadowBorder";
            shadowBorder.SetBinding(Border.CornerRadiusProperty,
                new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            shadowBorder.SetBinding(Border.BackgroundProperty,
                new Binding(nameof(Background)) { RelativeSource = RelativeSource.TemplatedParent });
            DropShadowEffect shadow = new DropShadowEffect
            {
                ShadowDepth = ShadowDepth,
                BlurRadius = ShadowBlurRadius,
                Opacity = ShadowOpacity,
                Color = ShadowColor
            };
            shadowBorder.SetValue(UIElement.EffectProperty, shadow);

            FrameworkElementFactory contentBorder = new FrameworkElementFactory(typeof(Border));
            contentBorder.Name = "PART_InnerBorder";
            contentBorder.SetBinding(Border.CornerRadiusProperty,
                new Binding(nameof(CornerRadius)) { RelativeSource = RelativeSource.TemplatedParent });
            contentBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.Transparent));
            contentBorder.SetBinding(Border.BorderThicknessProperty,
                new Binding(nameof(BorderThickness)) { RelativeSource = RelativeSource.TemplatedParent });
            contentBorder.SetBinding(Border.PaddingProperty,
                new Binding(nameof(ContentPadding)) { RelativeSource = RelativeSource.TemplatedParent });
            contentBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(BorderColor));
            contentBorder.SetValue(UIElement.ClipToBoundsProperty, true);

            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            contentBorder.AppendChild(contentPresenter);

            grid.AppendChild(shadowBorder);
            grid.AppendChild(contentBorder);
            template.VisualTree = grid;

            ColorAnimation hoverAnim = new ColorAnimation
            {
                To = HoverBorderBrush.Color,
                Duration = HoverAnimationDuration
            };
            Storyboard hoverStoryboard = new Storyboard();
            hoverStoryboard.Children.Add(hoverAnim);
            Storyboard.SetTargetName(hoverAnim, "PART_InnerBorder");
            Storyboard.SetTargetProperty(hoverAnim,
                new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));

            ColorAnimation leaveAnim = new ColorAnimation
            {
                To = BorderColor,
                Duration = HoverAnimationDuration
            };
            Storyboard leaveStoryboard = new Storyboard();
            leaveStoryboard.Children.Add(leaveAnim);
            Storyboard.SetTargetName(leaveAnim, "PART_InnerBorder");
            Storyboard.SetTargetProperty(leaveAnim,
                new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));

            DoubleAnimation scaleXHoverAnim = new DoubleAnimation
            {
                To = HoverScale,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DoubleAnimation scaleYHoverAnim = new DoubleAnimation
            {
                To = HoverScale,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            hoverStoryboard.Children.Add(scaleXHoverAnim);
            hoverStoryboard.Children.Add(scaleYHoverAnim);
            Storyboard.SetTargetName(scaleXHoverAnim, "PART_Grid");
            Storyboard.SetTargetProperty(scaleXHoverAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetName(scaleYHoverAnim, "PART_Grid");
            Storyboard.SetTargetProperty(scaleYHoverAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            DoubleAnimation scaleXLeaveAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DoubleAnimation scaleYLeaveAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            leaveStoryboard.Children.Add(scaleXLeaveAnim);
            leaveStoryboard.Children.Add(scaleYLeaveAnim);
            Storyboard.SetTargetName(scaleXLeaveAnim, "PART_Grid");
            Storyboard.SetTargetProperty(scaleXLeaveAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetName(scaleYLeaveAnim, "PART_Grid");
            Storyboard.SetTargetProperty(scaleYLeaveAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            EventTrigger mouseEnterTrigger = new EventTrigger(UIElement.MouseEnterEvent);
            mouseEnterTrigger.Actions.Add(new BeginStoryboard { Storyboard = hoverStoryboard });
            template.Triggers.Add(mouseEnterTrigger);

            EventTrigger mouseLeaveTrigger = new EventTrigger(UIElement.MouseLeaveEvent);
            mouseLeaveTrigger.Actions.Add(new BeginStoryboard { Storyboard = leaveStoryboard });
            template.Triggers.Add(mouseLeaveTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch));
            return style;
        }
    }
}