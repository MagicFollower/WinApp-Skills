using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PromptFavorites.Helpers
{
    /// <summary>拖动落点请求：被拖的行 + 落在第几道行间隙（0 表示最上方，Count 表示最下方）。</summary>
    public sealed class ReorderRequest
    {
        public ReorderRequest(object item, int gap)
        {
            Item = item;
            Gap = gap;
        }

        public object Item { get; private set; }
        public int Gap { get; private set; }
    }

    /// <summary>
    /// 挂在列表上的行内拖动排序。只在 Enabled 为 true 时接管左键拖动；
    /// 落下时不自己改集合，而是把落点交给 CommitCommand，由 ViewModel 用 Move 移动并写配置，
    /// 避免 Remove/Insert 把选中行冲成 null 连带清空右栏。
    /// </summary>
    public static class ReorderBehavior
    {
        private const string ReorderFormat = "PromptFavorites.ReorderItem";

        private static readonly DependencyPropertyKey ControllerPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly("ReorderController",
                typeof(DragController), typeof(ReorderBehavior), new PropertyMetadata(null));

        private static readonly DependencyProperty ControllerProperty = ControllerPropertyKey.DependencyProperty;

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ReorderBehavior),
                new PropertyMetadata(false, OnEnabledChanged));

        public static readonly DependencyProperty CommitCommandProperty =
            DependencyProperty.RegisterAttached("CommitCommand", typeof(ICommand), typeof(ReorderBehavior),
                new PropertyMetadata(null));

        public static void SetEnabled(DependencyObject target, bool value)
        {
            target.SetValue(EnabledProperty, value);
        }

        public static bool GetEnabled(DependencyObject target)
        {
            return (bool)target.GetValue(EnabledProperty);
        }

        public static void SetCommitCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(CommitCommandProperty, value);
        }

        public static ICommand GetCommitCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(CommitCommandProperty);
        }

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var list = d as ListBox;
            if (list == null) return;

            var controller = list.GetValue(ControllerProperty) as DragController;

            if (!(bool)e.NewValue)
            {
                if (controller == null) return;
                controller.Detach();
                list.SetValue(ControllerPropertyKey, null);
                return;
            }

            if (controller == null)
            {
                controller = new DragController(list);
                list.SetValue(ControllerPropertyKey, controller);
                controller.Attach();
            }
        }

        private sealed class DragController
        {
            private readonly ListBox _list;
            private object _dragItem;
            private Point _startPoint;
            private InsertionAdorner _adorner;
            private ListBoxItem _adornerOwner;
            private bool _adornerBelow;
            private int _gap = -1;

            public DragController(ListBox list)
            {
                _list = list;
            }

            public void Attach()
            {
                _list.AllowDrop = true;
                _list.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                _list.PreviewMouseMove += OnPreviewMouseMove;
                _list.DragOver += OnDragOver;
                _list.DragLeave += OnDragLeave;
                _list.Drop += OnDrop;
            }

            public void Detach()
            {
                _list.AllowDrop = false;
                _list.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                _list.PreviewMouseMove -= OnPreviewMouseMove;
                _list.DragOver -= OnDragOver;
                _list.DragLeave -= OnDragLeave;
                _list.Drop -= OnDrop;
                ClearAdorner();
                _dragItem = null;
                _gap = -1;
            }

            private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            {
                _dragItem = ItemAt(e.GetPosition(_list));
                _startPoint = e.GetPosition(_list);
                if (_dragItem == null) _gap = -1;
            }

            private void OnPreviewMouseMove(object sender, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;

                var position = e.GetPosition(_list);
                if (Math.Abs(position.X - _startPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(position.Y - _startPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;

                DragDrop.DoDragDrop(_list, new DataObject(ReorderFormat, _dragItem), DragDropEffects.Move);

                // DoDragDrop 是阻塞的，落下后才走到这里收尾
                _dragItem = null;
                _gap = -1;
                ClearAdorner();
            }

            private void OnDragOver(object sender, DragEventArgs e)
            {
                if (!e.Data.GetDataPresent(ReorderFormat))
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                _gap = GapAt(e.GetPosition(_list));
                ShowAdorner(_gap);
                AutoScroll(e.GetPosition(_list));
            }

            private void OnDragLeave(object sender, DragEventArgs e)
            {
                _gap = -1;
                ClearAdorner();
            }

            private void OnDrop(object sender, DragEventArgs e)
            {
                var gap = _gap;
                _gap = -1;
                ClearAdorner();

                if (!e.Data.GetDataPresent(ReorderFormat) || gap < 0)
                {
                    e.Handled = true;
                    return;
                }

                var item = e.Data.GetData(ReorderFormat);
                var command = GetCommitCommand(_list);
                if (command == null) return;

                var request = new ReorderRequest(item, gap);
                if (command.CanExecute(request)) command.Execute(request);
                e.Handled = true;
            }

            /// <summary>指针落在哪两道行间隙之间；越过最后一行算 Count。</summary>
            private int GapAt(Point position)
            {
                var count = _list.Items.Count;
                if (count == 0) return -1;

                for (int i = 0; i < count; i++)
                {
                    var container = _list.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (container == null) continue;

                    var top = container.TranslatePoint(new Point(0, 0), _list).Y;
                    var bottom = top + container.ActualHeight;

                    if (position.Y < bottom)
                        return position.Y < top + container.ActualHeight / 2 ? i : i + 1;
                }

                return count;
            }

            private void ShowAdorner(int gap)
            {
                var count = _list.Items.Count;
                if (count == 0 || gap < 0) return;

                var anchorIndex = gap >= count ? count - 1 : (gap == 0 ? 0 : gap - 1);
                var below = gap >= count ? true : gap != 0;
                var container = _list.ItemContainerGenerator.ContainerFromIndex(anchorIndex) as ListBoxItem;
                if (container == null) return;

                if (!ReferenceEquals(container, _adornerOwner) || _adornerBelow != below)
                {
                    ClearAdorner();
                    var layer = AdornerLayer.GetAdornerLayer(container);
                    if (layer == null) return;
                    _adorner = new InsertionAdorner(container, below);
                    _adornerOwner = container;
                    _adornerBelow = below;
                    layer.Add(_adorner);
                    return;
                }
            }

            private void ClearAdorner()
            {
                if (_adorner == null) return;

                var layer = AdornerLayer.GetAdornerLayer(_adornerOwner);
                if (layer != null) layer.Remove(_adorner);

                _adorner = null;
                _adornerOwner = null;
            }

            private void AutoScroll(Point position)
            {
                var scroll = FindScrollViewer(_list);
                if (scroll == null || scroll.ScrollableHeight <= 0) return;

                var edge = 24.0;
                var height = _list.ActualHeight;

                if (position.Y < edge)
                    scroll.LineUp();
                else if (position.Y > height - edge)
                    scroll.LineDown();
            }

            private static ScrollViewer FindScrollViewer(DependencyObject root)
            {
                var count = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(root, i);
                    var viewer = child as ScrollViewer;
                    if (viewer != null) return viewer;

                    var deeper = FindScrollViewer(child);
                    if (deeper != null) return deeper;
                }
                return null;
            }

            private object ItemAt(Point position)
            {
                var element = _list.InputHitTest(position) as DependencyObject;
                while (element != null && !(element is ListBoxItem))
                {
                    if (ReferenceEquals(element, _list)) return null;
                    element = VisualTreeHelper.GetParent(element);
                }

                var container = element as ListBoxItem;
                return container != null ? container.Content : null;
            }
        }

        private sealed class InsertionAdorner : Adorner
        {
            private readonly Pen _pen;
            private readonly bool _below;

            public InsertionAdorner(UIElement adorned, bool below)
                : base(adorned)
            {
                IsHitTestVisible = false;
                _below = below;
                _pen = new Pen(new SolidColorBrush(Theme.Accent), 2);
                _pen.Freeze();
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                var size = AdornedElement.RenderSize;
                var y = _below ? size.Height : 0;
                drawingContext.DrawLine(_pen, new Point(0, y), new Point(Math.Max(0, size.Width - 4), y));
            }
        }
    }
}
