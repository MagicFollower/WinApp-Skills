using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using StartUI4Controls;

namespace MemoTask.Services
{
    /// <summary>
    /// 设置页「关于」要展示的运行事实。全部现取，不缓存，避免打包后还显示开发机的版本号。
    /// </summary>
    internal static class AppInfo
    {
        private static readonly DateTime StartedAt = DateTime.Now;

        public static string AppName { get { return "MemoTask"; } }

        public static string DisplayName { get { return "备忘待办"; } }

        public static string Description { get { return "本地单机备忘录 + 待办，数据不出机器。"; } }

        public static string Version
        {
            get
            {
                AssemblyName name = Assembly.GetExecutingAssembly().GetName();
                return name.Version != null ? name.Version.ToString(3) : "0.0.0";
            }
        }

        public static string ComponentVersion
        {
            get
            {
                AssemblyName name = typeof(UI4Theme).Assembly.GetName();
                return name.Version != null ? name.Version.ToString(3) : "未知";
            }
        }

        public static string Framework { get { return RuntimeInformation.FrameworkDescription; } }

        public static string OsCaption { get { return RuntimeInformation.OSDescription; } }

        public static Architecture ProcessArchitecture { get { return RuntimeInformation.ProcessArchitecture; } }

        /// <summary>单文件包运行时会解到 %TEMP%\.net\，运行目录要现取才能看清是哪一档。</summary>
        public static string RuntimeMode
        {
            get
            {
                string dir = AppContext.BaseDirectory;
                bool selfExtracted = dir.IndexOf(".net" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
                return selfExtracted ? "单文件（已自带运行时）" : "开发运行 / 框架依赖";
            }
        }

        public static string DataDir { get { return AppPaths.DataDir; } }

        public static DateTime Started { get { return StartedAt; } }

        public static string StartedLabel { get { return StartedAt.ToString("yyyy-MM-dd HH:mm:ss"); } }

        public static string UpTime
        {
            get
            {
                TimeSpan t = DateTime.Now - StartedAt;
                if (t.TotalHours >= 1) return ((int)t.TotalHours) + " 小时 " + t.Minutes + " 分";
                if (t.TotalMinutes >= 1) return ((int)t.TotalMinutes) + " 分 " + t.Seconds + " 秒";
                return ((int)t.TotalSeconds) + " 秒";
            }
        }
    }
}
