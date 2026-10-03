using System;
using MemoTask.Helpers;
using MemoTask.Models;

namespace MemoTask.ViewModels
{
    /// <summary>待办卡片：展示现算，动作全部回调给 TodosViewModel，行本身不碰落盘。</summary>
    internal sealed class TodoRowViewModel : ViewModelBase
    {
        private readonly TodoItem _model;
        private readonly TodosViewModel _owner;

        public TodoRowViewModel(TodoItem model, TodosViewModel owner)
        {
            _model = model;
            _owner = owner;
            ToggleCommand = new RelayCommand(delegate { _owner.ToggleDone(this); });
            DeleteCommand = new RelayCommand(delegate { _owner.RequestDelete(this); });
            EditCommand = new RelayCommand(delegate { _owner.BeginEdit(this); });
        }

        public TodoItem Model { get { return _model; } }

        public string Id { get { return _model.Id; } }

        public string Title
        {
            get { return string.IsNullOrWhiteSpace(_model.Title) ? "未命名待办" : _model.Title.Trim(); }
        }

        public bool HasDetail { get { return !string.IsNullOrWhiteSpace(_model.Detail); } }

        public string Detail { get { return (_model.Detail ?? "").Trim(); } }

        public bool IsDone { get { return _model.IsDone; } }

        public bool IsOverdue { get { return _model.IsOverdue; } }

        public bool IsDueToday
        {
            get
            {
                return !_model.IsDone && _model.DueDate.HasValue && _model.DueDate.Value.Date == DateTime.Now.Date;
            }
        }

        public string PriorityKey { get { return _model.Priority.ToString(); } }

        public string PriorityLabel { get { return PriorityText.Of(_model.Priority); } }

        public bool HasPriorityTag { get { return _model.Priority != TodoPriority.Normal; } }

        public string DueLabel { get { return TimeText.Due(_model.DueDate, DateTime.Now); } }

        public bool HasDue { get { return _model.DueDate.HasValue; } }

        public string MetaLine
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (_model.DueDate.HasValue) parts.Add(DueLabel);
                if (_model.IsDone && _model.CompletedAt.HasValue)
                {
                    parts.Add("完成于 " + TimeText.Relative(_model.CompletedAt.Value, DateTime.Now));
                }
                if (parts.Count == 0) return TimeText.Relative(_model.CreatedAt, DateTime.Now) + "添加";
                return string.Join(" · ", parts);
            }
        }

        public RelayCommand ToggleCommand { get; private set; }
        public RelayCommand DeleteCommand { get; private set; }
        public RelayCommand EditCommand { get; private set; }

        public void Refresh()
        {
            RaisePropertyChanged("Title");
            RaisePropertyChanged("HasDetail");
            RaisePropertyChanged("Detail");
            RaisePropertyChanged("IsDone");
            RaisePropertyChanged("IsOverdue");
            RaisePropertyChanged("IsDueToday");
            RaisePropertyChanged("PriorityKey");
            RaisePropertyChanged("PriorityLabel");
            RaisePropertyChanged("HasPriorityTag");
            RaisePropertyChanged("DueLabel");
            RaisePropertyChanged("HasDue");
            RaisePropertyChanged("MetaLine");
        }
    }

    internal static class PriorityText
    {
        public static string Of(TodoPriority priority)
        {
            switch (priority)
            {
                case TodoPriority.High: return "高";
                case TodoPriority.Low: return "低";
                default: return "普通";
            }
        }

        public static TodoPriority Parse(string key, TodoPriority fallback)
        {
            TodoPriority parsed;
            if (Enum.TryParse(key, true, out parsed)) return parsed;
            return fallback;
        }
    }
}
