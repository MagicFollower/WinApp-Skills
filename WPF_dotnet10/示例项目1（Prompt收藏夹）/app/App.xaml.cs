using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;
using PromptFavorites.Helpers;
using PromptFavorites.Services;
using PromptFavorites.ViewModels;

namespace PromptFavorites
{
    public partial class App : Application
    {
        internal static string RootPath { get; private set; }
        internal static SettingsService Settings { get; private set; }
        internal static PromptService Service { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args != null && Array.IndexOf(e.Args, "--selftest") >= 0)
            {
                Shutdown(SelfTest.Run());
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            Settings = new SettingsService();
            Settings.LoadGlobal();

            // 配色接线：两档都注册进库，档位取设置里存的（默认 Light）。
            // 必须在 base.OnStartup 之后、且在第一个窗口 Show() 之前——库写回资源字典的第一句
            // 就是 Application.Current == null 则 return，早一步会静默不装字典。
            HostPalette.Apply(Settings.ThemeMode);
            ApplyDisplaySettings();

            string resolved;
            bool rootReady = RootPathResolver.TryEnsure(Settings.RootPath, out resolved)
                || RootPathResolver.TryEnsure(RootPathResolver.DefaultRoot(), out resolved);

            RootPath = rootReady ? resolved : string.Empty;
            Settings.RootPath = RootPath;

            // 挂上根目录才谈得上"这个根自己的配置"。inheritUnmigrated=true：本根还没有配置文件时，
            // 允许把旧版全局文件里残留的数据键接过来，第一次 Save 就把它们落到本根、并从全局摘掉。
            // 顺序上必须在 new MainViewModel 之前——那个构造函数会读 lastModule/排序档/收藏筛选。
            Settings.AttachRoot(RootPath, true);

            MainViewModel viewModel = null;
            try
            {
                Service = new PromptService(new FileSystemRepository(RootPath));
                viewModel = new MainViewModel(Service, Settings);
            }
            catch (Exception ex)
            {
                TryReport("初始化数据服务", ex);
            }

            // 启动不变量：无论上面的数据准备是否成功，窗口必须出现；
            // 连窗口都创建不了就明确退出，绝不留下"进程存活但无窗口"的状态。
            try
            {
                var mainWindow = new MainWindow();
                if (viewModel != null) mainWindow.DataContext = viewModel;
                ApplyWindowGeometry(mainWindow, Settings);
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                TryReport("创建主窗口", ex);
                Shutdown(-2);
                return;
            }

            if (!rootReady)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(PromptForRootPath));
            }
        }

        /// <summary>
        /// 把设置里的字体族与基准字号发布成资源键（含各层级字号阶梯）。
        /// 库自己带 <c>UI4.Font.Size.*</c> 兜底默认值，所以这里写的是覆盖值；设置面板改完会再调一次，
        /// 界面即时跟随（DynamicResource 在应用资源根上就地替换键值）。
        /// </summary>
        internal static void ApplyDisplaySettings()
        {
            if (Settings == null) return;
            Typography.Publish(Current, Settings.FontFamilyName,
                Typography.ClampBase(Settings.BaseFontSize));
        }

        /// <summary>
        /// 切换根目录：先校验可用再落盘，失败时不污染已保存的设置。
        /// 落盘顺序是这条流程的关键——旧根的状态必须先写回<b>旧根自己的</b>配置文件，
        /// 新根那份再单独读进来；两步都做过之后，两个根才真的各记各的。
        /// </summary>
        internal static bool ChangeRootPath(string newPath)
        {
            string resolved;
            if (!RootPathResolver.TryEnsure(newPath, out resolved))
            {
                UI4MessageBox.Show(
                    "\u8BE5\u76EE\u5F55\u4E0D\u53EF\u7528\uFF08\u8DEF\u5F84\u8FC7\u957F\u3001\u542B\u975E\u6CD5\u5B57\u7B26\u6216\u65E0\u6743\u9650\uFF09\u3002",
                    "\u5207\u6362\u6839\u76EE\u5F55\u5931\u8D25", UI4MessageBoxButtons.OK, 460);
                return false;
            }

            // 此刻 Settings 还挂在旧根上，这次 Save 写的就是旧根那份。
            Settings.Save();

            RootPath = resolved;
            Settings.RootPath = resolved;
            Settings.AttachRoot(resolved, false);

            Service = new PromptService(new FileSystemRepository(resolved));

            var window = Current.MainWindow;
            if (window != null)
            {
                var vm = new MainViewModel(Service, Settings);
                window.DataContext = vm;
                vm.MainWindow = window;
                vm.CaptureWindowState(window);
            }

            Settings.Save();
            return true;
        }

        private static void PromptForRootPath()
        {
            UI4MessageBox.Show(
                "\u672A\u627E\u5230\u53EF\u7528\u7684 Prompt \u6839\u76EE\u5F55\uFF0C\u8BF7\u9009\u62E9\u4E00\u4E2A\u76EE\u5F55\u3002",
                "\u9700\u8981\u9009\u62E9\u76EE\u5F55", UI4MessageBoxButtons.OK, 420);

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "\u9009\u62E9 Prompt \u6839\u76EE\u5F55"
            };

            if (dialog.ShowDialog() == true)
                ChangeRootPath(dialog.FolderName);
        }

        private static void ApplyWindowGeometry(Window window, SettingsService settings)
        {
            if (window == null || settings == null) return;

            if (settings.WindowWidth > 0 && settings.WindowHeight > 0
                && settings.WindowWidth <= 20000 && settings.WindowHeight <= 20000)
            {
                window.Width = settings.WindowWidth;
                window.Height = settings.WindowHeight;
            }

            // 换屏后保存过的坐标可能落在虚拟屏幕之外，症状与"启动看不到窗口"相同，这里钳回来。
            var left = settings.WindowLeft;
            var top = settings.WindowTop;
            var screenLeft = SystemParameters.VirtualScreenLeft;
            var screenTop = SystemParameters.VirtualScreenTop;
            var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
            var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

            if (left >= screenLeft && left <= screenRight - 120
                && top >= screenTop && top <= screenBottom - 80)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = left;
                window.Top = top;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (settings.WindowState == WindowState.Maximized)
            {
                window.SourceInitialized += (s, e) => window.WindowState = WindowState.Maximized;
            }
        }

        private void OnDispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs args)
        {
            args.Handled = true;
            TryReport("\u53D1\u751F\u672A\u5904\u7406\u7684\u9519\u8BEF", args.Exception);

            if (MainWindow == null && Current != null && Current.Windows.Count == 0)
                Shutdown(-1);
        }

        private static void TryReport(string title, Exception ex)
        {
            try
            {
                UI4MessageBox.Show(title + "\uFF1A" + (ex == null ? "" : ex.Message),
                    "\u9519\u8BEF", UI4MessageBoxButtons.OK, 460);
            }
            catch
            {
                // 兜底提示失败时不再抛异常，避免二次崩溃
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Settings != null)
                Settings.Save();
            base.OnExit(e);
        }
    }
}
