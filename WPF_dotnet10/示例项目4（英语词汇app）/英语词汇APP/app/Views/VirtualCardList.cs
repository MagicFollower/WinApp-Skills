using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;

namespace VocabDesk.Views
{
    /// <summary>
    /// 卡片列表：把虚拟化真正接上。三处缺一不可，且后两处都是库没做的半步。
    ///
    /// ① 面板：UI4ListView 的 Style 是库内代码搭的（lib/UI4ListView.cs:339 BuildTechCardStyle），
    /// 换了 Template 却没设 ItemsPanel；而静态构造把 DefaultStyleKey 指到自己类型上（:248-250），
    /// 库里又是一个 .xaml 都没有（ThemeInfo 的 ResourceLocation=None），于是"主题样式提供
    /// VirtualizingStackPanel"这条兜底通路根本不存在，ItemsPanel 落空 → 普通 StackPanel → 全量建容器。
    /// 同族的 UI4GridView 是显式设了 ItemsPanel 的（:417），所以只有 ListView 这一支漏了。
    ///
    /// ② ScrollViewer.CanContentScroll：模板里那个 ScrollViewer 只设了滚动条可见性与底色（:345-359），
    /// 没设这个，默认 false = 按像素滚。
    ///
    /// ③ 真正卡住的一条：库自带的 ScrollViewer 模板（:610-632，与 Internal/ScrollBarResources.cs:370-378
    /// 同一份）里那个 ScrollContentPresenter **没有** CanContentScroll 的 TemplateBinding——
    /// WPF 默认模板是有的。少了这句，presenter 自己那份 CanContentScroll 停在默认 false，
    /// 它就用"无限高"去量内容并按像素滚，②设成 true 也一点作用都没有。
    /// 实测（本机，300 项 + vh=465）：① ② 都补上后面板已是 VirtualizingStackPanel，
    /// 但 children 仍等于 300；补上 ③ 才降到视口那一小撮。
    ///
    /// 三处都该改在 lib/UI4ListView.cs；改组件源码要先过用户同意，所以先在宿主侧用公开 API 接上。
    /// ②③ 写在模板应用点而不是构造后一次：换主题会整份重建 Style 与模板，重建后又要再设一次。
    /// </summary>
    public class VirtualCardList : UI4ListView
    {
        public VirtualCardList()
        {
            ItemsPanel = new ItemsPanelTemplate(
                new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            VirtualizingStackPanel.SetIsVirtualizing(this, true);
            VirtualizingStackPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            // 滚动单位改像素：默认 Item 挡下一格滚轮 = 3 个条目，而卡片高度差极大
            // （折叠态约 60 DIP，展开态能到 400+ DIP），于是"滑一下跳过好几张"。
            // 像素挡仍然按视口虚拟化，只是滚动步进变成像素。
            VirtualizingStackPanel.SetScrollUnit(this, ScrollUnit.Pixel);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_ContentHost") is ScrollViewer host)
            {
                ScrollViewer.SetCanContentScroll(host, true);
                // 必须同步把 presenter 也设好：换主题会整份重建 Style 与模板，重建后的第一轮 measure
                // 就在这之后立刻发生。挂到 Dispatcher 的 Loaded 上等于让那一轮用"无限高"去量内容 →
                // 14,625 个容器全建出来 → UI 线程再也回不来（实测：第一次切换 26.9 ms 成功，
                // 紧接着的那轮布局把进程钉死，responding=False、工作集涨到 548 MB）。
                // ApplyTemplate() 在这里的作用就是把 presenter 当场造出来，好让它这一轮就是对的。
                host.ApplyTemplate();
                if (!EnablePresenterItemScrolling(host))
                {
                    // 极少数情况下（还没进可视树）模板造不出来，退回延迟一挡，别就此放弃
                    Dispatcher.BeginInvoke(new Action(() => EnablePresenterItemScrolling(host)),
                        DispatcherPriority.Loaded);
                }
            }
        }

        /// <summary>在子树里找那个 ScrollContentPresenter 并把它的 CanContentScroll 置 true。</summary>
        private static bool EnablePresenterItemScrolling(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollContentPresenter presenter)
                {
                    presenter.CanContentScroll = true;
                    return true;
                }
                if (EnablePresenterItemScrolling(child)) return true;
            }
            return false;
        }
    }
}
