using System;

namespace QuickPanel.Models
{
    /// <summary>本机探测方式：决定卡片要不要打「本机未找到」标签。</summary>
    public enum ToolProbe
    {
        /// <summary>不探测（ms-settings URI、shell: 名称、control /name 这类由 Shell 自己解析的入口）。</summary>
        None = 0,
        /// <summary>探测 <c>%SystemRoot%\System32\ProbeName</c> 是否存在。</summary>
        System32File = 1,
        /// <summary>探测 <c>%SystemRoot%\ProbeName</c>（regedit.exe 在 Windows 根目录，不在 System32）。</summary>
        WindowsFile = 2
    }

    /// <summary>探测结论。NotProbed 不打标签，别把"没探"显示成"没有"。</summary>
    public enum ToolAvailability
    {
        NotProbed = 0,
        Present = 1,
        Missing = 2
    }

    /// <summary>
    /// 一张卡片 = 一个可直接执行的系统入口。数据全部在 Services/ToolCatalog.cs，这里只放字段与标签文案。
    ///
    /// 标签文案在构造时算完（界面只做可见性绑定，不写第二份判断逻辑）：
    /// 「需管理员」「家庭版没有」是 SKU 与权限两件事，必须分列——把 SKU 缺失误报成"要提权"会误导用户。
    /// </summary>
    public sealed class ToolItem
    {
        public string Group { get; set; }
        /// <summary>所属分组的左栏标题，由 ToolCatalog 建项时填好（不在绑定里现查，避免 Models 反向依赖 Services）。</summary>
        public string GroupTitle { get; set; }
        /// <summary>卡片标题（中文功能名）。</summary>
        public string Title { get; set; }
        /// <summary>卡片上显示、也用于匹配搜索的命令原文（运行框里可直接粘的形态）。</summary>
        public string Command { get; set; }
        /// <summary>一句话功能介绍（写给普通用户，不是命令说明）。</summary>
        public string Description { get; set; }

        public bool RequiresAdmin { get; set; }
        /// <summary>家庭版（Home）SKU 里没有这个组件；与"需管理员"是两回事。</summary>
        public bool HomeEditionMissing { get; set; }
        /// <summary>兼容性备注；null = Win10 与 Win11 行为一致。</summary>
        public string CompatNote { get; set; }
        /// <summary>危险性提示（例如驱动验证程序会强制重启）；null = 无。</summary>
        public string DangerNote { get; set; }
        /// <summary>
        /// 悬浮在命令条上的详细说明。卡片正文那行受"介绍 ≤48 字"约束（UniformGrid 让所有卡片
        /// 等高等宽，一处长了整片都长），所以"什么时候该用哪一条"这类长解释放这里。
        /// </summary>
        public string DetailNote { get; set; }

        public ToolProbe Probe { get; set; }
        /// <summary>相对 System32 或 Windows 根的文件名；Probe=None 时忽略。</summary>
        public string ProbeName { get; set; }

        public ToolAvailability Availability { get; set; }

        // ── 界面直接绑的标签文案 ────────────────────────────────────────
        public string PlatformChip
        {
            get { return string.IsNullOrEmpty(CompatNote) ? "Win10 · Win11 通用" : CompatNote; }
        }
        public bool ShowAdminChip { get { return RequiresAdmin; } }
        public string AdminChip { get { return "需管理员"; } }
        public bool ShowEditionChip { get { return HomeEditionMissing; } }
        public string EditionChip { get { return "家庭版没有"; } }
        public bool ShowDangerChip { get { return !string.IsNullOrEmpty(DangerNote); } }
        public string DangerChip { get { return DangerNote; } }
        public bool ShowMissingChip { get { return Availability == ToolAvailability.Missing; } }
        public string MissingChip { get { return "本机未找到"; } }

        /// <summary>命令条的悬浮说明：写了 DetailNote 就用它，否则用通用提示。</summary>
        public string DetailTip
        {
            get
            {
                return string.IsNullOrEmpty(DetailNote)
                    ? "运行框里的原文，点击卡片即执行" : DetailNote;
            }
        }

        /// <summary>
        /// ListBoxItem 的 UIA Name 取自数据项的 ToString()，默认会露出 "QuickPanel.Models.ToolItem"
        /// 这种类型名（读屏与自动化都拿它当标签）。返回功能名既修了这个，也让脚本能按名字点卡片。
        /// </summary>
        public override string ToString()
        {
            return Title;
        }
    }
}
