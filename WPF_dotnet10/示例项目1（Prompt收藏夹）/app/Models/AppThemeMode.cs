namespace PromptFavorites.Models
{
    /// <summary>
    /// 界面明暗档。刻意<b>不含 System</b>：本项目不自动跟随系统明暗，只由界面上的切换按钮在两档间走，
    /// 档位落 <c>settings.json</c> 的 <c>themeMode</c> 键。加一个 System 成员就等于允许设置文件里出现
    /// 一个没人消费的取值，读回来还会上库的系统跟随与 <c>SystemEvents</c> 订阅。
    /// </summary>
    public enum AppThemeMode
    {
        Light,
        Dark
    }
}
