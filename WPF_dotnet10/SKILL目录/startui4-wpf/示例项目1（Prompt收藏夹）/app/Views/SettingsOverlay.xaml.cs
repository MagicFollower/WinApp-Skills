using System.Windows;
using System.Windows.Controls;
using PromptFavorites.ViewModels;

namespace PromptFavorites.Views
{
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

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm != null) vm.ToggleThemeMode();
        }

        /// <summary>默认值只由 MainViewModel 转给 Typography，这里不写第二份常量。</summary>
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
