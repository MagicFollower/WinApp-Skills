using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Markup;
using System.Xml;
using StartUI4Controls.Internal;

namespace StartUI4Controls
{

    /// <summary>
    /// 现代风格的滚动查看器控件，支持平滑滚动动画和自定义滚动条样式。
    /// </summary>
    /// <remarks>
    /// <para>继承自 <see cref="System.Windows.Controls.ScrollViewer"/>，提供平滑滚动动画支持。</para>
    /// </remarks>
    public class UI4ScrollViewer : ScrollViewer
    {
        private double _targetVerticalOffset;
        private double _targetHorizontalOffset;
        private bool _isAnimatingVertical;
        private bool _isAnimatingHorizontal;
        private const double AnimationDuration = 100;

        public static readonly DependencyProperty IsSmoothScrollEnabledProperty =
            DependencyProperty.Register(
                nameof(IsSmoothScrollEnabled),
                typeof(bool),
                typeof(UI4ScrollViewer),
                new PropertyMetadata(true));

        public bool IsSmoothScrollEnabled
        {
            get => (bool)GetValue(IsSmoothScrollEnabledProperty);
            set => SetValue(IsSmoothScrollEnabledProperty, value);
        }

        static UI4ScrollViewer()
        {
        }

        public UI4ScrollViewer()
        {
            var scrollStyle = ScrollBarResources.GetScrollViewerStyle();
            if (scrollStyle != null)
            {
                Style = scrollStyle;
            }
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _targetVerticalOffset = VerticalOffset;
            _targetHorizontalOffset = HorizontalOffset;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (!IsSmoothScrollEnabled)
            {
                base.OnMouseWheel(e);
                return;
            }

            e.Handled = true;

            double scrollAmount = e.Delta;
            double lineHeight = 16;
            double totalDelta = scrollAmount / 120.0 * 3 * lineHeight;

            _targetVerticalOffset = Math.Max(0, Math.Min(ScrollableHeight, _targetVerticalOffset - totalDelta));

            AnimateVerticalOffset(VerticalOffset, _targetVerticalOffset);
        }

        private void AnimateVerticalOffset(double from, double to)
        {
            if (Math.Abs(from - to) < 0.1) return;

            double startTime = Environment.TickCount;
            double duration = AnimationDuration;
            double startVal = from;
            double endVal = to;

            EventHandler renderHandler = null;
            renderHandler = (s, e) =>
            {
                double elapsed = Environment.TickCount - startTime;
                if (elapsed >= duration)
                {
                    CompositionTarget.Rendering -= renderHandler;
                    ScrollToVerticalOffset(endVal);
                    _isAnimatingVertical = false;
                    return;
                }

                double t = elapsed / duration;
                double eased = EaseOutCubic(t);
                double currentVal = startVal + (endVal - startVal) * eased;
                ScrollToVerticalOffset(currentVal);
            };

            CompositionTarget.Rendering += renderHandler;
            _isAnimatingVertical = true;
        }

        private static double EaseOutCubic(double t)
        {
            return 1 - Math.Pow(1 - t, 3);
        }

        public void SmoothScrollToVerticalOffset(double offset)
        {
            offset = Math.Max(0, Math.Min(ScrollableHeight, offset));
            _targetVerticalOffset = offset;
            AnimateVerticalOffset(VerticalOffset, offset);
        }

        public void SmoothScrollToHorizontalOffset(double offset)
        {
            offset = Math.Max(0, Math.Min(ScrollableWidth, offset));
            _targetHorizontalOffset = offset;
            AnimateHorizontalOffset(HorizontalOffset, offset);
        }

        private void AnimateHorizontalOffset(double from, double to)
        {
            if (Math.Abs(from - to) < 0.1) return;

            double startTime = Environment.TickCount;
            double duration = AnimationDuration;
            double startVal = from;
            double endVal = to;

            EventHandler renderHandler = null;
            renderHandler = (s, e) =>
            {
                double elapsed = Environment.TickCount - startTime;
                if (elapsed >= duration)
                {
                    CompositionTarget.Rendering -= renderHandler;
                    ScrollToHorizontalOffset(endVal);
                    _isAnimatingHorizontal = false;
                    return;
                }

                double t = elapsed / duration;
                double eased = EaseOutCubic(t);
                double currentVal = startVal + (endVal - startVal) * eased;
                ScrollToHorizontalOffset(currentVal);
            };

            CompositionTarget.Rendering += renderHandler;
            _isAnimatingHorizontal = true;
        }

    }
}
