using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PromptFavorites.Models;
using PromptFavorites.Services;

namespace PromptFavorites.ViewModels
{
    public class EntryDetailViewModel : ViewModelBase
    {
        private readonly IPromptService _service;
        private readonly SettingsService _settings;

        private string _originalTitle;
        private string _originalModule;
        private string _originalBody;
        private PromptItem _currentItem;
        private bool _suppressFavoriteWrite;

        public ObservableCollection<string> ModuleOptions { get; private set; }

        private string _editTitle;
        public string EditTitle
        {
            get { return _editTitle; }
            set
            {
                if (SetProperty(ref _editTitle, value))
                {
                    RaisePropertyChanged("HasChanges");
                    RaisePropertyChanged("CanSave");
                    RaisePropertyChanged("MetadataSummary");
                }
            }
        }

        private string _editModule;
        public string EditModule
        {
            get { return _editModule; }
            set
            {
                if (SetProperty(ref _editModule, value))
                {
                    RaisePropertyChanged("HasChanges");
                    RaisePropertyChanged("CanSave");
                    RaisePropertyChanged("MetadataSummary");
                }
            }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get { return _isFavorite; }
            set
            {
                if (SetProperty(ref _isFavorite, value))
                {
                    RaisePropertyChanged("MetadataSummary");

                    if (_currentItem != null && !_suppressFavoriteWrite)
                    {
                        _service.SetFavorite(_currentItem, value);
                        FavoriteChanged?.Invoke(_currentItem.FilePath, value);
                    }
                }
            }
        }

        private int _useCount;
        public int UseCount
        {
            get { return _useCount; }
            private set { SetProperty(ref _useCount, value); }
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get { return _createdAt; }
            private set { SetProperty(ref _createdAt, value); }
        }

        private DateTime _updatedAt;
        public DateTime UpdatedAt
        {
            get { return _updatedAt; }
            private set { SetProperty(ref _updatedAt, value); }
        }

        private string _bodyText;
        public string BodyText
        {
            get { return _bodyText; }
            set
            {
                if (SetProperty(ref _bodyText, value))
                {
                    RaisePropertyChanged("HasChanges");
                    RaisePropertyChanged("CanSave");
                }
            }
        }

        private bool _isMetadataCollapsed;
        public bool IsMetadataCollapsed
        {
            get { return _isMetadataCollapsed; }
            set
            {
                if (SetProperty(ref _isMetadataCollapsed, value))
                    _settings.MetadataCollapsed = value;
            }
        }

        public bool HasChanges
        {
            get
            {
                if (_currentItem == null) return false;
                return EditTitle != _originalTitle
                    || EditModule != _originalModule
                    || BodyText != _originalBody;
            }
        }

        public bool CanSave
        {
            get { return HasChanges; }
        }

        /// <summary>未选中任何条目时为空，用于右栏空态占位与按钮可用性。</summary>
        public bool IsEmpty
        {
            get { return _currentItem == null; }
        }

        public bool HasEntry
        {
            get { return _currentItem != null; }
        }

        public string MetadataSummary
        {
            get
            {
                if (_currentItem == null) return string.Empty;
                var star = IsFavorite ? " \u2605" : "";
                return (EditTitle ?? "") + " \u00B7 " + (EditModule ?? "") + star;
            }
        }

        public ICommand SaveCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand ToggleMetadataCommand { get; private set; }

        public event Action SaveRequested;
        public event Action CopyRequested;
        public event Func<bool> ConfirmDiscardChanges;

        /// <summary>右栏切换收藏后通知外部（文件路径 + 新状态），用于中栏列表实时联动。</summary>
        public event Action<string, bool> FavoriteChanged;

        public EntryDetailViewModel(IPromptService service, SettingsService settings)
        {
            _service = service;
            _settings = settings;
            _isMetadataCollapsed = settings.MetadataCollapsed;
            ModuleOptions = new ObservableCollection<string>();

            SaveCommand = new RelayCommand(OnSave, () => CanSave);
            CopyCommand = new RelayCommand(OnCopy, () => _currentItem != null);
            ToggleMetadataCommand = new RelayCommand(() => IsMetadataCollapsed = !IsMetadataCollapsed);
        }

