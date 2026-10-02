using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StartUI4Controls;
using PromptFavorites.Helpers;
using PromptFavorites.ViewModels;

namespace PromptFavorites.Views
{
    public partial class EntryDetailView : UserControl
    {
        private static readonly Color Neutral = Theme.Neutral;

        private bool _isSyncing;
        private EntryDetailViewModel _vm;

        public EntryDetailView()
        {
            InitializeComponent();
            Loaded += EntryDetailView_Loaded;
            DataContextChanged += EntryDetailView_DataContextChanged;
            BodyEditor.TextChanged += BodyEditor_TextChanged;
        }

        private void EntryDetailView_Loaded(object sender, RoutedEventArgs e)
        {
            BodyEditor.SyntaxHighlighting = null;
            BodyEditor.ShowLineNumbers = false;
            AttachVm();
        }

        private void EntryDetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachVm();
        }

        private void AttachVm()
        {
            DetachVm();
            _vm = DataContext as EntryDetailViewModel;
            if (_vm == null) return;

            _vm.PropertyChanged += Vm_PropertyChanged;
            ApplyMetadataVisibility();
            UpdateFavButton();
            UpdateSaveButton();
        }

        private void DetachVm()
        {
            if (_vm == null) return;
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm = null;
        }

        private void BodyEditor_TextChanged(object sender, EventArgs e)
        {
            if (_isSyncing || _vm == null) return;
            _isSyncing = true;
            _vm.BodyText = BodyEditor.Text;
            _isSyncing = false;
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "BodyText")
            {
                if (_isSyncing) return;
                _isSyncing = true;
                if (BodyEditor.Text != _vm.BodyText)
                    BodyEditor.Text = _vm.BodyText ?? string.Empty;
                _isSyncing = false;
            }
            else if (e.PropertyName == "IsMetadataCollapsed")
            {
                ApplyMetadataVisibility();
            }
            else if (e.PropertyName == "IsFavorite")
            {
                UpdateFavButton();
            }
            else if (e.PropertyName == "CanSave")
            {
                UpdateSaveButton();
            }
        }

        private void ApplyMetadataVisibility()
        {
            if (_vm == null) return;
            if (_vm.IsMetadataCollapsed)
            {
                CollapsedSummary.Visibility = Visibility.Visible;
                MetadataPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                CollapsedSummary.Visibility = Visibility.Collapsed;
                MetadataPanel.Visibility = Visibility.Visible;
            }
        }

        private void ToggleMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.IsMetadataCollapsed = !_vm.IsMetadataCollapsed;
        }

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.IsFavorite = !_vm.IsFavorite;
        }

        private void UpdateFavButton()
        {
            if (_vm == null) return;
            if (_vm.IsFavorite)
            {
                FavBtn.Content = "\u2605 \u5DF2\u6536\u85CF";
                FavBtn.GradientStart = Theme.Favorite;
                FavBtn.GradientEnd = Theme.FavoriteEnd;
            }
            else
            {
                FavBtn.Content = "\u2606 \u672A\u6536\u85CF";
                FavBtn.GradientStart = Neutral;
                FavBtn.GradientEnd = Neutral;
            }
        }

        private void UpdateSaveButton()
        {
            if (_vm == null) return;
            // 禁用态的灰色外观由 UI4Button 自己负责，这里只管可用性
            SaveBtn.IsEnabled = _vm.CanSave;
        }
    }
}
