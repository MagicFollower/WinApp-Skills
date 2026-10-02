namespace PromptFavorites.Models
{
    public enum SortMode
    {
        UseCount,
        UpdatedAt,
        CreatedAt,
        Name,

        /// <summary>用户拖动出来的顺序，存在本地设置里；不跟随 .md 文件。</summary>
        Custom
    }
}
