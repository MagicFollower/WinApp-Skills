using System;
using System.Collections.Generic;
using MemoTask.Models;

namespace MemoTask.Services
{
    /// <summary>导出的整包快照结构，带元数据方便日后核对；导入不承诺兼容，只做人工救援用的可读备份。</summary>
    internal sealed class BackupBundle
    {
        public string App { get; set; } = AppInfo.AppName;

        public string Version { get; set; } = AppInfo.Version;

        public string ExportedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public List<NoteItem> Notes { get; set; } = new List<NoteItem>();

        public List<TodoItem> Todos { get; set; } = new List<TodoItem>();
    }
}
