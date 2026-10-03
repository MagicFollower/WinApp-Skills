using System;
using MemoTask.Services;

namespace MemoTask.ViewModels
{
    /// <summary>
    /// 三个页面的宿主。导航控件只在这里落一个稳定的页面 Key（notes / todos / settings），
    /// 视图侧的 UI4NavigationViewItem 不进 ViewModel，免得业务层挂上控件库类型。
    /// </summary>
    internal sealed class MainViewModel : ViewModelBase
    {
        private readonly DataStore _store;
        private readonly SettingsStore _settings;

        private string _currentPage = "notes";
        private string _footer = "";

        public MainViewModel(DataStore store, SettingsStore settings)
        {
            _store = store;
            _settings = settings;

            Notes = new NotesViewModel(store, settings);
            Todos = new TodosViewModel(store, settings);
            SettingsVm = new SettingsViewModel(store, settings, FlushAll, ReloadAll);

            // 条数只随增删变，所以盯集合而不是盯每次落盘
            store.Notes.CollectionChanged += delegate { UpdateFooter(); };
            store.Todos.CollectionChanged += delegate { UpdateFooter(); };

            // 页脚那句「主题 …」会随切档变，绑静态属性会停在初值，所以订阅库的广播手动重发。
            StartUI4Controls.UI4Theme.ThemeChanged += delegate { UpdateFooter(); };
        }

        public NotesViewModel Notes { get; private set; }
        public TodosViewModel Todos { get; private set; }
        public SettingsViewModel SettingsVm { get; private set; }

        public DataStore Store { get { return _store; } }

        public SettingsStore Settings { get { return _settings; } }

        public string CurrentPage
        {
            get { return _currentPage; }
            private set { SetProperty(ref _currentPage, value); }
        }

        public string Footer
        {
            get { return _footer; }
            private set { SetProperty(ref _footer, value ?? ""); }
        }

        /// <summary>数据与设置的初装载。窗口出来之前调用一次。</summary>
        public void Initialize()
        {
            Notes.Reload();
            Todos.Reload();
            UpdateFooter();
        }

        public void ReloadAll()
        {
            Notes.Reload();
            Todos.Reload();
            UpdateFooter();
        }

        /// <summary>导航控件把选中项的 Tag 交进来：先把当前页的草稿落盘，再刷页脚。</summary>
        public void NotifyPageChanged(string pageKey)
        {
            if (string.IsNullOrEmpty(pageKey)) return;
            if (_currentPage != pageKey)
            {
                FlushAll();
                CurrentPage = pageKey;
                if (pageKey == "settings") SettingsVm.RefreshInfo();
            }
            Notes.RefreshOrder();
            Todos.RefreshOrder();
            UpdateFooter();
        }

        /// <summary>关窗前调用：两条落盘路径都走一遍，谁有草稿谁写。</summary>
        public void FlushAll()
        {
            Notes.Flush();
            Todos.Flush();
        }

        public void UpdateFooter()
        {
            string page = PageLabel(_currentPage);
            Footer = "MemoTask · 当前：" + page
                     + " · 备忘 " + _store.Notes.Count
                     + " · 待办 " + _store.Todos.Count
                     + " · 主题 " + ThemeService.ResolvedDescription;
        }

        private static string PageLabel(string key)
        {
            switch (key)
            {
                case "todos": return "待办";
                case "settings": return "设置";
                default: return "备忘录";
            }
        }
    }
}
