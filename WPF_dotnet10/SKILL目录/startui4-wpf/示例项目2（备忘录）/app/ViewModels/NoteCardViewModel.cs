using System;
using MemoTask.Helpers;
using MemoTask.Models;

namespace MemoTask.ViewModels
{
    /// <summary>备忘录卡片：包一个 NoteItem，卡片文本全部现算，编辑后由 Refresh 广播。</summary>
    internal sealed class NoteCardViewModel : ViewModelBase
    {
        private readonly NoteItem _model;
        private readonly Func<DateTime> _now;

        public NoteCardViewModel(NoteItem model, Func<DateTime> now)
        {
            _model = model;
            _now = now;
        }

        public NoteItem Model { get { return _model; } }

        public string Id { get { return _model.Id; } }

        public string Title { get { return _model.SafeTitle; } }

        public string Preview
        {
            get
            {
                string body = Flatten(_model.Body);
                if (body.Length == 0) return "（空备忘）";
                return body.Length <= 90 ? body : body.Substring(0, 90) + "…";
            }
        }

        public string UpdatedLabel { get { return TimeText.Relative(_model.UpdatedAt, _now()); } }

        public string CreatedLabel { get { return "建于 " + TimeText.Relative(_model.CreatedAt, _now()); } }

        public string TagsLabel { get { return TagText.Join(TagText.Split(_model.Tags)); } }

        public bool HasTags { get { return TagText.Split(_model.Tags).Count > 0; } }

        public bool Pinned { get { return _model.Pinned; } }

        public string MetaLine
        {
            get
            {
                int chars = _model.Body == null ? 0 : _model.Body.Replace("\r", "").Replace("\n", "").Length;
                int lines = _model.Body == null ? 0 : CountLines(_model.Body);
                string tail = chars == 0 ? "空" : chars + " 字 · " + lines + " 行";
                return UpdatedLabel + " · " + tail;
            }
        }

        public void Refresh()
        {
            RaisePropertyChanged("Title");
            RaisePropertyChanged("Preview");
            RaisePropertyChanged("UpdatedLabel");
            RaisePropertyChanged("CreatedLabel");
            RaisePropertyChanged("TagsLabel");
            RaisePropertyChanged("HasTags");
            RaisePropertyChanged("Pinned");
            RaisePropertyChanged("MetaLine");
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\r') continue;
                sb.Append(c == '\n' ? ' ' : c);
            }
            return sb.ToString().Trim();
        }

        private static int CountLines(string text)
        {
            int n = 1;
            foreach (char c in text)
            {
                if (c == '\n') n++;
            }
            return n;
        }
    }
}
