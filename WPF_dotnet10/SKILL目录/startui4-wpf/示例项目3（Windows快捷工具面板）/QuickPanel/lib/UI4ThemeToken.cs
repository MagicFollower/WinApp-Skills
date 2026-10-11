namespace StartUI4Controls
{
    /// <summary>
    /// 主题颜色令牌。每个令牌在 Light / Dark 两份 <see cref="UI4ThemeDefinition"/> 中各对应一个 <see cref="System.Windows.Media.Color"/>。
    /// 令牌名同时决定资源桥生成的资源键：<c>UI4.Color.{令牌名}</c> 与 <c>UI4.Brush.{令牌名}</c>。
    /// </summary>
    public enum UI4ThemeToken
    {
        Accent,
        AccentDark,
        AccentEnd,
        TextForeground,
        TextSecondary,
        Background,
        Surface,
        BorderNormal,
        BorderSecondary,
        BorderHover,
        BorderFocus,
        Placeholder,
        HoverOverlay,
        SelectedOverlay,
        TrackBackground,
        CheckBackground,
        Icon,
        IconHover,
        PanelBorder,
        OffBackground,
        MenuBackground,
        ListSelected,
        HeaderBackground,
        HeaderForeground,
        RowHoverBackground,
        RowSelectedBackground,
        GridLine,
        ProgressStart,
        CheckBoxUnchecked,
        HoverBorderColorLight,

        // ── 第二轮补齐（2026-10-02 主题套装化）：原先写死、无令牌可挂的取色点 ──

        /// <summary>强调色表面上的前景色（按钮白字、滑块、对勾、圆点）。高对比度下强调色为黄，须用黑字。</summary>
        OnAccent,
        /// <summary>投影颜色（DropShadowEffect.Color）。</summary>
        Shadow,
        /// <summary>滚动条滑块颜色。</summary>
        ScrollBarThumb,
        /// <summary>分隔线颜色（菜单分隔、翻牌中缝）。</summary>
        Separator,
        /// <summary>低对比弱边框（卡片描边、滚动区外框）。</summary>
        BorderWeak,
        /// <summary>弱化前景（次级标签、未选中标签、关闭/新增按钮图标）。</summary>
        TextMuted,
        /// <summary>页面级渐变底起始色（UI4Grid）。</summary>
        BackgroundGradientStart,
        /// <summary>页面级渐变底结束色（UI4Grid）。</summary>
        BackgroundGradientEnd,
    }
}