        public void LoadEntry(PromptItem item)
        {
            // 装载属于"回填显示"，不能触发收藏写盘与联动事件
            _suppressFavoriteWrite = true;
            try
            {
                if (item == null)
                {
                    _currentItem = null;
                    EditTitle = string.Empty;
                    EditModule = string.Empty;
                    BodyText = string.Empty;
                    UseCount = 0;
                    IsFavorite = false;
                    return;
                }

                _currentItem = item;
                _originalTitle = item.Title;
                _originalModule = item.Module;
                _originalBody = item.Body;

                RefreshModuleOptions();

                EditTitle = item.Title;
                EditModule = item.Module;
                BodyText = item.Body;
                UseCount = item.UseCount;
                CreatedAt = item.CreatedAt;
                UpdatedAt = item.UpdatedAt;
                IsFavorite = item.Favorite;
            }
            finally
            {
                _suppressFavoriteWrite = false;
                RaisePropertyChanged("IsEmpty");
                RaisePropertyChanged("HasEntry");
            }
        }

        /// <summary>中栏列表切换收藏后把状态同步到右栏（同一文件才处理，且不回写文件避免循环）。</summary>
        public void SyncFavoriteFromList(string filePath, bool value)
        {
            if (_currentItem == null || _currentItem.FilePath != filePath) return;
            if (_isFavorite == value) return;

            _suppressFavoriteWrite = true;
            try
            {
                _currentItem.Favorite = value;
                IsFavorite = value;
            }
            finally
            {
                _suppressFavoriteWrite = false;
            }
        }

        public void RefreshModuleOptions()
        {
            var modules = _service.GetModuleNames();
            var current = _editModule;

            ModuleOptions.Clear();
            foreach (var m in modules)
                ModuleOptions.Add(m);

            // 清空集合时 ComboBox 会把 SelectedItem 回写为 null，这里恢复原选择
            if (current != null && _editModule != current && ModuleOptions.Contains(current))
                EditModule = current;
        }

        public PromptItem CurrentItem { get { return _currentItem; } }

        public bool CheckDiscardChanges()
        {
            if (!HasChanges) return true;
            if (ConfirmDiscardChanges == null) return true;
            return ConfirmDiscardChanges();
        }

        public void Save()
        {
            if (_currentItem == null || !HasChanges) return;

            try
            {
                if (string.IsNullOrWhiteSpace(EditTitle)
                    || !FrontmatterData.IsValidTitle(EditTitle.Trim()))
                {
                    MessageBox.Show("\u6807\u9898\u4E0D\u80FD\u4E3A\u7A7A\u6216\u5305\u542B\u65E0\u6548\u5B57\u7B26\u3002",
                        "\u63D0\u793A", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var title = EditTitle.Trim();
                var module = string.IsNullOrEmpty(EditModule) ? _originalModule : EditModule;
                _currentItem.Title = title;
                _currentItem.Module = module;
                _currentItem.Body = BodyText;

                _service.SaveEntry(_currentItem, _originalTitle, _originalModule);

                // 右栏改标题等价于重命名，顺序表里就地换名，位置不变
                if (!string.Equals(title, _originalTitle, StringComparison.Ordinal))
                {
                    _settings.RenameEntryInOrder(_originalModule, _originalTitle, title);
                    _settings.Save();
                }

                EditTitle = title;
                EditModule = module;
                _originalTitle = title;
                _originalModule = module;
                _originalBody = BodyText;
                UpdatedAt = _currentItem.UpdatedAt;

                RaisePropertyChanged("HasChanges");
                RaisePropertyChanged("CanSave");
            }
            catch (Exception ex)
            {
                MessageBox.Show("\u4FDD\u5B58\u5931\u8D25: " + ex.Message, "\u9519\u8BEF",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSave()
        {
            Save();
            SaveRequested?.Invoke();
        }

        private void OnCopy()
        {
            if (_currentItem == null) return;
            CopyRequested?.Invoke();
        }
    }
}
