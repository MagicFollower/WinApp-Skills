using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Xml;

namespace StartUI4Controls
{
    public class UI4ListView : ListBox
    {
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(
                nameof(ItemWidth),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(double.NaN, OnStyleUpdate));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(
                nameof(ItemHeight),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(double.NaN, OnStyleUpdate));

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public static readonly DependencyProperty ItemCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ItemCornerRadius),
                typeof(CornerRadius),
                typeof(UI4ListView),
                new PropertyMetadata(new CornerRadius(12), OnStyleUpdate));

        public CornerRadius ItemCornerRadius
        {
            get => (CornerRadius)GetValue(ItemCornerRadiusProperty);
            set => SetValue(ItemCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty ItemBackgroundProperty =
            DependencyProperty.Register(
                nameof(ItemBackground),
                typeof(Brush),
                typeof(UI4ListView),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), OnStyleUpdate));

        public Brush ItemBackground
        {
            get => (Brush)GetValue(ItemBackgroundProperty);
            set => SetValue(ItemBackgroundProperty, value);
        }

        public static readonly DependencyProperty ItemBorderBrushProperty =
            DependencyProperty.Register(
                nameof(ItemBorderBrush),
                typeof(Color),
                typeof(UI4ListView),
                new PropertyMetadata(Color.FromArgb(60, 120, 140, 200), OnStyleUpdate));

        public Color ItemBorderBrush
        {
            get => (Color)GetValue(ItemBorderBrushProperty);
            set => SetValue(ItemBorderBrushProperty, value);
        }

        public static readonly DependencyProperty ItemBorderThicknessProperty =
            DependencyProperty.Register(
                nameof(ItemBorderThickness),
                typeof(Thickness),
                typeof(UI4ListView),
                new PropertyMetadata(new Thickness(1), OnStyleUpdate));

        public Thickness ItemBorderThickness
        {
            get => (Thickness)GetValue(ItemBorderThicknessProperty);
            set => SetValue(ItemBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty ItemPaddingProperty =
            DependencyProperty.Register(
                nameof(ItemPadding),
                typeof(Thickness),
                typeof(UI4ListView),
                new PropertyMetadata(new Thickness(0), OnStyleUpdate));

        public Thickness ItemPadding
        {
            get => (Thickness)GetValue(ItemPaddingProperty);
            set => SetValue(ItemPaddingProperty, value);
        }

        public static readonly DependencyProperty ItemMarginProperty =
            DependencyProperty.Register(
                nameof(ItemMargin),
                typeof(Thickness),
                typeof(UI4ListView),
                new PropertyMetadata(new Thickness(10), OnStyleUpdate));

        public Thickness ItemMargin
        {
            get => (Thickness)GetValue(ItemMarginProperty);
            set => SetValue(ItemMarginProperty, value);
        }

        public static readonly DependencyProperty HoverAnimationDurationProperty =
            DependencyProperty.Register(
                nameof(HoverAnimationDuration),
                typeof(Duration),
                typeof(UI4ListView),
                new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(200)), OnStyleUpdate));

        public Duration HoverAnimationDuration
        {
            get => (Duration)GetValue(HoverAnimationDurationProperty);
            set => SetValue(HoverAnimationDurationProperty, value);
        }

        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.Register(
                nameof(ShadowColor),
                typeof(Color),
                typeof(UI4ListView),
                new PropertyMetadata(Color.FromArgb(35, 0, 0, 0), OnStyleUpdate));
        public Color ShadowColor
        {
            get => (Color)GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        public static readonly DependencyProperty ShadowBlurRadiusProperty =
            DependencyProperty.Register(
                nameof(ShadowBlurRadius),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(12.0, OnStyleUpdate));
        public double ShadowBlurRadius
        {
            get => (double)GetValue(ShadowBlurRadiusProperty);
            set => SetValue(ShadowBlurRadiusProperty, value);
        }

        public static readonly DependencyProperty ShadowDepthProperty =
            DependencyProperty.Register(
                nameof(ShadowDepth),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(0.0, OnStyleUpdate));
        public double ShadowDepth
        {
            get => (double)GetValue(ShadowDepthProperty);
            set => SetValue(ShadowDepthProperty, value);
        }

        public static readonly DependencyProperty ShadowOpacityProperty =
            DependencyProperty.Register(
                nameof(ShadowOpacity),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(0.0, OnStyleUpdate));
        public double ShadowOpacity
        {
            get => (double)GetValue(ShadowOpacityProperty);
            set => SetValue(ShadowOpacityProperty, value);
        }

        public static readonly DependencyProperty HoverScaleProperty =
            DependencyProperty.Register(
                nameof(HoverScale),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(1.01, OnStyleUpdate));
        public double HoverScale
        {
            get => (double)GetValue(HoverScaleProperty);
            set => SetValue(HoverScaleProperty, value);
        }

        public static readonly DependencyProperty HoverMaxGrowProperty =
            DependencyProperty.Register(
                nameof(HoverMaxGrow),
                typeof(double),
                typeof(UI4ListView),
                new PropertyMetadata(8.0, OnStyleUpdate));

        /// <summary>悬浮放大时单项每边允许外扩的最大像素数。等比缩放的外扩量随行宽线性增长，
        /// 这个上限把观感固定在像素上，使各窗口尺寸下放大幅度一致。</summary>
        public double HoverMaxGrow
        {
            get => (double)GetValue(HoverMaxGrowProperty);
            set => SetValue(HoverMaxGrowProperty, value);
        }

        // 内层 ScrollViewer 给 items host 留的内容内缩，与 FitHoverScale 的可用余量同源，改一处要改两处。
        private const double ContentPadding = 4.0;
        // 放大态与控件边界之间硬留的空白：小于这个距离悬浮边框看着就像被控件边界切掉。
        private const double EdgeReserve = 6.0;

        private static void OnStyleUpdate(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UI4ListView list)
                list.Style = list.BuildTechCardStyle();
        }
        public static Style _scrollViewerStyle;
        static UI4ListView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UI4ListView),
                new FrameworkPropertyMetadata(typeof(UI4ListView)));
            _scrollViewerStyle = CreateScrollViewerStyleFromXaml();
        }

        public UI4ListView()
        {
            FontSize = 15;
            Style = BuildTechCardStyle();

            // 声明式跟随主题：卡片底 / 卡片描边 / 投影色都挂令牌
            SetResourceReference(ItemBackgroundProperty, "UI4.Brush.Surface");
            SetResourceReference(ItemBorderBrushProperty, "UI4.Color.PanelBorder");
            SetResourceReference(ShadowColorProperty, "UI4.Color.Shadow");
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (!(element is ListBoxItem container))
                return;

            // 缩放必须放在不带 Effect 的根节点上：带 Effect 的元素会先被光栅化成位图，
            // 再经 RenderTransform 缩放就会把文字拉虚。结构参照 UI4Panel。
            // 放在容器本体而不是模板里，是因为 FrameworkElementFactory.SetValue 传进去的
            // 对象由所有容器共享一份，悬浮一项会把其它项一起放大。
            container.RenderTransform = new ScaleTransform(1, 1);
            container.RenderTransformOrigin = new Point(0.5, 0.5);

            container.MouseEnter -= OnItemMouseEnter;
            container.MouseEnter += OnItemMouseEnter;
            container.MouseLeave -= OnItemMouseLeave;
            container.MouseLeave += OnItemMouseLeave;
        }

        private void OnItemMouseEnter(object sender, RoutedEventArgs e)
        {
            AnimateItemScale(sender as ListBoxItem, true);
        }

        private void OnItemMouseLeave(object sender, RoutedEventArgs e)
        {
            AnimateItemScale(sender as ListBoxItem, false);
        }

        private void AnimateItemScale(ListBoxItem container, bool enter)
        {
            if (container == null)
                return;
            if (!(container.RenderTransform is ScaleTransform scale))
                return;

            double toX = enter ? FitHoverScale(container.ActualWidth, ItemMargin.Left) : 1.0;
            double toY = enter ? FitHoverScale(container.ActualHeight, ItemMargin.Top) : 1.0;

            DoubleAnimation scaleXAnim = new DoubleAnimation
            {
                To = toX,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DoubleAnimation scaleYAnim = new DoubleAnimation
            {
                To = toY,
                Duration = HoverAnimationDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        // 放大后的矩形要落在 ItemMargin + 内容内缩 组成的余量之内，并再留 EdgeReserve 不贴边。
        // 余量折成像素后反解出缩放上限，与 HoverScale 取小：窄卡片仍按设计者的比例走，
        // 铺满一行的宽项则自动收敛，越界不会随窗口变宽而放大。
        private double FitHoverScale(double slotSize, double sideSlack)
        {
            if (slotSize <= 0 || HoverScale <= 1.0)
                return Math.Max(1.0, HoverScale);

            double allowed = Math.Min(HoverMaxGrow, sideSlack + ContentPadding - EdgeReserve);
            if (allowed <= 0)
                return 1.0;

            return Math.Min(HoverScale, 1.0 + 2.0 * allowed / slotSize);
        }

        private Style BuildTechCardStyle()
        {
            Style listStyle = new Style(typeof(ListBox));
            listStyle.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
            ControlTemplate template = new ControlTemplate(typeof(ListBox));

            FrameworkElementFactory scrollHost = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollHost.Name = "PART_ContentHost";

            if (_scrollViewerStyle != null)
            {
                scrollHost.SetValue(FrameworkElement.StyleProperty, _scrollViewerStyle);
            }

            scrollHost.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scrollHost.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scrollHost.SetValue(ScrollViewer.BackgroundProperty, Brushes.Transparent);
            scrollHost.SetValue(ScrollViewer.PaddingProperty, new Thickness(ContentPadding));
            scrollHost.SetValue(FrameworkElement.FocusableProperty, false);
            scrollHost.SetValue(Control.BorderThicknessProperty, new Thickness(0));

            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            scrollHost.AppendChild(itemsPresenter);

            template.VisualTree = scrollHost;
            listStyle.Setters.Add(new Setter(Control.TemplateProperty, template));

            Style itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Binding(nameof(ItemMargin)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) }));
            itemStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            itemStyle.Setters.Add(new Setter(ListBoxItem.VerticalContentAlignmentProperty, VerticalAlignment.Stretch));

            if (!double.IsNaN(ItemWidth))
                itemStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, ItemWidth));
            if (!double.IsNaN(ItemHeight))
                itemStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, ItemHeight));

            ControlTemplate itemTemplate = new ControlTemplate(typeof(ListBoxItem));

            // 缩放动画不在这里：见 PrepareContainerForItemOverride，缩放节点挂在容器本体上。
            FrameworkElementFactory itemRoot = new FrameworkElementFactory(typeof(Grid));
            itemRoot.Name = "PART_ItemRoot";

            FrameworkElementFactory itemBorder = new FrameworkElementFactory(typeof(Border));
            itemBorder.Name = "PART_ItemBorder";
            itemBorder.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(ItemCornerRadius)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) });
            itemBorder.SetBinding(Border.BackgroundProperty, new Binding(nameof(ItemBackground)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) });
            itemBorder.SetBinding(Border.BorderBrushProperty, new Binding(nameof(ItemBorderBrush))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1),
                Converter = Internal.ColorToBrushConverter.Instance
            });
            itemBorder.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(ItemBorderThickness)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) });

            DropShadowEffect cardShadow = new DropShadowEffect
            {
                Color = ShadowColor,
                BlurRadius = ShadowBlurRadius,
                ShadowDepth = ShadowDepth,
                Opacity = ShadowOpacity
            };
            itemBorder.SetValue(UIElement.EffectProperty, cardShadow);
            itemRoot.AppendChild(itemBorder);

            FrameworkElementFactory contentClip = new FrameworkElementFactory(typeof(Border));
            contentClip.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(ItemCornerRadius)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) });
            contentClip.SetBinding(Border.PaddingProperty, new Binding(nameof(ItemPadding)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UI4ListView), 1) });
            contentClip.SetValue(UIElement.ClipToBoundsProperty, true);

            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            contentClip.AppendChild(contentPresenter);
            itemRoot.AppendChild(contentClip);

            itemTemplate.VisualTree = itemRoot;

            // 悬浮不再改边框色：整项的反馈只留放大与投影，边框始终保持 ItemBorderBrush。

            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            listStyle.Setters.Add(new Setter(ListBox.ItemContainerStyleProperty, itemStyle));

            return listStyle;
        }
        public static Style CreateScrollViewerStyleFromXaml()
        {
            string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollViewer}'>
    <Style.Resources>
        <Style x:Key='ScrollBarThumb' TargetType='{x:Type Thumb}'>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='IsTabStop' Value='false'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type Thumb}'>
                        <Grid>
                            <Rectangle Fill='{DynamicResource UI4.Brush.ScrollBarThumb}' RadiusX='3' RadiusY='3'/>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key='HorizontalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Focusable' Value='false'/>
            <Setter Property='IsTabStop' Value='false'/>
            <Setter Property='Opacity' Value='0'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type RepeatButton}'>
                        <Rectangle Fill='{TemplateBinding Background}'
                                   Width='{TemplateBinding Width}'
                                   Height='{TemplateBinding Height}'/>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key='VerticalScrollBarPageButton' TargetType='{x:Type RepeatButton}'>
            <Setter Property='OverridesDefaultStyle' Value='true'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Focusable' Value='false'/>
            <Setter Property='IsTabStop' Value='false'/>
            <Setter Property='Opacity' Value='0'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type RepeatButton}'>
                        <Rectangle Fill='{TemplateBinding Background}'
                                   Width='{TemplateBinding Width}'
                                   Height='{TemplateBinding Height}'/>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key='for_scrollbar' TargetType='{x:Type ScrollBar}'>
            <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
            <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Margin' Value='0,1,2,6'/>
            <Setter Property='Width' Value='6'/>
            <Setter Property='MinWidth' Value='6'/>
            <Setter Property='Opacity' Value='0'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='{x:Type ScrollBar}'>
                        <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                            <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}' IsDirectionReversed='true'>
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                                  Command='{x:Static ScrollBar.PageUpCommand}'/>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Style='{StaticResource VerticalScrollBarPageButton}'
                                                  Command='{x:Static ScrollBar.PageDownCommand}'/>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb Style='{StaticResource ScrollBarThumb}'/>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Trigger.EnterActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.EnterActions>
                                <Trigger.ExitActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.ExitActions>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property='Orientation' Value='Horizontal'>
                    <Setter Property='Background' Value='Transparent'/>
                    <Setter Property='Margin' Value='2,0,6,2'/>
                    <Setter Property='Height' Value='6'/>
                    <Setter Property='MinHeight' Value='6'/>
                    <Setter Property='Width' Value='Auto'/>
                    <Setter Property='Opacity' Value='0'/>
                    <Setter Property='Template'>
                        <Setter.Value>
                            <ControlTemplate TargetType='{x:Type ScrollBar}'>
                                <Grid x:Name='Bg' SnapsToDevicePixels='true'>
                                    <Track x:Name='PART_Track' IsEnabled='{TemplateBinding IsMouseOver}'>
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                          Command='{x:Static ScrollBar.PageLeftCommand}'/>
                                        </Track.DecreaseRepeatButton>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Style='{StaticResource HorizontalScrollBarPageButton}'
                                                          Command='{x:Static ScrollBar.PageRightCommand}'/>
                                        </Track.IncreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb Style='{StaticResource ScrollBarThumb}'/>
                                        </Track.Thumb>
                                    </Track>
                                </Grid>
                                <ControlTemplate.Triggers>
                                    <Trigger Property='IsMouseOver' Value='True'>
                                        <Trigger.EnterActions>
                                            <BeginStoryboard>
                                                <Storyboard>
                                                    <DoubleAnimation Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                                </Storyboard>
                                            </BeginStoryboard>
                                        </Trigger.EnterActions>
                                        <Trigger.ExitActions>
                                            <BeginStoryboard>
                                                <Storyboard>
                                                    <DoubleAnimation Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5'/>
                                                </Storyboard>
                                            </BeginStoryboard>
                                        </Trigger.ExitActions>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Style.Resources>
    <Setter Property='BorderBrush' Value='{DynamicResource UI4.Brush.BorderWeak}'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='HorizontalContentAlignment' Value='Left'/>
    <Setter Property='HorizontalScrollBarVisibility' Value='Auto'/>
    <Setter Property='VerticalContentAlignment' Value='Top'/>
    <Setter Property='VerticalScrollBarVisibility' Value='Auto'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='{x:Type ScrollViewer}'>
                <Border BorderBrush='{TemplateBinding BorderBrush}'
                        BorderThickness='{TemplateBinding BorderThickness}'
                        SnapsToDevicePixels='True'>
                    <Grid Background='{TemplateBinding Background}'>
                        <ScrollContentPresenter
                            Cursor='{TemplateBinding Cursor}'
                            Margin='{TemplateBinding Padding}'
                            ContentTemplate='{TemplateBinding ContentTemplate}'/>
                        <ScrollBar x:Name='PART_VerticalScrollBar'
                                   HorizontalAlignment='Right'
                                   Maximum='{TemplateBinding ScrollableHeight}'
                                   Orientation='Vertical'
                                   Style='{StaticResource for_scrollbar}'
                                   ViewportSize='{TemplateBinding ViewportHeight}'
                                   Value='{TemplateBinding VerticalOffset}'
                                   Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'/>
                        <ScrollBar x:Name='PART_HorizontalScrollBar'
                                   Maximum='{TemplateBinding ScrollableWidth}'
                                   Orientation='Horizontal'
                                   Style='{StaticResource for_scrollbar}'
                                   VerticalAlignment='Bottom'
                                   Value='{TemplateBinding HorizontalOffset}'
                                   ViewportSize='{TemplateBinding ViewportWidth}'
                                   Visibility='{TemplateBinding ComputedHorizontalScrollBarVisibility}'/>
                    </Grid>
                </Border>
                <ControlTemplate.Triggers>
                    <EventTrigger RoutedEvent='ScrollChanged'>
                        <BeginStoryboard>
                            <Storyboard>
                                <DoubleAnimation Storyboard.TargetName='PART_VerticalScrollBar' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                <DoubleAnimation Storyboard.TargetName='PART_VerticalScrollBar' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5' BeginTime='0:0:1.5'/>
                                <DoubleAnimation Storyboard.TargetName='PART_HorizontalScrollBar' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.2'/>
                                <DoubleAnimation Storyboard.TargetName='PART_HorizontalScrollBar' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.5' BeginTime='0:0:1.5'/>
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";

            try
            {
                using (StringReader sr = new StringReader(xaml))
                using (XmlReader xr = XmlReader.Create(sr))
                {
                    return (Style)XamlReader.Load(xr);
                }
            }
            catch
            {

                return null;
            }
        }
    }
}
