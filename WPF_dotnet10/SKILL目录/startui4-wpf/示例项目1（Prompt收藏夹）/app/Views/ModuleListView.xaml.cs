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
                    UI4MenuItemType.Delete, "重命名", null,
                    delegate { OnRenameModule(); }));
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Delete, "删除", null,
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
            var name = DialogHelper.ShowInputDialog(this, "新建模块", "模块名称:", "");
            if (string.IsNullOrWhiteSpace(name)) return;

            var vm = DataContext as ModuleListViewModel;
            if (vm == null) return;

            if (vm.Modules.Count > 0)
            {
                foreach (var m in vm.Modules)
                {
                    if (m.Name == name.Trim())
                    {
                        UI4MessageBox.Show("已存在同名模块。", "提示",
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

            var newName = DialogHelper.ShowInputDialog(this, "重命名模块",
                "新名称:", vm.SelectedModule.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (newName.Trim() != vm.SelectedModule.Name)
            {
                foreach (var m in vm.Modules)
                {
                    if (m.Name == newName.Trim())
                    {
                        UI4MessageBox.Show("已存在同名模块。", "提示",
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
                UI4MessageBox.Show("该模块下还有条目，请先移动或删除。",
                    "无法删除", UI4MessageBoxButtons.OK, 360);
                return;
            }

            var result = UI4MessageBox.Show(
                "确定删除模块“" + vm.SelectedModule.Name + "”？",
                "删除确认",
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
            PaintChip(SortCreatedAtBtn, mode == ModuleSortMode.CreatedAt);
            PaintChip(SortNameBtn, mode == ModuleSortMode.Name);
            PaintChip(SortCustomBtn, mode == ModuleSortMode.Custom);
        }

        /// <summary>底色挂令牌资源引用、前景交还给库，口径与中栏排序胶囊一致（见 EntryListView.PaintChip）。</summary>
        private static void PaintChip(UI4Button btn, bool selected)
        {
            if (btn == null) return;

            string key = selected ? "UI4.Color.Accent" : "UI4.Color.OffBackground";
            btn.SetResourceReference(UI4Button.GradientStartProperty, key);
            btn.SetResourceReference(UI4Button.GradientEndProperty, key);
            btn.ClearValue(Control.ForegroundProperty);
        }
    }
}
