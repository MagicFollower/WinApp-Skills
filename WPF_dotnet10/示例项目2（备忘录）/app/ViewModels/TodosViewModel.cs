using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MemoTask.Helpers;
using MemoTask.Models;
using MemoTask.Services;
using StartUI4Controls;

namespace MemoTask.ViewModels
{
    /// <summary>
    /// 待办页：快速添加 + 筛选排序 + 卡片列表 + 选中项编辑面板。
    /// 与备忘录不同，待办的改动是即时落盘的（勾选、删除、保存编辑都直接写文件），
    /// 只有编辑面板里的草稿在切换选中/关窗时自动提交，避免「改了半天没保存」。
    /// </summary>
    internal sealed class TodosViewModel : ViewModelBase
    {
        private readonly DataStore _store;
        private readonly SettingsStore _settings;
        private readonly Dictionary<string, TodoRowViewModel> _rowCache =
            new Dictionary<string, TodoRowViewModel>(StringComparer.OrdinalIgnoreCase);

        private TodoRowViewModel _selectedRow;
        private string _selectedId = "";
        private bool _applyingSelection;
        private bool _editOpen;

        private string _filterKey = "active";
        private string _sortKey = "due";
        private string _newTitle = "";
        private string _newPriorityKey = "Normal";
        private string _newDueText = "";
        private string _editTitle = "";
        private string _editDetail = "";
        private string _editPriorityKey = "Normal";
        private string _editDueText = "";
        private string _statusText = "";
        private bool _isEditing;
        private bool _isEmpty;
        private bool _noMatch;
        private int _total;
        private int _open;
        private int _done;
        private int _overdue;
        private int _dueToday;
        private int _filtered;
        private double _percent;

        public TodosViewModel(DataStore store, SettingsStore settings)
        {
            _store = store;
            _settings = settings;

            AddCommand = new RelayCommand(Add);
            SaveEditCommand = new RelayCommand(SaveEdit, delegate { return _selectedRow != null; });
            CancelEditCommand = new RelayCommand(CloseEdit, delegate { return _selectedRow != null; });
            SetQuickDueCommand = new RelayCommand<string>(SetQuickDue);
            ClearDueCommand = new RelayCommand(delegate { EditDueText = ""; CommitEdit(true); });
            DeleteSelectedCommand = new RelayCommand(
                delegate { if (_selectedRow != null) RequestDelete(_selectedRow); },
                delegate { return _selectedRow != null; });
            ToggleSelectedCommand = new RelayCommand(
                delegate
                {
                    if (_selectedRow != null) ToggleDone(_selectedRow);
                },
                delegate { return _selectedRow != null; });
        }

        public ObservableCollection<TodoRowViewModel> Rows { get; } = new ObservableCollection<TodoRowViewModel>();

        public IList<OptionItem> FilterOptions { get; } = new List<OptionItem>
        {
            new OptionItem("active", "未完成"),
            new OptionItem("today", "今天到期"),
            new OptionItem("week", "未来 7 天"),
            new OptionItem("overdue", "已逾期"),
            new OptionItem("done", "已完成"),
            new OptionItem("all", "全部"),
        };

        public IList<OptionItem> SortOptions { get; } = new List<OptionItem>
        {
            new OptionItem("due", "按截止日"),
            new OptionItem("priority", "按优先级"),
            new OptionItem("created", "按添加时间"),
            new OptionItem("title", "按标题"),
        };

        public IList<OptionItem> PriorityOptions { get; } = new List<OptionItem>
        {
            new OptionItem("Low", "低"),
            new OptionItem("Normal", "普通"),
            new OptionItem("High", "高"),
        };

        public RelayCommand AddCommand { get; private set; }
        public RelayCommand SaveEditCommand { get; private set; }
        public RelayCommand CancelEditCommand { get; private set; }
        public RelayCommand<string> SetQuickDueCommand { get; private set; }
        public RelayCommand ClearDueCommand { get; private set; }
        public RelayCommand DeleteSelectedCommand { get; private set; }
        public RelayCommand ToggleSelectedCommand { get; private set; }

        public string FilterKey
        {
            get { return _filterKey; }
            set
            {
                if (value == null) return;
                if (SetProperty(ref _filterKey, value))
                {
                    _settings.Current.TodoFilter = value;
                    _settings.Save();
                    Rebuild();
                }
            }
        }

        public string SortKey
        {
            get { return _sortKey; }
            set
            {
                if (value == null) return;
                if (SetProperty(ref _sortKey, value)) Rebuild();
            }
        }

