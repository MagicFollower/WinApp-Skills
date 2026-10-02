using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StartUI4Controls.Internal
{
    /// <summary>
    /// 窗口 8 方向 resize 行为。
    /// 为指定窗口添加 8 个透明 resize 边框并处理拖拽缩放逻辑。
    /// </summary>
    internal class WindowResizeBehavior
    {
        private int _direction;
        private Point _startPoint;
        private double _startWidth, _startHeight;
        private readonly Window _window;

        /// <summary>resize 边框宽度/高度。</summary>
        public const double ThumbSize = 8;
        /// <summary>窗口最小宽度。</summary>
        public const double MinWindowWidth = 200;
        /// <summary>窗口最小高度。</summary>
        public const double MinWindowHeight = 150;

        /// <summary>
        /// 初始化 <see cref="WindowResizeBehavior"/> 的新实例。
        /// </summary>
        /// <param name="window">要附加 resize 行为的窗口。</param>
        public WindowResizeBehavior(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>
        /// 在指定 Grid 中创建 8 个透明 resize 边框并绑定事件。
        /// </summary>
        /// <param name="resizeGrid">用于容纳 resize 边框的 Grid。</param>
        public void Attach(Grid resizeGrid)
        {
            var left = CreateResizeBorder(ThumbSize, 0, HorizontalAlignment.Left, VerticalAlignment.Stretch, Cursors.SizeWE, 1);
            var right = CreateResizeBorder(ThumbSize, 0, HorizontalAlignment.Right, VerticalAlignment.Stretch, Cursors.SizeWE, 2);
            var top = CreateResizeBorder(0, ThumbSize, HorizontalAlignment.Stretch, VerticalAlignment.Top, Cursors.SizeNS, 3);
            var bottom = CreateResizeBorder(0, ThumbSize, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Cursors.SizeNS, 4);
            var topLeft = CreateResizeBorder(ThumbSize, ThumbSize, HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE, 5);
            var topRight = CreateResizeBorder(ThumbSize, ThumbSize, HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW, 6);
            var bottomLeft = CreateResizeBorder(ThumbSize, ThumbSize, HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW, 7);
            var bottomRight = CreateResizeBorder(ThumbSize, ThumbSize, HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE, 8);

            resizeGrid.Children.Add(left);
            resizeGrid.Children.Add(right);
            resizeGrid.Children.Add(top);
            resizeGrid.Children.Add(bottom);
            resizeGrid.Children.Add(topLeft);
            resizeGrid.Children.Add(topRight);
            resizeGrid.Children.Add(bottomLeft);
            resizeGrid.Children.Add(bottomRight);

            left.MouseLeftButtonDown += OnResizeBorderMouseDown;
            right.MouseLeftButtonDown += OnResizeBorderMouseDown;
            top.MouseLeftButtonDown += OnResizeBorderMouseDown;
            bottom.MouseLeftButtonDown += OnResizeBorderMouseDown;
            topLeft.MouseLeftButtonDown += OnResizeBorderMouseDown;
            topRight.MouseLeftButtonDown += OnResizeBorderMouseDown;
            bottomLeft.MouseLeftButtonDown += OnResizeBorderMouseDown;
            bottomRight.MouseLeftButtonDown += OnResizeBorderMouseDown;
        }

        private static Border CreateResizeBorder(double width, double height,
            HorizontalAlignment hAlign, VerticalAlignment vAlign, Cursor cursor, int direction)
        {
            var border = new Border
            {
                Width = width > 0 ? width : double.NaN,
                Height = height > 0 ? height : double.NaN,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Cursor = cursor,
                Background = Brushes.Transparent,
                Tag = direction
            };
            return border;
        }

        private void OnResizeBorderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b)
                StartResize(e, (int)b.Tag);
        }

        /// <summary>开始 resize 操作：捕获鼠标并记录初始状态。</summary>
        private void StartResize(MouseButtonEventArgs e, int direction)
        {
            _direction = direction;
            _startPoint = _window.PointToScreen(e.GetPosition(_window));
            _startWidth = _window.Width;
            _startHeight = _window.Height;
            Mouse.Capture(_window);
            _window.MouseMove += DoResize;
            _window.MouseLeftButtonUp += EndResize;
        }

        /// <summary>处理 resize 拖拽：根据方向和偏移量调整窗口尺寸。</summary>
        private void DoResize(object sender, MouseEventArgs e)
        {
            if (_direction == 0 || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = _window.PointToScreen(e.GetPosition(_window));
            double dx = current.X - _startPoint.X;
            double dy = current.Y - _startPoint.Y;

            switch (_direction)
            {
                case 1: _window.Width = Math.Max(MinWindowWidth, _startWidth - dx); break;
                case 2: _window.Width = Math.Max(MinWindowWidth, _startWidth + dx); break;
                case 3: _window.Height = Math.Max(MinWindowHeight, _startHeight - dy); break;
                case 4: _window.Height = Math.Max(MinWindowHeight, _startHeight + dy); break;
                case 5: _window.Width = Math.Max(MinWindowWidth, _startWidth - dx); _window.Height = Math.Max(MinWindowHeight, _startHeight - dy); break;
                case 6: _window.Width = Math.Max(MinWindowWidth, _startWidth + dx); _window.Height = Math.Max(MinWindowHeight, _startHeight - dy); break;
                case 7: _window.Width = Math.Max(MinWindowWidth, _startWidth - dx); _window.Height = Math.Max(MinWindowHeight, _startHeight + dy); break;
                case 8: _window.Width = Math.Max(MinWindowWidth, _startWidth + dx); _window.Height = Math.Max(MinWindowHeight, _startHeight + dy); break;
            }
        }

        /// <summary>结束 resize 操作：释放鼠标捕获并清理事件。</summary>
        private void EndResize(object sender, MouseButtonEventArgs e)
        {
            _direction = 0;
            Mouse.Capture(null);
            _window.MouseMove -= DoResize;
            _window.MouseLeftButtonUp -= EndResize;
        }
    }
}
