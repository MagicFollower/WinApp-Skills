using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    public class FileSystemRepository : IFileSystemRepository
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public string RootPath { get; private set; }

        public FileSystemRepository(string rootPath)
        {
            RootPath = rootPath;
        }

        public IReadOnlyList<string> GetModuleNames()
        {
            if (!Directory.Exists(RootPath))
                return new List<string>();

            return Directory.GetDirectories(RootPath)
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<string> GetEntryFiles(string modulePath)
        {
            if (!Directory.Exists(modulePath))
                return new List<string>();

            return Directory.GetFiles(modulePath, "*.md")
                .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void ReadEntry(string filePath, out FrontmatterData data, out string body)
        {
            var content = File.ReadAllText(filePath, Utf8NoBom);
            FrontmatterParser.Parse(content, out data, out body);

            var folderName = Path.GetFileName(Path.GetDirectoryName(filePath));
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrEmpty(data.Title))
                data.Title = fileName;
            if (string.IsNullOrEmpty(data.Module))
                data.Module = folderName;
            if (data.CreatedAt == default(DateTime))
            {
                data.CreatedAt = File.Exists(filePath)
                    ? File.GetCreationTime(filePath)
                    : DateTime.Now;
            }
            if (data.UpdatedAt == default(DateTime))
                data.UpdatedAt = data.CreatedAt;
        }

        public void WriteEntry(string filePath, FrontmatterData data, string body)
        {
            var content = FrontmatterParser.Serialize(data, body);
            File.WriteAllText(filePath, content, Utf8NoBom);
        }

        public string CreateModule(string name)
        {
            if (string.IsNullOrWhiteSpace(RootPath))
                throw new InvalidOperationException("\u672A\u8BBE\u7F6E Prompt \u6839\u76EE\u5F55");

            var path = Path.Combine(RootPath, name);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public void RenameModule(string oldPath, string newPath)
        {
            if (Directory.Exists(oldPath))
                Directory.Move(oldPath, newPath);
        }

        public void DeleteModule(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        public string CreateEntry(string modulePath, string title, FrontmatterData data)
        {
            if (!Directory.Exists(modulePath))
                Directory.CreateDirectory(modulePath);

            var filePath = Path.Combine(modulePath, title + ".md");
            WriteEntry(filePath, data, string.Empty);
            return filePath;
        }

        public void RenameEntry(string oldPath, string newPath)
        {
            if (File.Exists(oldPath))
                File.Move(oldPath, newPath);
        }

        public void DeleteEntry(string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        public void MoveEntry(string filePath, string targetModulePath)
        {
            if (!Directory.Exists(targetModulePath))
                Directory.CreateDirectory(targetModulePath);

            var fileName = Path.GetFileName(filePath);
            var targetPath = Path.Combine(targetModulePath, fileName);
            if (File.Exists(filePath))
                File.Move(filePath, targetPath);
        }

        public void UpdateFavorite(string filePath, bool favorite)
        {
            FrontmatterData data;
            string body;
            ReadEntry(filePath, out data, out body);
            data.Favorite = favorite;
            WriteEntry(filePath, data, body);
        }

        public void UpdateUseCount(string filePath, int useCount, DateTime lastUsedAt)
        {
            FrontmatterData data;
            string body;
            ReadEntry(filePath, out data, out body);
            data.UseCount = useCount;
            data.LastUsedAt = lastUsedAt;
            WriteEntry(filePath, data, body);
        }

        public bool ModuleExists(string name)
        {
            return Directory.Exists(Path.Combine(RootPath, name));
        }

        public bool EntryExists(string modulePath, string title)
        {
            return File.Exists(Path.Combine(modulePath, title + ".md"));
        }
    }
}
