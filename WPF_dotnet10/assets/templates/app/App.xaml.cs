using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;
using __APPNAME__.Helpers;
using __APPNAME__.Services;

namespace __APPNAME__
{
    public partial class App : Application
    {
        /// <summary>
        /// 启动顺序是承重的：库内写回资源字典的第一句是 Application.Current == null 就返回，
        /// 所以在构造函数或 Main 里调 Register/SetTheme 会静默不装资源，宿主 {DynamicResource UI4.*} 全空。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 自测分支排在最前：不开窗、不读设置、不挂 UI 异常钩子，退出码即失败断言数
            if (e.Args != null && Array.IndexOf(e.Args, "--selftest") >= 0)
            {
                Shutdown(SelfTest.Run());
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Exit += (s, args) => UI4Theme.ReleaseSystemFollow();

            RegisterAppTheme();
            // 套装键（"paper-grey" 等 8 套）必须先注册，否则 UI4Theme.Apply 认不出键、
            // 只返回 false 且不抛异常。内置 light/dark/highcontrast 由静态构造自带，不注册也能用。
            UI4ThemePacks.RegisterAll();
            UI4Theme.SetTheme(UI4ThemeMode.Light);

            // 启动不变量：数据准备失败也要让窗口出现，绝不留"进程存活但无窗口"
            try
            {
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                Report("创建主窗口", ex);
                Shutdown(-2);
            }
        }

        /// <summary>
        /// 宿主配色单源是 Helpers/Theme.cs，这里把它铺成库的语义令牌。
        /// 从 UI4ThemeDefinition.Light() 起步 = 38 个令牌天然齐全，With 只覆盖想改的那些；
        /// 别 new UI4ThemeDefinition("key") 只填几个令牌，未定义的令牌在取色时抛 KeyNotFoundException。
        /// 覆盖"当前正在用的键"会被库就地整体重应用，不需要先切到别的键再切回来。
        /// </summary>
        private static void RegisterAppTheme()
        {
            var accent = Theme.Accent;
            var def = UI4ThemeDefinition.Light();
            def.Key = "light";
            def.With(UI4ThemeToken.Background, Theme.Background)
               .With(UI4ThemeToken.Surface, Theme.Mix(Theme.Background, Colors.White, 0.85f))
               .With(UI4ThemeToken.TextForeground, Theme.Foreground)
               .With(UI4ThemeToken.TextMuted, Theme.Muted)
               .With(UI4ThemeToken.Accent, accent)
               .With(UI4ThemeToken.AccentDark, Theme.AccentHover)
               .With(UI4ThemeToken.AccentEnd, Theme.Signal)
               .With(UI4ThemeToken.BorderNormal, Theme.Muted)
               .With(UI4ThemeToken.BorderHover, accent)
               .With(UI4ThemeToken.BorderFocus, Theme.AccentHover)
               .With(UI4ThemeToken.PanelBorder, Color.FromArgb(60, accent.R, accent.G, accent.B))
               .With(UI4ThemeToken.Icon, Theme.Muted)
               .With(UI4ThemeToken.IconHover, accent);
            UI4Theme.Register(def);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Report("未处理异常", e.Exception);
            e.Handled = true;      // 单点异常不该带走整个会话；要崩就让它崩在下面的窗口创建上
        }

        private static void Report(string stage, Exception ex)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "__APPNAME__");
                System.IO.Directory.CreateDirectory(path);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(path, "error.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + stage + "：" + ex + Environment.NewLine);
            }
            catch
            {
                // 上报失败不得改变退出路径
            }
        }
    }
}
