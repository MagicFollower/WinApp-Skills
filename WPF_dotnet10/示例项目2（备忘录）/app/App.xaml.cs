using System;
using System.Windows;
using System.Windows.Threading;
using MemoTask.Helpers;
using MemoTask.Services;
using MemoTask.ViewModels;
using StartUI4Controls;

namespace MemoTask
{
    public partial class App : Application
    {
        private SettingsStore _settingsStore;

        /// <summary>
        /// 启动顺序是承重的：库内写回资源字典的第一句是 Application.Current == null 就返回，
        /// 所以在构造函数或 Main 里调 Register/SetTheme 会静默不装资源，宿主 {DynamicResource UI4.*} 全空。
        /// 顺序钉死为：base.OnStartup → 自测分支 → 异常钩子 → 读设置 → 注册定义 → 应用主题 → 读数据 → 建窗口。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args != null && Array.IndexOf(e.Args, "--selftest") >= 0)
            {
                Shutdown(SelfTest.Run());
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Exit += (s, args) => UI4Theme.ReleaseSystemFollow();

            _settingsStore = new SettingsStore();
            try
            {
                _settingsStore.Load();
            }
            catch (Exception ex)
            {
                Report("读取设置", ex);
            }
            ApplyRenderMode(_settingsStore.Current.RenderMode);

            // 两档定义都注册（明暗策略 both），高对比度继续用库内置那套。
            // 本次不分发 UI4ThemePacks 的 8 套业务主题，所以这里不调 RegisterAll()。
            ThemeService.RegisterDefinitions();
            ThemeService.InstallAccentFollowThrough(delegate { return _settingsStore.Current.Accent; });
            ThemeService.Apply(_settingsStore.Current.ThemeMode, _settingsStore.Current.Accent);

            var store = new DataStore();
            try
            {
                store.Load();
            }
            catch (Exception ex)
            {
                Report("加载数据", ex);
            }

            var main = new MainViewModel(store, _settingsStore);
            try
            {
                main.Initialize();
            }
            catch (Exception ex)
            {
                Report("准备数据", ex);
            }

            // 启动不变量：数据准备失败也要让窗口出现，绝不留"进程存活但无窗口"
            try
            {
                var window = new MainWindow(main);
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
        /// 渲染档位。本机实测过一次「标题栏在、客户区整片白」的故障：WPF 认为自己已经合成出帧
        /// （ContentRendered 正常触发、底色取到深色），但画面没被合成上屏；把进程切到软件渲染立刻正常。
        /// 默认仍走 auto（硬件），只在设置里留一档手动降级，改完重启生效。
        /// </summary>
        private static void ApplyRenderMode(string mode)
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode =
                string.Equals(mode, "software", StringComparison.OrdinalIgnoreCase)
                    ? System.Windows.Interop.RenderMode.SoftwareOnly
                    : System.Windows.Interop.RenderMode.Default;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Report("未处理异常", e.Exception);
            e.Handled = true;      // 单点异常不该带走整个会话；要崩就让它崩在上面的窗口创建上
        }

        private static void Report(string stage, Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    AppPaths.ErrorLog,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + stage + "：" + ex + Environment.NewLine);
            }
            catch
            {
                // 上报失败不得改变退出路径
            }
        }
    }
}
