using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;

namespace VocabDesk.Views
{
    /// <summary>
    /// 左栏导航：两处宿主侧补丁，都是库自己没给余量导致的。
    ///
    /// ① 横向滚动条：库把项容器 Width 绑成 LeftPanelWidth（UI4NavigationView.cs:803），而容器住在面板
    /// 内部的 UI4ListBox 里，列表视口还要再扣内边距 → "容器宽 = 面板宽"恒比视口宽几个像素，
    /// 横向永远有可滚内容（面板给多宽都一样）。解法：内部列表切 IsMenuMode
    /// （UI4ListBox.cs:288 正是 true → HorizontalScrollBarVisibility=Disabled），导航栏语义上就是菜单。
    ///
    /// ② 选中指示条压在标签上：指示条画在面板左侧 x=6..10（:597-603，宽 4 + 左边距 6），
    /// 而项内容是"按面板宽居中"的。面板贴着内容收口时，最长那条标签（85 px 在 95 px 面板里）
    /// 左缘落到 x≈5，第一个字就被指示条盖住一截。
    /// 给项加 Margin 是没用的——库用的是自己的 DataTemplate（:591 把模板挂到 ItemTemplate 上），
    /// 项对象只是数据、根本不进可视树。所以余量要加在**容器**上：拿库给的 ItemContainerStyle
    /// 做 BasedOn，只补一条 Margin。容器宽 = 内容 + 余量，面板仍是按内容自适应长的，字号变大也不会失效。
    /// </summary>
    public class NavRail : UI4NavigationView
    {
        /// <summary>左边要盖过指示条（6 + 4）再留一点呼吸，右边对称少给些。</summary>
        private static readonly Thickness ItemInset = new Thickness(18, 0, 8, 0);

        /// <summary>与右侧卡片（UI4ListView.ItemCornerRadius）同一档。</summary>
        private const double PanelCornerRadius = 12;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            // 内部两个列表是库在模板应用过程中造的，且换主题会重建它们的 Style——压一挡再做，
            // 保证读到的是它们刚设完的那份 ItemContainerStyle。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PatchInternalLists();
                RoundLeftPanel();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 左栏那块白底是 PART_LeftPanel（一个 Grid）的 Background，Grid 没有 CornerRadius 可给，
        /// 而把 LeftPanelBackground 设成 Transparent 会连白底一起没掉（实测过：那样只剩选中行自己那枚
        /// 圆角药丸，整条栏变成页面底色）。所以用 Clip 裁圆——底色、指示条、项都在同一块里，
        /// 只是四个角被切掉，不改动库画的任何东西。半径与右侧卡片同一档（12）。
        /// </summary>
        private void RoundLeftPanel()
        {
            var panel = GetTemplateChild("PART_LeftPanel") as FrameworkElement;
            if (panel == null || ReferenceEquals(_roundedPanel, panel)) return;

            if (_roundedPanel != null) _roundedPanel.SizeChanged -= OnPanelSizeChanged;
            _roundedPanel = panel;
            panel.SizeChanged += OnPanelSizeChanged;
            _railScroll = FindScrollViewer(panel);
            ApplyPanelClip();
        }

        /// <summary>面板里包着导航项列表的那个 ScrollViewer——它的视口就是"看得见的左栏"。</summary>
        private static ScrollViewer FindScrollViewer(DependencyObject node)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
                var scroll = child as ScrollViewer;
                if (scroll != null) return scroll;
                var deeper = FindScrollViewer(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private FrameworkElement _roundedPanel;
        private ScrollViewer _railScroll;
        private Size _clippedTo = Size.Empty;

        /// <summary>
        /// 每轮布局都校一次尺寸。只在 Loaded 那一挡算一次是不够的：面板拿到最终高度可能晚于
        /// 我们订阅的时刻，几何就会停在旧高度——表现为上面圆角、下面被切平（用户报的"底栏没圆角"）。
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            Size arranged = base.ArrangeOverride(finalSize);
            ApplyPanelClip();
            return arranged;
        }

        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyPanelClip();
        }

        private void ApplyPanelClip()
        {
            var panel = _roundedPanel;
            if (panel == null) return;

            double w = panel.ActualWidth;
            // 面板与控件的 ActualHeight 都远大于看得见的空间（实测 1241 DIP，而窗口给的大约 790），
            // 白底因此一路画到窗口之外：圆角落在屏幕外，用户看到的就是"上面圆、下面不圆"，
            // 而且白底还会盖住页脚那一行的左半。裁到内部滚动视口的下沿 = 真的看得见的那块。
            double h = VisibleBottomInPanel(panel);
            if (w <= 0 || h <= 0) return;
            if (w == _clippedTo.Width && h == _clippedTo.Height) return;

            _clippedTo = new Size(w, h);
            panel.Clip = new RectangleGeometry(new Rect(0, 0, w, h), PanelCornerRadius, PanelCornerRadius);
        }

        /// <summary>面板内部那个 ScrollViewer 的视口下沿，换算到面板坐标系。</summary>
        private double VisibleBottomInPanel(FrameworkElement panel)
        {
            var view = _railScroll;
            if (view == null || view.ViewportHeight <= 0) return Math.Min(panel.ActualHeight, ActualHeight);

            Point bottom = view.TranslatePoint(new Point(0, view.ViewportHeight), panel);
            double cap = Math.Min(panel.ActualHeight, ActualHeight);
            return Math.Min(bottom.Y, cap);
        }

        private void PatchInternalLists()
        {
            int seen = 0;
            PatchFrom(this, ref seen);
        }

        private readonly HashSet<Style> _patched = new HashSet<Style>();

        private void PatchFrom(DependencyObject node, ref int seen)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
                var list = child as UI4ListBox;
                if (list != null)
                {
                    list.IsMenuMode = true;
                    list.ItemContainerStyle = WithInset(list.ItemContainerStyle);
                    seen++;
                }
                PatchFrom(child, ref seen);
            }
        }

        /// <summary>
        /// 在库给的那份样式上只加一条 Margin。库换主题会重新发一份新样式（认不出来就再套一次），
        /// 所以用"我造过哪些"来判等，避免 BasedOn 链越套越长。
        /// </summary>
        private Style WithInset(Style baseStyle)
        {
            if (baseStyle == null || _patched.Contains(baseStyle)) return baseStyle;

            var style = new Style(typeof(ListBoxItem), baseStyle);
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, ItemInset));
            _patched.Add(style);
            return style;
        }
    }
}
