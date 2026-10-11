using System;

namespace MemoTask.Models
{
    public enum TodoPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
    }

    /// <summary>一条待办。</summary>
    public sealed class TodoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title { get; set; } = "";

        public string Detail { get; set; } = "";

        public TodoPriority Priority { get; set; } = TodoPriority.Normal;

        /// <summary>null = 无期限。</summary>
        public DateTime? DueDate { get; set; }

        public bool IsDone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public bool IsOverdue
        {
            get { return !IsDone && DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date; }
        }
    }
}
