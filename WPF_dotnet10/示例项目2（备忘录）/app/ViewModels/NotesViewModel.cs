using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using MemoTask.Helpers;
using MemoTask.Models;
using MemoTask.Services;
using StartUI4Controls;

namespace MemoTask.ViewModels
{
    /// <summary>
    /// 备忘录页：左列筛选与卡片列表，右列（窄窗时全宽）编辑器。
    /// 编辑只改草稿，落盘由「自动保存计时到点 / 切换选中 / 手动 Ctrl+S / 关窗」四条路触发，
    /// 所以打字过程中列表不会因为反复写盘而抖动。
    /// </summary>
    internal sealed class NotesViewModel : ViewModelBase
    {
        private readonly DataStore _store;
        private readonly SettingsStore _settings;
        private readonly DispatcherTimer _autosave;
        private readonly Dictionary<string, NoteCardViewModel> _cardCache =
            new Dictionary<string, NoteCardViewModel>(StringComparer.OrdinalIgnoreCase);

        private NoteCardViewModel _selectedCard;
        private string _selectedId = "";
        private bool _applyingSelection;
        private bool _loadingDraft;
        private bool _dirty;

        private string _searchText = "";
        private string _tagFilter = "";
        private string _sortKey = "updated";
        private string _editTitle = "";
        private string _editBody = "";
        private string _editTags = "";
        private bool _editPinned;
        private string _saveStatus = "";
        private bool _hasSelection;
        private bool _isEmpty;
        private bool _noMatch;
        private bool _isWide = true;
        private int _noteCount;
        private int _shown;
        private int _charCount;
        private int _lineCount;

        public NotesViewModel(DataStore store, SettingsStore settings)
        {
            _store = store;
            _settings = settings;
            _autosave = new DispatcherTimer();
            _autosave.Tick += delegate { _autosave.Stop(); CommitDraft(true); };

            NewNoteCommand = new RelayCommand(NewNote, delegate { return true; });
            DeleteNoteCommand = new RelayCommand(DeleteSelected, delegate { return _selectedCard != null; });
            TogglePinCommand = new RelayCommand(TogglePin, delegate { return _selectedCard != null; });
            SaveNowCommand = new RelayCommand(delegate { CommitDraft(false); });
            CopyBodyCommand = new RelayCommand(CopyBody, delegate { return _selectedCard != null; });
            CloseDetailCommand = new RelayCommand(delegate { SetSelection(null); });
        }

        public ObservableCollection<NoteCardViewModel> Cards { get; } = new ObservableCollection<NoteCardViewModel>();

        /// <summary>第一项恒为「全部标签」（Key 空串 = 不按标签筛），后面按字母序列出用过的标签。</summary>
        public ObservableCollection<OptionItem> TagOptions { get; } = new ObservableCollection<OptionItem>();

        public IList<OptionItem> SortOptions { get; } = new List<OptionItem>
        {
            new OptionItem("updated", "按修改时间"),
            new OptionItem("created", "按创建时间"),
            new OptionItem("title", "按标题"),
        };

        public RelayCommand NewNoteCommand { get; private set; }
        public RelayCommand DeleteNoteCommand { get; private set; }
        public RelayCommand TogglePinCommand { get; private set; }
        public RelayCommand SaveNowCommand { get; private set; }
        public RelayCommand CopyBodyCommand { get; private set; }

        public string SearchText
        {
            get { return _searchText; }
            set { if (SetProperty(ref _searchText, value ?? "")) Rebuild(); }
        }

        public string TagFilter
        {
            get { return _tagFilter; }
            // 忽略 null：重建标签下拉时 ComboBox 会先摘掉选中并把 null 写回来，
            // 照收的话用户正在用的筛选会随每次编辑被清掉。真实选项恒为非 null 的 Key。
            set { if (value == null) return; if (SetProperty(ref _tagFilter, value)) Rebuild(); }
        }

        public string SortKey
        {
            get { return _sortKey; }
            set
            {
                if (SetProperty(ref _sortKey, value ?? "updated"))
                {
                    _settings.Current.NoteSort = _sortKey;
                    _settings.Save();
                    Rebuild();
                }
            }
        }

        public string EditTitle
        {
            get { return _editTitle; }
            set
            {
                if (SetProperty(ref _editTitle, value ?? "")) MarkDirty();
            }
        }

        public string EditBody
        {
            get { return _editBody; }
            set
            {
                if (SetProperty(ref _editBody, value ?? ""))
                {
                    UpdateCounts();
                    MarkDirty();
                }
            }
        }

        public string EditTags
        {
            get { return _editTags; }
            set { if (SetProperty(ref _editTags, value ?? "")) MarkDirty(); }
        }

