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
                    UI4MenuItemType.Copy, "\u91CD\u547D\u540D", null,
                    delegate { OnRenameEntry(); }));
                _contextMenu.AddItem(new UI4MenuItem(
                    UI4MenuItemType.Delete, "\u5220\u9664", null,
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
            var accent = Theme.Accent;
            var neutral = Theme.Neutral;

            if (active)
            {
                FavoriteBtn.GradientStart = accent;
                FavoriteBtn.GradientEnd = accent;
                FavoriteBtn.Foreground = Brushes.White;
            }
            else
            {
                FavoriteBtn.GradientStart = neutral;
                FavoriteBtn.GradientEnd = neutral;
                FavoriteBtn.Foreground = Brushes.Black;
            }
        }

        private void UpdateSortButtons(SortMode mode)
        {
            var accent = Theme.Accent;
            var neutral = Theme.Neutral;

            SetSortBtnStyle(SortUseCountBtn, mode == SortMode.UseCount, accent, neutral);
            SetSortBtnStyle(SortUpdatedAtBtn, mode == SortMode.UpdatedAt, accent, neutral);
            SetSortBtnStyle(SortCreatedAtBtn, mode == SortMode.CreatedAt, accent, neutral);
            SetSortBtnStyle(SortNameBtn, mode == SortMode.Name, accent, neutral);
            SetSortBtnStyle(SortCustomBtn, mode == SortMode.Custom, accent, neutral);
        }

        private static void SetSortBtnStyle(UI4Button btn, bool selected, Color accent, Color neutral)
        {
            if (btn == null) return;
            if (selected)
            {
                btn.GradientStart = accent;
                btn.GradientEnd = accent;
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.GradientStart = neutral;
                btn.GradientEnd = neutral;
                btn.Foreground = Brushes.Black;
            }
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

            var newName = DialogHelper.ShowInputDialog(this, "\u91CD\u547D\u540D\u6761\u76EE",
                "\u65B0\u6807\u9898:", vm.SelectedEntry.Title);
            if (string.IsNullOrWhiteSpace(newName)) return;

            if (!FrontmatterData.IsValidTitle(newName.Trim()))
            {
                UI4MessageBox.Show("\u6807\u9898\u5305\u542B\u65E0\u6548\u5B57\u7B26\u3002", "\u63D0\u793A",
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
                        UI4MessageBox.Show("\u5DF2\u5B58\u5728\u540C\u540D\u6761\u76EE\u3002", "\u63D0\u793A",
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
                "\u786E\u5B9A\u5220\u9664\u6761\u76EE\u201C" + vm.SelectedEntry.Title + "\u201D\uFF1F",
                "\u5220\u9664\u786E\u8BA4",
                UI4MessageBoxButtons.OKCancel, 360);

            if (result == true)
                vm.DeleteEntry(vm.SelectedEntry);
        }
    }
}
