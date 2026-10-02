using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using PromptFavorites.Services;
using StartUI4Controls;

namespace PromptFavorites.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IPromptService _service;
        private readonly SettingsService _settings;
        private bool _suppressAutoSelect;

        public ModuleListViewModel Modules { get; private set; }
        public EntryListViewModel Entries { get; private set; }
        public EntryDetailViewModel Detail { get; private set; }

        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (SetProperty(ref _searchText, value))
                    OnSearchTextChanged(value);
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        public event Action<string> ToastRequested;

        private void ShowToast(string message)
        {
            StatusMessage = message;
            var handler = ToastRequested;
            if (handler != null) handler(message);
        }

        public Window MainWindow { get; set; }

        public MainViewModel(IPromptService service, SettingsService settings)
        {
            _service = service;
            _settings = settings;

            Modules = new ModuleListViewModel(service, settings);
            Entries = new EntryListViewModel(service, settings);
            Detail = new EntryDetailViewModel(service, settings);

            Modules.SelectedModuleChanged += OnModuleSelected;
            Entries.SelectedEntryChanged += OnEntrySelected;
            Entries.RequestQuickCopy += OnQuickCopy;
            Detail.SaveRequested += OnSaveRequested;
            Detail.CopyRequested += OnCopyRequested;
            Detail.ConfirmDiscardChanges += OnConfirmDiscard;

            // 收藏有两个入口（中栏星标、右栏按钮），各自持有不同的 PromptItem 实例，
            // 这里按文件路径双向同步，否则写盘成功但界面上的星标与筛选都不更新。
            Entries.FavoriteToggled += (path, value) => Detail.SyncFavoriteFromList(path, value);
            Detail.FavoriteChanged += (path, value) => Entries.ApplyFavorite(path, value);

            _suppressAutoSelect = true;
            try
            {
                Modules.CurrentSort = settings.ModuleSortMode;
                Modules.LoadModules();

                if (!string.IsNullOrEmpty(settings.LastModule))
                    Modules.SelectModule(settings.LastModule);
                else if (Modules.Modules.Count > 0)
                    Modules.SelectedModule = Modules.Modules[0];
            }
            finally
            {
                _suppressAutoSelect = false;
            }

            Entries.IsFavoriteFilter = settings.FavoriteFilter;
            Entries.CurrentSort = settings.SortMode;
        }

        private void OnModuleSelected(PromptModule module)
        {
            if (module == null) return;
            if (!Detail.CheckDiscardChanges()) return;

            // 启动恢复上次模块时不自动选中条目（中栏不该出现选中标记）；
            // 用户手工切换模块仍然自动选中第一条。
            Entries.LoadEntries(module.Name, !_suppressAutoSelect);
            _settings.LastModule = module.Name;
        }

        private void OnEntrySelected(PromptItem entry)
        {
            if (entry == null)
            {
                if (Detail.HasChanges) return;
                Detail.LoadEntry(null);
                return;
            }

            // 收藏等操作会重建列表并把选中项还原到同一条目：既不重新读盘（否则编辑器里未保存的改动会被冲掉），
            // 也不该弹"有未保存更改"的确认框
            if (Detail.CurrentItem != null && Detail.CurrentItem.FilePath == entry.FilePath) return;

            if (!Detail.CheckDiscardChanges()) return;

            var detail = _service.LoadEntryDetail(entry.FilePath);
            Detail.LoadEntry(detail);
        }

        private void OnSearchTextChanged(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Entries.RestoreModuleView();
                if (!string.IsNullOrEmpty(_settings.LastModule))
                    Modules.SelectModule(_settings.LastModule);
            }
            else
            {
                var results = _service.Search(text);
                Entries.ShowSearchResults(results);
                Modules.SelectedModule = null;
            }
        }

        private void OnQuickCopy(PromptItem item)
        {
            try
            {
                if (item == null) return;

                if (Detail.HasChanges)
                {
                    UI4MessageBox.Show(
                        "\u6709\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF0C\u8BF7\u5148\u4FDD\u5B58\u518D\u590D\u5236\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                DoCopy(item);
            }
            catch (Exception ex)
            {
                ShowError("\u590D\u5236\u5931\u8D25", ex);
            }
        }

        private void OnCopyRequested()
        {
            try
            {
                if (Detail.CurrentItem == null) return;

                if (Detail.HasChanges)
                {
                    UI4MessageBox.Show(
                        "\u6709\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF0C\u8BF7\u5148\u4FDD\u5B58\u518D\u590D\u5236\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                if (!string.IsNullOrEmpty(Detail.BodyText))
                {
                    CopyToClipboard(Detail.BodyText, Detail.CurrentItem);
                    return;
                }

                FinishCopy(Detail.CurrentItem);
            }
            catch (Exception ex)
            {
                ShowError("\u590D\u5236\u5931\u8D25", ex);
            }
        }

        private void DoCopy(PromptItem item)
        {
            var detail = _service.LoadEntryDetail(item.FilePath);
            if (!string.IsNullOrEmpty(detail.Body))
            {
                CopyToClipboard(detail.Body, item);
                return;
            }

            FinishCopy(item);
        }

        private void CopyToClipboard(string text, PromptItem item)
        {
            UI4Clipboard.TrySetTextAsync(text, delegate(bool ok)
            {
                if (!ok)
                {
                    UI4MessageBox.Show(
                        "\u65E0\u6CD5\u5199\u5165\u526A\u8D34\u677F\uFF0C\u8BF7\u5173\u95ED\u5360\u7528\u526A\u8D34\u677F\u7684\u7A0B\u5E8F\u540E\u91CD\u8BD5\u3002",
                        "\u63D0\u793A",
                        UI4MessageBoxButtons.OK,
                        360);
                    return;
                }

                FinishCopy(item);
            });
        }

        private void FinishCopy(PromptItem item)
        {
            _service.RecordCopy(item);
            Entries.RefreshCurrentModule();
            ShowToast("\u5DF2\u590D\u5236");
        }

        private static void ShowError(string title, Exception ex)
        {
            UI4MessageBox.Show(ex.Message, title, UI4MessageBoxButtons.OK, 420);
        }

        private void OnSaveRequested()
        {
            Entries.RefreshCurrentModule();
            Modules.RefreshModuleCounts();
            ShowToast("\u5DF2\u4FDD\u5B58");
        }

        private bool OnConfirmDiscard()
        {
            var result = UI4MessageBox.Show(
                "\u662F\u5426\u653E\u5F03\u672A\u4FDD\u5B58\u7684\u66F4\u6539\uFF1F",
                "\u672A\u4FDD\u5B58",
                UI4MessageBoxButtons.OKCancel,
                360);
            return result == true;
        }

        /// <summary>把窗口几何与会话状态采集进设置对象（不写盘）。最大化时不采集几何，避免把最大化尺寸当还原尺寸存。</summary>
        public void CaptureWindowState(Window window)
        {
            if (window == null) return;

            if (window.WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = window.ActualWidth;
                _settings.WindowHeight = window.ActualHeight;
                _settings.WindowLeft = window.Left;
                _settings.WindowTop = window.Top;
            }

            _settings.WindowState = window.WindowState;
            _settings.LastModule = Modules.SelectedModule != null
                ? Modules.SelectedModule.Name : "";
            _settings.SortMode = Entries.CurrentSort;
            _settings.ModuleSortMode = Modules.CurrentSort;
            _settings.FavoriteFilter = Entries.IsFavoriteFilter;
        }

        public void SaveWindowState(Window window)
        {
            CaptureWindowState(window);
            _settings.Save();
        }
    }
}
