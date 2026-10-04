using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    public class PromptService : IPromptService
    {
        private readonly IFileSystemRepository _repo;

        public string RootPath { get { return _repo.RootPath; } }

        public PromptService(IFileSystemRepository repo)
        {
            _repo = repo;
        }

        public IReadOnlyList<PromptModule> LoadModules()
        {
            var names = _repo.GetModuleNames();
            var modules = new List<PromptModule>();

            foreach (var name in names)
            {
                var path = Path.Combine(_repo.RootPath, name);
                var files = _repo.GetEntryFiles(path);
                modules.Add(new PromptModule
                {
                    Name = name,
                    FullPath = path,
                    EntryCount = files.Count,
                    CreatedAt = Directory.GetCreationTime(path)
                });
            }

            return modules;
        }

        public IReadOnlyList<PromptItem> LoadEntries(string moduleName)
        {
            var modulePath = Path.Combine(_repo.RootPath, moduleName);
            var files = _repo.GetEntryFiles(modulePath);
            var items = new List<PromptItem>();

            foreach (var file in files)
            {
                FrontmatterData data;
                string body;
                _repo.ReadEntry(file, out data, out body);

                items.Add(new PromptItem
                {
                    Title = data.Title,
                    Module = data.Module,
                    FilePath = file,
                    Favorite = data.Favorite,
                    UseCount = data.UseCount,
                    CreatedAt = data.CreatedAt,
                    UpdatedAt = data.UpdatedAt,
                    LastUsedAt = data.LastUsedAt,
                    Body = body
                });
            }

            return items;
        }

        public PromptItem LoadEntryDetail(string filePath)
        {
            FrontmatterData data;
            string body;
            _repo.ReadEntry(filePath, out data, out body);

            return new PromptItem
            {
                Title = data.Title,
                Module = data.Module,
                FilePath = filePath,
                Favorite = data.Favorite,
                UseCount = data.UseCount,
                CreatedAt = data.CreatedAt,
                UpdatedAt = data.UpdatedAt,
                LastUsedAt = data.LastUsedAt,
                Body = body
            };
        }

        public PromptItem CreateEntry(string moduleName)
        {
            var modulePath = Path.Combine(_repo.RootPath, moduleName);
            var title = GenerateUniqueTitle(modulePath, "未命名");
            var now = DateTime.Now;

            var data = new FrontmatterData
            {
                Title = title,
                Module = moduleName,
                Favorite = false,
                UseCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
                LastUsedAt = null
            };

            var filePath = _repo.CreateEntry(modulePath, title, data);

            return new PromptItem
            {
                Title = title,
                Module = moduleName,
                FilePath = filePath,
                Favorite = false,
                UseCount = 0,
                CreatedAt = now,
                UpdatedAt = now,
                LastUsedAt = null,
                Body = string.Empty
            };
        }

        public void SaveEntry(PromptItem item, string originalTitle, string originalModule)
        {
            var now = DateTime.Now;
            var data = new FrontmatterData
            {
                Title = item.Title,
                Module = item.Module,
                Favorite = item.Favorite,
                UseCount = item.UseCount,
                CreatedAt = item.CreatedAt,
                UpdatedAt = now,
                LastUsedAt = item.LastUsedAt
            };

            // 磁盘上那份可能有外部工具加的字段，本程序不认识也要带回去（未知键不能靠保存来"清洗"）
            FrontmatterData onDisk;
            string diskBody;
            _repo.ReadEntry(item.FilePath, out onDisk, out diskBody);
            data.AdoptUnknownFrom(onDisk);

            _repo.WriteEntry(item.FilePath, data, item.Body);
            item.UpdatedAt = now;

            var titleChanged = item.Title != originalTitle;
            var moduleChanged = item.Module != originalModule;

            if (titleChanged)
            {
                var dir = Path.GetDirectoryName(item.FilePath);
                var newPath = Path.Combine(dir, item.Title + ".md");
                _repo.RenameEntry(item.FilePath, newPath);
                item.FilePath = newPath;
            }

            if (moduleChanged)
            {
                var targetModulePath = Path.Combine(_repo.RootPath, item.Module);
                var fileName = Path.GetFileName(item.FilePath);
                var newPath = Path.Combine(targetModulePath, fileName);
                _repo.MoveEntry(item.FilePath, targetModulePath);
                item.FilePath = newPath;
            }
        }

        public void ToggleFavorite(PromptItem item)
        {
            item.Favorite = !item.Favorite;
            _repo.UpdateFavorite(item.FilePath, item.Favorite);
        }

        public void SetFavorite(PromptItem item, bool value)
        {
            item.Favorite = value;
            _repo.UpdateFavorite(item.FilePath, value);
        }

        public void RecordCopy(PromptItem item)
        {
            item.UseCount++;
            item.LastUsedAt = DateTime.Now;
            _repo.UpdateUseCount(item.FilePath, item.UseCount, item.LastUsedAt.Value);
        }

        public void RenameModule(string oldName, string newName)
        {
            var oldPath = Path.Combine(_repo.RootPath, oldName);
            var newPath = Path.Combine(_repo.RootPath, newName);
            _repo.RenameModule(oldPath, newPath);

            var files = _repo.GetEntryFiles(newPath);
            foreach (var file in files)
            {
                FrontmatterData data;
                string body;
                _repo.ReadEntry(file, out data, out body);
                data.Module = newName;
                _repo.WriteEntry(file, data, body);
            }
        }

        public void DeleteModule(string name)
        {
            var path = Path.Combine(_repo.RootPath, name);
            _repo.DeleteModule(path);
        }

        public string CreateModule(string name)
        {
            return _repo.CreateModule(name);
        }

        public void RenameEntry(PromptItem item, string newTitle)
        {
            var dir = Path.GetDirectoryName(item.FilePath);
            var newPath = Path.Combine(dir, newTitle + ".md");
            _repo.RenameEntry(item.FilePath, newPath);
            item.FilePath = newPath;
            item.Title = newTitle;

            FrontmatterData data;
            string body;
            _repo.ReadEntry(item.FilePath, out data, out body);
            data.Title = newTitle;
            _repo.WriteEntry(item.FilePath, data, body);
        }

        public void DeleteEntry(PromptItem item)
        {
            _repo.DeleteEntry(item.FilePath);
        }

        public IReadOnlyList<PromptItem> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<PromptItem>();

            var results = new List<PromptItem>();
            var kw = keyword.Trim();
            var modules = _repo.GetModuleNames();

            foreach (var moduleName in modules)
            {
                var modulePath = Path.Combine(_repo.RootPath, moduleName);
                var files = _repo.GetEntryFiles(modulePath);

                foreach (var file in files)
                {
                    FrontmatterData data;
                    string body;
                    _repo.ReadEntry(file, out data, out body);

                    var titleMatch = data.Title != null &&
                        data.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                    var bodyMatch = body != null &&
                        body.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (titleMatch || bodyMatch)
                    {
                        results.Add(new PromptItem
                        {
                            Title = data.Title,
                            Module = data.Module,
                            FilePath = file,
                            Favorite = data.Favorite,
                            UseCount = data.UseCount,
                            CreatedAt = data.CreatedAt,
                            UpdatedAt = data.UpdatedAt,
                            LastUsedAt = data.LastUsedAt,
                            Body = body
                        });
                    }
                }
            }

            return results;
        }

        public IReadOnlyList<PromptItem> ApplySort(IReadOnlyList<PromptItem> items, SortMode mode)
        {
            IEnumerable<PromptItem> query;
            switch (mode)
            {
                case SortMode.UseCount:
                    query = items.OrderByDescending(i => i.UseCount)
                                 .ThenByDescending(i => i.UpdatedAt);
                    break;
                case SortMode.UpdatedAt:
                    query = items.OrderByDescending(i => i.UpdatedAt);
                    break;
                case SortMode.CreatedAt:
                    query = items.OrderByDescending(i => i.CreatedAt);
                    break;
                case SortMode.Name:
                    query = items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    query = items;
                    break;
            }
            return query.ToList();
        }

        public bool ModuleExists(string name)
        {
            return _repo.ModuleExists(name);
        }

        public bool EntryExists(string moduleName, string title)
        {
            var modulePath = Path.Combine(_repo.RootPath, moduleName);
            return _repo.EntryExists(modulePath, title);
        }

        public IReadOnlyList<string> GetModuleNames()
        {
            return _repo.GetModuleNames();
        }

        private string GenerateUniqueTitle(string modulePath, string baseTitle)
        {
            var title = baseTitle;
            var counter = 2;
            while (_repo.EntryExists(modulePath, title))
            {
                title = baseTitle + "(" + counter + ")";
                counter++;
            }
            return title;
        }
    }
}
