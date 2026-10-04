using System;
using System.IO;
using Microsoft.Win32;

namespace StartUI4Controls
{
    /// <summary>
    /// 主题持久化契约。宿主把实例赋给 <see cref="UI4Theme.Persistence"/> 后，
    /// 可调用 <see cref="UI4Theme.Save"/> / <see cref="UI4Theme.ApplyPersisted"/> 存取模式。默认不启用。
    /// </summary>
    public interface IThemePersistence
    {
        void Save(UI4ThemeMode mode);
        /// <summary>读取已保存的模式；无记录时返回 null。</summary>
        UI4ThemeMode? Load();
    }

    /// <summary>把主题模式存到当前用户注册表（HKCU\Software\StartUI4）。</summary>
    public sealed class RegistryThemePersistence : IThemePersistence
    {
        private const string SubKey = @"Software\StartUI4";
        private const string ValueName = "ThemeMode";
        private readonly string _subKey;

        public RegistryThemePersistence() : this(SubKey) { }
        public RegistryThemePersistence(string subKey) { _subKey = subKey; }

        public void Save(UI4ThemeMode mode)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(_subKey))
                key?.SetValue(ValueName, (int)mode, RegistryValueKind.DWord);
        }

        public UI4ThemeMode? Load()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(_subKey))
                {
                    object v = key?.GetValue(ValueName);
                    if (v is int)
                    {
                        int i = (int)v;
                        if (Enum.IsDefined(typeof(UI4ThemeMode), i)) return (UI4ThemeMode)i;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    /// <summary>把主题模式存到一个极简 JSON 文件（不引第三方依赖）。</summary>
    public sealed class JsonThemePersistence : IThemePersistence
    {
        private readonly string _path;

        /// <param name="path">文件路径；通常放在 %APPDATA% 或程序目录。</param>
        public JsonThemePersistence(string path)
        {
            _path = path;
        }

        public void Save(UI4ThemeMode mode)
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, "{\"mode\":\"" + mode + "\"}");
            }
            catch { }
        }

        public UI4ThemeMode? Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                string text = File.ReadAllText(_path);
                foreach (UI4ThemeMode m in Enum.GetValues(typeof(UI4ThemeMode)))
                    if (text.IndexOf("\"" + m + "\"", StringComparison.Ordinal) >= 0)
                        return m;
            }
            catch { }
            return null;
        }
    }
}
