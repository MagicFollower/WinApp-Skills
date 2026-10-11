using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StartUI4Controls
{
    /// <summary>带页面级渐变底的 Grid。渐变两端颜色挂主题令牌，切换主题自动跟随。</summary>
    public class UI4Grid : Grid
    {
        public static readonly DependencyProperty GradientStartProperty =
            DependencyProperty.Register(
                nameof(GradientStart),
                typeof(Color),
                typeof(UI4Grid),
                new PropertyMetadata(Colors.White, OnGradientChanged));

        public Color GradientStart
        {
            get => (Color)GetValue(GradientStartProperty);
            set => SetValue(GradientStartProperty, value);
        }

        public static readonly DependencyProperty GradientEndProperty =
            DependencyProperty.Register(
                nameof(GradientEnd),
                typeof(Color),
                typeof(UI4Grid),
                new PropertyMetadata(Colors.White, OnGradientChanged));

        public Color GradientEnd
        {
            get => (Color)GetValue(GradientEndProperty);
            set => SetValue(GradientEndProperty, value);
        }

        public UI4Grid()
        {
            SetResourceReference(GradientStartProperty, "UI4.Color.BackgroundGradientStart");
            SetResourceReference(GradientEndProperty, "UI4.Color.BackgroundGradientEnd");
            UpdateBackground();
        }

        private static void OnGradientChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((UI4Grid)d).UpdateBackground();
        }

        private void UpdateBackground()
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(GradientStart, 0.0),
                    new GradientStop(GradientEnd, 1.0)
                }
            };
        }
    }
}
