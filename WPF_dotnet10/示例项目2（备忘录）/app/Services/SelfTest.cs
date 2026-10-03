using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using StartUI4Controls;
using MemoTask.Helpers;

namespace MemoTask.Services
{
    /// <summary>
    /// 由 "MemoTask.exe --selftest" 触发。约定：返回值 = 失败断言数（0 为全通过），
    /// 发布脚本拿它当门禁。只跑纯函数与主题定义，不开窗、不读用户设置、不挂 UI 异常钩子。
    /// 报告写 %APPDATA%\MemoTask\selftest.txt，写不进去回落程序目录，且报告失败不得改变退出码。
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
            failed += Check(report, "亮档正文/底色对比度 ≥ 4.5",
                Contrast(Theme.LightForeground, Theme.LightBackground) >= 4.5,
                Contrast(Theme.LightForeground, Theme.LightBackground).ToString("0.00") + ":1");
            failed += Check(report, "亮档强调色对白对比度 ≥ 3.0",
                Contrast(Theme.Accent, Colors.White) >= 3.0,
                Contrast(Theme.Accent, Colors.White).ToString("0.00") + ":1");
            failed += Check(report, "暗档正文/底色对比度 ≥ 4.5",
                Contrast(Theme.DarkForeground, Theme.DarkBackground) >= 4.5,
                Contrast(Theme.DarkForeground, Theme.DarkBackground).ToString("0.00") + ":1");

            // 4) 状态色是有意不跟主题的固定色相，那就必须在两档底色上都站得住
            failed += Check(report, "危险色对亮底/暗底均 ≥ 3.0",
                Contrast(Theme.Danger, Theme.LightBackground) >= 3.0 && Contrast(Theme.Danger, Theme.DarkBackground) >= 3.0,
                "亮 " + Contrast(Theme.Danger, Theme.LightBackground).ToString("0.00")
                + " / 暗 " + Contrast(Theme.Danger, Theme.DarkBackground).ToString("0.00"));
            failed += Check(report, "警示色对亮底/暗底均 ≥ 3.0",
                Contrast(Theme.Warning, Theme.LightBackground) >= 3.0 && Contrast(Theme.Warning, Theme.DarkBackground) >= 3.0,
                "亮 " + Contrast(Theme.Warning, Theme.LightBackground).ToString("0.00")
                + " / 暗 " + Contrast(Theme.Warning, Theme.DarkBackground).ToString("0.00"));

            // 5) 混色边界与亮度判据
            failed += Check(report, "Mix(w<=0) 返回源色", Theme.Mix(Theme.Accent, Theme.LightForeground, 0f) == Theme.Accent, "");
            failed += Check(report, "Mix(w>=1) 返回目标色", Theme.Mix(Theme.Accent, Theme.LightForeground, 1f) == Theme.LightForeground, "");
            failed += Check(report, "IsDark 与标题栏同判据", Theme.IsDark(Colors.Black) && !Theme.IsDark(Colors.White), "");
            failed += Check(report, "暗档底色确实判为暗", Theme.IsDark(Theme.DarkBackground) && !Theme.IsDark(Theme.LightBackground), "");

            // 6) HEX 往返：设置里存的强调色必须能被读回来
            Color round;
            failed += Check(report, "HEX 往返一致",
                Theme.TryParseHex(Theme.ToHex(Theme.Accent), out round) && round == Theme.Accent,
                Theme.ToHex(Theme.Accent));
            Color junk;
            failed += Check(report, "HEX 拒绝脏输入",
                !Theme.TryParseHex("#12", out junk) && !Theme.TryParseHex("#GGHHII", out junk) && !Theme.TryParseHex("", out junk), "");

            // 7) 明暗策略必须是显式决策：留在 "TODO" 就挡住发布，别让工程带着"没定"上路
            failed += Check(report, "明暗策略已显式决策（Theme.Policy）",
                Theme.Policy == "both" || Theme.Policy == "light-only" || Theme.Policy == "dark-only",
                "当前 \"" + Theme.Policy + "\"，要求 both / light-only / dark-only");

            // 8) 日期与标签这两套用户输入解析：界面全靠它们把中文写成日期
            DateTime anchor = new DateTime(2026, 10, 3);
            DateTime parsedDate;
            failed += Check(report, "今天/明天/后天解析",
                TimeText.TryParseDate("今天", anchor, out parsedDate) && parsedDate == anchor
                && TimeText.TryParseDate("明天", anchor, out parsedDate) && parsedDate == anchor.AddDays(1)
                && TimeText.TryParseDate("后天", anchor, out parsedDate) && parsedDate == anchor.AddDays(2), "");
            failed += Check(report, "相对写法 +3 / 3天后 / -2 天前",
                TimeText.TryParseDate("+3", anchor, out parsedDate) && parsedDate == anchor.AddDays(3)
                && TimeText.TryParseDate("3天后", anchor, out parsedDate) && parsedDate == anchor.AddDays(3)
                && TimeText.TryParseDate("-2天前", anchor, out parsedDate) && parsedDate == anchor.AddDays(-2), "");
            failed += Check(report, "绝对写法 2026-10-20 / 10-20 / 10/20",
                TimeText.TryParseDate("2026-10-20", anchor, out parsedDate) && parsedDate == new DateTime(2026, 10, 20)
                && TimeText.TryParseDate("10-20", anchor, out parsedDate) && parsedDate == new DateTime(2026, 10, 20)
                && TimeText.TryParseDate("10/20", anchor, out parsedDate) && parsedDate == new DateTime(2026, 10, 20), "");
            failed += Check(report, "非法日期被拒",
                !TimeText.TryParseDate("下辈子", anchor, out parsedDate)
                && !TimeText.TryParseDate("2026-2-30", anchor, out parsedDate)
                && !TimeText.TryParseDate("13-45", anchor, out parsedDate), "");
            failed += Check(report, "逾期文案带天数",
                TimeText.Due(new DateTime(2026, 10, 1), anchor) == "逾期 2 天"
                && TimeText.Due(null, anchor) == "无期限"
                && TimeText.Due(new DateTime(2026, 10, 4), anchor) == "明天", TimeText.Due(new DateTime(2026, 10, 1), anchor));
            failed += Check(report, "标签分隔与去重",
                string.Join("|", TagText.Split("工作，灵感; 工作、读书")).Split('|')
                    .Length == 3 && string.Join("|", TagText.Split("a,,b , a")) == "a|b",
                string.Join("|", TagText.Split("工作，灵感; 工作、读书")));

