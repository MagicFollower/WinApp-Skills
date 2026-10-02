using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using StartUI4Controls;
using __APPNAME__.Helpers;

namespace __APPNAME__.Services
{
    /// <summary>
    /// 由 "__APPNAME__.exe --selftest" 触发。约定：返回值 = 失败断言数（0 为全通过），
    /// 发布脚本拿它当门禁。只跑纯函数与主题定义，不开窗、不读用户设置、不挂 UI 异常钩子。
    /// 报告写 %APPDATA%\__APPNAME__\selftest.txt，写不进去回落程序目录，且报告失败不得改变退出码。
    /// </summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            var report = new StringBuilder();
            int failed = 0;

            // 1) 令牌总数：新增令牌没同步进手册与宿主配色，这里先崩在自测而不是运行期观感
            int tokenCount = Enum.GetValues(typeof(UI4ThemeToken)).Length;
            failed += Check(report, "令牌总数为 38", tokenCount == 38, "实际 " + tokenCount);

            // 2) 三份内置定义必须逐令牌可取色：漏一个就在 GetColor 上抛 KeyNotFoundException
            foreach (Func<UI4ThemeDefinition> factory in new Func<UI4ThemeDefinition>[]
                     { () => UI4ThemeDefinition.Light(), () => UI4ThemeDefinition.Dark(), () => UI4ThemeDefinition.HighContrast() })
            {
                var def = factory();
                var missing = new List<string>();
                foreach (UI4ThemeToken t in Enum.GetValues(typeof(UI4ThemeToken)))
                {
                    try { def.GetColor(t); } catch (KeyNotFoundException) { missing.Add(t.ToString()); }
                    catch (Exception ex) { missing.Add(t + ":" + ex.GetType().Name); }
                }
                failed += Check(report, def.Key + " 全令牌可取色", missing.Count == 0, string.Join(",", missing));
            }

            // 3) 宿主单源色的可读性：正文对底按 AA 4.5:1，强调色对白按非文本 3:1
            failed += Check(report, "正文/底色对比度 ≥ 4.5",
                Contrast(Theme.Foreground, Theme.Background) >= 4.5,
                Contrast(Theme.Foreground, Theme.Background).ToString("0.00") + ":1");
            failed += Check(report, "强调色对白对比度 ≥ 3.0",
                Contrast(Theme.Accent, Colors.White) >= 3.0,
                Contrast(Theme.Accent, Colors.White).ToString("0.00") + ":1");

            // 4) 混色边界与亮度判据
            failed += Check(report, "Mix(w<=0) 返回源色", Theme.Mix(Theme.Accent, Theme.Foreground, 0f) == Theme.Accent, "");
            failed += Check(report, "Mix(w>=1) 返回目标色", Theme.Mix(Theme.Accent, Theme.Foreground, 1f) == Theme.Foreground, "");
            failed += Check(report, "IsDark 与标题栏同判据", Theme.IsDark(Colors.Black) && !Theme.IsDark(Colors.White), "");

            // 5) 明暗策略必须是显式决策：留在 "TODO" 就挡住发布，别让工程带着"没定"上路
            failed += Check(report, "明暗策略已显式决策（Theme.Policy）",
                Theme.Policy == "both" || Theme.Policy == "light-only" || Theme.Policy == "dark-only",
                "当前 \"" + Theme.Policy + "\"，要求 both / light-only / dark-only");

            // 6) 资源键完整性：8 套预置逐套装进字典后，38 个令牌的 UI4.Color.* / UI4.Brush.* 与三个别名都要查得到。
            //    XAML 里键名写错不报错、不抛异常，只表现为"这处颜色不跟主题"，所以在这里用 TryFindResource 抓成 FAIL。
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                failed += Check(report, "预置套装资源键齐全", false, "Application.Current 为空——这条断言要在 OnStartup 里跑");
            }
            else
            {
                var badKeys = new List<string>();
                UI4ThemePacks.RegisterAll();
                foreach (string packKey in UI4ThemePacks.Keys)
                {
                    UI4Theme.Register(UI4ThemePacks.DefinitionFor(packKey));
                    if (!UI4Theme.Apply(packKey)) { badKeys.Add("apply:" + packKey); continue; }
                    foreach (UI4ThemeToken t in Enum.GetValues(typeof(UI4ThemeToken)))
                    {
                        if (app.TryFindResource("UI4.Color." + t) == null) badKeys.Add(packKey + ">UI4.Color." + t);
                        if (app.TryFindResource("UI4.Brush." + t) == null) badKeys.Add(packKey + ">UI4.Brush." + t);
                    }
                    foreach (string alias in new string[] { "UI4.Brush.Text", "UI4.Brush.Border", "UI4.Brush.Accent" })
                    {
                        if (app.TryFindResource(alias) == null) badKeys.Add(packKey + ">" + alias);
                    }
                }
                failed += Check(report, "预置套装 × 38 令牌资源键齐全", badKeys.Count == 0,
                    badKeys.Count == 0 ? "" : badKeys.Count + " 个缺失，前 6 个：" + string.Join(",", badKeys.GetRange(0, Math.Min(6, badKeys.Count))));

                // 7) 字典里的值与定义表必须同源：不一致说明有第二份定义在写同一批键（宿主覆盖同名键属预期，但要看得见）
                var dark = UI4ThemeDefinition.Dark();
                UI4Theme.Register(dark);
                UI4Theme.Apply(dark.Key);
                Color dictBg = (Color)app.TryFindResource("UI4.Color.Background");
                Color defBg = dark.GetColor(UI4ThemeToken.Background);
                failed += Check(report, "字典底色与 Dark() 定义同源", dictBg == defBg,
                    "字典 " + dictBg + " vs 定义 " + defBg);
            }

            string text = report.ToString();
            TryWrite(text);
            return failed;
        }

        private static int Check(StringBuilder report, string what, bool ok, string detail)
        {
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
            return ok ? 0 : 1;
        }

        /// <summary>WCAG 2.x 对比度：先线性化再取相对亮度。</summary>
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double Luminance(Color c)
        {
            return 0.2126 * Chan(c.R) + 0.7152 * Chan(c.G) + 0.0722 * Chan(c.B);
        }

        private static double Chan(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static void TryWrite(string text)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "__APPNAME__");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "selftest.txt"), text, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), text, Encoding.UTF8);
                }
                catch
                {
                    // 报告写不进去也不改退出码
                }
            }
        }
    }
}
