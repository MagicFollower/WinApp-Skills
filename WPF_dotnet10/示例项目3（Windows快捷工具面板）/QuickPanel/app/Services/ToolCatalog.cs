using System;
using System.Collections.Generic;
using QuickPanel.Models;
using QuickPanel.ViewModels;

namespace QuickPanel.Services
{
    /// <summary>
    /// 快捷入口数据源：10 个功能分组 + 1 个「全部工具」，共 90 项。
    ///
    /// 这份表是调研结论的落地（依据见 doc/运行与构建（T0Level）.md 第 7 节的来源清单），三条硬口径：
    /// ① 只收 Win10 与 Win11 都能解析的入口，行为变化的写进 CompatNote 而不是删掉；
    /// ② 家庭版 SKU 缺少的组件（gpedit/secpol/lusrmgr/wf/fsmgmt/certlm/rsop）单独记
    ///    HomeEditionMissing——它是"这台机器根本没有这个文件"，与"要提权"是两回事，混用会误导；
    /// ③ 社区命令表里流传但本机实测不存在的名字（input.cpl、access.cpl、fstrim.msc、wmic.exe…）
    ///    一律不收，--selftest 里有一条专门点名挡它们。
    ///
    /// 探测口径也来自实测：regedit.exe 住在 %Windows% 根而不是 System32，写错挡位就永远探不到。
    /// </summary>
    internal static class ToolCatalog
    {
        public sealed class GroupInfo
        {
            public GroupInfo(string key, string title, string glyph) { Key = key; Title = title; Glyph = glyph; }
            public string Key { get; private set; }
            public string Title { get; private set; }
            public string Glyph { get; private set; }
        }

        /// <summary>导航里的分组（不含「全部工具」）；顺序即左栏顺序。</summary>
        public static readonly GroupInfo[] Groups = new[]
        {
            new GroupInfo("classic",  "控制面板",   "\uE770"),
            new GroupInfo("mmc",      "管理工具",   "\uE822"),
            new GroupInfo("network",  "网络共享",   "\uE704"),
            new GroupInfo("storage",  "磁盘存储",   "\uE860"),
            new GroupInfo("security", "安全账户",   "\uE72E"),
            new GroupInfo("perf",     "性能诊断",   "\uE8F1"),
            new GroupInfo("input",    "输入个性化", "\uE790"),
            new GroupInfo("ip",       "IP 与 DNS",  "\uF6FA"),
            new GroupInfo("settings", "设置直达",   "\uE713"),
            new GroupInfo("run",      "命令与运行", "\uE756")
        };

        /// <summary>分组键 → 左栏标题。声明顺序必须在 Items 之前：建项时就要取到标题。</summary>
        static readonly Dictionary<string, string> GroupTitles = BuildGroupTitles();