            // 9) 窗口位置恢复判据：越界的坐标必须被判为不可见（Step 7 的换屏场景）
            bool onPrimary;
            try { onPrimary = Monitors.IsVisibleOnSomeMonitor(100, 100, 800, 600); }
            catch (Exception ex) { onPrimary = false; Note(report, "显示器枚举异常 " + ex.GetType().Name); }
            failed += Check(report, "屏内坐标判为可见", onPrimary, "");
            failed += Check(report, "离屏坐标判为不可见",
                !Monitors.IsVisibleOnSomeMonitor(-24000, -24000, 800, 600), "");

            // 10) 资源键完整性：8 套预置逐套装进字典后，38 个令牌的 UI4.Color.* / UI4.Brush.* 与三个别名都要查得到。
            //     应用本身不发放套装（本次决策），但界面里每个 {DynamicResource UI4.*} 的键名写错不报错，
            //     只有这条断言能在运行期把它抓成 FAIL，所以照跑。
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

                // 11) 字典里的值与定义表必须同源：不一致说明有第二份定义在写同一批键（宿主覆盖同名键属预期，但要看得见）
                var dark = UI4ThemeDefinition.Dark();
                UI4Theme.Register(dark);
                UI4Theme.Apply(dark.Key);
                Color dictBg = (Color)app.TryFindResource("UI4.Color.Background");
                Color defBg = dark.GetColor(UI4ThemeToken.Background);
                failed += Check(report, "字典底色与 Dark() 定义同源", dictBg == defBg,
                    "字典 " + dictBg + " vs 定义 " + defBg);

                // 12) 界面实际用到的键必须逐个存在（新增界面写了新键，这里先替用户挡住"这处不跟主题"）
                string[] usedKeys =
                {
                    "UI4.Brush.Background", "UI4.Brush.Surface", "UI4.Brush.Text", "UI4.Brush.TextForeground",
                    "UI4.Brush.TextMuted", "UI4.Brush.Accent", "UI4.Brush.OnAccent", "UI4.Brush.Icon",
                    "UI4.Brush.Placeholder", "UI4.Brush.BorderWeak", "UI4.Color.HoverOverlay", "UI4.Color.Surface",
                };
                var missingUsed = new List<string>();
                UI4Theme.Apply(UI4ThemeDefinition.Light().Key);
                foreach (string key in usedKeys)
                {
                    if (app.TryFindResource(key) == null) missingUsed.Add(key);
                }
                failed += Check(report, "宿主 XAML 用到的资源键齐全", missingUsed.Count == 0, string.Join(",", missingUsed));
            }

            // 13) 设置整份往返：首轮运行窗口坐标还没落位（NaN），序列化炸一次就是"改了设置没保存"
            failed += Check(report, "设置往返含未落位坐标", SettingsRoundTrip(), "");

            string text = report.ToString();
            TryWrite(text);
            return failed;
        }

        /// <summary>
        /// 用临时文件走一遍真实的写盘—读盘。AutoSaveMs 改成非默认值当标记：
        /// 读盘任何一步失败都会回落到默认 800，这条断言就红了，不会被"看上去还是 NaN"糊过去。
        /// </summary>
        private static bool SettingsRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "MemoTask-selftest-" + Guid.NewGuid().ToString("N") + ".settings.json");
            try
            {
                var probe = new MemoTask.Models.AppSettings();
                probe.AutoSaveMs = 4321;
                probe.Window.Left = double.NaN;
                probe.Window.Top = double.NaN;
                if (!JsonStore.Save(path, probe)) return false;

                MemoTask.Models.AppSettings back = JsonStore.Load<MemoTask.Models.AppSettings>(path);
                if (back == null || back.AutoSaveMs != 4321) return false;
                return double.IsNaN(back.Window.Left) && double.IsNaN(back.Window.Top)
                       && Math.Abs(back.Window.Width - 1080) < 0.001;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
        }

        private static int Check(StringBuilder report, string what, bool ok, string detail)
        {
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
            return ok ? 0 : 1;
        }

        private static void Note(StringBuilder report, string text)
        {
            report.AppendLine("NOTE  " + text);
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
                File.WriteAllText(AppPaths.SelfTestReport, text, Encoding.UTF8);
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
