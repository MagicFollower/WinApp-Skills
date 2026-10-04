using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace StartUI4Controls.Internal
{
    /// <summary>
    /// 窗口打开/关闭动画辅助。
    /// 提供统一的淡入淡出 + 滑动 + 缩放 + 模糊过渡动画。
    /// </summary>
    internal static class WindowAnimationHelper
    {
        private const double SlideOffset = 90;
        private const double ScaleFrom = 0.88;
        private const double BlurRadius = 14;
        private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// 播放窗口打开动画：淡入 + 上滑 + 放大 + 去模糊。
        /// </summary>
        /// <param name="root">窗口内容根元素。</param>
        internal static void PlayOpenAnimation(FrameworkElement root)
        {
            root.Opacity = 0;
            root.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new TranslateTransform(0, SlideOffset),
                    new ScaleTransform(ScaleFrom, ScaleFrom)
                }
            };
            root.RenderTransformOrigin = new Point(0.5, 0.5);
            root.Effect = new BlurEffect { Radius = BlurRadius };

            var fadeAnim = new DoubleAnimation(0, 1, Duration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            root.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            var tg = (TransformGroup)root.RenderTransform;
            var translate = (TranslateTransform)tg.Children[0];
            var scale = (ScaleTransform)tg.Children[1];

            var slideAnim = new DoubleAnimation(SlideOffset, 0, Duration)
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } };
            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);

            var scaleAnim = new DoubleAnimation(ScaleFrom, 1, Duration)
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            var blurAnim = new DoubleAnimation(BlurRadius, 0, Duration)
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            root.Effect.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
        }

        /// <summary>
        /// 播放窗口关闭动画：淡出 + 下滑 + 缩小 + 加模糊。
        /// 动画全部完成后调用 <paramref name="onComplete"/> 回调。
        /// </summary>
        /// <param name="root">窗口内容根元素。</param>
        /// <param name="onComplete">动画完成后执行的回调。</param>
        internal static void PlayCloseAnimation(FrameworkElement root, Action onComplete)
        {
            if (!(root.RenderTransform is TransformGroup tg) || tg.Children.Count < 2) return;
            if (!(tg.Children[0] is TranslateTransform trans) || !(tg.Children[1] is ScaleTransform scale)) return;

            var blurEffect = root.Effect as BlurEffect;
            if (blurEffect == null)
            {
                blurEffect = new BlurEffect { Radius = 0 };
                root.Effect = blurEffect;
            }

            var fadeOut = new DoubleAnimation(1, 0, Duration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.HoldEnd };
            var slideDown = new DoubleAnimation(0, SlideOffset, Duration)
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.35 }, FillBehavior = FillBehavior.HoldEnd };
            var shrinkX = new DoubleAnimation(1, ScaleFrom, Duration)
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.5 }, FillBehavior = FillBehavior.HoldEnd };
            var shrinkY = new DoubleAnimation(1, ScaleFrom, Duration)
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.5 }, FillBehavior = FillBehavior.HoldEnd };
            var blurOut = new DoubleAnimation(0, BlurRadius, Duration)
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.HoldEnd };

            int completeCount = 0;
            const int totalAnim = 5;
            void OnCompleted(object s, EventArgs e)
            {
                completeCount++;
                if (completeCount >= totalAnim)
                    onComplete?.Invoke();
            }

            fadeOut.Completed += OnCompleted;
            slideDown.Completed += OnCompleted;
            shrinkX.Completed += OnCompleted;
            shrinkY.Completed += OnCompleted;
            blurOut.Completed += OnCompleted;

            root.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            trans.BeginAnimation(TranslateTransform.YProperty, slideDown);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkY);
            blurEffect.BeginAnimation(BlurEffect.RadiusProperty, blurOut);
        }
    }
}
