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
                Shutdown(SettingsSelfTest.Run());
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            RegisterAppTheme();
            UI4Theme.SetTheme(UI4ThemeMode.Light);

            Settings = new SettingsService();
            Settings.Load();

            string resolved;
            bool rootReady = RootPathResolver.TryEnsure(Settings.RootPath, out resolved)
                || RootPathResolver.TryEnsure(RootPathResolver.DefaultRoot(), out resolved);

            RootPath = rootReady ? resolved : string.Empty;
            Settings.RootPath = RootPath;

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
        /// 应用配色：单一定死的浅色方案（终端靛），在内置 Light 上覆盖全部语义令牌后接管 "light" 键。
        /// 中性按钮的底必须是浅的——UI4Button 按底色亮度自动挑字色（L&lt;0.45 给白字），
        /// 中灰底配白字只有 2.4:1；浅中性会让它自动换成正文色（实测 10.8:1）。
        /// </summary>
        private static void RegisterAppTheme()
        {
            var accent = Theme.Accent;
            var accentDark = Theme.AccentHover;
            var surface = Mix(Theme.Background, Colors.White, 0.85f);
            // 源色的 dark_foreground 对底只有 4.49:1，卡在 AA 线上；向正文混 18% 推到 5.7:1，观感不变
            var secondary = Mix(Theme.Secondary, Theme.Foreground, 0.18f);
            var neutral = Mix(Theme.Background, Theme.Muted, 0.62f);

            var def = UI4ThemeDefinition.Light();
            def.Key = "light";
            def.With(UI4ThemeToken.Background, Theme.Background)
               .With(UI4ThemeToken.Surface, surface)
               .With(UI4ThemeToken.TextForeground, Theme.Foreground)
               .With(UI4ThemeToken.TextSecondary, secondary)
               .With(UI4ThemeToken.Accent, accent)
               .With(UI4ThemeToken.AccentDark, accentDark)
               .With(UI4ThemeToken.AccentEnd, Theme.Signal)
               .With(UI4ThemeToken.BorderNormal, Theme.Muted)
               .With(UI4ThemeToken.BorderSecondary, Mix(Theme.Muted, Theme.Background, 0.45f))
               .With(UI4ThemeToken.BorderHover, accent)
               .With(UI4ThemeToken.BorderFocus, accentDark)
               .With(UI4ThemeToken.Placeholder, Mix(Theme.Muted, Theme.Foreground, 0.30f))
               .With(UI4ThemeToken.CheckBackground, accentDark)
               .With(UI4ThemeToken.Icon, secondary)
               .With(UI4ThemeToken.IconHover, Mix(Theme.Foreground, accent, 0.35f))
               .With(UI4ThemeToken.PanelBorder, Color.FromArgb(60, accent.R, accent.G, accent.B))
               .With(UI4ThemeToken.OffBackground, neutral)
               .With(UI4ThemeToken.MenuBackground, surface)
               .With(UI4ThemeToken.ListSelected, accent)
               .With(UI4ThemeToken.HeaderBackground, Mix(Theme.Background, Theme.Foreground, 0.04f))
               .With(UI4ThemeToken.HeaderForeground, Theme.Foreground)
               .With(UI4ThemeToken.RowHoverBackground, Mix(Theme.Background, accent, 0.06f))
               .With(UI4ThemeToken.RowSelectedBackground, Theme.Selection)
               .With(UI4ThemeToken.GridLine, Mix(Theme.Background, Theme.Muted, 0.45f))
               .With(UI4ThemeToken.ProgressStart, accent)
               .With(UI4ThemeToken.CheckBoxUnchecked, Mix(Theme.Muted, Theme.Background, 0.3f))
               .With(UI4ThemeToken.HoverBorderColorLight, Mix(Theme.Muted, accent, 0.35f));
            UI4Theme.Register(def);
        }

        private static Color Mix(Color a, Color b, float w)
        {
            if (w <= 0f) return a;
            if (w >= 1f) return b;
            return Color.FromArgb(a.A,
                (byte)(a.R + (b.R - a.R) * w),
                (byte)(a.G + (b.G - a.G) * w),
                (byte)(a.B + (b.B - a.B) * w));
        }

        /// <summary>切换根目录：先校验可用再落盘，失败时不污染已保存的设置。</summary>
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

            RootPath = resolved;
            Settings.RootPath = resolved;
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