        public string NewTitle
        {
            get { return _newTitle; }
            set { SetProperty(ref _newTitle, value ?? ""); }
        }

        public string NewPriorityKey
        {
            get { return _newPriorityKey; }
            set { SetProperty(ref _newPriorityKey, value ?? "Normal"); }
        }

        public string NewDueText
        {
            get { return _newDueText; }
            set { SetProperty(ref _newDueText, value ?? ""); }
        }

        public string EditTitle
        {
            get { return _editTitle; }
            set { SetProperty(ref _editTitle, value ?? ""); }
        }

        public string EditDetail
        {
            get { return _editDetail; }
            set { SetProperty(ref _editDetail, value ?? ""); }
        }

        public string EditPriorityKey
        {
            get { return _editPriorityKey; }
            set { SetProperty(ref _editPriorityKey, value ?? "Normal"); }
        }

        public string EditDueText
        {
            get { return _editDueText; }
            set { SetProperty(ref _editDueText, value ?? ""); }
        }

        public bool IsEditing
        {
            get { return _isEditing; }
            private set { SetProperty(ref _isEditing, value); }
        }

        public bool IsEmpty
        {
            get { return _isEmpty; }
            private set { SetProperty(ref _isEmpty, value); }
        }

        public bool NoMatch
        {
            get { return _noMatch; }
            private set { SetProperty(ref _noMatch, value); }
        }

        public int TotalCount { get { return _total; } private set { SetProperty(ref _total, value); } }
        public int OpenCount { get { return _open; } private set { SetProperty(ref _open, value); } }
        public int DoneCount { get { return _done; } private set { SetProperty(ref _done, value); } }
        public int OverdueCount { get { return _overdue; } private set { SetProperty(ref _overdue, value); } }
        public int DueTodayCount { get { return _dueToday; } private set { SetProperty(ref _dueToday, value); } }
        public int FilteredCount { get { return _filtered; } private set { SetProperty(ref _filtered, value); } }
        public double Percent { get { return _percent; } private set { SetProperty(ref _percent, value); } }

        public bool HasOverdue { get { return _overdue > 0; } }

        public string ProgressText
        {
            get
            {
                if (_total == 0) return "还没有待办";
                return "已完成 " + _done + " / " + _total + " 项 · 还剩 " + _open + " 项";
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value ?? ""); }
        }

        public TodoRowViewModel SelectedRow
        {
            get { return _selectedRow; }
            set
            {
                if (_applyingSelection) return;
                SetSelected(value);
            }
        }

        public void Reload()
        {
            string saved = _settings.Current.TodoFilter;
            if (!string.IsNullOrEmpty(saved)) _filterKey = saved;
            RaisePropertyChanged("FilterKey");
            _newPriorityKey = _settings.Current.DefaultPriority ?? "Normal";
            RaisePropertyChanged("NewPriorityKey");
            Rebuild();
        }

        /// <summary>关窗或切页前把编辑面板里的草稿落盘。</summary>
        public void Flush()
        {
            CommitEdit(true);
        }

        public void RefreshOrder()
        {
            Rebuild();
        }

        private void Add()
        {
            string title = (_newTitle ?? "").Trim();
            if (title.Length == 0)
            {
                StatusText = "先写点什么再添加";
                return;
            }

            DateTime? due;
            string warn;
            if (!TryReadDue(_newDueText, out due, out warn))
            {
                StatusText = warn;
                return;
            }

            TodoPriority fallback = PriorityText.Parse(_settings.Current.DefaultPriority, TodoPriority.Normal);
            var item = new TodoItem
            {
                Title = title,
                Detail = "",
                Priority = PriorityText.Parse(_newPriorityKey, fallback),
                DueDate = due,
            };
            _store.Todos.Add(item);
            _store.SaveTodos();

            _selectedId = item.Id;
            _applyingSelection = true;
            Rebuild();
            _applyingSelection = false;
            TodoRowViewModel row = FindRow(item.Id);
            if (row != null) SetSelected(row);

            NewTitle = "";
            NewDueText = "";
            StatusText = "已添加「" + item.Title + "」";
        }

        internal void ToggleDone(TodoRowViewModel row)
        {
            if (row == null) return;
            CommitEdit(true);
            TodoItem model = row.Model;
            model.IsDone = !model.IsDone;
            model.CompletedAt = model.IsDone ? (DateTime?)DateTime.Now : null;
            _store.SaveTodos();
            row.Refresh();
            Rebuild();
            StatusText = model.IsDone ? "已完成「" + model.Title + "」" : "已恢复「" + model.Title + "」";
        }

