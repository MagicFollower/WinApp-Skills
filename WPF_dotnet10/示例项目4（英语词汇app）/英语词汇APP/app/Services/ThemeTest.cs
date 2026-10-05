using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using StartUI4Controls;

namespace VocabDesk.Services
{
    /// <summary>
    /// 主题切换的回归挡（--themetest）：把设置面板里那三个按钮做的事按顺序跑一遍，
    /// 点名报出每次切换占用了 UI 线程多久。
    ///
    /// 为什么直接计时就够：UI4Theme.SetTheme 是同步在 UI 线程上整份重建 Style 与模板的，
    /// 所以"这句调用花了多久"就是"界面冻住多久"——不用另搭帧间隔探针。
    /// 退出码 = 超预算或没真切过去的次数。
    /// </summary>
    internal static class ThemeTest
    {
        /// <summary>一次切换允许占住 UI 线程多久（与检索同一把闸门）。超了就是"点一下卡死"。</summary>
        const double SwitchBudgetMs = 500;

        public static int Run()
        {
            var report = new StringBuilder();
            int failed = 0;

            void Line(string s)
            {
                report.AppendLine(s);
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocabDesk");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "themetest.txt"), report.ToString());
                }
                catch { }
            }

            try
            {
                UI4Theme.SetTheme(UI4ThemeMode.Light);
            }
            catch (Exception ex)
            {
                Line("SETUP-FAIL " + ex.GetType().Name + " " + ex.Message);
                return 1;
            }

            var window = new MainWindow();
            window.Show();

            // 界面要先真的画出来（含后台建好的语料与卡片列表），否则测的是"空窗口换主题"，
            // 而用户报的那次是在有内容之后点的
            var steps = new[] { ThemeService.Dark, ThemeService.Light, ThemeService.System, ThemeService.Dark };
            int index = 0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (index >= steps.Length)
                {
                    Line("RESULT " + (failed == 0 ? "all switches within budget" : failed + " switch(es) failed"));
                    Application.Current.Shutdown(failed);
                    return;
                }

                string key = steps[index++];
                string before = UI4Theme.ResolvedKey;
                var sw = Stopwatch.StartNew();
                string error = null;
                try
                {
                    if (!ThemeService.Apply(key)) error = "Apply returned false";
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + " " + ex.Message;
                }
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                bool over = ms > SwitchBudgetMs;
                if (over || error != null) failed++;
                Line((over || error != null ? "FAIL " : "OK   ") + key
                     + " uiBlocked=" + ms.ToString("F1") + "ms"
                     + " resolved " + before + " -> " + UI4Theme.ResolvedKey
                     + (error != null ? " err=" + error : string.Empty)
                     + (over ? "  <超 " + SwitchBudgetMs + "ms 预算" : string.Empty));

                // 下一挡：再等一轮渲染，让这一挡的重建真的落到屏幕上
                var next = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                next.Tick += (s2, e2) => { next.Stop(); timer.Start(); };
                next.Start();
            };
            timer.Start();

            return 0;   // 真正的退出码由 Shutdown(failed) 给
        }
    }
}
