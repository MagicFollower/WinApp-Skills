using System;
using MemoTask.Models;

namespace MemoTask.Services
{
    internal sealed class SettingsStore
    {
        public AppSettings Current { get; private set; } = new AppSettings();

        public void Load()
        {
            Current = JsonStore.Load<AppSettings>(AppPaths.SettingsFile);
            if (Current.Window == null) Current.Window = new WindowBounds();
        }

        public bool Save()
        {
            return JsonStore.Save(AppPaths.SettingsFile, Current);
        }
    }
}
