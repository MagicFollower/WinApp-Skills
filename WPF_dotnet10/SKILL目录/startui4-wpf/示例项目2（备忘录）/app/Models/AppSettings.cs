namespace MemoTask.Models
{
    /// <summary>
    /// 用户配置。全部落 settings.json，字段有默认值，读不到旧字段时用默认填补。
    /// 主题模式自己存而不走库的 IThemePersistence：一个应用一份配置，避免两处真源漂移。
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>light / dark / system / highcontrast。</summary>
        public string ThemeMode { get; set; } = "system";

        /// <summary>空串 = 不覆盖，用当前主题自带的强调色；否则是 #RRGGBB。</summary>
        public string Accent { get; set; } = "";

        /// <summary>notes / todos / settings / last。</summary>
        public string StartPage { get; set; } = "notes";

        public string LastPage { get; set; } = "notes";

        /// <summary>编辑停顿多久后自动落盘；0 = 关闭自动保存，只认 Ctrl+S。</summary>
        public int AutoSaveMs { get; set; } = 800;

        public string DefaultPriority { get; set; } = "Normal";

        public string TodoFilter { get; set; } = "active";

        public string NoteSort { get; set; } = "updated";

        /// <summary>auto = 交给 WPF 决定（默认硬件加速）；software = 进程级软件渲染。改完重启生效。</summary>
        public string RenderMode { get; set; } = "auto";

        public WindowBounds Window { get; set; } = new WindowBounds();
    }

    public sealed class WindowBounds
    {
        public double Left { get; set; } = double.NaN;

        public double Top { get; set; } = double.NaN;

        public double Width { get; set; } = 1080;

        public double Height { get; set; } = 700;

        public bool Maximized { get; set; }
    }
}
