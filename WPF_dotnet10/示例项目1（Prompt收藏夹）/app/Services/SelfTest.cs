using System;
using System.IO;
using System.Text;

namespace PromptFavorites.Services
{
    /// <summary>
    /// <c>--selftest</c> 的合成入口与报告落盘。契约不变：<b>退出码 = 失败断言数</b>，0 即全通过。
    /// 四段判据分工：<see cref="SettingsSelfTest"/> 管设置与顺序编解码、含两层配置的键归属，
    /// <see cref="ThemeSelfTest"/> 管主题资源键与对比度门槛，
    /// <see cref="DataSelfTest"/> 管 frontmatter 保真、仓储的文件读写与每根配置的端到端落盘，
    /// <see cref="DisplaySelfTest"/> 管排印键通路、字号阶梯与缩放换算。
    /// </summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            var r = new SelfTestResult();

            // 任一段崩了也要落成 FAIL 而不是让进程异常退出：退出码＝失败断言数这条契约
            // 是发布门禁的唯一依据，异常会把契约变成"看不懂的堆栈"。
            RunSuite(r, "settings", SettingsSelfTest.Run);
            RunSuite(r, "theme", ThemeSelfTest.Run);
            RunSuite(r, "data", DataSelfTest.Run);
            RunSuite(r, "display", DisplaySelfTest.Run);

            var body = new StringBuilder();
            body.Append("SelfTest ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append(" failed=").Append(r.Failed)
                .Append(" groups=").Append(r.Groups)
                .Append("\r\n");
            body.Append(r.Failed == 0
                ? "PASS 断言组=" + r.Groups
                : r.Report.ToString());
            if (r.Notes.Length > 0)
            {
                if (r.Failed == 0) body.Append("\r\n");
                body.Append(r.Notes);
            }

            WriteReport(body.ToString());
            return r.Failed;
        }

        private static void RunSuite(SelfTestResult r, string name, Action<SelfTestResult> suite)
        {
            try
            {
                suite(r);
            }
            catch (Exception ex)
            {
                r.Check(false, name + " 段异常中断：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void WriteReport(string body)
        {
            // 单文件发布下 AppDomain.CurrentDomain.BaseDirectory 可能指向会被清理的临时解包目录，
            // 产物优先落设置目录旁边；写失败不影响退出码（契约仍是"退出码＝失败断言数"）。
            try
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PromptFavorites");
                Directory.CreateDirectory(appData);
                File.WriteAllText(Path.Combine(appData, "selftest.txt"), body);
            }
            catch
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), body);
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>
    /// 断言收集器。<see cref="Check"/> 每调一次记一组，所以报告里的组数是<b>实际跑过的断言数</b>，
    /// 不再是旧口径那个由数组长度反推的估计值（用例增减时它会失真）。
    /// </summary>
    internal sealed class SelfTestResult
    {
        private string _section = string.Empty;

        public int Groups { get; private set; }
        public int Failed { get; private set; }
        public StringBuilder Report { get; } = new StringBuilder();

        /// <summary>不判成败、只留实测数值的附注（全绿时也写进报告，供文档抄真实数字）。</summary>
        public StringBuilder Notes { get; } = new StringBuilder();

        public void Note(string line)
        {
            Notes.AppendLine("INFO " + line);
        }

        public void Section(string name)
        {
            _section = name;
        }

        public void Check(bool ok, string what)
        {
            Groups++;
            if (ok) return;
            Failed++;
            Report.AppendLine("FAIL [" + _section + "] " + what);
        }
    }
}
