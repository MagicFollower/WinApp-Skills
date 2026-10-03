using System;

namespace MemoTask.Models
{
    /// <summary>一条备忘。字段即落盘结构，改名字要考虑旧数据（见 Services/JsonStore 的容错读）。</summary>
    public sealed class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title { get; set; } = "";

        public string Body { get; set; } = "";

        /// <summary>逗号分隔的自由标签，编辑框直接绑这一个字段，不另存数组。</summary>
        public string Tags { get; set; } = "";

        public bool Pinned { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>展示用派生值，不落盘。</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string SafeTitle
        {
            get { return string.IsNullOrWhiteSpace(Title) ? "未命名备忘" : Title.Trim(); }
        }
    }
}
