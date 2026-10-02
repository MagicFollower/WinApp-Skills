using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PromptFavorites.Helpers;
using PromptFavorites.Models;
using PromptFavorites.Services;
using StartUI4Controls;

namespace PromptFavorites.ViewModels
{
    public class ModuleListViewModel : ViewModelBase
    {
        private readonly IPromptService _service;
        private readonly SettingsService _settings;
        private List<PromptModule> _allModules;

        /// <summary>从没拖过的模块直接切到"自定义"时记的起点顺序，只在内存里，不写配置。</summary>
        private List<string> _virtualOrder;

        public ObservableCollection<PromptModule> Modules { get; private set; }

        private PromptModule _selectedModule;
        public PromptModule SelectedModule
        {
            get { return _selectedModule; }
            set
            {
                if (SetProperty(ref _selectedModule, value))
                    SelectedModuleChanged?.Invoke(value);
            }
        }

        private ModuleSortMode _currentSort = ModuleSortMode.CreatedAt;
        public ModuleSortMode CurrentSort
        {
            get { return _currentSort; }
            set
            {
                if (_currentSort == value) return;

                if (value == ModuleSortMode.Custom && ActiveOrder == null)
                    _virtualOrder = Modules.Select(m => m.Name).ToList();

                _currentSort = value;
                RefreshModules();
                RaisePropertyChanged("CanReorder");
            }
        }

        /// <summary>配置里的真快照优先；没有就用内存起点，两者都没有就按进来的顺序。</summary>
        private IList<string> ActiveOrder
        {
            get
            {
                var stored = _settings != null ? _settings.ModuleOrder : null;
                return stored ?? _virtualOrder;
            }
        }

        /// <summary>只有"自定义"排序且至少两行才允许拖。</summary>
        public bool CanReorder
        {
            get { return _currentSort == ModuleSortMode.Custom && Modules.Count > 1; }
        }

        public ICommand AddModuleCommand { get; private set; }
        public ICommand ReorderCommand { get; private set; }

        public event Action<PromptModule> SelectedModuleChanged;
        public event Action<string> RequestNewModuleName;

        public ModuleListViewModel(IPromptService service, SettingsService settings)
        {
            _service = service;
            _settings = settings;
            _allModules = new List<PromptModule>();
            Modules = new ObservableCollection<PromptModule>();
            AddModuleCommand = new RelayCommand(OnAddModule);
            ReorderCommand = new RelayCommand<ReorderRequest>(OnReorder);
        }

        /// <summary>
        /// 落点间隙换成集合下标后用 Move 移动（不用 Remove+Insert，那会把选中模块冲成 null、
        /// 连带重加载中栏并弹「有未保存更改」），移动成功后才写配置。
        /// </summary>
        private void OnReorder(ReorderRequest request)
        {
            if (request == null || !CanReorder) return;

            var item = request.Item as PromptModule;
            if (item == null) return;

            var oldIndex = Modules.IndexOf(item);
            if (oldIndex < 0) return;

            var newIndex = request.Gap > oldIndex ? request.Gap - 1 : request.Gap;
            newIndex = Math.Max(0, Math.Min(newIndex, Modules.Count - 1));
            if (newIndex == oldIndex) return;

            Modules.Move(oldIndex, newIndex);
            CommitManualOrder();
        }

        public void LoadModules()
        {
            _allModules = _service.LoadModules().ToList();
            RefreshModules();
            RaisePropertyChanged("CanReorder");
        }

        /// <summary>
        /// 按当前排序重建可见集合。重建会让 UI4ListBox 通过 SelectedItem 双向绑定把选中项冲成 null，
        /// 因此这里静默还原到同名模块：不触发 SelectedModuleChanged，否则每次换排序都会重加载中栏
        /// 条目，还可能弹「有未保存更改」。
        /// </summary>
        private void RefreshModules()
        {
            var selectedName = _selectedModule != null ? _selectedModule.Name : null;

            IEnumerable<PromptModule> ordered;
            if (_currentSort == ModuleSortMode.Name)
            {
                ordered = _allModules.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
            }
            else if (_currentSort == ModuleSortMode.Custom)
            {
                ordered = CustomOrderCodec.Apply(_allModules, m => m.Name, ActiveOrder);
            }
            else
            {
                ordered = _allModules.OrderByDescending(m => m.CreatedAt)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
            }

            Modules.Clear();
            foreach (var m in ordered)
                Modules.Add(m);

            if (selectedName == null) return;

            var match = Modules.FirstOrDefault(m => m.Name == selectedName);
            if (!ReferenceEquals(match, _selectedModule))
            {
                _selectedModule = match;
                RaisePropertyChanged("SelectedModule");
            }
        }

        /// <summary>拖动结束后把可见顺序写进本地配置并立即落盘；不在可拖动状态时拒绝。</summary>
        public bool CommitManualOrder()
        {
            if (!CanReorder || _settings == null) return false;

            _settings.SetModuleOrder(Modules.Select(m => m.Name));
            _virtualOrder = null;
            _settings.Save();
            return true;
        }

        public void SelectModule(string name)
        {
            var module = Modules.FirstOrDefault(m => m.Name == name);
            if (module != null)
                SelectedModule = module;
            else if (Modules.Count > 0)
                SelectedModule = Modules[0];
        }

        public void RefreshModuleCounts()
        {
            var modules = _service.LoadModules();
            foreach (var m in modules)
            {
                var existing = Modules.FirstOrDefault(x => x.Name == m.Name);
                if (existing != null)
                    existing.EntryCount = m.EntryCount;
            }
        }

        private void OnAddModule()
        {
            RequestNewModuleName?.Invoke(null);
        }

        public void AddModule(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();

            var rootPath = ((PromptService)_service).RootPath;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                UI4MessageBox.Show(
                    "\u8BF7\u5148\u9009\u62E9 Prompt \u6839\u76EE\u5F55\u3002",
                    "\u63D0\u793A", UI4MessageBoxButtons.OK, 360);
                return;
            }

            if (_service.ModuleExists(name)) return;

            var modulePath = new FileSystemRepository(rootPath)
                .CreateModule(name);

            _allModules.Add(new PromptModule
            {
                Name = name,
                FullPath = modulePath,
                EntryCount = 0,
                CreatedAt = DateTime.Now
            });
            RefreshModules();
            RaisePropertyChanged("CanReorder");

            var added = Modules.FirstOrDefault(m => m.Name == name);
            if (added != null)
                SelectedModule = added;
        }

        public void RenameModule(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();
            if (oldName == newName) return;
            if (_service.ModuleExists(newName)) return;

            _service.RenameModule(oldName, newName);

            if (_settings != null)
            {
                _settings.RenameModuleInOrders(oldName, newName);
                _settings.Save();
            }

            LoadModules();
            SelectModule(newName);
        }

        public void DeleteModule(string name)
        {
            _service.DeleteModule(name);
            LoadModules();
            if (Modules.Count > 0)
                SelectedModule = Modules[0];
            else
                SelectedModule = null;
        }
    }
}