        internal void RequestDelete(TodoRowViewModel row)
        {
            if (row == null) return;
            string title = row.Title;
            bool yes = UI4MessageBox.Show(
                "删除待办「" + title + "」？这条记录会从本机移除，无法撤销。",
                "删除待办",
                UI4MessageBoxButtons.OKCancel,
                owner: System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow) == true;
            if (!yes) return;

            _store.Todos.Remove(row.Model);
            _rowCache.Remove(row.Model.Id);
            _store.SaveTodos();
            if (_selectedRow == row)
            {
                _selectedRow = null;
                _selectedId = "";
                IsEditing = false;
                _editOpen = false;
            }
            Rebuild();
            StatusText = "已删除「" + title + "」";
        }

        internal void BeginEdit(TodoRowViewModel row)
        {
            if (row == null) return;
            if (_selectedRow != row) SetSelected(row);
            LoadEdit(row);
        }

        private void SetSelected(TodoRowViewModel next)
        {
            if (next == null)
            {
                CommitEdit(true);
                _selectedRow = null;
                _selectedId = "";
                IsEditing = false;
                _editOpen = false;
                RaisePropertyChanged("SelectedRow");
                return;
            }
            bool same = _selectedRow == next;
            if (!same) CommitEdit(true);
            _selectedRow = next;
            _selectedId = next.Id;
            _editOpen = true;
            IsEditing = true;
            RaisePropertyChanged("SelectedRow");
            if (!same) LoadEdit(next);
        }

        private void LoadEdit(TodoRowViewModel row)
        {
            TodoItem m = row.Model;
            EditTitle = m.Title ?? "";
            EditDetail = m.Detail ?? "";
            EditPriorityKey = m.Priority.ToString();
            EditDueText = m.DueDate.HasValue ? m.DueDate.Value.ToString("yyyy-MM-dd") : "";
        }

        private void CloseEdit()
        {
            _editOpen = false;
            IsEditing = false;
            if (_selectedRow != null) LoadEdit(_selectedRow);
        }

        private void SaveEdit()
        {
            if (_selectedRow == null) return;
            CommitEdit(false);
        }

        private void CommitEdit(bool silent)
        {
            TodoRowViewModel row = _selectedRow;
            if (row == null || !_editOpen) return;
            TodoItem m = row.Model;

            string title = (_editTitle ?? "").Trim();
            if (title.Length == 0)
            {
                if (!silent) StatusText = "标题不能为空";
                return;
            }

            DateTime? due;
            string warn;
            if (!TryReadDue(_editDueText, out due, out warn))
            {
                if (!silent) StatusText = warn;
                return;
            }

            bool touched = m.Title != title
                           || !string.Equals(m.Detail, _editDetail ?? "", StringComparison.Ordinal)
                           || m.Priority.ToString() != _editPriorityKey
                           || m.DueDate != due;
            if (!touched)
            {
                if (!silent) StatusText = "没有需要保存的改动";
                return;
            }

            m.Title = title;
            m.Detail = _editDetail ?? "";
            m.Priority = PriorityText.Parse(_editPriorityKey, m.Priority);
            m.DueDate = due;
            _store.SaveTodos();
            row.Refresh();
            Rebuild();
            if (!silent) StatusText = "已保存「" + m.Title + "」";
        }

        /// <summary>日期框的三种合法输入：空或「无」= 无期限，其它走宽容解析。</summary>
        private static bool TryReadDue(string text, out DateTime? due, out string warning)
        {
            due = null;
            warning = "";
            string s = (text ?? "").Trim();
            if (s.Length == 0 || TimeText.IsClearDateWord(s)) return true;

            DateTime parsed;
            if (TimeText.TryParseDate(s, DateTime.Now, out parsed))
            {
                due = parsed;
                return true;
            }
            due = null;
            warning = "日期没看懂：「" + s + "」。可写 2026-10-20、10-20、明天、+3，或留空表示无期限。";
            return false;
        }

        /// <summary>快捷改期：参数是相对今天的天数。</summary>
        private void SetQuickDue(string daysText)
        {
            int days;
            if (!int.TryParse(daysText, out days)) return;
            EditDueText = DateTime.Now.Date.AddDays(days).ToString("yyyy-MM-dd");
            CommitEdit(true);
        }

