using System.Collections.Generic;
using PromptFavorites.Models;

namespace PromptFavorites.Services
{
    public interface IPromptService
    {
        string RootPath { get; }
        IReadOnlyList<PromptModule> LoadModules();
        IReadOnlyList<PromptItem> LoadEntries(string moduleName);
        PromptItem LoadEntryDetail(string filePath);
        PromptItem CreateEntry(string moduleName);
        void SaveEntry(PromptItem item, string originalTitle, string originalModule);
        void ToggleFavorite(PromptItem item);
        void SetFavorite(PromptItem item, bool value);
        void RecordCopy(PromptItem item);
        void RenameModule(string oldName, string newName);
        void DeleteModule(string name);
        void RenameEntry(PromptItem item, string newTitle);
        void DeleteEntry(PromptItem item);
        IReadOnlyList<PromptItem> Search(string keyword);
        IReadOnlyList<PromptItem> ApplySort(IReadOnlyList<PromptItem> items, SortMode mode);
        bool ModuleExists(string name);
        bool EntryExists(string moduleName, string title);
        IReadOnlyList<string> GetModuleNames();
    }
}