        public bool EditPinned
        {
            get { return _editPinned; }
            set { if (SetProperty(ref _editPinned, value)) MarkDirty(); }
        }

        public bool HasSelection
        {
            get { return _hasSelection; }
            private set
            {
                if (SetProperty(ref _hasSelection, value)) RaisePanes();
            }
        }

        /// <summary>
        /// 宽档 = 列表与编辑器并排；窄档 = 一次只显示一栏，选中时进编辑器。
        /// 由主窗口的 SizeChanged 喂进来，视图自己不在 XAML 里钉死像素宽。
        /// </summary>
        public bool IsWide
        {
            get { return _isWide; }
            set
            {
                if (SetProperty(ref _isWide, value)) RaisePanes();
            }
        }

        public bool ShowList { get { return _isWide || !_hasSelection; } }

        public bool ShowDetail { get { return _isWide || _hasSelection; } }

        public bool ShowBackButton { get { return !_isWide && _hasSelection; } }

        /// <summary>列宽随分档走：并排时 2*:3*，单栏时活动的那一栏吃满。</summary>
        public System.Windows.GridLength ListColumnWidth
        {
            get
            {
                if (!ShowList) return new System.Windows.GridLength(0);
                return ShowDetail ? new System.Windows.GridLength(2, System.Windows.GridUnitType.Star)
                                  : new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            }
        }

        public System.Windows.GridLength DetailColumnWidth
        {
            get
            {
                if (!ShowDetail) return new System.Windows.GridLength(0);
                return ShowList ? new System.Windows.GridLength(3, System.Windows.GridUnitType.Star)
                                : new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
            }
        }

        public System.Windows.GridLength GapColumnWidth
        {
            get { return ShowList && ShowDetail ? new System.Windows.GridLength(16) : new System.Windows.GridLength(0); }
        }

        private void RaisePanes()
        {
            RaisePropertyChanged("ShowList");
            RaisePropertyChanged("ShowDetail");
            RaisePropertyChanged("ShowBackButton");
            RaisePropertyChanged("ListColumnWidth");
            RaisePropertyChanged("DetailColumnWidth");
            RaisePropertyChanged("GapColumnWidth");
        }

        /// <summary>窄档下的「返回列表」。</summary>
        public RelayCommand CloseDetailCommand { get; private set; }

        /// <summary>
        /// Ctrl+F 从别的页跳过来时，搜索框还不在可视树里（导航内容区只挂当前页），
        /// 所以这里只发信号，由 NotesView 在自己 Loaded 之后接手聚焦。
        /// </summary>
        public event EventHandler SearchFocusRequested;

        public event EventHandler TitleFocusRequested;

