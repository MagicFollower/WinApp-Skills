using System.Windows;
using System.Windows.Controls;
using VocabDesk.ViewModels;

namespace VocabDesk.Views
{
    /// <summary>
    /// 设置浮层的内容。这里只放"把点击转给视图模型"的胶水：
    /// 默认值与区间住在 Helpers/Typography.cs，状态住在 Services/SettingsService.cs，
    /// 代码后置里再写一份常量就是第二处真源，迟早会漂。
    /// </summary>
    public partial class SettingsOverlay : UserControl
    {
        public SettingsOverlay()
        {
            InitializeComponent();
        }

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        private void ResetTypography_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.ResetTypography();
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.ResetZoom();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.IsSettingsOpen = false;
        }
    }
}
