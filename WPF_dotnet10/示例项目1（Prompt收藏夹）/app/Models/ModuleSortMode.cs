namespace PromptFavorites.Models
{
    /// <summary>左栏模块列表的排序方式（模块就是磁盘目录，创建日期取目录创建时间）。</summary>
    public enum ModuleSortMode
    {
        CreatedAt,
        Name,

        /// <summary>用户拖动出来的顺序，存在本地设置里；不跟随目录。</summary>
        Custom
    }
}
