using System.Collections.Generic;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    public interface IFileSystemRepository
    {
        string RootPath { get; }
        IReadOnlyList<string> GetModuleNames();
        IReadOnlyList<string> GetEntryFiles(string modulePath);
        void ReadEntry(string filePath, out FrontmatterData data, out string body);
        void WriteEntry(string filePath, FrontmatterData data, string body);
        string CreateModule(string name);
        void RenameModule(string oldPath, string newPath);
        void DeleteModule(string path);
        string CreateEntry(string modulePath, string title, FrontmatterData data);
        void RenameEntry(string oldPath, string newPath);
        void DeleteEntry(string filePath);
        void MoveEntry(string filePath, string targetModulePath);
        void UpdateFavorite(string filePath, bool favorite);
        void UpdateUseCount(string filePath, int useCount, System.DateTime lastUsedAt);
        bool ModuleExists(string name);
        bool EntryExists(string modulePath, string title);
    }
}
