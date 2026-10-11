using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StartUI4Controls;
using QuickPanel.Helpers;
using QuickPanel.Services;

namespace QuickPanel
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

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Exit += (s, args) => UI4Theme.ReleaseSystemFollow();

            Settings = new SettingsService();
            Settings.Load();

            // Policy = both：两套宿主定义都注册，切档只换键名，颜色由库按当前键写回字典
            UI4Theme.Register(Theme.BuildLightDefinition());
            UI4Theme.Register(Theme.BuildDarkDefinition());
            // 本项目只用内置明暗档，不引入 UI4ThemePacks 的 8 套业务档与 UI4ThemeScope 局部换肤。
            ThemeService.Apply(Settings.ThemeKey);
            // 排印覆盖要在装字典之后：库写的是 MergedDictionaries 里那份，宿主写的是资源根自有项，
            // 顺序反了不会出错（两层不同），但早于 Register 时 Application.Current 还没准备好。
            ApplyDisplaySettings();

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
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickPanel");
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
