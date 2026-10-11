using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace StartUI4Controls
{
    public class UI4FlipTextBlock : ContentControl
    {
        //外部可设置属性

        public static readonly DependencyProperty FlipRateProperty =
            DependencyProperty.Register(
                nameof(FlipRate),
                typeof(double),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(0.3));

        public double FlipRate
        {
            get => (double)GetValue(FlipRateProperty);
            set => SetValue(FlipRateProperty, value);
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(
                nameof(CardBackground),
                typeof(Color),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(Colors.White, OnCardBackgroundChanged));

        public Color CardBackground
        {
            get => (Color)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
        }

        public static readonly DependencyProperty CardForegroundProperty =
            DependencyProperty.Register(
                nameof(CardForeground),
                typeof(Color),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(Colors.Black, OnCardForegroundChanged));

        public Color CardForeground
        {
            get => (Color)GetValue(CardForegroundProperty);
            set => SetValue(CardForegroundProperty, value);
        }

        public static readonly DependencyProperty CardBorderBrushProperty =
            DependencyProperty.Register(
                nameof(CardBorderBrush),
                typeof(Color),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(Colors.Gray, OnCardBorderBrushChanged));

        public Color CardBorderBrush
        {
            get => (Color)GetValue(CardBorderBrushProperty);
            set => SetValue(CardBorderBrushProperty, value);
        }

        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.Register(
                nameof(ShadowColor),
                typeof(Color),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(Colors.Black, OnShadowColorChanged));

        public Color ShadowColor
        {
            get => (Color)GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        public static readonly DependencyProperty CardCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CardCornerRadius),
                typeof(CornerRadius),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(new CornerRadius(12), OnCardCornerRadiusChanged));

        public CornerRadius CardCornerRadius
        {
            get => (CornerRadius)GetValue(CardCornerRadiusProperty);
            set => SetValue(CardCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CardBorderThicknessProperty =
            DependencyProperty.Register(
                nameof(CardBorderThickness),
                typeof(Thickness),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(new Thickness(1), OnCardBorderThicknessChanged));

        public Thickness CardBorderThickness
        {
            get => (Thickness)GetValue(CardBorderThicknessProperty);
            set => SetValue(CardBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty CardShadowDepthProperty =
            DependencyProperty.Register(
                nameof(CardShadowDepth),
                typeof(double),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(10.0, OnCardShadowDepthChanged));

        public double CardShadowDepth
        {
            get => (double)GetValue(CardShadowDepthProperty);
            set => SetValue(CardShadowDepthProperty, value);
        }

        public static readonly DependencyProperty CardShadowBlurRadiusProperty =
            DependencyProperty.Register(
                nameof(CardShadowBlurRadius),
                typeof(double),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(15.0, OnCardShadowBlurRadiusChanged));

        public double CardShadowBlurRadius
        {
            get => (double)GetValue(CardShadowBlurRadiusProperty);
            set => SetValue(CardShadowBlurRadiusProperty, value);
        }

        public static readonly DependencyProperty CardShadowOpacityProperty =
            DependencyProperty.Register(
                nameof(CardShadowOpacity),
                typeof(double),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata(0.1, OnCardShadowOpacityChanged));

        public double CardShadowOpacity
        {
            get => (double)GetValue(CardShadowOpacityProperty);
            set => SetValue(CardShadowOpacityProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(UI4FlipTextBlock),
                new PropertyMetadata("0", OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        //

        private Grid _g_main;
        private UI4Panel _userset_ui4panel;
        private UI4TextBlock _t_num;
        private Grid _g_top;
        private Grid _g_bottom;
        private Grid _g_bottom_inner;
        private ScaleTransform _st_gtop;
        private ScaleTransform _st_gbottom;
        private ScaleTransform _st_gbottominner;

        private bool _isLoaded;

        static UI4FlipTextBlock()
        {
            FontSizeProperty.OverrideMetadata(typeof(UI4FlipTextBlock),
                new FrameworkPropertyMetadata(60.0));
        }

        public UI4FlipTextBlock()
        {
            BuildVisualTree();
            this.Loaded += OnLoaded;

            // 声明式跟随主题：卡底/卡前景/卡边框挂令牌，既有 DP 回调负责把值推进内部元素。
            // 旧实现靠 ReadLocalValue == UnsetValue 判断"用户没设过"，而 SetResourceReference
            // 会让 ReadLocalValue 返回 ResourceReferenceExpression，该判定会静默失效。
            SetResourceReference(CardBackgroundProperty, "UI4.Color.Surface");
            SetResourceReference(CardForegroundProperty, "UI4.Color.TextForeground");
            SetResourceReference(CardBorderBrushProperty, "UI4.Color.BorderNormal");
        }

        private void BuildVisualTree()
        {
            _g_main = new Grid();
            _g_main.HorizontalAlignment = HorizontalAlignment.Center;
            _g_main.VerticalAlignment = VerticalAlignment.Center;

            _userset_ui4panel = new UI4Panel();

            var panelContent = new Grid();

            _t_num = new UI4TextBlock
            {
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var flipGrid = new Grid();
            flipGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            flipGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _g_top = new Grid
            {
                RenderTransformOrigin = new Point(0.5, 1)
            };
            _st_gtop = new ScaleTransform { ScaleY = 0 };
            _g_top.RenderTransform = _st_gtop;
            Grid.SetRow(_g_top, 0);

            _g_bottom = new Grid
            {
                Margin = new Thickness(-1),
                RenderTransformOrigin = new Point(0.5, 0)
            };
            _st_gbottom = new ScaleTransform { ScaleY = 0 };
            _g_bottom.RenderTransform = _st_gbottom;
            Grid.SetRow(_g_bottom, 1);

            _g_bottom_inner = new Grid
            {
                RenderTransformOrigin = new Point(0.5, 0)
            };
            _st_gbottominner = new ScaleTransform { ScaleY = 0 };
            _g_bottom_inner.RenderTransform = _st_gbottominner;
            _g_bottom.Children.Add(_g_bottom_inner);

            flipGrid.Children.Add(_g_top);
            flipGrid.Children.Add(_g_bottom);

            panelContent.Children.Add(_t_num);
            panelContent.Children.Add(flipGrid);

            _userset_ui4panel.Content = panelContent;

            _g_main.Children.Add(_userset_ui4panel);

            var centerLine = new Grid
            {
                Height = 30,
                VerticalAlignment = VerticalAlignment.Center
            };
            centerLine.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            centerLine.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var borderTop = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0))
            };
            borderTop.Background = new LinearGradientBrush(
                Color.FromArgb(0x02, 0, 0, 0),
                Color.FromArgb(0x26, 0, 0, 0),
                new Point(0.5, 0),
                new Point(0.5, 1));
            Grid.SetRow(borderTop, 0);

            var borderBottom = new Border();
            borderBottom.Background = new LinearGradientBrush(
                Color.FromArgb(0x26, 0, 0, 0),
                Color.FromArgb(0x02, 0, 0, 0),
                new Point(0.5, 0),
                new Point(0.5, 1));
            Grid.SetRow(borderBottom, 1);

            centerLine.Children.Add(borderTop);
            centerLine.Children.Add(borderBottom);

            _g_main.Children.Add(centerLine);

            this.Content = _g_main;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;

            UserSet();
            GetRenderTop();
            GetRenderBottom();

            var descriptor = DependencyPropertyDescriptor.FromProperty(
                TextProperty, typeof(UI4FlipTextBlock));
            descriptor.AddValueChanged(this, OnTextBlockTextChanged);
        }

        private void UserSet()
        {
            _userset_ui4panel.Background = new SolidColorBrush(CardBackground);
            _t_num.Foreground = new SolidColorBrush(CardForeground);
            _userset_ui4panel.BorderColor = CardBorderBrush;
            _userset_ui4panel.ShadowColor = ShadowColor;
            _userset_ui4panel.CornerRadius = CardCornerRadius;
            _userset_ui4panel.BorderThickness = CardBorderThickness;
            _userset_ui4panel.ShadowDepth = CardShadowDepth;
            _userset_ui4panel.ShadowBlurRadius = CardShadowBlurRadius;
            _userset_ui4panel.ShadowOpacity = CardShadowOpacity;
            _t_num.Text = Text;
            _t_num.FontFamily = FontFamily;
            _t_num.FontWeight = FontWeight;
            _t_num.FontSize = FontSize;
            _t_num.FontStyle = FontStyle;
        }

        private void AnimationTop()
        {
            _st_gbottominner.ScaleY = 1.0;
            _st_gbottom.ScaleY = 1.0;
            _st_gtop.ScaleY = 1.0;

            DoubleAnimation anim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(FlipRate),
                FillBehavior = FillBehavior.HoldEnd
            };
            anim.Completed += (ss, ee) =>
            {
                _st_gbottom.ScaleY = 0.0;
                GetRenderTop();
                GetRenderBottominner();
                _st_gbottom.ScaleY = 1.0;

                DoubleAnimation anim2 = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(FlipRate),
                    FillBehavior = FillBehavior.HoldEnd
                };
                anim2.Completed += (ss2, ee2) =>
                {
                    _st_gbottominner.ScaleY = 0.0;
                    _st_gbottom.ScaleY = 0.0;
                    GetRenderBottom();
                    GetRenderBottominner();
                    GetRenderTop();
                };
                _st_gbottominner.BeginAnimation(ScaleTransform.ScaleYProperty, anim2);
            };
            _st_gtop.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void OnTextBlockTextChanged(object sender, EventArgs e)
        {
            AnimationTop();
        }

        private void GetRenderTop()
        {
            this.Dispatcher.Invoke(() =>
            {
                var target = _g_main;

                double actualWidth = target.ActualWidth;
                double actualHeight = target.ActualHeight;

                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return;
                }

                int w = (int)Math.Round(actualWidth);
                int h = (int)Math.Round(actualHeight);

                var rtb = new RenderTargetBitmap(
                    w,
                    h,
                    96, 96, PixelFormats.Pbgra32);

                rtb.Render(target);

                int bitmapW = rtb.PixelWidth;
                int bitmapH = rtb.PixelHeight;

                int halfH = bitmapH / 2;

                Int32Rect rectTop = new Int32Rect(0, 0, bitmapW, halfH);

                if (!CheckRect(rtb, rectTop))
                {
                    return;
                }

                var cropTop = new CroppedBitmap(rtb, rectTop);

                _g_top.Background = new ImageBrush(cropTop) { Stretch = Stretch.UniformToFill };

            }, DispatcherPriority.Render);
        }

        private void GetRenderBottom()
        {
            this.Dispatcher.Invoke(() =>
            {
                var target = _g_main;

                double actualWidth = target.ActualWidth;
                double actualHeight = target.ActualHeight;

                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return;
                }

                int w = (int)Math.Round(actualWidth);
                int h = (int)Math.Round(actualHeight);

                var rtb = new RenderTargetBitmap(
                    w,
                    h,
                    96, 96, PixelFormats.Pbgra32);

                rtb.Render(target);

                int bitmapW = rtb.PixelWidth;
                int bitmapH = rtb.PixelHeight;

                int halfH = bitmapH / 2;

                int bottomY = halfH;
                int bottomH = bitmapH - bottomY;
                Int32Rect rectBottom = new Int32Rect(0, bottomY, bitmapW, bottomH);

                if (!CheckRect(rtb, rectBottom))
                {
                    return;
                }

                var cropBottom = new CroppedBitmap(rtb, rectBottom);

                _g_bottom.Background = new ImageBrush(cropBottom) { Stretch = Stretch.UniformToFill };

            }, DispatcherPriority.Render);
        }

        private void GetRenderBottominner()
        {
            this.Dispatcher.Invoke(() =>
            {
                var target = _g_main;

                double actualWidth = target.ActualWidth;
                double actualHeight = target.ActualHeight;

                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return;
                }

                int w = (int)Math.Round(actualWidth);
                int h = (int)Math.Round(actualHeight);

                var rtb = new RenderTargetBitmap(
                    w,
                    h,
                    96, 96, PixelFormats.Pbgra32);

                rtb.Render(target);

                int bitmapW = rtb.PixelWidth;
                int bitmapH = rtb.PixelHeight;

                int halfH = bitmapH / 2;

                int bottomY = halfH;
                int bottomH = bitmapH - bottomY;
                Int32Rect rectBottom = new Int32Rect(0, bottomY, bitmapW, bottomH);

                if (!CheckRect(rtb, rectBottom))
                {
                    return;
                }

                var cropBottom = new CroppedBitmap(rtb, rectBottom);

                _g_bottom_inner.Background = new ImageBrush(cropBottom) { Stretch = Stretch.UniformToFill };

            }, DispatcherPriority.Render);
        }

        private bool CheckRect(RenderTargetBitmap source, Int32Rect rect)
        {
            if (rect.X < 0 || rect.Y < 0) return false;
            if (rect.Width <= 0 || rect.Height <= 0) return false;
            if ((rect.X + rect.Width) > source.PixelWidth) return false;
            if ((rect.Y + rect.Height) > source.PixelHeight) return false;
            return true;
        }

        private static void OnCardBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.Background = new SolidColorBrush((Color)e.NewValue);
        }

        private static void OnCardForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._t_num != null)
                control._t_num.Foreground = new SolidColorBrush((Color)e.NewValue);
        }

        private static void OnCardBorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.BorderColor = (Color)e.NewValue;
        }

        private static void OnShadowColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.ShadowColor = (Color)e.NewValue;
        }

        private static void OnCardCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.CornerRadius = (CornerRadius)e.NewValue;
        }

        private static void OnCardBorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.BorderThickness = (Thickness)e.NewValue;
        }

        private static void OnCardShadowDepthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.ShadowDepth = (double)e.NewValue;
        }

        private static void OnCardShadowBlurRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.ShadowBlurRadius = (double)e.NewValue;
        }

        private static void OnCardShadowOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._userset_ui4panel != null)
                control._userset_ui4panel.ShadowOpacity = (double)e.NewValue;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UI4FlipTextBlock)d;
            if (control._t_num != null)
            {
                control._t_num.Text = (string)e.NewValue;
            }
        }
    }
}