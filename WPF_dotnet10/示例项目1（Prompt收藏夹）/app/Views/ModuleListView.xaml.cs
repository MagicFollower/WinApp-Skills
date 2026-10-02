using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StartUI4Controls;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using PromptFavorites.ViewModels;

namespace PromptFavorites.Views
{
    public partial class ModuleListView : UserControl
    {
        private UI4ContextMenu _contextMenu;
        private ModuleListViewModel _vm;

        public ModuleListView()
        {
            InitializeComponent();
            Loaded += ModuleListView_Loaded;
            Unloaded += ModuleListView_Unloaded;
            DataContextChanged += ModuleListView_DataContextChanged;
        }

        private void ModuleListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_contextMenu == null)
            {
                _contextMenu = new UI4ContextMenu();
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Delete, "\u91CD\u547D\u540D", null,
                    delegate { OnRenameModule(); }));
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Delete, "\u5220\u9664", null,
                    delegate { OnDeleteModule(); }));
                _contextMenu.Attach(ModuleList);
            }

            AttachVm();
        }

        private void ModuleListView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_contextMenu != null)
            {
                _contextMenu.Detach();
                _contextMenu = null;
            }
        }

        private void ModuleListView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachVm();
        }

        private void AttachVm()
        {
            DetachVm();
            _vm = DataContext as ModuleListViewModel;
            if (_vm == null) return;
            _vm.RequestNewModuleName += OnRequestNewModuleName;
            UpdateSortButtons(_vm.CurrentSort);
        }

        private void DetachVm()
        {
            if (_vm == null) return;
            _vm.RequestNewModuleName -= OnRequestNewModuleName;
            _vm = null;
        }

        private void OnRequestNewModuleName(string _)
        {
            var name = DialogHelper.ShowInputDialog(this, "\u65B0\u5EFA\u6A21\u5757", "\u6A21\u5757\u540D\u79F0:", "");
            if (string.IsNullOrWhiteSpace(name)) return;

            var vm = DataContext as ModuleListViewModel;
            if (vm == null) return;

            if (vm.Modules.Count > 0)
            {
                foreach (var m in vm.Modules)
                {
                    if (m.Name == name.Trim())
                    {
                        UI4MessageBox.Show("\u5DF2\u5B58\u5728\u540C\u540D\u6A21\u5757\u3002", "\u63D0\u793A",
                            UI4MessageBoxButtons.OK, 320);
                        return;
                    }
                }
            }

            vm.AddModule(name);
        }

        private void OnRenameModule()
        {
            var vm = DataContext as ModuleListViewModel;
            if (vm == null || vm.SelectedModule == null) return;

            var newName = DialogHelper.ShowInputDialog(this, "\u91CD\u547D\u540D\u6A21\u5757",
                "\u65B0\u540D\u79F0:", vm.SelectedModule.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (newName.Trim() != vm.SelectedModule.Name)
            {
                foreach (var m in vm.Modules)
                {
                    if (m.Name == newName.Trim())
                    {
                        UI4MessageBox.Show("\u5DF2\u5B58\u5728\u540C\u540D\u6A21\u5757\u3002", "\u63D0\u793A",
                            UI4MessageBoxButtons.OK, 320);
                        return;
                    }
                }
            }

            vm.RenameModule(vm.SelectedModule.Name, newName);
        }

        private void OnDeleteModule()
        {
            var vm = DataContext as ModuleListViewModel;
            if (vm == null || vm.SelectedModule == null) return;

            if (vm.SelectedModule.EntryCount > 0)
            {
                UI4MessageBox.Show("\u8BE5\u6A21\u5757\u4E0B\u8FD8\u6709\u6761\u76EE\uFF0C\u8BF7\u5148\u79FB\u52A8\u6216\u5220\u9664\u3002",
                    "\u65E0\u6CD5\u5220\u9664", UI4MessageBoxButtons.OK, 360);
                return;
            }

            var result = UI4MessageBox.Show(
                "\u786E\u5B9A\u5220\u9664\u6A21\u5757\u201C" + vm.SelectedModule.Name + "\u201D\uFF1F",
                "\u5220\u9664\u786E\u8BA4",
                UI4MessageBoxButtons.OKCancel, 360);

            if (result == true)
                vm.DeleteModule(vm.SelectedModule.Name);
        }

        private void SortCreatedAt_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ModuleListViewModel;
            if (vm != null) vm.CurrentSort = ModuleSortMode.CreatedAt;
            UpdateSortButtons(ModuleSortMode.CreatedAt);
        }

        private void SortName_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ModuleListViewModel;
            if (vm != null) vm.CurrentSort = ModuleSortMode.Name;
            UpdateSortButtons(ModuleSortMode.Name);
        }

        private void SortCustom_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ModuleListViewModel;
            if (vm != null) vm.CurrentSort = ModuleSortMode.Custom;
            UpdateSortButtons(ModuleSortMode.Custom);
        }

        private void UpdateSortButtons(ModuleSortMode mode)
        {
            SetSortBtnStyle(SortCreatedAtBtn, mode == ModuleSortMode.CreatedAt);
            SetSortBtnStyle(SortNameBtn, mode == ModuleSortMode.Name);
            SetSortBtnStyle(SortCustomBtn, mode == ModuleSortMode.Custom);
        }

        private static void SetSortBtnStyle(UI4Button btn, bool selected)
        {
            if (btn == null) return;

            if (selected)
            {
                btn.GradientStart = Theme.Accent;
                btn.GradientEnd = Theme.Accent;
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.GradientStart = Theme.Neutral;
                btn.GradientEnd = Theme.Neutral;
                btn.Foreground = Brushes.Black;
            }
        }
    }
}
