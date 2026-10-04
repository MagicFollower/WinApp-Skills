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
                // 每根配置也住在根目录下（<root>\.PromptFavorites），它不是模块。
                // 少这层过滤的症状是左栏凭空多一项、还能被改名和删除——删掉等于把本根的视图配置删了。
                .Where(n => !SettingsService.IsReservedRootEntryName(n))
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
            WriteAtomic(filePath, content);
        }

        /// <summary>
        /// 临时文件 + 原子替换。<see cref="File.WriteAllText"/> 是截断写：进程被杀或磁盘满时留下半截文件，
        /// 而半截 frontmatter 缺了闭合的 <c>---</c> 就会被当成"没有 frontmatter"，下次读把整篇当正文——
        /// 用户看到的现象是元数据凭空消失。替换/改名都是同目录内的原子操作，不存在半截窗口。
        /// </summary>
        private static void WriteAtomic(string filePath, string content)
        {
            string temp = filePath + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                File.WriteAllText(temp, content, Utf8NoBom);
                if (File.Exists(filePath))
                    File.Replace(temp, filePath, null);
                else
                    File.Move(temp, filePath);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
                throw;
            }
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
            if (!File.Exists(oldPath)) return;
            if (File.Exists(newPath))
                throw new IOException("\u76ee\u6807\u5df2\u5b58\u5728\uff1a" + newPath);
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
            if (!File.Exists(filePath)) return;

            // 同一路径的两种写法（相对/短名）不该被判成冲突，所以先归一化再比
            if (File.Exists(targetPath) && !SamePath(filePath, targetPath))
                throw new IOException("\u76ee\u6807\u5df2\u5b58\u5728\uff1a" + targetPath);

            if (!SamePath(filePath, targetPath))
                File.Move(filePath, targetPath);
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
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