        private void Rebuild()
        {
            _applyingSelection = true;
            DateTime today = DateTime.Now.Date;
            var matches = new List<TodoRowViewModel>();
            foreach (TodoItem todo in _store.Todos)
            {
                if (!PassesFilter(todo, today)) continue;
                matches.Add(RowFor(todo));
            }
            Sort(matches, today);

            Rows.Clear();
            foreach (TodoRowViewModel row in matches) Rows.Add(row);

            int total = _store.Todos.Count, done = 0, open = 0, overdue = 0, dueToday = 0;
            foreach (TodoItem todo in _store.Todos)
            {
                if (todo.IsDone) done++;
                else
                {
                    open++;
                    if (todo.DueDate.HasValue)
                    {
                        if (todo.DueDate.Value.Date < today) overdue++;
                        else if (todo.DueDate.Value.Date == today) dueToday++;
                    }
                }
            }
            TotalCount = total;
            DoneCount = done;
            OpenCount = open;
            OverdueCount = overdue;
            DueTodayCount = dueToday;
            FilteredCount = matches.Count;
            Percent = total == 0 ? 0 : done * 100.0 / total;
            IsEmpty = total == 0;
            NoMatch = total > 0 && matches.Count == 0;
            RaisePropertyChanged("ProgressText");
            RaisePropertyChanged("HasOverdue");
            _applyingSelection = false;

            TodoRowViewModel keep = string.IsNullOrEmpty(_selectedId) ? null : FindRow(_selectedId);
            if (keep != null && !ReferenceEquals(keep, _selectedRow))
            {
                _selectedRow = keep;
                RaisePropertyChanged("SelectedRow");
            }
            else if (keep == null && _selectedRow != null)
            {
                _selectedRow = null;
                IsEditing = false;
                _editOpen = false;
            }
        }

        private bool PassesFilter(TodoItem todo, DateTime today)
        {
            switch (_filterKey)
            {
                case "done": return todo.IsDone;
                case "all": return true;
                case "today":
                    return !todo.IsDone && todo.DueDate.HasValue && todo.DueDate.Value.Date == today;
                case "week":
                    return !todo.IsDone && todo.DueDate.HasValue
                           && todo.DueDate.Value.Date >= today && todo.DueDate.Value.Date <= today.AddDays(7);
                case "overdue":
                    return !todo.IsDone && todo.DueDate.HasValue && todo.DueDate.Value.Date < today;
                default:
                    return !todo.IsDone;
            }
        }

        private void Sort(List<TodoRowViewModel> items, DateTime today)
        {
            Comparison<TodoRowViewModel> byKey;
            switch (_sortKey)
            {
                case "priority":
                    byKey = delegate(TodoRowViewModel a, TodoRowViewModel b)
                    {
                        return b.Model.Priority.CompareTo(a.Model.Priority);
                    };
                    break;
                case "created":
                    byKey = delegate(TodoRowViewModel a, TodoRowViewModel b)
                    {
                        return b.Model.CreatedAt.CompareTo(a.Model.CreatedAt);
                    };
                    break;
                case "title":
                    byKey = delegate(TodoRowViewModel a, TodoRowViewModel b)
                    {
                        return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                    };
                    break;
                default:
                    byKey = delegate(TodoRowViewModel a, TodoRowViewModel b)
                    {
                        return CompareDue(a.Model, b.Model, today);
                    };
                    break;
            }
            // 未完成恒在已完成之前，逾期再顶到最前
            items.Sort(delegate(TodoRowViewModel a, TodoRowViewModel b)
            {
                if (a.IsDone != b.IsDone) return a.IsDone ? 1 : -1;
                if (a.IsDone) return b.Model.CompletedAt.GetValueOrDefault().CompareTo(a.Model.CompletedAt.GetValueOrDefault());
                if (a.IsOverdue != b.IsOverdue) return a.IsOverdue ? -1 : 1;
                return byKey(a, b);
            });
        }

        private static int CompareDue(TodoItem a, TodoItem b, DateTime today)
        {
            if (!a.DueDate.HasValue && !b.DueDate.HasValue) return b.CreatedAt.CompareTo(a.CreatedAt);
            if (!a.DueDate.HasValue) return 1;
            if (!b.DueDate.HasValue) return -1;
            int byDate = a.DueDate.Value.Date.CompareTo(b.DueDate.Value.Date);
            if (byDate != 0) return byDate;
            return b.Priority.CompareTo(a.Priority);
        }

        private TodoRowViewModel RowFor(TodoItem todo)
        {
            TodoRowViewModel row;
            if (_rowCache.TryGetValue(todo.Id, out row) && ReferenceEquals(row.Model, todo)) return row;
            row = new TodoRowViewModel(todo, this);
            _rowCache[todo.Id] = row;
            return row;
        }

        private TodoRowViewModel FindRow(string id)
        {
            foreach (TodoRowViewModel row in Rows)
            {
                if (row.Id == id) return row;
            }
            return null;
        }
    }
}
