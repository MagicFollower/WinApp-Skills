using System;
using System.IO;

namespace MemoTask.Services
{
    /// <summary>
    /// 数据落点。根目录固定用工程名 MemoTask（与 AssemblyName 无关），
    /// %APPDATA% 不可写时退回程序目录，保证「进程能起、数据有地方放」。
    /// </summary>
    internal static class AppPaths
    {
        private const string AppFolderName = "MemoTask";

        private static string _dataDir;

        public static string DataDir
        {
            get
            {
                if (_dataDir != null) return _dataDir;
                try
                {
                    string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _dataDir = Path.Combine(root, AppFolderName);
                    Directory.CreateDirectory(_dataDir);
                    return _dataDir;
                }
                catch (Exception)
                {
                    _dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppFolderName);
                    try { Directory.CreateDirectory(_dataDir); } catch (Exception) { /* 让上层写文件时报错 */ }
                    return _dataDir;
                }
            }
        }

        public static string NotesFile { get { return Path.Combine(DataDir, "notes.json"); } }

        public static string TodosFile { get { return Path.Combine(DataDir, "todos.json"); } }

        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.json"); } }

        public static string BackupDir
        {
            get
            {
                string dir = Path.Combine(DataDir, "backups");
                try { Directory.CreateDirectory(dir); } catch (Exception) { /* 导出失败由调用方提示 */ }
                return dir;
            }
        }

        public static string SelfTestReport { get { return Path.Combine(DataDir, "selftest.txt"); } }

        public static string ErrorLog { get { return Path.Combine(DataDir, "error.log"); } }
    }
}
