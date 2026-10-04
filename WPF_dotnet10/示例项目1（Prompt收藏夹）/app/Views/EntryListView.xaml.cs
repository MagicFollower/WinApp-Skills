using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StartUI4Controls;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using PromptFavorites.ViewModels;

namespace PromptFavorites.Views
{
    public partial class EntryListView : UserControl
    {
        private UI4ContextMenu _contextMenu;
        private EntryListViewModel _vm;

        public EntryListView()
        {
            InitializeComponent();
            Loaded += EntryListView_Loaded;
            Unloaded += EntryListView_Unloaded;
            DataContextChanged += EntryListView_DataContextChanged;
        }

        private void EntryListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_contextMenu == null)
            {
                _contextMenu = new UI4ContextMenu();
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Copy, "重命名", null,
                    delegate { OnRenameEntry(); }));
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Delete, "删除", null,
                    delegate { OnDeleteEntry(); }));
                _contextMenu.Attach(EntryList);
            }

            AttachVm();
        }

        private void EntryListView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_contextMenu != null)
            {
                _contextMenu.Detach();
                _contextMenu = null;
            }
        }

        private void EntryListView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachVm();
        }

        private void AttachVm()
        {
            DetachVm();
            _vm = DataContext as EntryListViewModel;
            if (_vm == null) return;

            _vm.RequestNewEntryName += OnRequestNewEntryName;
            UpdateSortButtons(_vm.CurrentSort);
            UpdateFavoriteButton(_vm.IsFavoriteFilter);
        }

        private void DetachVm()
        {
            if (_vm == null) return;
            _vm.RequestNewEntryName -= OnRequestNewEntryName;
            _vm = null;
        }

        private void FavoriteFilter_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm == null) return;
            vm.IsFavoriteFilter = !vm.IsFavoriteFilter;
            UpdateFavoriteButton(vm.IsFavoriteFilter);
        }

        private void UpdateFavoriteButton(bool active)
        {
            PaintChip(FavoriteBtn, active);
        }

        private void UpdateSortButtons(SortMode mode)
        {
            PaintChip(SortUseCountBtn, mode == SortMode.UseCount);
            PaintChip(SortUpdatedAtBtn, mode == SortMode.UpdatedAt);
            PaintChip(SortCreatedAtBtn, mode == SortMode.CreatedAt);
            PaintChip(SortNameBtn, mode == SortMode.Name);
            PaintChip(SortCustomBtn, mode == SortMode.Custom);
        }

        /// <summary>
        /// 选中态的底色挂令牌资源引用（切档自动跟随），前景清掉本地值交还给库：
        /// <c>UI4Button</c> 在 <c>OnAccent</c> 与正文色之间取与底色对比度更高者，
        /// 而本地赋 <c>Brushes.White/Black</c> 占的就是本地值槽，会把这条自动判据顶掉
        /// ——浅色档看不出问题，夜景档会变成白字压亮靛（实测 2.67:1）。
        /// </summary>
        private static void PaintChip(UI4Button btn, bool selected)
        {
            if (btn == null) return;

            string key = selected ? "UI4.Color.Accent" : "UI4.Color.OffBackground";
            btn.SetResourceReference(UI4Button.GradientStartProperty, key);
            btn.SetResourceReference(UI4Button.GradientEndProperty, key);
            btn.ClearValue(Control.ForegroundProperty);
        }

        private void SortUseCount_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm != null) vm.CurrentSort = SortMode.UseCount;
            UpdateSortButtons(SortMode.UseCount);
        }

        private void SortUpdatedAt_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm != null) vm.CurrentSort = SortMode.UpdatedAt;
            UpdateSortButtons(SortMode.UpdatedAt);
        }

        private void SortCreatedAt_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm != null) vm.CurrentSort = SortMode.CreatedAt;
            UpdateSortButtons(SortMode.CreatedAt);
        }

        private void SortName_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm != null) vm.CurrentSort = SortMode.Name;
            UpdateSortButtons(SortMode.Name);
        }

        private void SortCustom_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm != null) vm.CurrentSort = SortMode.Custom;
            UpdateSortButtons(SortMode.Custom);
        }

        private void QuickCopy_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as PromptItem;
            if (item == null) return;

            var vm = DataContext as EntryListViewModel;
            if (vm != null && vm.QuickCopyCommand.CanExecute(item))
                vm.QuickCopyCommand.Execute(item);
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as PromptItem;
            if (item == null) return;

            var vm = DataContext as EntryListViewModel;
            if (vm != null && vm.ToggleEntryFavoriteCommand.CanExecute(item))
                vm.ToggleEntryFavoriteCommand.Execute(item);
        }

        private void OnRequestNewEntryName(string _)
        {
            var vm = DataContext as EntryListViewModel;
            if (vm == null) return;
            vm.AddEntry();
        }

        private void OnRenameEntry()
        {
            var vm = DataContext as EntryListViewModel;
            if (vm == null || vm.SelectedEntry == null) return;

            var newName = DialogHelper.ShowInputDialog(this, "重命名条目",
                "新标题:", vm.SelectedEntry.Title);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (!FrontmatterData.IsValidTitle(newName.Trim()))
            {
                UI4MessageBox.Show("标题包含无效字符。", "提示",
                    UI4MessageBoxButtons.OK, 320);
                return;
            }

            if (newName.Trim() != vm.SelectedEntry.Title)
            {
                var entries = vm.Entries;
                foreach (var entry in entries)
                {
                    if (entry.Title == newName.Trim())
                    {
                        UI4MessageBox.Show("已存在同名条目。", "提示",
                            UI4MessageBoxButtons.OK, 320);
                        return;
                    }
                }
            }

            vm.RenameEntry(vm.SelectedEntry, newName);
        }

        private void OnDeleteEntry()
        {
            var vm = DataContext as EntryListViewModel;
            if (vm == null || vm.SelectedEntry == null) return;

            var result = UI4MessageBox.Show(
                "确定删除条目“" + vm.SelectedEntry.Title + "”？",
                "删除确认",
                UI4MessageBoxButtons.OKCancel, 360);

            if (result == true)
                vm.DeleteEntry(vm.SelectedEntry);
        }
    }
}
