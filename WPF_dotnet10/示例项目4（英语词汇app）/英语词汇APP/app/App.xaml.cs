using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;
using VocabDesk.Helpers;
using VocabDesk.Services;

namespace VocabDesk
{
    public partial class App : Application
    {
        /// <summary>
        /// 设置的真源。构造本身不碰磁盘（默认值即构造结果），Load 只在启动序列里调一次——
        /// 这样 --selftest 里构造主窗口也不会读到用户那份文件。
        /// </summary>
        public static SettingsService Settings { get; private set; }

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

            // 真实语料核对：与 --selftest 分开一挡，因为每条断言都要求 json/ 在场，
            // 而发布门禁必须能在没数据的机器上跑绿。退出码同样等于失败数。
            if (e.Args != null && Array.IndexOf(e.Args, "--corpuscheck") >= 0)
            {
                Shutdown(CorpusCheck.Run());
                return;
            }

            // 抓屏自证用：这台机器的抓屏通路（BitBlt 与 PrintWindow(PW_RENDERFULLCONTENT) 都试过）
            // 取不到 GPU 合成的 WPF 内容——裸的红色 Window 也会被拍成一片白，只有 DWM 画的非客户区在。
            // 置成软件渲染后同一套抓屏脚本就能看见界面。产品路径不碰这里。
            if (e.Args != null && Array.IndexOf(e.Args, "--softrender") >= 0)
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Exit += (s, args) => UI4Theme.ReleaseSystemFollow();

            Settings = new SettingsService();
            Settings.Load();

            RegisterAppTheme();
            // 决策（开工确认）：只暴露内置 light / dark / highcontrast，不注册 8 套预置套装键。
            // ApplyEffective 保证即使旧设置文件里存着已撤掉的档，也一定有一次显式 SetTheme 落地。
            ThemeService.ApplyEffective(Settings.ThemeKey);
            // 排印覆盖要在装字典之后：库写的是 MergedDictionaries 里那份，宿主写的是资源根自有项，
            // 顺序反了不会出错（两层不同），但早于 Register 时 Application.Current 还没准备好。
            ApplyDisplaySettings();

            // 主题切换回归挡：要开真实窗口、等语料建好，所以排在设置与配色都接完之后
            if (e.Args != null && Array.IndexOf(e.Args, "--themetest") >= 0)
            {
                int setupFailed = ThemeTest.Run();
                if (setupFailed != 0) Shutdown(setupFailed);
                return;
            }

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
        /// 把设置里的字体族与基准字号发布成资源键（含各层级字号阶梯与固定件尺寸）。
        /// 库自带 UI4.Font.* 的兜底默认值，所以这里写的是覆盖值；设置面板改完会再调一次，
        /// 界面即时跟随（DynamicResource 在应用资源根上就地替换键值）。
        /// </summary>
        internal static void ApplyDisplaySettings()
        {
            var settings = Settings ?? new SettingsService();
            Typography.Publish(Current, settings.FontFamilyName, Typography.ClampBase(settings.BaseFontSize));
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
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk");
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