        public void RequestSearchFocus()
        {
            EventHandler handler = SearchFocusRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RequestTitleFocus()
        {
            EventHandler handler = TitleFocusRequested;
            if (handler != null) handler(this, EventArgs.Empty);
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

        public int NoteCount
        {
            get { return _noteCount; }
            private set { SetProperty(ref _noteCount, value); }
        }

        public int ShownCount
        {
            get { return _shown; }
            private set { SetProperty(ref _shown, value); }
        }

        /// <summary>列表上方那行说明；筛选生效时才报「显示 M 条」。</summary>
        public string SummaryText
        {
            get
            {
                if (_noteCount == 0) return "还没有备忘";
                if (_shown == _noteCount) return "共 " + _noteCount + " 条备忘";
                return "共 " + _noteCount + " 条 · 筛出 " + _shown + " 条";
            }
        }

        public int CharCount
        {
            get { return _charCount; }
            private set { SetProperty(ref _charCount, value); }
        }

        public int LineCount
        {
            get { return _lineCount; }
            private set { SetProperty(ref _lineCount, value); }
        }

        public string SaveStatus
        {
            get { return _saveStatus; }
            private set { SetProperty(ref _saveStatus, value ?? ""); }
        }

        /// <summary>列表里的选中项由 UI4ListView 双向带回。</summary>
        public NoteCardViewModel SelectedCard
        {
            get { return _selectedCard; }
            set
            {
                if (_applyingSelection) return;
                SetSelection(value);
            }
        }

        /// <summary>启动时或数据变化后调用：按设置里的排序重建列表。</summary>
        public void Reload()
        {
            if (!string.IsNullOrEmpty(_settings.Current.NoteSort)) _sortKey = _settings.Current.NoteSort;
            RaisePropertyChanged("SortKey");
            Rebuild();
        }

        /// <summary>切页或再次进入备忘录时补一次排序，打字期间不重排是为了列表不跳。</summary>
        public void RefreshOrder()
        {
            Rebuild();
        }

        /// <summary>把当前草稿写盘（关窗、切页前调用）。</summary>
        public void Flush()
        {
            _autosave.Stop();
            CommitDraft(true);
        }

        private void SetSelection(NoteCardViewModel next)
        {
            if (next == null)
            {
                CommitDraft(true);
                _selectedId = "";
                _selectedCard = null;
                LoadDraft(null);
                HasSelection = false;
                RaisePropertyChanged("SelectedCard");
                return;
            }

            if (_selectedCard == next) return;
            CommitDraft(true);
            _selectedCard = next;
            _selectedId = next.Id;
            LoadDraft(next.Model);
            HasSelection = true;
            RaisePropertyChanged("SelectedCard");
        }

        private void LoadDraft(NoteItem model)
        {
            _loadingDraft = true;
            EditTitle = model == null ? "" : (model.Title ?? "");
            EditBody = model == null ? "" : (model.Body ?? "");
            EditTags = model == null ? "" : TagText.Join(TagText.Split(model.Tags));
            EditPinned = model != null && model.Pinned;
            _loadingDraft = false;
            _dirty = false;
            UpdateCounts();
            SaveStatus = model == null ? "" : SavedLabel(model.UpdatedAt);
        }

        private void UpdateCounts()
        {
            string body = _editBody ?? "";
            int chars = 0;
            foreach (char c in body)
            {
                if (c != '\r' && c != '\n' && c != ' ' && c != '\t') chars++;
            }
            CharCount = chars;
            int lines = body.Length == 0 ? 0 : 1;
            foreach (char c in body)
            {
                if (c == '\n') lines++;
            }
            LineCount = lines;
        }

        private void MarkDirty()
        {
            if (_loadingDraft) return;
            _dirty = true;
            SaveStatus = "有未保存的更改";
            int ms = _settings.Current.AutoSaveMs;
            if (ms > 0)
            {
                _autosave.Stop();
                _autosave.Interval = TimeSpan.FromMilliseconds(ms);
                _autosave.Start();
            }
        }

        private void CommitDraft(bool fromTimer)
        {
            NoteCardViewModel card = _selectedCard;
            if (card == null) return;
            NoteItem model = card.Model;

            string title = _editTitle ?? "";
            string body = _editBody ?? "";
            string tags = TagText.Join(TagText.Split(_editTags));

            bool touched = model.Title != title || model.Body != body
                           || !string.Equals(model.Tags, tags, StringComparison.Ordinal)
                           || model.Pinned != _editPinned;
            if (!touched)
            {
                bool wasDirty = _dirty;
                _dirty = false;
                if (!fromTimer) SaveStatus = wasDirty ? SavedLabel(model.UpdatedAt) : "没有需要保存的改动";
                return;
            }

            model.Title = title;
            model.Body = body;
            model.Tags = tags;
            model.Pinned = _editPinned;
            model.UpdatedAt = DateTime.Now;

            _store.SaveNotes();
            _dirty = false;
            card.Refresh();
            SaveStatus = SavedLabel(model.UpdatedAt);
            RebuildTagOptions();
        }

        private void NewNote()
        {
            CommitDraft(true);
            var note = new NoteItem { Title = "", Body = "", Tags = _tagFilter == "" ? "" : _tagFilter };
            _store.Notes.Insert(0, note);
            _selectedId = note.Id;
            _applyingSelection = true;
            Rebuild();
            _applyingSelection = false;

            NoteCardViewModel card = FindCard(note.Id);
            SetSelection(card);
            _store.SaveNotes();
            RebuildTagOptions();
            RequestTitleFocus();
        }

        private void DeleteSelected()
        {
            NoteCardViewModel card = _selectedCard;
            if (card == null) return;
            bool yes = UI4MessageBox.Show(
                "删除「" + card.Title + "」？这条备忘会从本机移除，无法撤销。",
                "删除备忘",
                UI4MessageBoxButtons.OKCancel,
                owner: System.Windows.Application.Current == null ? null : System.Windows.Application.Current.MainWindow) == true;
            if (!yes) return;

            _autosave.Stop();
            _selectedCard = null;
            _selectedId = "";
            RaisePropertyChanged("SelectedCard");
            _store.Notes.Remove(card.Model);
            _cardCache.Remove(card.Model.Id);
            _store.SaveNotes();
            Rebuild();
            RebuildTagOptions();
            LoadDraft(null);
            HasSelection = false;
        }

        private void TogglePin()
        {
            if (_selectedCard == null) return;
            _editPinned = !_editPinned;
            RaisePropertyChanged("EditPinned");
            CommitDraft(false);
            _selectedCard.Refresh();
            Rebuild();
        }

        private void CopyBody()
        {
            string text = (_editBody ?? "").Trim();
            if (text.Length == 0)
            {
                SaveStatus = "正文是空的，没有可复制的内容";
                return;
            }
            UI4Clipboard.TrySetTextAsync(text, delegate(bool ok)
            {
                SaveStatus = ok ? "正文已复制到剪贴板" : "剪贴板被其它程序占用，稍后再试";
            });
        }

        private void Rebuild()
        {
            _applyingSelection = true;
            string q = (_searchText ?? "").Trim();
            var matches = new List<NoteCardViewModel>();
            foreach (NoteItem note in _store.Notes)
            {
                if (!MatchesFilter(note, q)) continue;
                matches.Add(CardFor(note));
            }
            Sort(matches);

            Cards.Clear();
            foreach (NoteCardViewModel card in matches) Cards.Add(card);
            NoteCount = _store.Notes.Count;
            ShownCount = matches.Count;
            IsEmpty = _store.Notes.Count == 0;
            NoMatch = _store.Notes.Count > 0 && matches.Count == 0;
            RaisePropertyChanged("SummaryText");
            RebuildTagOptions();
            _applyingSelection = false;

            NoteCardViewModel keep = string.IsNullOrEmpty(_selectedId) ? null : FindCard(_selectedId);
            if (keep != null && !ReferenceEquals(keep, _selectedCard))
            {
                _selectedCard = keep;
                HasSelection = true;
                RaisePropertyChanged("SelectedCard");
            }
            else if (keep == null && _selectedCard != null)
            {
                _selectedCard = null;
                HasSelection = false;
                LoadDraft(null);
                RaisePropertyChanged("SelectedCard");
            }
        }

        private bool MatchesFilter(NoteItem note, string q)
        {
            if (!string.IsNullOrEmpty(_tagFilter))
            {
                if (!TagText.Contains(TagText.Split(note.Tags), _tagFilter)) return false;
            }
            if (q.Length == 0) return true;
            return Contains(note.SafeTitle, q) || Contains(note.Body, q) || Contains(note.Tags, q);
        }

        private static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Sort(List<NoteCardViewModel> items)
        {
            Comparison<NoteCardViewModel> byKey;
            switch (_sortKey)
            {
                case "created":
                    byKey = delegate(NoteCardViewModel a, NoteCardViewModel b)
                    {
                        return b.Model.CreatedAt.CompareTo(a.Model.CreatedAt);
                    };
                    break;
                case "title":
                    byKey = delegate(NoteCardViewModel a, NoteCardViewModel b)
                    {
                        return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                    };
                    break;
                default:
                    byKey = delegate(NoteCardViewModel a, NoteCardViewModel b)
                    {
                        return b.Model.UpdatedAt.CompareTo(a.Model.UpdatedAt);
                    };
                    break;
            }
            // 置顶恒在最前，与排序键无关
            items.Sort(delegate(NoteCardViewModel a, NoteCardViewModel b)
            {
                if (a.Pinned != b.Pinned) return a.Pinned ? -1 : 1;
                return byKey(a, b);
            });
        }

        private NoteCardViewModel CardFor(NoteItem note)
        {
            NoteCardViewModel card;
            if (_cardCache.TryGetValue(note.Id, out card) && ReferenceEquals(card.Model, note)) return card;
            card = new NoteCardViewModel(note, CurrentTime);
            _cardCache[note.Id] = card;
            return card;
        }

        private NoteCardViewModel FindCard(string id)
        {
            foreach (NoteCardViewModel card in Cards)
            {
                if (card.Id == id) return card;
            }
            return null;
        }

        private void RebuildTagOptions()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var next = new List<OptionItem> { new OptionItem("", "全部标签") };
            foreach (NoteItem note in _store.Notes)
            {
                foreach (string tag in TagText.Split(note.Tags))
                {
                    if (seen.Add(tag)) next.Add(new OptionItem(tag, tag));
                }
            }
            next.Sort(delegate(OptionItem a, OptionItem b)
            {
                if (a.Key.Length == 0) return -1;
                if (b.Key.Length == 0) return 1;
                return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });

            // 逐项比对后才动集合：下拉展开时被整表清空会让弹层闪一下
            if (SameOptions(next)) return;
            TagOptions.Clear();
            foreach (OptionItem item in next) TagOptions.Add(item);
        }

        private bool SameOptions(List<OptionItem> next)
        {
            if (next.Count != TagOptions.Count) return false;
            for (int i = 0; i < next.Count; i++)
            {
                if (!string.Equals(next[i].Key, TagOptions[i].Key, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static DateTime CurrentTime()
        {
            return DateTime.Now;
        }

        private static string SavedLabel(DateTime moment)
        {
            return "已保存 " + moment.ToString("HH:mm:ss");
        }
    }
}
