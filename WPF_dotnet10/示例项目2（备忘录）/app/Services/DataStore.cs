using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MemoTask.Models;

namespace MemoTask.Services
{
    /// <summary>
    /// 备忘与待办的内存真源。UI 侧的筛选/排序列表由 ViewModel 从这两个集合重建，
    /// 所以这里只管原始数据与落盘，不含任何视图逻辑。
    /// </summary>
    internal sealed class DataStore
    {
        public ObservableCollection<NoteItem> Notes { get; } = new ObservableCollection<NoteItem>();

        public ObservableCollection<TodoItem> Todos { get; } = new ObservableCollection<TodoItem>();

        public string LastError { get; private set; } = "";

        public void Load()
        {
            Notes.Clear();
            Todos.Clear();
            LastError = "";

            List<NoteItem> notes = JsonStore.Load<List<NoteItem>>(AppPaths.NotesFile);
            List<TodoItem> todos = JsonStore.Load<List<TodoItem>>(AppPaths.TodosFile);
            if (notes != null)
            {
                foreach (NoteItem n in notes)
                {
                    if (n != null) Notes.Add(n);
                }
            }
            if (todos != null)
            {
                foreach (TodoItem t in todos)
                {
                    if (t != null) Todos.Add(t);
                }
            }
        }

        public bool SaveNotes()
        {
            bool ok = JsonStore.Save(AppPaths.NotesFile, new List<NoteItem>(Notes));
            LastError = ok ? "" : "备忘写入失败：" + AppPaths.NotesFile;
            return ok;
        }

        public bool SaveTodos()
        {
            bool ok = JsonStore.Save(AppPaths.TodosFile, new List<TodoItem>(Todos));
            LastError = ok ? "" : "待办写入失败：" + AppPaths.TodosFile;
            return ok;
        }

        public bool SaveAll()
        {
            bool notes = SaveNotes();
            bool todos = SaveTodos();
            return notes && todos;
        }

        public long DataSizeBytes()
        {
            long total = 0;
            foreach (string path in new string[] { AppPaths.NotesFile, AppPaths.SettingsFile, AppPaths.TodosFile })
            {
                try
                {
                    if (System.IO.File.Exists(path)) total += new System.IO.FileInfo(path).Length;
                }
                catch (Exception)
                {
                    // 统计失败返回已有部分，不打扰用户
                }
            }
            return total;
        }
    }
}