        static Dictionary<string, string> BuildGroupTitles()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in Groups) map[group.Key] = group.Title;
            map[AllKey] = AllTitle;
            return map;
        }

        /// <summary>未知键回原键本身而不是 null：卡片标签宁可露出可疑的键名，也不能显示空白。</summary>
        public static string GroupTitleOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string title;
            return GroupTitles.TryGetValue(key, out title) ? title : key;
        }

        /// <summary>「全部工具」在左栏用的码位（分组顺序之前插一项）。</summary>
        public const string AllKey = "all";
        public const string AllTitle = "全部工具";
        public const string AllGlyph = "\uE71D";

        public static readonly ToolItem[] Items =
        {
            // ── 控制面板经典项（.cpl）─────────────────────────────────
            I("classic", "系统属性", "sysdm.cpl", "计算机名、硬件、远程与高级性能设置一处看全", probe: "sysdm.cpl"),
            I("classic", "程序与功能", "appwiz.cpl", "卸载、更改或修复已安装的桌面程序", probe: "appwiz.cpl"),
            I("classic", "鼠标属性", "main.cpl", "指针移动速度、双击速度与按键分配", probe: "main.cpl"),
            I("classic", "声音", "mmsys.cpl", "设置播放与录制设备的默认值和音量级别", probe: "mmsys.cpl"),
            I("classic", "电源选项", "powercfg.cpl", "切换性能计划，设定合盖与电源按钮行为", probe: "powercfg.cpl"),
            I("classic", "日期和时间", "timedate.cpl", "改时区、手动校时或与网络时间同步", admin: true, probe: "timedate.cpl"),
            I("classic", "区域", "intl.cpl", "格式、位置、非 Unicode 语言与键盘布局", admin: true, probe: "intl.cpl"),
            I("classic", "游戏控制器", "joy.cpl", "校准并测试手柄、方向盘等输入设备", probe: "joy.cpl"),
            I("classic", "电话和调制解调器", "telephon.cpl", "配置拨号规则与调制解调器", admin: true, probe: "telephon.cpl"),
            I("classic", "添加旧式硬件", "hdwwiz.cpl", "手动指定 INF 安装遗留驱动程序", admin: true, probe: "hdwwiz.cpl"),
            I("classic", "显示属性", "desk.cpl", "经典显示页；Win11 直启会跳到「设置」", admin: false, compat: "Win11 转「设置」显示", probe: "desk.cpl"),
            I("classic", "Internet 属性", "inetcpl.cpl", "安全区域、代理、证书与启动页设置", compat: "Win11 无 IE，部分页签变体", probe: "inetcpl.cpl"),

            // ── 管理与维护（MMC 控制台，全部在 System32 且是友好命名）──
            I("mmc", "服务", "services.msc", "启动、停止、重启服务并设置启动类型", admin: true, probe: "services.msc"),
            I("mmc", "设备管理器", "devmgmt.msc", "查看硬件树，更新或回滚驱动程序", admin: true, probe: "devmgmt.msc"),
            I("mmc", "计算机管理", "compmgmt.msc", "服务、设备、磁盘与用户的一体式控制台", admin: true, probe: "compmgmt.msc"),
            I("mmc", "任务计划程序", "taskschd.msc", "创建并管理定时或事件触发的任务", probe: "taskschd.msc"),
            I("mmc", "事件查看器", "eventvwr.msc", "按日志排查系统与应用里的错误警告", admin: true, probe: "eventvwr.msc"),
            I("mmc", "性能监视器控制台", "perfmon.msc", "添加计数器，建立长时间性能视图", admin: true, probe: "perfmon.msc"),
            I("mmc", "本地组策略编辑器", "gpedit.msc", "细粒度配置计算机与用户策略", admin: true, homeMissing: true, probe: "gpedit.msc"),
            I("mmc", "本地安全策略", "secpol.msc", "审计策略、密码策略与用户权限分配", admin: true, homeMissing: true, probe: "secpol.msc"),
            I("mmc", "本地用户和组", "lusrmgr.msc", "创建账户、管理组成员与账户属性", admin: true, homeMissing: true, probe: "lusrmgr.msc"),
            I("mmc", "高级安全防火墙", "wf.msc", "按规则与连接策略精确控制收发流量", admin: true, homeMissing: true, probe: "wf.msc"),
            I("mmc", "组件服务", "comexp.msc", "管理 COM+ 应用程序与分布式事务", admin: true, probe: "comexp.msc"),
            I("mmc", "WMI 控制", "wmimgmt.msc", "查看本地 WMI 服务属性与日志", admin: true, probe: "wmimgmt.msc"),

            // ── 网络与共享 ────────────────────────────────────────────
            I("network", "网络连接", "ncpa.cpl", "查看适配器、改 IP 与 DNS、启用禁用网卡", admin: true, probe: "ncpa.cpl"),
            I("network", "防火墙", "firewall.cpl", "允许应用通过防火墙，分别开关各网络防护", admin: true, probe: "firewall.cpl"),
            I("network", "用户账户", "netplwiz.exe", "设置自动登录、管理组成员与密码", admin: true, probe: "netplwiz.exe"),
            I("network", "网络和共享中心", "control /name Microsoft.NetworkAndSharingCenter", "查看网络类型并进入高级共享设置"),
            I("network", "iSCSI 发起程序", "iscsicpl.exe", "连接或断开 iSCSI 存储目标", admin: true, probe: "iscsicpl.exe"),
            I("network", "共享文件夹", "fsmgmt.msc", "管理开放文件夹、会话与磁盘配额", admin: true, homeMissing: true, probe: "fsmgmt.msc"),
            I("network", "打印管理", "printmanagement.msc", "集中添加打印机、驱动与打印队列", admin: true, probe: "printmanagement.msc"),
            I("network", "打印机文件夹", "explorer.exe shell:PrintersFolder", "经典打印机视图与添加打印机入口", probe: "explorer.exe", probeKind: ToolProbe.WindowsFile),

            // ── 存储与磁盘 ────────────────────────────────────────────
            I("storage", "磁盘管理", "diskmgmt.msc", "分区、格式化、扩展卷与更改盘符", admin: true, probe: "diskmgmt.msc"),
            I("storage", "优化驱动器", "dfrgui.exe", "查看并执行 SSD 的 TRIM 与碎片整理", admin: true, probe: "dfrgui.exe"),
            I("storage", "磁盘清理", "cleanmgr.exe", "扫描并删除临时文件与旧系统还原点", admin: true, probe: "cleanmgr.exe"),
            I("storage", "系统保护", "sdclt.exe", "开关还原保护并配置还原点占用空间", admin: true, probe: "sdclt.exe"),
            I("storage", "系统还原", "rstrui.exe", "选择还原点把系统回退到较早时间", admin: true, danger: "会改动系统状态", probe: "rstrui.exe"),

            // ── 安全与账户 ────────────────────────────────────────────
            I("security", "安全与维护", "wscui.cpl", "查看安全、维护与备份的告警汇总", compat: "Win11 转「Windows 安全中心」", probe: "wscui.cpl"),
            I("security", "用户账户控制", "UserAccountControlSettings.exe", "调整程序请求权限时的通知级别", admin: true, probe: "UserAccountControlSettings.exe"),
            I("security", "证书 - 当前用户", "certmgr.msc", "管理个人证书、受信任根与中间 CA", probe: "certmgr.msc"),
            I("security", "证书 - 本地计算机", "certlm.msc", "管理计算机级的服务与系统证书", admin: true, homeMissing: true, probe: "certlm.msc"),
            I("security", "安全芯片 TPM", "tpm.msc", "查看 TPM 状态、清除与所有者信息", admin: true, probe: "tpm.msc"),
            I("security", "组策略结果", "rsop.msc", "查看当前实际生效的策略设置", homeMissing: true, probe: "rsop.msc"),
            I("security", "Windows 功能", "optionalfeatures.exe", "开关 Hyper-V、沙盒、.NET 3.5 等组件", admin: true, probe: "optionalfeatures.exe"),

            // ── 性能与诊断 ────────────────────────────────────────────
            I("perf", "任务管理器", "taskmgr.exe", "结束进程，查看 CPU、磁盘与网络占用", probe: "taskmgr.exe"),
            I("perf", "资源监视器", "resmon.exe", "把 CPU、内存、磁盘、网络细分到进程", probe: "resmon.exe"),
            I("perf", "性能监视器", "perfmon.exe", "实时性能视图与数据收集器集", admin: true, probe: "perfmon.exe"),
            I("perf", "系统信息", "msinfo32.exe", "汇总硬件资源、组件与软件环境明细", probe: "msinfo32.exe"),
            I("perf", "DirectX 诊断", "dxdiag.exe", "检查显卡、声音与 Direct3D 状态", probe: "dxdiag.exe"),
            I("perf", "Windows 内存诊断", "mdsched.exe", "重启前后检测内存条故障", admin: true, danger: "需要重启电脑", probe: "mdsched.exe"),
            I("perf", "系统配置", "msconfig.exe", "管理启动项、服务与引导启动选项", admin: true, danger: "改错会启动异常", probe: "msconfig.exe"),
            I("perf", "高级系统设置", "SystemPropertiesAdvanced.exe", "直开性能、启动与恢复故障页面", probe: "SystemPropertiesAdvanced.exe"),
            I("perf", "驱动程序验证程序", "verifier.exe", "开启驱动压力测试以定位蓝屏来源", admin: true, danger: "高危：需安全模式才能关", probe: "verifier.exe"),

            // ── 输入与个性化 ──────────────────────────────────────────
            I("input", "蓝牙", "bthprops.cpl", "添加设备、查看已配对设备与 COM 端口", probe: "bthprops.cpl"),
            I("input", "蓝牙文件传送", "fsquirt.exe", "向已配对设备发送或接收文件", probe: "fsquirt.exe"),
            I("input", "红外线通信", "irprops.cpl", "管理红外端口与文件传输", compat: "需红外硬件，多数机器没有", probe: "irprops.cpl"),
            I("input", "平板电脑设置", "TabletPC.cpl", "校正笔、配置按钮与屏幕键盘", compat: "非平板机型可能报错", probe: "TabletPC.cpl"),
            I("input", "字符映射表", "charmap.exe", "查找并复制生僻符号与特殊字符", probe: "charmap.exe"),
            I("input", "字体文件夹", "explorer.exe shell:Fonts", "查看已装字体、预览并删除", probe: "explorer.exe", probeKind: ToolProbe.WindowsFile),
            // input.cpl 在本机已不存在（社区命令表还在抄它），输入法设置只能用 rundll32 这条形态
            I("input", "文本服务和输入语言", "rundll32.exe shell32.dll,Options_RunDLL 7", "管理输入法、键盘布局与语言栏显示", probe: "rundll32.exe"),

            // ── IP 与 DNS（命令行查询项，全部本机实测跑通）──────────────
            // 这批一律 Probe=None：命令以 cmd /k 或 powershell 起头，不以 .exe 结尾，
            // 按第 5 条断言的口径属"非文件型"，设了探测反而会红。cmd.exe 与 powershell.exe 两档都在。
            I("ip", "本机 IP 地址", "cmd /k ipconfig", "列出每张网卡的 IPv4、IPv6 与默认网关"),
            I("ip", "IP 与 DNS 详情", "cmd /k ipconfig /all", "再加上 DNS 服务器、DHCP 租约与物理地址"),
            I("ip", "DNS 缓存内容", "cmd /k ipconfig /displaydns", "查看解析缓存里的域名与剩余有效期"),
            // 非管理员即成功（本机实测："已成功刷新 DNS 解析缓存"），所以不打「需管理员」标签
            I("ip", "清除 DNS 缓存", "cmd /k ipconfig /flushdns", "域名解析刚改过，用它立刻拿到新结果"),
            I("ip", "DNS 解析测试", "cmd /k nslookup", "输入域名看它解析到哪些地址与所用服务器"),
            I("ip", "每块网卡的 DNS", "powershell -NoProfile -NoExit Get-DnsClientServerAddress", "按适配器列出实际下发的 DNS 服务器地址"),
            I("ip", "本机 IPv4 一览", "powershell -NoProfile -NoExit Get-NetIPAddress -AddressFamily IPv4", "只要地址与掩码，比 ipconfig 更紧凑"),
            I("ip", "活动连接与端口", "cmd /k netstat -ano", "列出连接与监听端口，PID 对应到进程"),
            I("ip", "局域网邻居表", "cmd /k arp -a", "查看 IP 与 MAC 的对应缓存，排查同段冲突"),
            I("ip", "路由表", "cmd /k route print", "看目的网络走哪个网关、接口跃点数多少"),
            I("ip", "无线网卡状态", "cmd /k netsh wlan show interfaces", "信号强度、信道与当前连接速率", compat: "机器没有无线网卡时会提示无接口"),

            // ── 设置直达（ms-settings，Win10 自 1607 起支持）──────────
            I("settings", "显示", "ms-settings:display", "分辨率、缩放、多显示器排列与夜间模式"),
            I("settings", "关于", "ms-settings:about", "查看版本、设备名、CPU 与内存规格"),
            I("settings", "网络和 Internet", "ms-settings:network", "查看连接状态并进入适配器选项", compat: "Win11 页名改「网络和 Internet」"),
            I("settings", "存储", "ms-settings:storagesense", "分类占用分析与各分区剩余空间"),
            I("settings", "故障排除", "ms-settings:troubleshoot", "一键修复更新、音频、网络等常见问题", compat: "Win10 叫「疑难解答」"),
            I("settings", "Windows 更新", "ms-settings:windowsupdate", "检查更新、暂停更新与查看更新历史"),
            I("settings", "已安装的应用", "ms-settings:appsfeatures", "按大小排序并卸载或移动应用"),
            I("settings", "电源与睡眠", "ms-settings:powersleep", "设置屏幕关闭时间与睡眠计划"),
            I("settings", "磁盘和卷", "ms-settings:disksandvolumes", "在设置内格式化、扩展与回收空间", admin: true, compat: "需 Win10 1809+"),
            I("settings", "颜色", "ms-settings:personalization-colors", "明暗模式、强调色与透明效果开关"),
            I("settings", "辅助功能", "ms-settings:easeofaccess", "放大镜、讲述人、字幕与对比度", compat: "Win10 叫「轻松使用」"),

            // ── 命令行与运行入口 ──────────────────────────────────────
            I("run", "命令提示符", "cmd.exe", "执行批处理与传统命令行工具", probe: "cmd.exe"),
            I("run", "应用列表", "explorer.exe shell:AppsFolder", "以资源管理器视图启动已安装的应用", probe: "explorer.exe", probeKind: ToolProbe.WindowsFile),
            I("run", "当前用户启动项", "explorer.exe shell:startup", "把快捷方式放进去即随登录自启", probe: "explorer.exe", probeKind: ToolProbe.WindowsFile),
            I("run", "所有用户启动项", "explorer.exe shell:Common Startup", "为全部账户添加开机自启项", admin: true, probe: "explorer.exe", probeKind: ToolProbe.WindowsFile),
            I("run", "注册表编辑器", "C:\\Windows\\regedit.exe", "查找并修改系统配置项", admin: true, danger: "改错可能损坏系统", probe: "regedit.exe", probeKind: ToolProbe.WindowsFile),
            I("run", "记事本", "notepad.exe", "快速打开并编辑纯文本文件", compat: "Win11 是带标签页的新版", probe: "notepad.exe"),

            // ── 桌面图标缓存：两条力度差一档，差别写在介绍里，长解释在命令条的悬浮说明里 ──
            // 刷新那条探 ie4uinit.exe（System32，实测在）；重建那条以 cmd 起头，按"启动的文件"口径不探。
            I("run", "刷新桌面图标", "ie4uinit.exe -show", "只让 Shell 重画，缓存文件原样保留（实测一字节不变）", probe: "ie4uinit.exe",
              detail: "这一条不删任何东西：它通知资源管理器重新绘制桌面图标，图标缓存文件（%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache_*.db，本机 15 个 / 33.4 MB）一个字节都不会变，实测前后完全一致。\n"
                    + "够用场景：图标只是显示没刷新、新放的快捷方式还是空白、改了图标但桌面没跟上。\n"
                    + "救不了的场景：缓存本身坏了（图标成片变白块、缩略图错乱、Explorer 里文件类型图标全不对）——那要用下一条真的重建。\n"
                    + "不打断任何窗口，免管理员，随时可点。"),
            I("run", "重建图标缓存", "cmd /k taskkill /f /im explorer.exe & del /q \"%localappdata%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\" \"%localappdata%\\IconCache.db\" 2>nul & start explorer.exe",
              "先停 Explorer 才删得动被锁的缓存，会关窗口", danger: "会关闭所有文件夹窗口",
              detail: "与「刷新桌面图标」的差别：这一条真的把缓存删掉重建。必须先停资源管理器——实测那批 iconcache_*.db 被 explorer.exe 锁着（独占打开失败），不停就删不动。\n"
                    + "后果：所有资源管理器窗口会关掉、桌面与任务栏闪一下；Explorer 重启后由系统在后台重建缓存，不用等，期间图标可能短暂显示为默认样式。\n"
                    + "该用它的场景：图标成片白块、缩略图错乱、卸载程序后残留图标删不掉。先试上一条。\n"
                    + "免管理员（动的是自己账户下的文件与自己的 Explorer）。"),
        };

        /// <summary>建一条数据。参数名与字段同名，调用处只写非默认的那几项。</summary>
        private static ToolItem I(string group, string title, string command, string description,
                                  bool admin = false, bool homeMissing = false,
                                  string compat = null, string danger = null, string detail = null,
                                  string probe = null,
                                  ToolProbe probeKind = ToolProbe.System32File)
        {
            return new ToolItem
            {
                Group = group,
                GroupTitle = GroupTitleOf(group),
                Title = title,
                Command = command,
                Description = description,
                RequiresAdmin = admin,
                HomeEditionMissing = homeMissing,
                CompatNote = compat,
                DangerNote = danger,
                DetailNote = detail,
                Probe = probe == null ? ToolProbe.None : probeKind,
                ProbeName = probe
            };
        }

        /// <summary>
        /// 搜索命中口径。纯函数，--selftest 直接对它出题，界面与自测共用同一份判断。
        /// 大小写不敏感：命令里有 Taskmgr.exe 这类混合大小写，中文里不会——一并按 OrdinalIgnoreCase 最省事。
        /// </summary>
        public static bool Matches(ToolItem item, string query)
        {
            if (item == null) return false;
            if (string.IsNullOrEmpty(query)) return true;
            return Contains(item.Title, query) || Contains(item.Command, query) || Contains(item.Description, query);
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 探测目标路径（纯函数，selftest 用假 systemRoot 出题）。Probe=None 回 null = 不探测。
        /// regedit.exe 的绝对路径形态是实测出来的坑：拼进 System32 永远探不到。
        /// </summary>
        public static string ProbePath(ToolItem item, string systemRoot)
        {
            if (item == null || item.Probe == ToolProbe.None || string.IsNullOrEmpty(item.ProbeName)) return null;
            string root = string.IsNullOrEmpty(systemRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.Windows) : systemRoot;
            switch (item.Probe)
            {
                case ToolProbe.System32File: return System.IO.Path.Combine(root, "System32", item.ProbeName);
                case ToolProbe.WindowsFile: return System.IO.Path.Combine(root, item.ProbeName);
                default: return null;
            }
        }

        /// <summary>
        /// 给每一项打本机可用性结论。只在启动时跑一次（几十次 File.Exists，量级不到 10 ms）。
        /// 探不到就打「本机未找到」标签，但仍然允许点击——Home 版可能被 DISM 补装、路径也可能在 SysWOW64。
        /// </summary>
        public static void ProbeAvailability()
        {
            foreach (var item in Items)
            {
                string path = ProbePath(item, null);
                if (path == null) { item.Availability = ToolAvailability.NotProbed; continue; }
                item.Availability = System.IO.File.Exists(path) ? ToolAvailability.Present : ToolAvailability.Missing;
            }
        }

        /// <summary>
        /// 把数据源切成左栏用的 Section（「全部工具」在最前）。
        /// ListCollectionView 要 UI 线程，所以这里在 MainWindow 构造时调，不在静态构造里做。
        /// </summary>
        public static List<ToolSection> BuildSections()
        {
            var sections = new List<ToolSection>();
            foreach (var group in Groups)
            {
                var members = new List<ToolItem>();
                foreach (var item in Items) if (item.Group == group.Key) members.Add(item);
                sections.Add(new ToolSection(group.Key, group.Title, group.Glyph, members));
            }
            // 「全部工具」按分组顺序收全部成员，搜索时它就是"跨组命中"的那一屏
            var all = new List<ToolItem>(Items.Length);
            foreach (var section in sections) all.AddRange(section.Items);
            sections.Insert(0, new ToolSection(AllKey, AllTitle, AllGlyph, all));
            return sections;
        }
    }
}
